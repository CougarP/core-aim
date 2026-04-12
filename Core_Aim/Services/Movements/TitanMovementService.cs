using Core_Aim.Services.Configuration;
using Core_Aim.Services.Hardware;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace Core_Aim.Services.Movements
{
    public class TitanMovementService : IMovementService
    {
        private readonly AppSettingsService _settings;
        private TitanTwo? _tt2;
        private bool _isConnected;

        private bool  _wasFiring;

        // ── Controlador PD com velocidade real (px/s) ────────────────────────
        // Kd opera em px/segundo (não px/frame): independente do loop rate.
        // Boost limitado ao FOV interno de 50px: evita overshoot longe do alvo.
        private float _lastErrX = float.NaN;
        private float _lastErrY = float.NaN;
        private float _velX;            // velocidade filtrada do erro (px/s)
        private float _velY;
        private long  _lastMoveTime;

        // ── PixelBot: EMA no stick de saída (amortecedor real) ───────────────
        // PixelBotAimResponse controla o alpha do amortecedor.
        // Baixo = muito amortecido (movimento suave/lento a reagir)
        // Alto  = pouco amortecido (resposta rápida, mais direto)
        private float _pbStickSmoothX;
        private float _pbStickSmoothY;
        private double _lastTrackX;     // último vetor de tracking enviado (para FlushRecoil)
        private double _lastTrackY;
        private double _lastFinalX;    // último vetor final (track + recoil) enviado ao hardware
        private double _lastFinalY;
        private const float VelAlpha   = 0.50f;    // EMA da velocidade
        private const float VelMaxPxS  = 8000f;   // clamp de velocidade bruta (px/s)
        private const float VelNorm    = 3000f;    // normalização: 3000px/s = saída 1.0
        private const float BoostFovPx = 50.0f;   // raio do FOV invisível do boost

        // ── Auto Tuner ──────────────────────────────────────────────────────
        private const int   TunerWindow     = 48;
        private const long  TunerIntervalMs = 300;
        private const float SpeedMin        = 0.10f;
        private const float SpeedMax        = 5.00f;
        private readonly float[] _tunerErrX = new float[TunerWindow];
        private readonly float[] _tunerErrY = new float[TunerWindow];
        private int  _tunerIdx;
        private int  _tunerCount;
        private long _lastTunerTick;
        private int  _oscConfirmCount;

        // ── Anti-recoil ───────────────────────────────────────────────────────
        // Modo velocity: recoil é um OFFSET CONTÍNUO enquanto dispara, não um pulso.
        // _activeRecoilX/Y são setados a cada ciclo pelo HandleRecoil e zerados ao soltar.
        // SendCombined soma o offset em cada envio sem consumi-lo (não é pending).
        private double _activeRecoilX;
        private double _activeRecoilY;
        private long   _lastRecoilTimeTT2;

        // ── Disconnect detection ─────────────────────────────────────────────
        private System.Threading.Timer? _disconnectTimer;

        public bool HasActiveRecoil => _activeRecoilX != 0 || _activeRecoilY != 0;

        public string LastError { get; private set; } = "";
        public event Action<bool>? OnConnectionStateChanged;

        private int _sendCount;
        public int AiOutputX => (int)Math.Round(_lastFinalX);
        public int AiOutputY => (int)Math.Round(_lastFinalY);
        public int DrainSendCount() => Interlocked.Exchange(ref _sendCount, 0);
        public bool IsConnected => _isConnected && _tt2 != null;

        public int LTValue => _tt2?.LTValue ?? 0;
        public int RTValue => _tt2?.RTValue ?? 0;

        public TT2StickState GetStickState() => _tt2?.GetStickState() ?? default;
        public TT2ButtonState GetButtonState() => _tt2?.GetButtonState() ?? default;
        public TT2Info GetDeviceInfo() => _tt2?.GetInfo() ?? default;

        public bool IsFiring
        {
            get
            {
                var tt = _tt2; // snapshot local para evitar race condition
                if (!_isConnected || tt == null) return false;
                try
                {
                    return _settings.TriggerSelection switch
                    {
                        0 => tt.IsLTDown,
                        1 => tt.IsRTDown,
                        _ => tt.IsLTDown || tt.IsRTDown
                    };
                }
                catch { return false; }
            }
        }

        public TitanMovementService(AppSettingsService settings)
        {
            _settings = settings;
        }

        public Task<bool> InitializeAsync()
        {
            return Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                LastError = "";
                try
                {
                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] Início");
                    if (_tt2 != null) { _tt2.Dispose(); _tt2 = null; }

                    var config = new TT2Config
                    {
                        exclusiveHandle = false,
                        rateHz          = 1000,
                        invertX         = false,
                        invertY         = false,
                        clampMagnitude  = 1.0,
                        enableInputRead = true
                    };

                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] new TitanTwo(config)...");
                    _tt2 = new TitanTwo(config);
                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] TitanTwo criado");

                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] _tt2.Start()...");
                    if (!_tt2.Start())
                    {
                        LastError = "tt2_start falhou — TitanTwo não conectado ou ocupado.";
                        Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] *** Start() FALHOU");
                        _tt2.Dispose(); _tt2 = null;
                        return false;
                    }
                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] Start() OK — aguardando Connected...");

                    // tt2_start() inicia a thread de scan mas não garante dispositivo presente.
                    // Aguarda o SDK confirmar Connected (ou desistir após timeout).
                    const int pollMs   = 100;
                    const int timeoutMs = 5000;
                    int elapsed = 0;
                    TT2Status finalStatus;
                    while (true)
                    {
                        finalStatus = _tt2.Status;
                        if (finalStatus == TT2Status.Connected || finalStatus == TT2Status.Error)
                            break;
                        if (elapsed >= timeoutMs)
                            break;
                        Thread.Sleep(pollMs);
                        elapsed += pollMs;
                    }
                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] Status={finalStatus} (poll {elapsed}ms)");

                    if (finalStatus != TT2Status.Connected)
                    {
                        LastError = "TitanTwo não encontrado. Verifique se o dispositivo está conectado ao PC.";
                        _tt2.Stop();
                        _tt2.Dispose(); _tt2 = null;
                        return false;
                    }

                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] SetOutputProtocol({_settings.TitanTwoProtocol})...");
                    _tt2.SetOutputProtocol(ParseProtocol(_settings.TitanTwoProtocol));
                    Thread.Sleep(50);
                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] SlotLoad({_settings.TitanTwoSlot})...");
                    _tt2.SlotLoad((byte)_settings.TitanTwoSlot);
                    Console.WriteLine($"[TT2 Init +{sw.ElapsedMilliseconds}ms] ✓ TT2 conectado e slot carregado");

                    _isConnected = true;
                    OnConnectionStateChanged?.Invoke(true);
                    StartDisconnectMonitor();
                    return true;
                }
                catch (DllNotFoundException)
                {
                    LastError = "tt2_bridge.dll não encontrado na pasta do executável.";
                    return false;
                }
                catch (Exception ex)
                {
                    LastError = $"TitanTwo: {ex.Message}";
                    _tt2?.Dispose(); _tt2 = null;
                    return false;
                }
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // HandleRecoil — chamado a cada ciclo do TrackingLoop (thread dedicada).
        //
        // Modo velocity do TT2: o stick define VELOCIDADE, não delta.
        // Anti-recoil eficaz = offset de velocidade CONTÍNUO enquanto dispara.
        //
        // Enquanto gatilho pressionado: _activeRecoilY = força (% de stick),
        // aplicado em CADA SendAimVector via SendCombined.
        // Ao soltar: zerado imediatamente.
        // ─────────────────────────────────────────────────────────────────────
        // Anti-recoil do sistema DESABILITADO para TT2 — o GPC cuida via gcv[3]/gcv[4].
        public void HandleRecoil()
        {
            _activeRecoilX = 0;
            _activeRecoilY = 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // FlushRecoil — mantém tracking + recoil entre detecções.
        //
        // BUG CORRIGIDO: antes enviava SendCombined(0, 0) → apagava o tracking.
        // O TrackingLoop gira em spin (milhares de vezes/s). Entre detecções YOLO
        // (~12ms), FlushRecoil era chamado centenas de vezes, cada vez zerando o
        // vetor de aim. O tracking durava só 0.01ms, depois era sobrescrito pelo recoil.
        //
        // Fix: reenvia o ÚLTIMO vetor de tracking + recoil. Assim o stick do TT2
        // mantém a direção correta entre detecções.
        // ─────────────────────────────────────────────────────────────────────
        public void FlushRecoil()
        {
            if (_activeRecoilX == 0 && _activeRecoilY == 0) return;

            // Se faz >50ms sem Move() (sem detecção), tracking está stale → recoil puro.
            // Entre frames YOLO (~12ms), mantém o último vetor de tracking.
            long elapsed = Environment.TickCount64 - _lastMoveTime;
            double tx = elapsed > 50 ? 0.0 : _lastTrackX;
            double ty = elapsed > 50 ? 0.0 : _lastTrackY;

            SendCombined(tx, ty);
            _tt2?.NotifyAimActive();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Move — controlador PD com velocidade real + boost no FOV interno
        //
        //   Kp    = Velocidade HIP/ADS  → ganho proporcional (stick/px)
        //   Kd    = Amortecimento       → velocidade real do erro (px/s), funciona
        //   Boost = Multiplicador       → ativo apenas dentro dos 50px do FOV interno
        //   Curve = Modelo de Curva     → 0=Linear 1=Quad 2=Sqrt 3=S 4=Exp 5=Bang 6=Cubic
        //   MinStick = Força Mínima     → supera deadzone interna do jogo
        //
        // Deadzone fixa 5 px — não exposta na interface.
        // ─────────────────────────────────────────────────────────────────────
        public void Move(PointF rawTarget, int modelWidth, int modelHeight, bool isPixelBot = false)
        {
            var tt = _tt2;
            if (!_isConnected || tt == null) return;

            // Mode 2 = "Constante rastreio sempre ativo" — move sem precisar de gatilho
            bool firing = IsFiring || _settings.InferenceMode == 2;
            if (!firing)
            {
                if (_wasFiring) { _wasFiring = false; ResetState(); Stop(); }
                return;
            }

            if (!_wasFiring)
            {
                _wasFiring    = true;
                _lastErrX     = float.NaN;
                _lastErrY     = float.NaN;
                _velX         = 0f;
                _velY         = 0f;
                _lastMoveTime = Environment.TickCount64;
            }

            float halfW  = modelWidth  * 0.5f;
            float halfH  = modelHeight * 0.5f;
            float deltaX = rawTarget.X - halfW;
            float deltaY = rawTarget.Y - halfH;
            float dist   = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Deadzone — usa configurações do modo e hardware ativos
            float Deadzone = isPixelBot
                ? Math.Max((float)_settings.PixelBotDeadZoneTT2, 0.5f)
                : 5.0f;
            if (dist < Deadzone)
            {
                _lastErrX     = deltaX;
                _lastErrY     = deltaY;
                _velX         = 0f;
                _velY         = 0f;
                _lastMoveTime = Environment.TickCount64;
                SendCombined(0.0, 0.0);
                return;
            }

            // ── Velocidade real do erro (px/segundo) ─────────────────────────
            // Usa tempo real entre chamadas → independente do loop rate.
            // Kd com velX=300px/s, VelNorm=3000 → normVel=0.10 → Kd=1.0 contribui 10 stick.
            // Kd com velX=1500px/s (convergência rápida) → normVel=0.50 → contribui 50 stick.
            long  now   = Environment.TickCount64;
            float dtMs  = Math.Clamp((float)(now - _lastMoveTime), 1f, 50f);
            _lastMoveTime = now;

            if (!float.IsNaN(_lastErrX))
            {
                float rawVelX = Math.Clamp((deltaX - _lastErrX) / dtMs * 1000f, -VelMaxPxS, VelMaxPxS);
                float rawVelY = Math.Clamp((deltaY - _lastErrY) / dtMs * 1000f, -VelMaxPxS, VelMaxPxS);
                _velX = VelAlpha * rawVelX + (1f - VelAlpha) * _velX;
                _velY = VelAlpha * rawVelY + (1f - VelAlpha) * _velY;
            }
            _lastErrX = deltaX;
            _lastErrY = deltaY;

            // ── Curva de resposta ─────────────────────────────────────────────
            int   curve  = _settings.TT2CurveModel;
            float normX  = Math.Clamp(deltaX / halfW, -1f, 1f);
            float normY  = Math.Clamp(deltaY / halfH, -1f, 1f);
            float curveX = ApplyCurve(normX, curve) * halfW;
            float curveY = ApplyCurve(normY, curve) * halfH;

            // Velocidade normalizada para o Kd (VelNorm = 3000 px/s)
            float normVelX = Math.Clamp(_velX / VelNorm, -1f, 1f);
            float normVelY = Math.Clamp(_velY / VelNorm, -1f, 1f);

            // TT2 usa os mesmos sliders para YOLO e PixelBot.
            // SpeedHIP/SpeedADS aplicam-se independentemente de YOLO ou PB.
            bool   isAds = tt?.IsLTDown ?? false;
            double Kp    = isAds ? _settings.TT2SpeedADS : _settings.TT2SpeedHIP;
            double Kd    = _settings.TT2Kd;
            double boost = _settings.TT2SpeedBoost;

            // ── Boost restrito ao FOV interno de 50px ────────────────────────
            // Fora dos 50px: boost=1.0 (não interfere na velocidade normal).
            // Dentro dos 50px: boost sobe suavemente até o valor configurado.
            // Isso evita que o boost cause overshoot quando longe do alvo.
            double boostBlend;
            if (dist >= BoostFovPx)
                boostBlend = 0.0;
            else
            {
                double t = 1.0 - dist / BoostFovPx;
                boostBlend = t * t * (3.0 - 2.0 * t); // smoothstep 0→1
            }
            double effectiveBoost = 1.0 + boostBlend * (boost - 1.0);

            // Proporcional: Kp × curveX
            // Derivada:     Kd × normVel × 100  (em px/s normalizados)
            double stickX = Math.Clamp((Kp * curveX + Kd * normVelX * 100.0) * effectiveBoost, -100.0, 100.0);
            double stickY = Math.Clamp((Kp * curveY + Kd * normVelY * 100.0) * effectiveBoost, -100.0, 100.0);

            // ── Força mínima vetorial (apenas YOLO) ───────────────────────────
            if (!isPixelBot)
            {
                double minStick = _settings.TT2MinStick;
                if (minStick > 0)
                {
                    double mag = Math.Sqrt(stickX * stickX + stickY * stickY);
                    if (mag > 0.01 && mag < minStick)
                    {
                        double scale = minStick / mag;
                        stickX = Math.Clamp(stickX * scale, -100.0, 100.0);
                        stickY = Math.Clamp(stickY * scale, -100.0, 100.0);
                    }
                }
            }

            // ── PixelBot: amortecedor real via EMA no stick de saída ──────────
            // PixelBotAimResponse = alpha do amortecedor
            //   alpha baixo → muita inércia → movimento suave/lento
            //   alpha alto  → pouca inércia → resposta rápida
            if (isPixelBot)
            {
                // slider 1–99 → alpha = 1 − (slider/100)  (TT2-specific property)
                float alpha = 1f - Math.Clamp((float)_settings.PixelBotAimResponseTT2 / 100f, 0.01f, 0.99f);
                _pbStickSmoothX = alpha * (float)stickX + (1f - alpha) * _pbStickSmoothX;
                _pbStickSmoothY = alpha * (float)stickY + (1f - alpha) * _pbStickSmoothY;
                stickX = _pbStickSmoothX;
                stickY = _pbStickSmoothY;
            }

            // ── Auto Tuner ───────────────────────────────────────────────────
            RunAutoTuner(deltaX, deltaY, (float)Kp);

            _lastTrackX = stickX;
            _lastTrackY = stickY;
            SendCombined(stickX, stickY);
            _tt2?.NotifyAimActive();
        }

        // ─────────────────────────────────────────────────────────────────────
        // RunAutoTuner — mesma lógica do KmboxMovementService
        // ─────────────────────────────────────────────────────────────────────
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

            double sumSq = 0;
            for (int i = 0; i < n; i++)
                sumSq += _tunerErrX[i] * _tunerErrX[i] + _tunerErrY[i] * _tunerErrY[i];
            float rms = (float)Math.Sqrt(sumSq / (2.0 * n));

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

            if (!_settings.AutoTuneEnabled)
            {
                _oscConfirmCount = 0;
                _settings.AutoTuneStatus = $"Vel={speed:F2}  RMS={rms:F1}px  Osc={osc * 100:F0}%  → manual";
                return;
            }

            string action = "estável";

            if (osc > 0.35f)
            {
                _oscConfirmCount++;
                if (_oscConfirmCount >= 2)
                {
                    float ns = Math.Clamp(speed * 0.88f, SpeedMin, SpeedMax);
                    if (MathF.Abs(ns - speed) > 0.001f)
                    {
                        _settings.TT2SpeedHIP = ns;
                        speed = ns;
                        action = "↓vel (oscilando)";
                    }
                    _oscConfirmCount = 0;
                }
                else
                {
                    action = "⚠ verificando...";
                }
            }
            else
            {
                _oscConfirmCount = 0;
            }

            _settings.AutoTuneStatus = $"Vel={speed:F2}  RMS={rms:F1}px  Osc={osc * 100:F0}%  → {action}";
        }

        // ─────────────────────────────────────────────────────────────────────
        // ApplyCurve — transforma erro normalizado [-1,1] com diferentes formas
        //
        //  0  Linear          — saída = entrada (comportamento padrão)
        //  1  Quadrático      — lento perto do alvo, rápido longe
        //  2  Raiz Quadrada   — rápido perto do alvo, suave longe
        //  3  S-Curve (smooth)— suave nos extremos, linear no meio
        //  4  Exponencial     — crescimento muito agressivo longe do alvo
        //  5  Bang + Linear   — força mínima garantida fora da deadzone interna
        //  6  Cúbico          — extremamente lento perto, máximo muito rápido
        // ─────────────────────────────────────────────────────────────────────
        private static float ApplyCurve(float norm, int model)
        {
            float t = MathF.Abs(norm);
            float s = MathF.Sign(norm);
            return model switch
            {
                1 => s * t * t,                                          // Quadratic
                2 => s * MathF.Sqrt(t),                                  // Sqrt
                3 => s * (3f * t * t - 2f * t * t * t),                 // Smoothstep
                4 => s * (MathF.Exp(t) - 1f) / (MathF.E - 1f),         // Exponential
                5 => t < 0.04f ? 0f : s * (0.15f + 0.85f * t),         // Bang + Linear
                6 => s * t * t * t,                                      // Cubic
                _ => norm                                                 // 0 = Linear (default)
            };
        }

        public void Stop()            => SendCombined(0.0, 0.0);
        public void ResetPrediction() { }
        public bool IsLogging => false;
        public void StartLogging() { }
        public void StopLogging()  { }

        public void SetMacroFlags(uint flags)
        {
            _tt2?.SetMacroFlags(flags);
        }
        public void SetMacroArVertical(double val)   => _tt2?.SetMacroArVertical(val);
        public void SetMacroArHorizontal(double val) => _tt2?.SetMacroArHorizontal(val);
        public void SetMacroRapidFireMs(double val)  => _tt2?.SetMacroRapidFireMs(val);
        public void SetPlayerBlend(double val)       => _tt2?.SetPlayerBlend(val);
        public void SetDisableOnMove(double val)     => _tt2?.SetDisableOnMove(val);
        public void SetQuickscopeDelay(double val)   => _tt2?.SetQuickscopeDelay(val);
        public void SetJumpShotDelay(double val)     => _tt2?.SetJumpShotDelay(val);
        public void SetBunnyHopDelay(double val)     => _tt2?.SetBunnyHopDelay(val);
        public void SetStrafePower(double val)       => _tt2?.SetStrafePower(val);

        public bool UploadGpc(byte[] data, byte slot, string name, string author)
        {
            var tt = _tt2;
            if (tt == null || !_isConnected) return false;
            return tt.UploadGpc(data, slot, name, author);
        }

        public bool UploadGbc(byte[] gbcData, byte slot)
        {
            var tt = _tt2;
            if (tt == null || !_isConnected) return false;
            return tt.UploadGbc(gbcData, slot);
        }

        // ── Slot management (snapshot local para evitar TOCTOU) ─────────
        public bool SlotLoad(byte slot)   { var tt = _tt2; return tt != null && _isConnected && tt.SlotLoad(slot); }
        public bool SlotUp()              { var tt = _tt2; return tt != null && _isConnected && tt.SlotUp(); }
        public bool SlotDown()            { var tt = _tt2; return tt != null && _isConnected && tt.SlotDown(); }
        public bool SlotUnload()          { var tt = _tt2; return tt != null && _isConnected && tt.SlotUnload(); }

        // ── Config passthrough ──────────────────────────────────────────
        public bool SetOutputProtocol(TT2_Proto protocol) { var tt = _tt2; return tt != null && _isConnected && tt.SetOutputProtocol(protocol); }
        public bool SetOutputPolling(TT2_Poll poll)       { var tt = _tt2; return tt != null && _isConnected && tt.SetOutputPolling(poll); }
        public bool SetInputPolling(TT2_Poll poll)        { var tt = _tt2; return tt != null && _isConnected && tt.SetInputPolling(poll); }
        public bool SetHighSpeedMode(bool enable)  { var tt = _tt2; return tt != null && _isConnected && tt.SetHighSpeedMode(enable); }
        public void SetInvertX(bool value)          => _tt2?.SetInvertX(value);
        public void SetInvertY(bool value)          => _tt2?.SetInvertY(value);
        public void SetClampMagnitude(double value)  => _tt2?.SetClampMagnitude(value);

        private void ResetState()
        {
            _lastErrX       = float.NaN;
            _lastErrY       = float.NaN;
            _velX           = 0f;
            _velY           = 0f;
            _lastMoveTime   = 0;
            _lastTrackX     = 0.0;
            _lastTrackY     = 0.0;
            _pbStickSmoothX = 0f;
            _pbStickSmoothY = 0f;
            _tunerIdx        = 0;
            _tunerCount      = 0;
            _oscConfirmCount = 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Combina aim + offset de recoil ativo e envia num único SendAimVector.
        // _activeRecoilX/Y NÃO são zerados aqui — continuam ativos até HandleRecoil
        // zerá-los ao soltar o gatilho.
        // ─────────────────────────────────────────────────────────────────────
        private void SendCombined(double trackX, double trackY)
        {
            var tt = _tt2;
            if (!_isConnected || tt == null) return;

            double finalX = Math.Clamp(trackX + _activeRecoilX, -100.0, 100.0);
            double finalY = Math.Clamp(trackY + _activeRecoilY, -100.0, 100.0);
            _lastFinalX = finalX;
            _lastFinalY = finalY;

            try { tt.SendAimVector(finalX / 100.0, finalY / 100.0); Interlocked.Increment(ref _sendCount); }
            catch { }
        }

        // ── Disconnect monitor — verifica status do TT2 a cada 500ms ────────
        private void StartDisconnectMonitor()
        {
            _disconnectTimer?.Dispose();
            _disconnectTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (_tt2 == null || !_isConnected) return;
                    var st = _tt2.Status;
                    if (st == TT2Status.Disconnected || st == TT2Status.Error)
                    {
                        Console.WriteLine($"[TT2 Monitor] Dispositivo desconectado (status={st})");
                        HandleDisconnect();
                    }
                }
                catch { HandleDisconnect(); }
            }, null, 500, 500);
        }

        private void HandleDisconnect()
        {
            _disconnectTimer?.Dispose();
            _disconnectTimer = null;
            _isConnected = false;
            _activeRecoilX = 0;
            _activeRecoilY = 0;
            _lastFinalX = 0;
            _lastFinalY = 0;

            var tt2 = _tt2;
            _tt2 = null;
            if (tt2 != null)
            {
                // ForceDisconnect é instantâneo — apenas seta flags no C++
                try { tt2.ForceDisconnect(); } catch { }

                // Stop/Dispose em background (pode demorar até 3s pelo timeout)
                Task.Run(() =>
                {
                    try { tt2.Stop(); } catch { }
                    try { tt2.Dispose(); } catch { }
                });
            }

            OnConnectionStateChanged?.Invoke(false);
        }

        public void Dispose()
        {
            _disconnectTimer?.Dispose();
            _disconnectTimer = null;
            if (_tt2 != null)
            {
                try { Stop(); } catch { }
                _tt2.Dispose();
                _tt2 = null;
            }
            _isConnected = false;
            OnConnectionStateChanged?.Invoke(false);
        }

        private static TT2_Proto ParseProtocol(string name)
        {
            if (string.IsNullOrEmpty(name)) return TT2_Proto.Auto;
            string n = name.ToLower();
            if (n.Contains("360") || n.Contains("pc"))   return TT2_Proto.Xbox360;
            if (n.Contains("ps4"))                        return TT2_Proto.PS4;
            if (n.Contains("ps5"))                        return TT2_Proto.PS5;
            if (n.Contains("xbox") && !n.Contains("360")) return TT2_Proto.XboxOne;
            return TT2_Proto.Auto;
        }
    }
}
