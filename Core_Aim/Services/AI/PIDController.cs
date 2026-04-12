using System;

namespace Core_Aim.Services.AI
{
    public class PIDController
    {
        private double _kp;
        private double _ki;
        private double _kd;

        private double _integral = 0;
        private double _lastError = 0;
        private double _lastDerivative = 0;
        private bool   _firstUpdate = true; // evita spike de derivada no primeiro frame

        private double _integralLimit = 1.0;
        private double _minOut = -1.0;
        private double _maxOut = 1.0;

        private const double DerivativeAlpha = 0.15; // filtro mais suave

        public PIDController(double kp, double ki, double kd)
        {
            _kp = kp;
            _ki = ki;
            _kd = kd;
        }

        public void SetGains(double kp, double ki, double kd)
        {
            _kp = kp;
            _ki = ki;
            _kd = kd;
        }

        public void SetIntegralLimit(double limit)
        {
            _integralLimit = Math.Abs(limit);
        }

        public void SetOutputLimits(double min, double max)
        {
            _minOut = min;
            _maxOut = max;
        }

        public double Update(double error, double dt)
        {
            if (dt <= 0) dt = 0.001;
            if (dt > 0.1) dt = 0.1; // clamp: evita integral explosion após pausa

            // Primeiro update após Reset() — inicializa lastError sem spike de derivada
            if (_firstUpdate)
            {
                _lastError   = error;
                _firstUpdate = false;
            }

            double p = _kp * error;

            _integral += error * dt;
            _integral  = Math.Clamp(_integral, -_integralLimit, _integralLimit);
            double i   = _ki * _integral;

            // Derivada calculada na medição (não no erro) — mais estável
            double rawD = (error - _lastError) / dt;
            _lastDerivative = _lastDerivative * (1.0 - DerivativeAlpha) + rawD * DerivativeAlpha;
            double d = _kd * _lastDerivative;

            _lastError = error;

            return Math.Clamp(p + i + d, _minOut, _maxOut);
        }

        public void Reset()
        {
            _integral       = 0;
            _lastError      = 0;
            _lastDerivative = 0;
            _firstUpdate    = true; // próximo Update() não terá spike
        }
    }
}
