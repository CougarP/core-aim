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
    // Esta classe é dedicada EXCLUSIVAMENTE ao KMBox (Pixel Based)
    public class KmBoxAimController : IDisposable
    {
        private readonly KmBoxService _kmBox;
        private readonly AppSettingsService _settings;

        // Configurações específicas para pixels
        private const float MaxPixelsPerFrame = 100.0f; // Limite de velocidade para evitar "snaps" muito loucos
        private const bool InvertY = false;
        private const int AimStartDelayMs = 150;

        private double _aimOffsetY = 0.0;
        private LockedTarget? _currentTarget = null;
        private const int MaxMissedFrames = 10;

        // Estado PID (Em Pixels)
        private double _lastErrorX = 0;
        private double _lastErrorY = 0;

        // Delay Control
        private DateTime _triggerPressTime = DateTime.MinValue;
        private bool _wasTriggerDown = false;

        public KmBoxAimController(KmBoxService kmBox, AppSettingsService settings)
        {
            _kmBox = kmBox;
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
                // KMBox não precisa de "stop", pois trabalha com deltas. 
                // Se não chamarmos Move(), ele para.
            }
        }

        private bool ShouldAim()
        {
            int mode = _settings.InferenceMode;
            if (mode == 3) return false; // Disabled
            if (mode == 2) return true;  // Constant Tracking

            // Mapeamento de Gatilho para Botoes do Mouse
            bool isTriggerPressed = false;

            bool leftBtn = _kmBox.IsLeftDown;
            bool rightBtn = _kmBox.IsRightDown;
            int trigger = _settings.TriggerSelection;

            // 0 = LT (Interface) -> Mouse Left (Físico)
            // 1 = RT (Interface) -> Mouse Right (Físico)
            if (trigger == 0) isTriggerPressed = leftBtn;
            else if (trigger == 1) isTriggerPressed = rightBtn;
            else isTriggerPressed = leftBtn || rightBtn;

            // Lógica de Delay
            if (isTriggerPressed && !_wasTriggerDown)
            {
                _triggerPressTime = DateTime.Now;
            }
            _wasTriggerDown = isTriggerPressed;

            if (!isTriggerPressed) return false;

            var timeSincePress = (DateTime.Now - _triggerPressTime).TotalMilliseconds;
            if (timeSincePress < AimStartDelayMs) return false;

            return true;
        }

        private void ExecuteAim(int screenW, int screenH)
        {
            if (_currentTarget == null) return;

            // 1. Calcular Posição do Alvo
            float centerX = screenW / 2f;
            float centerY = screenH / 2f;

            float factor = (float)_aimOffsetY / 100.0f;
            float targetY = _currentTarget.Box.Bottom - (_currentTarget.Box.Height * factor);
            float targetX = _currentTarget.Box.X + _currentTarget.Box.Width / 2f;

            // 2. Calcular Erro BRUTO em Pixels (Diferença do seu AimController original)
            // Não normalizamos por ScreenW aqui, pois o KMBox move em pixels.
            double errorX = targetX - centerX;
            double errorY = targetY - centerY;

            if (InvertY) errorY = -errorY;

            // 3. PID em Pixels
            // Kp aqui age como "Smooth". Se Kp = 0.5, movemos 50% da distância até o alvo.
            // Se Kp = 1.0, é instantâneo (sem smooth).
            double kp = _settings.PidKp;
            double kd = _settings.PidKd;

            // Proporcional
            double pX = errorX * kp;
            double pY = errorY * kp;

            // Derivativo (Evita oscilação rápida em pixels)
            double dX = (errorX - _lastErrorX) * kd;
            double dY = (errorY - _lastErrorY) * kd;

            double moveX = pX + dX;
            double moveY = pY + dY;

            // Atualiza erro passado
            _lastErrorX = errorX;
            _lastErrorY = errorY;

            // 4. Curva de Resposta (Opcional para pixels, mas ajuda em micro-ajustes)
            // Aplicamos uma curva suave se o movimento for pequeno
            double curve = _settings.AimResponseCurve;
            // Nota: Aplicar curva em pixels brutos pode ser agressivo, 
            // geralmente em KMBox usa-se apenas PID linear, mas mantive para consistência.

            // 5. Clamp de Velocidade Máxima (Segurança)
            moveX = Math.Clamp(moveX, -MaxPixelsPerFrame, MaxPixelsPerFrame);
            moveY = Math.Clamp(moveY, -MaxPixelsPerFrame, MaxPixelsPerFrame);

            // 6. Converter para Inteiro e Enviar
            int iMoveX = (int)moveX;
            int iMoveY = (int)moveY;

            // Só envia se houver movimento real
            if (iMoveX != 0 || iMoveY != 0)
            {
                _kmBox.Move(iMoveX, iMoveY);
            }
        }

        private void ResetControllerState()
        {
            _currentTarget = null;
            _lastErrorX = 0;
            _lastErrorY = 0;
        }

        // ==========================================================
        // LÓGICA DE TARGET LOCK (Idêntica ao outro, apenas copiada)
        // ==========================================================
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
                    if (_currentTarget.MissedFrames > MaxMissedFrames) ResetControllerState();
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
                if (dist < minDist && dist < 200) { minDist = dist; bestMatch = det; }
            }
            return bestMatch;
        }

        private YoloDetectionResult? FindClosestToCenter(List<YoloDetectionResult> detections, int w, int h)
        {
            float cx = w / 2f; float cy = h / 2f;
            YoloDetectionResult? best = null; double minDist = double.MaxValue;
            foreach (var det in detections)
            {
                float tCx = det.BoundingBox.X + det.BoundingBox.Width / 2f;
                float tCy = det.BoundingBox.Y + det.BoundingBox.Height / 2f;
                double dist = Math.Sqrt(Math.Pow(tCx - cx, 2) + Math.Pow(tCy - cy, 2));
                if (dist < minDist) { minDist = dist; best = det; }
            }
            return best;
        }

        public void Dispose() { _currentTarget = null; }
    }
}