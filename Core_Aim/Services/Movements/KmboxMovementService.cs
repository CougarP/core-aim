using Core_Aim.Services.Configuration;
using Core_Aim.Services.Hardware;
using KMBox.NET;
using KMBox.NET.Structures;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Core_Aim.Services.Movements
{
    /// <summary>
    /// KmBox Movement Service — controle proporcional puro com EMA smoothing.
    ///
    /// Princípio: move = screenError × speed
    ///   • screenError = erro em pixels reais da tela (escala correta)
    ///   • speed ∈ (0, 1) → sempre se aproxima do alvo, NUNCA ultrapassa
    ///   • Logo: matematicamente impossível de oscilar para qualquer speed < 1.0
    ///
    /// Dois parâmetros únicos expostos na UI:
    ///   • Velocidade  (speed 0.05–0.90)  — default 0.35
    ///   • Suavidade   (EMA alpha 0.10–0.90) — default 0.40
    /// </summary>
    public class KmboxMovementService : IMovementService
    {
        private readonly AppSettingsService _settings;

        private volatile KmBoxClient? _client;
        private ReportListener? _listener;
        private readonly SemaphoreSlim _opLock = new(1, 1);

        private volatile bool _isRightMouseButtonDown;
        private volatile bool _isLeftMouseButtonDown;
        private volatile bool _isConnected;

        // ── EMA smoothing do alvo (YOLO) ────────────────────────────────────
        private float _smoothX = float.NaN;
        private float _smoothY = float.NaN;

        // ── PixelBot: EMA aplicada no DELTA de movimento (amortecedor real) ─
        // Diferença do YOLO: filtramos a SAÍDA (quanto movemos), não a entrada.
        // Efeito: aceleração/desaceleração suave — resistência a mudanças bruscas.
        private float _pbSmoothMoveX;
        private float _pbSmoothMoveY;

        // ── Sub-pixel carry-over (sem perda de precisão fracional) ───────────
        private float _residualX;
        private float _residualY;

        // (histerese removida — controle proporcional converge naturalmente para zero)

        // ── Controle de burst ────────────────────────────────────────────────
        private bool _wasFiring;

        // ── Auto-tuner ───────────────────────────────────────────────────────
        private const int   TunerWindow     = 48;    // ~600ms de histórico a 80fps
        private const long  TunerIntervalMs = 300;   // analisa a cada 300ms
        private const float SpeedMin        = 0.05f;
        private const float SpeedMax        = 0.88f;

        // Contador de períodos consecutivos com oscilação confirmada
        // Exige confirmação antes de reduzir velocidade (evita falso-positivo)
        private int _oscConfirmCount;

        private readonly float[] _tunerErrX = new float[TunerWindow];
        private readonly float[] _tunerErrY = new float[TunerWindow];
        private int  _tunerIdx;
        private int  _tunerCount;
        private long _lastTunerTick;

        // ── Diagnósticos públicos ────────────────────────────────────────────
        public float LastRmsScreen         { get; private set; }
        public float LastOscillationScore  { get; private set; }

        public int AiOutputX => 0;
        public int AiOutputY => 0;
        private int _sendCount;
        public int DrainSendCount() => Interlocked.Exchange(ref _sendCount, 0);

        // ── Anti-recoil ──────────────────────────────────────────────────────
        private long _lastRecoilTime;

        public event Action<bool>? OnConnectionStateChanged;
        public bool IsConnected => _isConnected;
        public string LastError { get; private set; } = "";
        public int LTValue => 0;
        public int RTValue => 0;

        public TT2StickState GetStickState() => default;
        public TT2ButtonState GetButtonState() => default;
        public TT2Info GetDeviceInfo() => default;

        public bool HasActiveRecoil => false;

        public bool IsFiring
        {
            get
            {
                if (!_isConnected) return false;
                return _settings.TriggerSelection switch
                {
                    0 => _isLeftMouseButtonDown,
                    1 => _isRightMouseButtonDown,
                    _ => _isLeftMouseButtonDown || _isRightMouseButtonDown,
                };
            }
        }

        public KmboxMovementService(AppSettingsService settings)
        {
            _settings      = settings;
            _lastTunerTick = Stopwatch.GetTimestamp();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Move — pipeline completo
        //   YOLO  : EMA na posição do alvo  → suaviza detecções instáveis
        //   PB    : EMA no DELTA de saída   → amortecedor real (resistência a jitter)
        // ═══════════════════════════════════════════════════════════════════════
        public void Move(PointF rawTarget, int modelWidth, int modelHeight, bool isPixelBot = false)
        {
            var c = _client;
            if (c == null || !_isConnected) return;

            // Mode 2 = "Constante rastreio sempre ativo" — move sem precisar de gatilho
            bool firing = IsFiring || _settings.InferenceMode == 2;

            // ── Fim do burst → limpa todo o estado ───────────────────────────
            if (!firing)
            {
                if (_wasFiring)
                {
                    _wasFiring      = false;
                    _smoothX        = float.NaN;
                    _smoothY        = float.NaN;
                    _pbSmoothMoveX  = 0f;
                    _pbSmoothMoveY  = 0f;
                    _residualX      = 0f;
                    _residualY      = 0f;
                }
                return;
            }

            // ── Início do burst → estado inicial limpo ────────────────────────
            if (!_wasFiring)
            {
                _wasFiring        = true;
                _smoothX          = float.NaN;
                _smoothY          = float.NaN;
                _pbSmoothMoveX    = 0f;
                _pbSmoothMoveY    = 0f;
                _residualX        = 0f;
                _residualY        = 0f;
                _tunerIdx         = 0;
                _tunerCount       = 0;
                _oscConfirmCount  = 0;
                _lastTunerTick    = Stopwatch.GetTimestamp();
            }

            float centerX = modelWidth  * 0.5f;
            float centerY = modelHeight * 0.5f;

            // ═════════════════════════════════════════════════════════════════
            // PATH PIXELBOT — amortecedor real
            // EMA aplicada no DELTA DE MOVIMENTO (saída), não na posição.
            // Resultado: resistência suave a mudanças bruscas (jitter do blob).
            // alpha baixo = mais amortecimento (resp. lenta, muy suave)
            // alpha alto  = menos amortecimento (resp. rápida, mais direta)
            // ═════════════════════════════════════════════════════════════════
            if (isPixelBot)
            {
                float errX = rawTarget.X - centerX;
                float errY = rawTarget.Y - centerY;
                float dist = MathF.Sqrt(errX * errX + errY * errY);

                // FOV
                float fovR = (modelWidth * 0.5f) * (_settings.RestrictedTracking / 100f);
                if (_settings.RestrictedTracking < 100 && dist > fovR) return;

                // Converte para pixels de tela
                float toScr  = (float)_settings.FovSize / modelWidth;
                float sErrX  = errX * toScr;
                float sErrY  = errY * toScr;
                float sDist  = dist * toScr;

                // Deadzone
                float dz = Math.Max((float)_settings.PixelBotDeadZone, 0.5f);
                if (sDist < dz)
                {
                    // Dentro da zona morta: decai o amortecedor gradualmente (não corta bruscamente)
                    _pbSmoothMoveX *= 0.5f;
                    _pbSmoothMoveY *= 0.5f;
                    return;
                }

                // Velocidade: slider 1–100 → interno /1000 (ex: 35 → 0.035)
                float spd = Math.Clamp((float)_settings.PixelBotTrackingSpeed / 1000f, 0.001f, 0.10f);

                // Delta desejado este frame
                float wantX = sErrX * spd;
                float wantY = sErrY * spd;

                // EMA no DELTA (= amortecedor):
                //   slider 1–99 → alpha = 1 − (slider/100)
                //   slider alto  = mais suave (alpha baixo = mais inércia)
                //   slider baixo = resposta rápida (alpha alto = menos inércia)
                float alpha = 1f - Math.Clamp((float)_settings.PixelBotAimResponse / 100f, 0.01f, 0.99f);
                _pbSmoothMoveX = alpha * wantX + (1f - alpha) * _pbSmoothMoveX;
                _pbSmoothMoveY = alpha * wantY + (1f - alpha) * _pbSmoothMoveY;

                // Sub-pixel carry-over
                float totX = _pbSmoothMoveX + _residualX;
                float totY = _pbSmoothMoveY + _residualY;
                int   sndX = (int)MathF.Round(totX);
                int   sndY = (int)MathF.Round(totY);
                _residualX = totX - sndX;
                _residualY = totY - sndY;

                if (sndX != 0 || sndY != 0)
                {
                    try { _ = c.MouseMoveSimple((short)sndX, (short)sndY); } catch { }
                    Interlocked.Increment(ref _sendCount);
                }
                return;
            }

            // ═════════════════════════════════════════════════════════════════
            // PATH YOLO — EMA na posição (comportamento original inalterado)
            // ═════════════════════════════════════════════════════════════════
            float yAlpha = Math.Clamp((float)_settings.AimResponseCurve, 0.05f, 0.95f);
            if (float.IsNaN(_smoothX))
            {
                _smoothX = rawTarget.X;
                _smoothY = rawTarget.Y;
            }
            else
            {
                _smoothX = yAlpha * rawTarget.X + (1f - yAlpha) * _smoothX;
                _smoothY = yAlpha * rawTarget.Y + (1f - yAlpha) * _smoothY;
            }

            float modelErrX = _smoothX - centerX;
            float modelErrY = _smoothY - centerY;
            float modelDist = MathF.Sqrt(modelErrX * modelErrX + modelErrY * modelErrY);

            float fovRadius = (modelWidth * 0.5f) * (_settings.RestrictedTracking / 100f);
            if (_settings.RestrictedTracking < 100 && modelDist > fovRadius) return;

            float toScreen   = (float)_settings.FovSize / modelWidth;
            float screenErrX = modelErrX * toScreen;
            float screenErrY = modelErrY * toScreen;
            float screenDist = modelDist * toScreen;

            float deadzone = Math.Max((float)_settings.AimDeadZone, 0.5f);
            if (screenDist < deadzone) return;

            float speed  = Math.Clamp((float)_settings.TrackingSpeedHIP, SpeedMin, SpeedMax);
            float moveX  = screenErrX * speed;
            float moveY  = screenErrY * speed;

            float totalX = moveX + _residualX;
            float totalY = moveY + _residualY;
            int sendX    = (int)MathF.Round(totalX);
            int sendY    = (int)MathF.Round(totalY);
            _residualX   = totalX - sendX;
            _residualY   = totalY - sendY;

            if (sendX != 0 || sendY != 0)
            {
                try { _ = c.MouseMoveSimple((short)sendX, (short)sendY); } catch { }
                Interlocked.Increment(ref _sendCount);
            }

            RunAutoTuner(screenErrX, screenErrY, speed);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Auto-Tuner — SOMENTE proteção contra oscilação
        //
        // Regra fundamental: NUNCA aumenta velocidade automaticamente.
        // Motivo: quando o alvo está se movendo, o RMS de erro NUNCA zera —
        // logo qualquer lógica de "RMS alto → aumenta velocidade" cria um loop
        // positivo que sobe até oscilar. O usuário define a velocidade base;
        // o auto-tune apenas a REDUZ quando detecta oscilação real e confirmada.
        //
        // Oscilação confirmada = 2 períodos consecutivos com osc > 35%
        // Isso evita falsos positivos quando o alvo muda de direção bruscamente.
        // ═══════════════════════════════════════════════════════════════════════
        private void RunAutoTuner(float sErrX, float sErrY, float speed)
        {
            _tunerErrX[_tunerIdx] = sErrX;
            _tunerErrY[_tunerIdx] = sErrY;
            _tunerIdx = (_tunerIdx + 1) % TunerWindow;
            if (_tunerCount < TunerWindow) _tunerCount++;

            long now = Stopwatch.GetTimestamp();
            long elapsedMs = (now - _lastTunerTick) * 1000L / Stopwatch.Frequency;
            if (elapsedMs < TunerIntervalMs) return;
            _lastTunerTick = now;

            int n = _tunerCount;
            if (n < 16) return;

            // ── RMS do erro combinado ─────────────────────────────────────────
            double sumSq = 0;
            for (int i = 0; i < n; i++)
                sumSq += _tunerErrX[i] * _tunerErrX[i] + _tunerErrY[i] * _tunerErrY[i];
            float rms = (float)Math.Sqrt(sumSq / (2.0 * n));
            LastRmsScreen = rms;

            // ── Score de oscilação: zero-crossings ponderados por amplitude ───
            // Usa apenas crossings com amplitude significativa (> 2px) para
            // ignorar micro-tremidos do YOLO que não são oscilação real.
            int crossings = 0;
            for (int i = 1; i < n; i++)
            {
                float p = _tunerErrX[(i - 1 + TunerWindow) % TunerWindow];
                float c = _tunerErrX[i % TunerWindow];
                bool crossed = (p > 0f && c < 0f) || (p < 0f && c > 0f);
                bool significant = MathF.Abs(p) > 2f || MathF.Abs(c) > 2f;
                if (crossed && significant) crossings++;
            }
            float osc = (float)crossings / (n - 1);
            LastOscillationScore = osc;

            if (!_settings.AutoTuneEnabled)
            {
                _oscConfirmCount = 0;
                UpdateStatus(speed, rms, osc, "manual");
                return;
            }

            string action = "estável";

            // ── Reduz velocidade apenas com oscilação CONFIRMADA ──────────────
            // Exige 2 períodos consecutivos acima do limiar antes de agir.
            // Limiar = 35%: um alvo mudando de direção pode causar 1 crossing,
            // mas oscilação real produz muitos crossings repetidos.
            if (osc > 0.35f)
            {
                _oscConfirmCount++;
                if (_oscConfirmCount >= 2)
                {
                    float ns = Math.Clamp(speed * 0.88f, SpeedMin, SpeedMax);
                    if (MathF.Abs(ns - speed) > 0.001f)
                    {
                        _settings.TrackingSpeedHIP = ns;
                        speed = ns;
                        action = "↓vel (oscilando)";
                    }
                    _oscConfirmCount = 0; // reset após agir
                }
                else
                {
                    action = "⚠ verificando...";
                }
            }
            else
            {
                // Sem oscilação → zera contador de confirmação
                _oscConfirmCount = 0;
            }

            UpdateStatus(speed, rms, osc, action);
        }

        private void UpdateStatus(float speed, float rms, float osc, string action)
        {
            _settings.AutoTuneStatus =
                $"Vel={speed:F2}  RMS={rms:F1}px  Osc={osc * 100:F0}%  → {action}";
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Stop / Reset
        // ═══════════════════════════════════════════════════════════════════════
        public void Stop()
        {
            _wasFiring     = false;
            _smoothX       = float.NaN;
            _smoothY       = float.NaN;
            _pbSmoothMoveX = 0f;
            _pbSmoothMoveY = 0f;
            _residualX     = 0f;
            _residualY     = 0f;
        }

        public void FlushRecoil() { } // KMBox: recoil é additive via mouse delta, sem flush necessário
        public void ResetPrediction() { /* sem estado de predição nesta arquitetura */ }

        // Log de diagnóstico — sem implementação no KmBox (só TT2 usa stick analógico)
        public bool IsLogging => false;
        public void StartLogging() { }
        public void StopLogging()  { }
        public void SetMacroFlags(uint flags) { }
        public void SetMacroArVertical(double val) { }
        public void SetMacroArHorizontal(double val) { }
        public void SetMacroRapidFireMs(double val) { }
        public void SetPlayerBlend(double val) { }
        public void SetDisableOnMove(double val) { }
        public void SetQuickscopeDelay(double val) { }
        public void SetJumpShotDelay(double val) { }
        public void SetBunnyHopDelay(double val) { }
        public void SetStrafePower(double val) { }
        public bool UploadGpc(byte[] data, byte slot, string name, string author) => false;
        public bool UploadGbc(byte[] gbcData, byte slot) => false;

        // ── Slot/config stubs (KmBox não suporta) ──
        public bool SlotLoad(byte slot) => false;
        public bool SlotUp() => false;
        public bool SlotDown() => false;
        public bool SlotUnload() => false;
        public bool SetOutputProtocol(TT2_Proto protocol) => false;
        public bool SetOutputPolling(TT2_Poll poll) => false;
        public bool SetInputPolling(TT2_Poll poll) => false;
        public bool SetHighSpeedMode(bool enable) => false;
        public void SetInvertX(bool value) { }
        public void SetInvertY(bool value) { }
        public void SetClampMagnitude(double value) { }

        // ═══════════════════════════════════════════════════════════════════════
        // Anti-Recoil
        // ═══════════════════════════════════════════════════════════════════════
        public void HandleRecoil()
        {
            var c = _client;
            if (c == null || !_isConnected || !_settings.RecoilEnabledKm) return;

            if (_isLeftMouseButtonDown)
            {
                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (now - _lastRecoilTime >= _settings.RecoilIntervalKm)
                {
                    try
                    {
                        short rx = (short)_settings.RecoilHorizontalKm;
                        short ry = (short)_settings.RecoilVerticalKm;
                        if (rx != 0 || ry != 0) _ = c.MouseMoveSimple(rx, ry);
                        _lastRecoilTime = now;
                    }
                    catch { }
                }
            }
            else
            {
                _lastRecoilTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Conexão
        // ═══════════════════════════════════════════════════════════════════════
        public async Task<bool> InitializeAsync()
        {
            await _opLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _isConnected = false;
                LastError    = "";

                if (string.IsNullOrEmpty(_settings.KmBoxIp))
                { LastError = "KmBox IP não configurado."; return false; }

                int    port = _settings.KmBoxPort > 0 ? _settings.KmBoxPort : 8888;
                string uuid = _settings.KmBoxUuid ?? "";

                if (!IPAddress.TryParse(_settings.KmBoxIp, out var ip))
                { LastError = $"IP inválido: '{_settings.KmBoxIp}'"; return false; }

                await DisposeCoreAsync().ConfigureAwait(false);

                var client      = new KmBoxClient(ip, port, uuid);
                var connectTask = client.Connect();

                if (await Task.WhenAny(connectTask, Task.Delay(3000)).ConfigureAwait(false) != connectTask
                    || !await connectTask.ConfigureAwait(false))
                { LastError = $"KmBox não respondeu em 3s ({ip}:{port})"; return false; }

                _client = client;
                StartInputListener(client);
                _isConnected = true;
                OnConnectionStateChanged?.Invoke(true);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"KmBox exceção: {ex.Message}";
                await DisposeCoreAsync().ConfigureAwait(false);
                return false;
            }
            finally { _opLock.Release(); }
        }

        private void StartInputListener(KmBoxClient client)
        {
            try
            {
                StopListenerSync();
                var l = client.CreateReportListener();
                l.EventListener = report =>
                {
                    var b = report.MouseReport.Buttons;
                    _isLeftMouseButtonDown  = (b & (int)MouseButton.MouseLeft)  != 0;
                    _isRightMouseButtonDown = (b & (int)MouseButton.MouseRight) != 0;
                };
                l.Start();
                _listener = l;
            }
            catch { }
        }

        private void StopListenerSync()
        {
            var l = _listener; _listener = null;
            if (l == null) return;
            try { Task.Run(() => { try { l.Stop(); } catch { } }).Wait(2000); } catch { }
        }

        private Task StopListenerAsync()
        {
            var l = _listener; _listener = null;
            if (l == null) return Task.CompletedTask;
            return Task.Run(() => { try { l.Stop(); } catch { } });
        }

        private async Task DisposeCoreAsync()
        {
            _isConnected            = false;
            _isLeftMouseButtonDown  = false;
            _isRightMouseButtonDown = false;
            _wasFiring              = false;
            _smoothX                = float.NaN;
            _smoothY                = float.NaN;
            _pbSmoothMoveX          = 0f;
            _pbSmoothMoveY          = 0f;
            _residualX              = 0f;
            _residualY              = 0f;
            _client                 = null;
            await StopListenerAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_opLock.Wait(0))
            {
                try
                {
                    bool was = _isConnected;
                    _ = DisposeCoreAsync();
                    if (was) OnConnectionStateChanged?.Invoke(false);
                }
                finally { _opLock.Release(); }
            }
            else
            {
                bool was = _isConnected;
                _isConnected = false; _client = null;
                if (was) OnConnectionStateChanged?.Invoke(false);
            }
        }
    }
}
