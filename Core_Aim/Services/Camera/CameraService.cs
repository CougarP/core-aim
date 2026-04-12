using Core_Aim.Services.Configuration;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace Core_Aim.Services.Camera
{
    /// <summary>
    /// Pipeline de captura de câmara (DirectShow) ou ecrã (DXGI OutputDuplication).
    ///
    /// Arquitectura de zero-cópia:
    ///   • MatPool: frames pré-alocados reutilizados — sem alocação no LOH durante captura
    ///   • Slot atómico: Interlocked.Exchange — consumidor sempre recebe o frame mais recente
    ///   • AutoResetEvent: acorda delivery task sem polling activo
    /// </summary>
    public sealed class CameraService : IDisposable
    {
        private readonly AppSettingsService _appSettings;

        // ── Slot atômico ─────────────────────────────────────────────────────
        private Mat?                     _slot;
        private readonly AutoResetEvent _frameReady = new(false);

        // ── Pool de frames (pré-alocados, sem alocação LOH em regime) ─────────
        internal volatile MatPool? Pool;

        // ── Slot interno DXGI: leitor → publicador ────────────────────────────
        // O leitor DXGI corre à velocidade do hardware e escreve aqui.
        // O publicador corre a 240fps fixos e lê daqui para publicar no _slot.
        private volatile Mat? _dxgiLatest;

        // ── Controle de ciclo de vida ─────────────────────────────────────────
        private volatile bool            _isRunning;
        private CancellationTokenSource? _cts;
        private Thread?                  _captureThread;
        private Thread?                  _dxgiPublishThread;
        private Task?                    _deliveryTask;
        private VideoCapture?            _capture;
        private MfSourceReaderCapture?   _mfNativeCapture;

        /// <summary>Disparado quando a captura de tela falha ao inicializar (ex: GPU bloqueada, modo exclusivo).</summary>
        public event Action<string>? OnCaptureError;

        // ── Métricas ──────────────────────────────────────────────────────────
        private int             _rawCount;
        private readonly Stopwatch _fpsSw = Stopwatch.StartNew();
        public int CaptureFps { get; private set; }

        // ── Contadores de diagnóstico (reset por segundo em MainViewModel) ────
        public int PublishCount;  // frames publicados pelo loop de captura
        public int DeliverCount;  // frames entregues pelo DeliveryTask

        public bool IsRunning => _isRunning;

        // Resolução real capturada pelo DXGI (0 antes do primeiro frame)
        public int CaptureWidth  { get; private set; }
        public int CaptureHeight { get; private set; }

        /// <summary>FPS nativo negociado pelo driver (ex: 240). Não muda durante captura.</summary>
        public int NativeFps { get; private set; }

        /// <summary>Disparado pela delivery task quando um frame novo está pronto.</summary>
        public event EventHandler<Mat>? NewFrameAvailable;

        // ── Suprime mensagens de diagnóstico do OpenCV ────────────────────────
        static CameraService()
        {
            try { Environment.SetEnvironmentVariable("OPENCV_LOG_LEVEL",    "OFF"); } catch { }
            try { Environment.SetEnvironmentVariable("OPENCV_VIDEOIO_DEBUG", "0");  } catch { }
        }

        public CameraService(AppSettingsService appSettings) => _appSettings = appSettings;

        // ═════════════════════════════════════════════════════════════════════
        // Inicialização
        // ═════════════════════════════════════════════════════════════════════

        public async Task<bool> StartCaptureAsync(int mode = 0, int cropW = 640, int cropH = 640)
        {
            if (_isRunning) await StopCaptureAsync();

            // ── Backend 5 = Media Foundation nativo (IMFSourceReader) ────────────
            // Captura nativa NV12/YUY2 com MF_LOW_LATENCY, 240fps+.
            // Bypassa OpenCV VideoCapture completamente.
            if (mode == 0 && _appSettings.CaptureBackend == 5)
                return await StartMfNativeCaptureAsync();

            return await Task.Run(() =>
            {
                try
                {
                    _cts       = new CancellationTokenSource();
                    _isRunning = true;
                    _rawCount  = 0;
                    CaptureFps = 0;
                    _fpsSw.Restart();

                    if (mode == 0) // ── Câmara (DirectShow) ──
                    {
                        // Pool criado dentro de CameraLoop após detectar resolução real da câmara
                        int idx = Math.Max(0, _appSettings.SelectedCameraIndex);
                        _capture = OpenCamera(idx);
                        if (_capture == null) { _isRunning = false; return false; }

                        _captureThread = new Thread(() => CameraLoop(_cts.Token, cropW, cropH))
                        {
                            Name         = "Capture_Cam",
                            IsBackground = true,
                            Priority     = ThreadPriority.Highest
                        };
                    }
                    else // ── Ecrã (DXGI) — Pool criado dentro de DxgiLoop após detectar resolução ──
                    {
                        CaptureWidth  = 0;
                        CaptureHeight = 0;

                        _captureThread = new Thread(() => DxgiLoop(_cts.Token))
                        {
                            Name         = "Capture_DXGI_Read",
                            IsBackground = true,
                            Priority     = ThreadPriority.Highest
                        };
                        _dxgiPublishThread = new Thread(() => DxgiPublishLoop(_cts.Token))
                        {
                            Name         = "Capture_DXGI_Pub",
                            IsBackground = true,
                            Priority     = ThreadPriority.Highest
                        };
                    }

                    _captureThread.Start();
                    _dxgiPublishThread?.Start();
                    StartDelivery(_cts.Token);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Camera] Start: {ex.Message}");
                    _isRunning = false;
                    return false;
                }
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // MF Native capture path (backend 5) — IMFSourceReader com NV12/YUY2
        // ─────────────────────────────────────────────────────────────────────

        private async Task<bool> StartMfNativeCaptureAsync()
        {
            try
            {
                _cts       = new CancellationTokenSource();
                _isRunning = true;
                _rawCount  = 0;
                CaptureFps = 0;
                _fpsSw.Restart();

                var (advFps, _) = ParseAdvancedConfig(_appSettings.CaptureAdvancedConfig);
                int w   = Math.Max(1, _appSettings.CaptureWidth);
                int h   = Math.Max(1, _appSettings.CaptureHeight);
                int fps = Math.Max(1, advFps);
                int idx = Math.Max(0, _appSettings.SelectedCameraIndex);

                _mfNativeCapture = new MfSourceReaderCapture();
                bool ok = await Task.Run(() => _mfNativeCapture.Start(idx, w, h, fps, OnMfNativeFrame));

                if (!ok)
                {
                    _isRunning = false;
                    _mfNativeCapture.Dispose();
                    _mfNativeCapture = null;
                    return false;
                }

                NativeFps = _mfNativeCapture.NegotiatedFps;

                StartDelivery(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MF Native] StartCaptureAsync: {ex.Message}");
                _isRunning = false;
                return false;
            }
        }

        private void OnMfNativeFrame(Mat frame)
        {
            if (!_isRunning) { frame.Dispose(); return; }

            if (Pool == null)
                Pool = new MatPool(frame.Rows, frame.Cols, MatType.CV_8UC3, 8);

            var pub = Pool?.Rent();
            if (pub == null) { frame.Dispose(); return; }

            frame.CopyTo(pub);
            frame.Dispose();
            Publish(pub);
        }

        // ─────────────────────────────────────────────────────────────────────

        public async Task StopCaptureAsync()
        {
            if (!_isRunning) return;
            _isRunning = false;

            _cts?.Cancel();
            _frameReady.Set(); // desbloqueia delivery task

            if (_captureThread?.IsAlive == true)
                await Task.Run(() => _captureThread.Join(1000));

            if (_dxgiPublishThread?.IsAlive == true)
                await Task.Run(() => _dxgiPublishThread.Join(1000));
            _dxgiPublishThread = null;

            if (_deliveryTask != null)
                await Task.WhenAny(_deliveryTask, Task.Delay(800));

            // Devolve frames residuais ao pool antes de destruí-lo
            var p = Pool;
            Pool = null;
            p?.Return(Interlocked.Exchange(ref _slot, null));
            p?.Return(Interlocked.Exchange(ref _dxgiLatest, null));
            p?.Dispose();

            _capture?.Release();
            _capture?.Dispose();
            _capture = null;

            // Cleanup MF Native backend
            if (_mfNativeCapture != null)
            {
                await Task.Run(() => _mfNativeCapture.Stop());
                _mfNativeCapture = null;
            }

            CaptureFps = 0;
            NativeFps  = 0;
        }

        public void Dispose()
        {
            try { StopCaptureAsync().GetAwaiter().GetResult(); } catch { }
            _frameReady.Dispose();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Delivery Task
        // ═════════════════════════════════════════════════════════════════════

        private void StartDelivery(CancellationToken token)
        {
            _deliveryTask = Task.Factory.StartNew(() =>
            {
                while (_isRunning && !token.IsCancellationRequested)
                {
                    if (!_frameReady.WaitOne(8)) continue;

                    var frame = Interlocked.Exchange(ref _slot, null);
                    if (frame == null || frame.IsDisposed) continue;

                    try
                    {
                        NewFrameAvailable?.Invoke(this, frame);
                        Interlocked.Increment(ref DeliverCount);
                    }
                    catch { Pool?.Return(frame); }
                }
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Loop de câmara (DirectShow) — corta para modelo, sem Clone no hot path
        // ═════════════════════════════════════════════════════════════════════

        private void CameraLoop(CancellationToken token, int cropW, int cropH)
        {
            using var full = new Mat();
            while (!token.IsCancellationRequested && _isRunning && _capture != null)
            {
                try
                {
                    if (!_capture.IsOpened() || !_capture.Read(full) || full.Empty())
                    {
                        Thread.SpinWait(100);
                        continue;
                    }

                    // Cria Pool com resolução real da câmara após o primeiro frame
                    if (Pool == null)
                        Pool = new MatPool(full.Height, full.Width, MatType.CV_8UC3, 8);

                    var pub = Pool?.Rent();
                    if (pub == null) { Thread.SpinWait(100); continue; }

                    // Publica frame completo — recorte para inferência feito em OnCameraFrameReceived
                    full.CopyTo(pub);
                    Publish(pub);
                }
                catch { Thread.SpinWait(100); }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Loop DXGI — zero-timeout, máxima velocidade, sem Clone no hot path
        // ═════════════════════════════════════════════════════════════════════

        private void DxgiLoop(CancellationToken token)
        {
            int failCount = 0;

            while (!token.IsCancellationRequested && _isRunning)
            {
                SharpDX.DXGI.Factory1?          factory = null;
                SharpDX.DXGI.Adapter1?          adapter = null;
                SharpDX.Direct3D11.Device?      d3d     = null;
                SharpDX.DXGI.Output1?           out1    = null;
                SharpDX.DXGI.OutputDuplication? dup     = null;
                SharpDX.Direct3D11.Texture2D?   staging = null;

                try
                {
                    factory = new SharpDX.DXGI.Factory1();

                    // Tenta todos os adaptadores/outputs até encontrar um que suporte DuplicateOutput.
                    // Necessário em sistemas multi-GPU (Intel + NVIDIA/AMD): o output primário pode
                    // estar num adaptador diferente do que tentamos primeiro.
                    SharpDX.DXGI.OutputDescription desc = default;
                    bool found = false;

                    for (int ai = 0; ai < 8 && !found; ai++)
                    {
                        SharpDX.DXGI.Adapter1? a;
                        try { a = factory.GetAdapter1(ai); } catch { break; }

                        SharpDX.Direct3D11.Device? dev;
                        try { dev = new SharpDX.Direct3D11.Device(a); }
                        catch { a.Dispose(); continue; }

                        for (int oi = 0; oi < 8 && !found; oi++)
                        {
                            SharpDX.DXGI.Output? o;
                            try { o = a.GetOutput(oi); } catch { break; }

                            var d = o.Description;
                            if (!d.IsAttachedToDesktop) { o.Dispose(); continue; }

                            var o1 = o.QueryInterface<SharpDX.DXGI.Output1>();
                            o.Dispose();

                            try
                            {
                                var candidate = o1.DuplicateOutput(dev);
                                // Sucesso — usa este adaptador/output
                                adapter = a;
                                d3d     = dev;
                                out1    = o1;
                                dup     = candidate;
                                desc    = d;
                                found   = true;
                            }
                            catch { o1.Dispose(); }
                        }

                        if (!found) { dev.Dispose(); a.Dispose(); }
                    }

                    if (!found)
                        throw new InvalidOperationException(
                            "DuplicateOutput falhou em todos os adaptadores. " +
                            "Execute o jogo em modo janela sem borda (borderless window) ou verifique se outro processo está capturando a tela.");

                    // Init bem-sucedido — reseta contador de falhas
                    failCount = 0;

                    int sw = desc.DesktopBounds.Right  - desc.DesktopBounds.Left;
                    int sh = desc.DesktopBounds.Bottom - desc.DesktopBounds.Top;

                    // Cria Pool com frames de resolução completa do ecrã (apenas se ainda não existe)
                    if (Pool == null)
                        Pool = new MatPool(sh, sw, MatType.CV_8UC3, 8);
                    CaptureWidth  = sw;
                    CaptureHeight = sh;

                    // Staging de resolução completa — sem crop de hardware
                    staging = new SharpDX.Direct3D11.Texture2D(d3d,
                        new SharpDX.Direct3D11.Texture2DDescription
                        {
                            Width             = sw,
                            Height            = sh,
                            MipLevels         = 1,
                            ArraySize         = 1,
                            Format            = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                            SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                            Usage             = SharpDX.Direct3D11.ResourceUsage.Staging,
                            BindFlags         = SharpDX.Direct3D11.BindFlags.None,
                            CpuAccessFlags    = SharpDX.Direct3D11.CpuAccessFlags.Read,
                            OptionFlags       = SharpDX.Direct3D11.ResourceOptionFlags.None
                        });

                    // ── Loop interno (leitor) ──────────────────────────────────
                    // Apenas adquire frames DXGI à velocidade máxima do hardware
                    // e escreve no slot _dxgiLatest. O DxgiPublishLoop lê desse
                    // slot a 240fps fixos e publica (com duplicação se necessário).
                    while (!token.IsCancellationRequested && _isRunning)
                    {
                        SharpDX.DXGI.Resource? res      = null;
                        bool                   acquired = false;
                        try
                        {
                            var hr = dup.TryAcquireNextFrame(
                                0,
                                out SharpDX.DXGI.OutputDuplicateFrameInformation _,
                                out res);

                            if (hr.Failure || res == null)
                            {
                                Thread.SpinWait(500);
                                continue;
                            }
                            acquired = true;

                            using var tex = res.QueryInterface<SharpDX.Direct3D11.Texture2D>();

                            // Copia ecrã completo para staging (sem crop)
                            d3d.ImmediateContext.CopyResource(tex, staging);

                            var box = d3d.ImmediateContext.MapSubresource(
                                staging, 0,
                                SharpDX.Direct3D11.MapMode.Read,
                                SharpDX.Direct3D11.MapFlags.None);
                            try
                            {
                                var raw = Pool?.Rent();
                                if (raw == null) continue;

                                using var bgra = Mat.FromPixelData(
                                    sh, sw, MatType.CV_8UC4,
                                    box.DataPointer, (long)box.RowPitch);
                                Cv2.CvtColor(bgra, raw, ColorConversionCodes.BGRA2BGR);

                                // Entrega ao publicador — devolve frame anterior ao pool
                                var old = Interlocked.Exchange(ref _dxgiLatest, raw);
                                Pool?.Return(old);
                            }
                            finally
                            {
                                d3d.ImmediateContext.UnmapSubresource(staging, 0);
                            }
                        }
                        catch (SharpDX.SharpDXException ex)
                            when (ex.ResultCode == SharpDX.DXGI.ResultCode.WaitTimeout)
                        {
                            Thread.SpinWait(500);
                        }
                        catch (SharpDX.SharpDXException ex)
                            when (ex.ResultCode == SharpDX.DXGI.ResultCode.AccessLost    ||
                                  ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceRemoved ||
                                  ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceReset)
                        {
                            Debug.WriteLine($"[DXGI] Session lost ({ex.ResultCode}), reinitializing.");
                            break;
                        }
                        finally
                        {
                            res?.Dispose();
                            if (acquired) try { dup.ReleaseFrame(); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    Debug.WriteLine($"[DXGI] Init error #{failCount}: {ex.Message}");

                    if (failCount >= 3)
                    {
                        OnCaptureError?.Invoke("DXGI indisponível — usando captura GDI (performance reduzida). Execute o jogo em modo janela sem borda para melhor desempenho.");
                        GdiCaptureLoop(token);
                        return;
                    }

                    if (_isRunning) Thread.Sleep(500);
                }
                finally
                {
                    staging?.Dispose();
                    dup?.Dispose();
                    out1?.Dispose();
                    d3d?.Dispose();
                    adapter?.Dispose();
                    factory?.Dispose();
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Loop publicador DXGI — 240fps fixos, independente da taxa do hardware
        //
        // Lê o último frame entregue por DxgiLoop (_dxgiLatest).
        // Se há frame novo  → actualiza lastGood, publica cópia.
        // Se não há frame novo → duplica lastGood e publica.
        // Resultado: Publish é chamado exactamente a 240fps mesmo com ecrã
        // estático, garantindo fps constante no pipeline.
        // ═════════════════════════════════════════════════════════════════════

        private void DxgiPublishLoop(CancellationToken token)
        {
            long TargetTicks = Stopwatch.Frequency / 240; // ticks por frame a 240fps
            Mat?       lastGood    = null;
            long       nextTick    = Stopwatch.GetTimestamp();

            try
            {
                while (!token.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                    // Tenta obter frame novo do leitor DXGI
                    var latest = Interlocked.Exchange(ref _dxgiLatest, null);
                    if (latest != null)
                    {
                        Pool?.Return(lastGood); // devolve o frame anterior ao pool
                        lastGood = latest;      // agora somos donos deste frame
                    }

                    // Publica (frame real ou duplicado)
                    if (lastGood != null && !lastGood.IsDisposed)
                    {
                        var pub = Pool?.Rent();
                        if (pub != null)
                        {
                            lastGood.CopyTo(pub);
                            Publish(pub);
                        }
                    }
                    }
                    catch (Exception ex) { Debug.WriteLine($"[DxgiPub] {ex.Message}"); }

                    // Pace a 240fps — dorme o que faltar até ao próximo tick alvo
                    nextTick += TargetTicks;
                    long rem = nextTick - Stopwatch.GetTimestamp();
                    if (rem > 0)
                    {
                        int ms = (int)(rem * 1000L / Stopwatch.Frequency);
                        if (ms >= 2) Thread.Sleep(ms - 1);
                        // Spin fino para precisão sub-ms
                        while (Stopwatch.GetTimestamp() < nextTick)
                            Thread.SpinWait(10);
                    }
                    else
                    {
                        // Atrasado — repõe o alvo para evitar acumulação de dívida
                        nextTick = Stopwatch.GetTimestamp();
                    }
                }
            }
            finally
            {
                Pool?.Return(lastGood);
                Pool?.Return(Interlocked.Exchange(ref _dxgiLatest, null));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Publicação de frame — devolve frame antigo ao pool (sem Dispose/free)
        // ═════════════════════════════════════════════════════════════════════

        private void Publish(Mat frame)
        {
            var old = Interlocked.Exchange(ref _slot, frame);
            Pool?.Return(old);

            Interlocked.Increment(ref _rawCount);
            Interlocked.Increment(ref PublishCount);
            if (_fpsSw.ElapsedMilliseconds >= 1000)
            {
                CaptureFps = Interlocked.Exchange(ref _rawCount, 0);
                _fpsSw.Restart();
            }

            _frameReady.Set();
        }

        // ═════════════════════════════════════════════════════════════════════
        // GDI fallback — usado quando DXGI falha (modo exclusivo, HAGS, etc.)
        // Captura a ~60fps via Graphics.CopyFromScreen, mais lento que DXGI
        // mas funciona em qualquer configuração de GPU/driver.
        // ═════════════════════════════════════════════════════════════════════

        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

        private void GdiCaptureLoop(CancellationToken token)
        {
            int sw = GetSystemMetrics(0); // SM_CXSCREEN
            int sh = GetSystemMetrics(1); // SM_CYSCREEN

            if (sw <= 0 || sh <= 0) return;

            if (Pool == null)
                Pool = new MatPool(sh, sw, MatType.CV_8UC3, 8);
            CaptureWidth  = sw;
            CaptureHeight = sh;

            long targetTicks = Stopwatch.Frequency / 60;
            long nextTick    = Stopwatch.GetTimestamp();

            using var bmp = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
            using var gfx = Graphics.FromImage(bmp);
            var size = new System.Drawing.Size(sw, sh);
            var rect = new Rectangle(0, 0, sw, sh);

            while (!token.IsCancellationRequested && _isRunning)
            {
                try
                {
                    gfx.CopyFromScreen(0, 0, 0, 0, size, CopyPixelOperation.SourceCopy);

                    var raw = Pool?.Rent();
                    if (raw != null)
                    {
                        var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        try
                        {
                            using var bgra = Mat.FromPixelData(sh, sw, MatType.CV_8UC4, bd.Scan0, bd.Stride);
                            Cv2.CvtColor(bgra, raw, ColorConversionCodes.BGRA2BGR);
                        }
                        finally { bmp.UnlockBits(bd); }

                        Publish(raw);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GDI] {ex.Message}");
                    if (_isRunning) Thread.Sleep(100);
                }

                nextTick += targetTicks;
                long rem = nextTick - Stopwatch.GetTimestamp();
                if (rem > 0)
                {
                    int ms = (int)(rem * 1000L / Stopwatch.Frequency);
                    if (ms >= 2) Thread.Sleep(ms - 1);
                    while (Stopwatch.GetTimestamp() < nextTick)
                        Thread.SpinWait(10);
                }
                else
                {
                    nextTick = Stopwatch.GetTimestamp();
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // OpenCamera (DirectShow / MSMF) — com resolução e FPS configuráveis
        //
        // Backend:
        //  0 = Auto  (DSHOW → MSMF, sem MJPEG)
        //  1 = DSHOW (sem MJPEG — webcam/câmara padrão YUV2/NV12)
        //  2 = DSHOW + MJPEG (placa de captura — necessário para >60fps)
        //  3 = MSMF  (Media Foundation, sem MJPEG)
        //  4 = MSMF + MJPEG (placa de captura via WMF)
        //
        // Resolução (CaptureResolutionIndex):  0=1080p | 1=720p | 2=480p
        // FPS alvo  (CaptureFpsIndex):          0=30    | 1=60  | 2=120 | 3=240
        //
        // Ordem correta das propriedades OpenCV + DirectShow:
        //   1. FourCC (codec) — PRIMEIRO, antes de resolução e fps
        //   2. FrameWidth / FrameHeight
        //   3. Fps
        //   4. BufferSize
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Analisa "CAP_PROP_FPS=60\nCAP_PROP_FOURCC=MJPG" → (fps, forceMjpeg).
        /// </summary>
        private static (int fps, bool forceMjpg) ParseAdvancedConfig(string? cfg)
        {
            int  fps      = 60;
            bool forceMjpg = false;
            if (string.IsNullOrWhiteSpace(cfg)) return (fps, forceMjpg);
            foreach (var raw in cfg.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = raw.IndexOf('=');
                if (eq < 1) continue;
                var key = raw[..eq].Trim().ToUpperInvariant();
                var val = raw[(eq + 1)..].Trim();
                if (key == "CAP_PROP_FPS" && int.TryParse(val, out int f) && f > 0) fps = f;
                if (key == "CAP_PROP_FOURCC" && val.ToUpperInvariant() is "MJPG" or "MJPEG") forceMjpg = true;
            }
            return (fps, forceMjpg);
        }

        private VideoCapture? OpenCamera(int index)
        {
            int w   = Math.Max(1, _appSettings.CaptureWidth);
            int h   = Math.Max(1, _appSettings.CaptureHeight);
            var (advFps, advMjpg) = ParseAdvancedConfig(_appSettings.CaptureAdvancedConfig);
            int fps = Math.Max(1, advFps);

            var orig = Console.Error;
            Console.SetError(TextWriter.Null);
            try
            {
                VideoCapture? result;
                int nfps;
                switch (_appSettings.CaptureBackend)
                {
                    case 1:
                        result = TryOpenCamera(index, VideoCaptureAPIs.MSMF, advMjpg, w, h, fps, out nfps);
                        break;
                    case 2:
                        result = TryOpenCamera(index, VideoCaptureAPIs.DSHOW, advMjpg, w, h, fps, out nfps);
                        break;
                    case 3:
                        result = TryOpenCamera(index, VideoCaptureAPIs.DSHOW, true, w, h, fps, out nfps);
                        break;
                    case 4:
                        result = TryOpenCamera(index, VideoCaptureAPIs.MSMF, true, w, h, fps, out nfps);
                        break;
                    default: // 0 = Auto: DSHOW+MJPEG → MSMF+MJPEG → DSHOW (sem MJPEG)
                        // MJPEG é essencial para taxas >60fps em placas de captura.
                        // Se a primeira tentativa falhar (ex: câmera presa de release
                        // anterior), tenta MSMF antes de cair para sem-MJPEG, evitando
                        // o bug em que reconnect deixava a câmera capada a ~120fps.
                        result = TryOpenCamera(index, VideoCaptureAPIs.DSHOW, true, w, h, fps, out nfps);
                        if (result == null)
                            result = TryOpenCamera(index, VideoCaptureAPIs.MSMF, true, w, h, fps, out nfps);
                        if (result == null)
                            result = TryOpenCamera(index, VideoCaptureAPIs.DSHOW, false, w, h, fps, out nfps);
                        break;
                }
                if (nfps > 0) NativeFps = nfps;
                return result;
            }
            finally { Console.SetError(orig); }
        }

        /// <summary>
        /// Abre VideoCapture com os parâmetros exactos pedidos.
        /// Ordem obrigatória DirectShow/MSMF: FourCC → Width → Height → Fps → BufferSize.
        /// O driver negoceia o formato mais próximo suportado; o resultado real é lido de volta e logado.
        /// </summary>
        private static VideoCapture? TryOpenCamera(int index, VideoCaptureAPIs backend,
            bool forceMjpg, int w, int h, int fps, out int negotiatedFps)
        {
            negotiatedFps = 0;
            try
            {
                // VideoCapture constructor pode bloquear em drivers lentos (MSMF).
                // Timeout de 8s para não travar startup indefinidamente.
                VideoCapture? c = null;
                var openTask = Task.Run(() => c = new VideoCapture(index, backend));
                if (!openTask.Wait(TimeSpan.FromSeconds(8)))
                {
                    Console.WriteLine($"[Camera] TIMEOUT ao abrir {backend} idx={index} (>8s)");
                    return null;
                }
                if (c == null || !c.IsOpened()) { c?.Dispose(); return null; }

                // ── Ordem crítica para 240fps em placas de captura ─────────────
                // 1. FourCC PRIMEIRO — codec antes de qualquer negociação
                if (forceMjpg)
                    c.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));

                // 2. FPS ANTES da resolução — DirectShow seleciona o media type
                //    (ex: MJPEG@240fps vs MJPEG@120fps) quando recebe o FPS pedido;
                //    sem este set antecipado, o driver fixa no primeiro media type
                //    encontrado para aquela resolução (normalmente 120fps).
                c.Set(VideoCaptureProperties.Fps, fps);

                // 3. Resolução
                c.Set(VideoCaptureProperties.FrameWidth,  w);
                c.Set(VideoCaptureProperties.FrameHeight, h);

                // 4. FPS novamente — confirma negociação após a resolução ter sido aceite
                c.Set(VideoCaptureProperties.Fps, fps);

                // 5. Buffer adaptativo:
                //    ≥120fps → 3 frames (~12ms tolerância — evita drops sem latência excessiva)
                //    ≥ 60fps → 2 frames (~16ms)
                //    < 60fps → 1 frame  (latência mínima para webcams lentas)
                int bufSize = fps >= 120 ? 3 : fps >= 60 ? 2 : 1;
                c.Set(VideoCaptureProperties.BufferSize, bufSize);

                // Lê o que foi efectivamente negociado pelo driver
                double gotW   = c.Get(VideoCaptureProperties.FrameWidth);
                double gotH   = c.Get(VideoCaptureProperties.FrameHeight);
                double gotFps = c.Get(VideoCaptureProperties.Fps);

                // Muitos drivers (especialmente placas de captura) ecoam o valor SET
                // em vez do FPS real do media type negociado. Para detectar isso,
                // medimos o intervalo entre os primeiros frames reais.
                //
                // IMPORTANTE: o driver demora a estabilizar — os primeiros frames têm
                // latência de init e fazem a medição parecer muito mais lenta do que
                // o regime real. Descartamos um warmup grande antes de medir.
                int measuredFps = 0;
                try
                {
                    using var probe = new OpenCvSharp.Mat();

                    // Warmup: descarta até 60 frames ou 800ms — o que vier primeiro
                    var warmup = System.Diagnostics.Stopwatch.StartNew();
                    int warmedFrames = 0;
                    while (warmup.ElapsedMilliseconds < 800 && warmedFrames < 60)
                    {
                        if (c.Read(probe) && !probe.Empty()) warmedFrames++;
                    }

                    // Medição: 30 frames de regime estável
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    int probeCount = 0;
                    while (sw.ElapsedMilliseconds < 1500 && probeCount < 30)
                    {
                        if (c.Read(probe) && !probe.Empty()) probeCount++;
                    }
                    sw.Stop();
                    if (probeCount >= 5 && sw.ElapsedMilliseconds > 0)
                    {
                        double realFps = probeCount * 1000.0 / sw.ElapsedMilliseconds;
                        measuredFps = (int)Math.Round(realFps);
                        Console.WriteLine($"[Camera] Warmup={warmedFrames}f  Medido: {probeCount} frames em {sw.ElapsedMilliseconds}ms → {realFps:F1} fps real");
                    }
                }
                catch { /* probe failed, use driver value */ }

                // Se o driver ecoou um valor muito diferente do medido, confiar na medição
                int finalFps;
                if (measuredFps > 0 && Math.Abs(gotFps - measuredFps) > measuredFps * 0.3)
                {
                    finalFps = measuredFps;
                    Console.WriteLine($"[Camera] Driver FPS ({gotFps:0}) diverge do medido ({measuredFps}) — usando medido");
                }
                else
                {
                    finalFps = measuredFps > 0 ? measuredFps : (int)Math.Round(gotFps);
                }

                Console.WriteLine($"[Camera] Backend={backend}  MJPEG={forceMjpg}  " +
                                  $"Pedido={w}×{h}@{fps}fps  →  " +
                                  $"Negociado={gotW:0}×{gotH:0}@{gotFps:0}fps  Real={finalFps}fps");
                negotiatedFps = finalFps;
                return c;
            }
            catch { return null; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Enumeração de câmeras (DirectShow)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enumera câmeras/placas de captura disponíveis.
        /// Testa DSHOW+MJPEG, DSHOW sem MJPEG e MSMF para detectar
        /// correctamente placas de captura e as suas resoluções reais.
        /// </summary>
        public static List<string> GetAvailableCameras()
        {
            // Apenas lista nomes via DirectShow COM — NÃO abre nenhum dispositivo.
            // Probing (abrir câmera para testar resolução/FPS) é lento, instável e
            // pode crashar com drivers problemáticos. O formato é negociado ao iniciar.
            var names = GetDirectShowCameraNames();
            var cameras = new List<string>();
            for (int i = 0; i < names.Count; i++)
                cameras.Add($"{names[i]}  [Index {i}]");

            if (cameras.Count == 0) cameras.Add("No Camera Found");
            return cameras;
        }

        /// <summary>
        /// Testa um índice de câmara com o backend e codec indicados.
        /// Retorna descrição "WxH@FPS (codec)" ou null se não abrir.
        /// </summary>
        private static string? ProbeCamera(int index, VideoCaptureAPIs backend, bool mjpeg)
        {
            try
            {
                using var t = new VideoCapture(index, backend);
                if (!t.IsOpened()) return null;

                if (mjpeg)
                    t.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));

                // Experimenta 1080p primeiro, depois 720p
                foreach (var (pw, ph) in new[] { (1920, 1080), (1280, 720) })
                {
                    t.Set(VideoCaptureProperties.FrameWidth,  pw);
                    t.Set(VideoCaptureProperties.FrameHeight, ph);
                    double gw  = t.Get(VideoCaptureProperties.FrameWidth);
                    double gh  = t.Get(VideoCaptureProperties.FrameHeight);
                    double gfp = t.Get(VideoCaptureProperties.Fps);
                    if (gw > 0 && gh > 0)
                        return $"— {gw:0}×{gh:0} @{gfp:0}fps ({(mjpeg ? "MJPEG" : backend == VideoCaptureAPIs.MSMF ? "MSMF" : "DSHOW")})";
                }
                return null;
            }
            catch { return null; }
        }

        private static List<string> GetDirectShowCameraNames()
        {
            var names = new List<string>();
            try
            {
                ICreateDevEnum? devEnum = null;
                IEnumMoniker?   enumMon = null;
                try
                {
                    var t = Type.GetTypeFromCLSID(new Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86"));
                    if (t == null) return names;
                    devEnum = (ICreateDevEnum?)Activator.CreateInstance(t);
                    if (devEnum == null) return names;
                    var vg = new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
                    devEnum.CreateClassEnumerator(ref vg, out enumMon, 0);
                    if (enumMon == null) return names;
                    IMoniker[] mk = new IMoniker[1];
                    while (enumMon.Next(1, mk, IntPtr.Zero) == 0)
                    {
                        try
                        {
                            object? bagObj = null;
                            mk[0].BindToStorage(null, null, ref IID_IPropertyBag, out bagObj);
                            if (bagObj is IPropertyBag bag)
                            { bag.Read("FriendlyName", out object val, null); names.Add((string)val); }
                        }
                        catch { }
                        finally { Marshal.ReleaseComObject(mk[0]); }
                    }
                }
                finally
                {
                    if (enumMon != null) Marshal.ReleaseComObject(enumMon);
                    if (devEnum != null) Marshal.ReleaseComObject(devEnum);
                }
            }
            catch { }
            return names;
        }

        [ComImport, Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICreateDevEnum
        { [PreserveSig] int CreateClassEnumerator([In] ref Guid pType, [Out] out IEnumMoniker ppEnumMoniker, [In] int dwFlags); }

        [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyBag
        {
            [PreserveSig] int Read([In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
                                   [Out, MarshalAs(UnmanagedType.Struct)] out object pVar,
                                   [In] object? pErrorLog);
            [PreserveSig] int Write([In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
                                    [In, MarshalAs(UnmanagedType.Struct)] ref object pVar);
        }
        private static Guid IID_IPropertyBag = new Guid("55272A00-42CB-11CE-8135-00AA004BB851");
    }
}
