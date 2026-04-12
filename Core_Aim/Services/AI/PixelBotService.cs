using Core_Aim.Services.Configuration;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace Core_Aim.Services.AI
{
    // ─────────────────────────────────────────────────────────────────────────
    // PixelBotService — detecção de alvos por cor (HSV)
    //
    // Replica o sistema PixelBot do projeto C++ (C:\Projeto-ONNX-DML).
    // Opera em modo FALLBACK (quando YOLO não detecta nada) ou PRIMÁRIO.
    //
    // Pipeline:
    //   1. Converte frame BGR → HSV
    //   2. Cria máscara binária com tolerância em torno da cor alvo
    //   3. Encontra contornos e filtra por área, aspect ratio, solidez, circularidade
    //   4. Seleciona blob mais próximo do centro do frame
    //   5. Mantém persistência do alvo por até N frames sem detecção
    // ─────────────────────────────────────────────────────────────────────────
    public class PixelBotService
    {
        private readonly AppSettingsService _settings;

        // ── Estado de persistência ────────────────────────────────────────────
        private PointF      _lastCenter;
        private RectangleF  _lastBounds;
        private int         _framesLost;
        private bool        _hasTarget;
        private ulong       _nextId;
        private ulong       _currentId;

        // ── Cache da cor HSV (evita reconversão a cada frame) ─────────────────
        private string  _cachedHex    = "";
        private int     _cachedTol    = -1;
        private int     _cachedSatMin = -1;
        private int     _cachedValMin = -1;
        private Scalar  _lowerHsv;
        private Scalar  _upperHsv;

        // ── Blobs para visualização no preview ────────────────────────────────
        // _lastBlobs é mutável (cleared a cada frame); _blobSnapshot é o snapshot
        // imutável lido pelo DisplayLoop — evita race condition.
        private readonly List<PixelBotBlob> _lastBlobs = new();
        private PixelBotBlob[] _blobSnapshot = Array.Empty<PixelBotBlob>();
        public IReadOnlyList<PixelBotBlob> LastBlobs => _blobSnapshot;

        // ── Throttle de debug — imprime no console a cada 3 segundos ──────────
        private readonly Stopwatch _dbgSw = Stopwatch.StartNew();

        // Raio de persistência: lido da config a cada frame (não mais constante)

        public PixelBotService(AppSettingsService settings)
        {
            _settings = settings;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Detect — processa um frame e retorna o melhor alvo.
        //
        // frame   : Mat BGR ou BGRA já no tamanho do modelo (modelW × modelH)
        // modelW/H: dimensões do modelo (ex.: 640 × 640)
        //
        // Retorna null se nenhum alvo encontrado e persistência expirou.
        // ─────────────────────────────────────────────────────────────────────
        public PixelBotDetection? Detect(Mat frame, int modelW, int modelH)
        {
            _lastBlobs.Clear();

            if (frame == null || frame.IsDisposed)
            {
                _blobSnapshot = Array.Empty<PixelBotBlob>();
                return PersistOrNull();
            }

            RefreshHsvCache();

            int centerX = modelW / 2;
            int centerY = modelH / 2;

            // Garante frame BGR de 3 canais para CvtColor
            Mat workFrame = frame;
            Mat? converted = null;
            if (frame.Channels() == 4)
            {
                converted = new Mat();
                Cv2.CvtColor(frame, converted, ColorConversionCodes.BGRA2BGR);
                workFrame = converted;
            }

            PointF?      bestCenter = null;
            RectangleF   bestBounds = default;
            double       bestDist   = double.MaxValue;

            int dbgMaskPixels = 0;
            int dbgContours   = 0;

            try
            {
                using var hsv  = new Mat();
                using var mask = new Mat();
                Cv2.CvtColor(workFrame, hsv, ColorConversionCodes.BGR2HSV);
                Cv2.InRange(hsv, _lowerHsv, _upperHsv, mask);

                dbgMaskPixels = Cv2.CountNonZero(mask);

                // SEM morfologia — igual ao Python (generate_mask só faz InRange)
                // A morfologia deslocava centróides ao fundir blobs próximos

                // RETR_TREE: igual ao Python cv2.RETR_TREE
                Cv2.FindContours(mask, out var contours, out _,
                    RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

                dbgContours = contours.Length;

                int    minArea = _settings.PixelBotMinArea;
                int    maxArea = _settings.PixelBotMaxArea;
                double minAR   = _settings.PixelBotMinAspectRatio;
                double minSol  = _settings.PixelBotMinSolidity;
                double minCirc = _settings.PixelBotMinCircularity;

                var validTargets = new List<OpenCvSharp.Point[]>();

                foreach (var cnt in contours)
                {
                    double area = Cv2.ContourArea(InputArray.Create(cnt));
                    if (area < minArea || area > maxArea) continue;

                    var rect = Cv2.BoundingRect(InputArray.Create(cnt));
                    int rw = rect.Width, rh = rect.Height;
                    if (rw == 0 || rh == 0) continue;

                    // Aspect ratio — igual ao Python: float(w)/h, verifica min < ar < 1/min
                    double ar = (double)rw / rh;
                    if (ar <= minAR || ar >= (1.0 / minAR)) continue;

                    // Solidez: área / área do hull convexo (returnPoints=true, igual Python)
                    using var hullPtsMat = new Mat();
                    Cv2.ConvexHull(InputArray.Create(cnt), hullPtsMat, false, true);
                    double hullArea = Cv2.ContourArea(hullPtsMat);
                    if (hullArea == 0) continue;
                    double solidity = area / hullArea;
                    if (solidity < minSol) continue;

                    // Circularidade: 4πA / P²
                    double perimeter = Cv2.ArcLength(InputArray.Create(cnt), true);
                    if (perimeter == 0) continue;
                    double circularity = (4.0 * Math.PI * area) / (perimeter * perimeter);
                    if (circularity < minCirc) continue;

                    validTargets.Add(cnt);
                }

                // Seleciona blob mais próximo do centro — só distância horizontal (igual Python)
                OpenCvSharp.Point[]? bestCnt = null;
                foreach (var cnt in validTargets)
                {
                    var M = Cv2.Moments(InputArray.Create(cnt));
                    if (M.M00 == 0) continue;
                    double cx = M.M10 / M.M00;
                    double dist = Math.Abs(cx - centerX); // só eixo X, igual Python
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestCnt  = cnt;
                    }
                }

                // Registra todos os blobs válidos para visualização
                foreach (var cnt in validTargets)
                {
                    var M2   = Cv2.Moments(InputArray.Create(cnt));
                    var rect2 = Cv2.BoundingRect(InputArray.Create(cnt));
                    float bcx = M2.M00 != 0 ? (float)(M2.M10 / M2.M00) : rect2.X + rect2.Width  / 2f;
                    float bcy = M2.M00 != 0 ? (float)(M2.M01 / M2.M00) : rect2.Y + rect2.Height / 2f;
                    var blobBounds  = new RectangleF(rect2.X, rect2.Y, rect2.Width, rect2.Height);
                    var blobCenter  = new PointF(bcx, bcy);
                    bool isSel = bestCnt != null && ReferenceEquals(cnt, bestCnt);
                    _lastBlobs.Add(new PixelBotBlob { Bounds = blobBounds, Center = blobCenter, IsSelected = isSel });
                }

                // Extrai centro e bounds do melhor blob via moments (igual Python)
                if (bestCnt != null)
                {
                    var Mb   = Cv2.Moments(InputArray.Create(bestCnt));
                    var rectb = Cv2.BoundingRect(InputArray.Create(bestCnt));
                    float fcx = Mb.M00 != 0 ? (float)(Mb.M10 / Mb.M00) : rectb.X + rectb.Width  / 2f;
                    float fcy = Mb.M00 != 0 ? (float)(Mb.M01 / Mb.M00) : rectb.Y + rectb.Height / 2f;
                    bestCenter = new PointF(fcx, fcy);
                    bestBounds = new RectangleF(rectb.X, rectb.Y, rectb.Width, rectb.Height);
                }
            }
            finally
            {
                converted?.Dispose();
            }

            // Persistência e resultado
            if (bestCenter.HasValue)
            {
                // Persistência: verifica se é o mesmo alvo de antes
                float persistRadius = Math.Max(1f, _settings.PixelBotPersistRadius);
                bool reused = _hasTarget && (
                    _lastBounds.Contains(bestCenter.Value) ||
                    Dist(bestCenter.Value, _lastCenter) <= persistRadius);

                if (!reused) _currentId = _nextId++;

                _lastCenter = bestCenter.Value;
                _lastBounds = bestBounds;
                _framesLost = 0;
                _hasTarget  = true;

                // Snapshot DEPOIS de marcar o blob selecionado
                _blobSnapshot = _lastBlobs.ToArray();

                // Debug throttled — mostra centróide bruto vs centro do frame
                if (_dbgSw.Elapsed.TotalSeconds >= 2.0)
                {
                    _dbgSw.Restart();
                    float errX  = _lastCenter.X - centerX;
                    float errY  = _lastCenter.Y - centerY;
                    string dirX = errX > 0 ? "→DIREITA" : errX < 0 ? "←ESQUERDA" : "CENTER";
                    string dirY = errY > 0 ? "↓BAIXO"   : errY < 0 ? "↑CIMA"     : "CENTER";
                    Console.WriteLine(
                        $"[PixelBot] maskPx={dbgMaskPixels}  contours={dbgContours}  blobs={_lastBlobs.Count}" +
                        $"  center=({_lastCenter.X:F0},{_lastCenter.Y:F0})" +
                        $"  bounds=({_lastBounds.X:F0},{_lastBounds.Y:F0},{_lastBounds.Width:F0}x{_lastBounds.Height:F0})" +
                        $"  frame_center=({centerX},{centerY})  err=({errX:F0},{errY:F0})  dir={dirX} {dirY}");
                }

                return new PixelBotDetection
                {
                    Center = _lastCenter,
                    Bounds = _lastBounds,
                    Id     = _currentId
                };
            }

            // Nenhum blob encontrado — snapshot vazio + debug
            _blobSnapshot = Array.Empty<PixelBotBlob>();
            if (_dbgSw.Elapsed.TotalSeconds >= 3.0)
            {
                _dbgSw.Restart();
                Console.WriteLine(
                    $"[PixelBot] HSV H[{_lowerHsv[0]:F0},{_upperHsv[0]:F0}] " +
                    $"S[{_lowerHsv[1]:F0},255] V[{_lowerHsv[2]:F0},255] | " +
                    $"maskPx={dbgMaskPixels}  contours={dbgContours}  blobs=0  NO TARGET");
            }

            return PersistOrNull();
        }

        // ─────────────────────────────────────────────────────────────────────
        // PersistOrNull — retorna posição anterior por até MaxFramesLost frames.
        // ─────────────────────────────────────────────────────────────────────
        private PixelBotDetection? PersistOrNull()
        {
            if (!_hasTarget) return null;

            _framesLost++;
            if (_framesLost > _settings.PixelBotMaxFramesLost)
            {
                _hasTarget  = false;
                _framesLost = 0;
                return null;
            }

            return new PixelBotDetection
            {
                Center = _lastCenter,
                Bounds = _lastBounds,
                Id     = _currentId
            };
        }

        public void Reset()
        {
            _hasTarget    = false;
            _framesLost   = 0;
            _lastBlobs.Clear();
            _blobSnapshot = Array.Empty<PixelBotBlob>();
        }

        // ─────────────────────────────────────────────────────────────────────
        // RefreshHsvCache — converte a cor hex configurada para range HSV.
        // Recalcula apenas quando a cor ou tolerância muda.
        // ─────────────────────────────────────────────────────────────────────
        private void RefreshHsvCache()
        {
            string hex    = _settings.PixelBotColorHex ?? "#FF0000";
            int    tol    = _settings.PixelBotColorTolerance;
            int    satMin = _settings.PixelBotSatMin;
            int    valMin = _settings.PixelBotValMin;

            if (hex == _cachedHex && tol == _cachedTol &&
                satMin == _cachedSatMin && valMin == _cachedValMin) return;

            _cachedHex    = hex;
            _cachedTol    = tol;
            _cachedSatMin = satMin;
            _cachedValMin = valMin;

            try
            {
                var color = System.Drawing.ColorTranslator.FromHtml(
                    hex.StartsWith("#") ? hex : "#" + hex);

                using var tmp = new Mat(1, 1, MatType.CV_8UC3,
                    new Scalar(color.B, color.G, color.R));
                using var hsvTmp = new Mat();
                Cv2.CvtColor(tmp, hsvTmp, ColorConversionCodes.BGR2HSV);

                double h = hsvTmp.At<Vec3b>(0, 0)[0]; // 0–180 no OpenCV
                double s = hsvTmp.At<Vec3b>(0, 0)[1]; // 0–255
                double v = hsvTmp.At<Vec3b>(0, 0)[2]; // 0–255

                // Igual ao Python: lower = [max(h-tol,0), max(s-70,50), max(v-70,50)]
                // satMin/valMin da config substituem o hardcoded 50, mas nunca ficam abaixo de 50
                _lowerHsv = new Scalar(
                    Math.Max(0,                   h - tol),
                    Math.Max(Math.Max(satMin, 50), s - 70),
                    Math.Max(Math.Max(valMin, 50), v - 70));
                _upperHsv = new Scalar(
                    Math.Min(179, h + tol),
                    255, 255);
            }
            catch
            {
                // Fallback: vermelho puro
                _lowerHsv = new Scalar(0,  100, 100);
                _upperHsv = new Scalar(10, 255, 255);
            }
        }

        private static float Dist(PointF a, PointF b)
            => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PixelBotDetection — resultado de uma detecção
    // ─────────────────────────────────────────────────────────────────────────
    public class PixelBotDetection
    {
        public PointF      Center { get; set; }
        public RectangleF  Bounds { get; set; }
        public ulong       Id     { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PixelBotBlob — dado de um blob para visualização
    // ─────────────────────────────────────────────────────────────────────────
    public struct PixelBotBlob
    {
        public RectangleF Bounds     { get; set; }
        public PointF     Center     { get; set; }
        public bool       IsSelected { get; set; }
    }
}
