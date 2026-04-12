using Core_Aim.Data;
using Core_Aim.Services.Configuration;
using Core_Aim.Services.Hardware;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Core_Aim.Services.AI
{
    public class LockedTarget
    {
        public RectangleF Box { get; set; }
        public DateTime LastSeen { get; set; }
        public int MissedFrames { get; set; }
    }

    public class AimController : IDisposable
    {
        private readonly TitanTwoService _titan;
        private readonly AppSettingsService _settings;

        // ==========================================================
        // CONSTANTES DE CONFIGURAÇÃO
        // ==========================================================

        private const double MinimumForce = 0.02;
        private const double MaxInputError = 1.00; // 1.00 permite velocidade máxima do stick
        private const double MaxOutput = 1.00;
        private const float MaxMovementPerFrame = 120.0f;
        private const bool InvertY = false;

        // ATRASO DE GATILHO (TRIGGER DELAY)
        // Tempo em ms que o sistema espera após apertar o gatilho antes de mover a mira.
        private const int AimStartDelayMs = 150;

        private const double NormalizationScale = 0.05;

        private double _aimOffsetY = 0.0;

        private LockedTarget? _currentTarget = null;
        private const int MaxMissedFrames = 10;

        // Estado do Controlador PD (Erros Anteriores)
        private double _lastErrorX = 0;
        private double _lastErrorY = 0;

        // Variáveis de Controle do Delay
        private DateTime _triggerPressTime = DateTime.MinValue;
        private bool _wasTriggerDown = false;

        public AimController(TitanTwoService titan, KmBoxService kmBox, AppSettingsService settings)
        {
            _titan = titan;
            _settings = settings;
            _aimOffsetY = _settings.AimOffsetY;
        }

        public void SetAimOffset(double offsetY)
        {
            _aimOffsetY = offsetY;
        }

        public void ProcessDetections(List<YoloDetectionResult>? detections, Mat frame)
        {
            if (detections == null) detections = new List<YoloDetectionResult>();

            var validTargets = detections.Where(d => d.ClassId == 0).ToList();

            UpdateLockState(validTargets, frame.Width, frame.Height);

            if (ShouldAim())
            {
                ExecuteAim(frame.Width, frame.Height);
            }
            else
            {
                ResetControllerState();
                _titan.SendAim(0, 0);
            }
        }

        private bool ShouldAim()
        {
            int mode = _settings.InferenceMode;

            if (mode == 3) return false; // Disabled
            if (mode == 2) return true;  // Constant Tracking (geralmente sem delay)

            // Lógica de Gatilho Físico
            bool lt = _titan.IsLTDown;
            bool rt = _titan.IsRTDown;
            int trigger = _settings.TriggerSelection;

            bool isTriggerPressed = false;

            if (trigger == 0) isTriggerPressed = lt;
            else if (trigger == 1) isTriggerPressed = rt;
            else isTriggerPressed = lt || rt;

            // ==============================================================
            // LÓGICA DE ATRASO (DELAY)
            // ==============================================================

            // 1. Detetar borda de subida (momento exato do clique)
            if (isTriggerPressed && !_wasTriggerDown)
            {
                _triggerPressTime = DateTime.Now;
            }

            _wasTriggerDown = isTriggerPressed;

            if (!isTriggerPressed) return false;

            // 2. Verificar se o tempo de espera já passou
            var timeSincePress = (DateTime.Now - _triggerPressTime).TotalMilliseconds;

            if (timeSincePress < AimStartDelayMs)
            {
                return false; // Ainda aguardando o delay
            }

            return true;
        }

        private void ResetControllerState()
        {
            _currentTarget = null;
            _lastErrorX = 0;
            _lastErrorY = 0;
        }

        private void UpdateLockState(List<YoloDetectionResult> detections, int screenW, int screenH)
        {
            if (_currentTarget != null)
            {
                var match = FindBestMatchForLockedTarget(detections);
                if (match != null)
                {
                    _currentTarget.Box = match.Value.BoundingBox;
                    _currentTarget.LastSeen = DateTime.Now;
                    _currentTarget.MissedFrames = 0;
                }
                else
                {
                    _currentTarget.MissedFrames++;
                    if (_currentTarget.MissedFrames > MaxMissedFrames)
                    {
                        ResetControllerState();
                    }
                }
            }

            if (_currentTarget == null && detections.Count > 0)
            {
                var bestNew = FindClosestToCenter(detections, screenW, screenH);
                if (bestNew != null)
                {
                    _currentTarget = new LockedTarget
                    {
                        Box = bestNew.Value.BoundingBox,
                        LastSeen = DateTime.Now,
                        MissedFrames = 0
                    };
                    // Reset imediato ao trocar de alvo para evitar "pulos" do Kd
                    _lastErrorX = 0;
                    _lastErrorY = 0;
                }
            }
        }

        private YoloDetectionResult? FindBestMatchForLockedTarget(List<YoloDetectionResult> detections)
        {
            if (_currentTarget == null) return null;

            float lastCx = _currentTarget.Box.X + _currentTarget.Box.Width / 2f;
            float lastCy = _currentTarget.Box.Y + _currentTarget.Box.Height / 2f;

            YoloDetectionResult? bestMatch = null;
            double minDist = double.MaxValue;

            foreach (var det in detections)
            {
                float cx = det.BoundingBox.X + det.BoundingBox.Width / 2f;
                float cy = det.BoundingBox.Y + det.BoundingBox.Height / 2f;
                double dist = Math.Sqrt(Math.Pow(cx - lastCx, 2) + Math.Pow(cy - lastCy, 2));

                if (dist < minDist && dist < MaxMovementPerFrame)
                {
                    minDist = dist;
                    bestMatch = det;
                }
            }
            return bestMatch;
        }

        private YoloDetectionResult? FindClosestToCenter(List<YoloDetectionResult> detections, int w, int h)
        {
            float cx = w / 2f;
            float cy = h / 2f;
            YoloDetectionResult? best = null;
            double minDist = double.MaxValue;

            foreach (var det in detections)
            {
                float tCx = det.BoundingBox.X + det.BoundingBox.Width / 2f;
                float tCy = det.BoundingBox.Y + det.BoundingBox.Height / 2f;
                double dist = Math.Sqrt(Math.Pow(tCx - cx, 2) + Math.Pow(tCy - cy, 2));

                if (dist < minDist) { minDist = dist; best = det; }
            }
            return best;
        }

        private void ExecuteAim(int screenW, int screenH)
        {
            if (_currentTarget == null)
            {
                _titan.SendAim(0, 0);
                return;
            }

            // 1. Calcular Posição do Alvo
            float centerX = screenW / 2f;
            float centerY = screenH / 2f;

            float factor = (float)_aimOffsetY / 100.0f;
            float targetY = _currentTarget.Box.Bottom - (_currentTarget.Box.Height * factor);
            float targetX = _currentTarget.Box.X + _currentTarget.Box.Width / 2f;

            // 2. Calcular Erro em Pixels
            float errorX = targetX - centerX;
            float errorY = targetY - centerY;

            // 3. Normalização FIXA (Baseada na Tela)
            // Agora o cálculo é estável e não depende do tamanho do FOV.
            double normX = errorX / (screenW * NormalizationScale);
            double normY = errorY / (screenH * NormalizationScale);

            if (InvertY) normY = -normY;

            // 4. Aplicar Curva de Resposta (Slider)
            double curve = _settings.AimResponseCurve;
            normX = ApplyResponseCurve(normX, curve);
            normY = ApplyResponseCurve(normY, curve);

            // 5. Controlador PD
            double kp = _settings.PidKp;
            double kd = _settings.PidKd;

            // Proporcional (Velocidade)
            double pX = normX * kp;
            double pY = normY * kp;

            // Derivativo (Amortecimento)
            double dX = (normX - _lastErrorX) * kd;
            double dY = (normY - _lastErrorY) * kd;

            // Saída Final
            double finalX = pX + dX;
            double finalY = pY + dY;

            // Atualizar estado para o próximo frame
            _lastErrorX = normX;
            _lastErrorY = normY;

            // 6. Clamp de Segurança (Corte de Entrada)
            finalX = Math.Clamp(finalX, -MaxInputError, MaxInputError);
            finalY = Math.Clamp(finalY, -MaxInputError, MaxInputError);

            // 7. Zona Morta Mínima (Vencer inércia)
            if (Math.Abs(finalX) > 0.001)
                finalX += (finalX > 0 ? MinimumForce : -MinimumForce);

            if (Math.Abs(finalY) > 0.001)
                finalY += (finalY > 0 ? MinimumForce : -MinimumForce);

            // 8. Clamp Final (Hardware do Titan)
            finalX = Math.Clamp(finalX, -MaxOutput, MaxOutput);
            finalY = Math.Clamp(finalY, -MaxOutput, MaxOutput);

            _titan.SendAim(finalX, finalY);
        }

        private double ApplyResponseCurve(double val, double exponent)
        {
            if (Math.Abs(val) < 0.0001) return 0;
            return Math.Sign(val) * Math.Pow(Math.Abs(val), exponent);
        }

        public void Dispose()
        {
            _currentTarget = null;
        }
    }
}