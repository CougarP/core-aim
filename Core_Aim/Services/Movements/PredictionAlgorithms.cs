using System;
using System.Collections.Generic;
using System.Linq;

namespace Core_Aim.Services.Movements
{
    // Algoritmos matemáticos puros (copiados do seu envio)
    public class KalmanFilter2D
    {
        private double x, y, dx, dy;
        private double p = 10, q = 0.1, r = 5;

        public double X => x;
        public double Y => y;
        public double XAxisVelocity => dx;
        public double YAxisVelocity => dy;

        public void Push(double newX, double newY)
        {
            double predX = x + dx;
            double predY = y + dy;
            double k = p / (p + r);
            x = predX + k * (newX - predX);
            y = predY + k * (newY - predY);
            dx = x - predX;
            dy = y - predY;
            p = (1 - k) * p + q;
        }
    }

    public class KalmanPrediction
    {
        private readonly KalmanFilter2D kalmanFilter = new KalmanFilter2D();
        private DateTime lastFilterUpdateTime = DateTime.UtcNow;

        public (float X, float Y) GetKalmanPosition()
        {
            double timeStep = (DateTime.UtcNow - lastFilterUpdateTime).TotalSeconds;
            double predictedX = kalmanFilter.X + kalmanFilter.XAxisVelocity * timeStep;
            double predictedY = kalmanFilter.Y + kalmanFilter.YAxisVelocity * timeStep;
            return ((float)predictedX, (float)predictedY);
        }

        public void UpdateKalmanFilter(float x, float y)
        {
            kalmanFilter.Push(x, y);
            lastFilterUpdateTime = DateTime.UtcNow;
        }
    }

    public class WiseTheFoxPrediction
    {
        private const double alpha = 0.5;
        private double emaX, emaY;
        public void UpdateDetection(float x, float y) { emaX = alpha * x + (1 - alpha) * emaX; emaY = alpha * y + (1 - alpha) * emaY; }
        public (float X, float Y) GetEstimatedPosition() => ((float)emaX, (float)emaY);
    }

    public class ShalloePrediction
    {
        private List<float> xValues = new List<float>();
        private List<float> yValues = new List<float>();
        private float _prevX, _prevY;
        public void AddValues(float x, float y)
        {
            float deltaX = x - _prevX; float deltaY = y - _prevY;
            _prevX = x; _prevY = y;
            if (xValues.Count >= 2) xValues.RemoveAt(0);
            if (yValues.Count >= 2) yValues.RemoveAt(0);
            xValues.Add(deltaX); yValues.Add(deltaY);
        }
        public (float X, float Y) GetPredictedPosition(float cx, float cy) => (cx + (xValues.Average() * 2), cy + (yValues.Average() * 2));
    }
}