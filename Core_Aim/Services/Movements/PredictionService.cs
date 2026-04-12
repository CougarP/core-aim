using Core_Aim.Services.Configuration;
using System.Drawing;

namespace Core_Aim.Services.Movements
{
    public class PredictionService
    {
        private readonly AppSettingsService _settings;
        private KalmanPrediction _kalman = new KalmanPrediction();
        private WiseTheFoxPrediction _wiseFox = new WiseTheFoxPrediction();
        private ShalloePrediction _shalloe = new ShalloePrediction();

        public PredictionService(AppSettingsService settings)
        {
            _settings = settings;
        }

        public PointF Predict(PointF currentTarget)
        {
            if (!_settings.PredictionEnabled) return currentTarget;

            switch (_settings.PredictionMethod)
            {
                case "Kalman Filter":
                    _kalman.UpdateKalmanFilter(currentTarget.X, currentTarget.Y);
                    var k = _kalman.GetKalmanPosition();
                    return new PointF(k.X, k.Y);
                case "WiseTheFox":
                    _wiseFox.UpdateDetection(currentTarget.X, currentTarget.Y);
                    var w = _wiseFox.GetEstimatedPosition();
                    return new PointF(w.X, w.Y);
                case "Shalloe":
                    _shalloe.AddValues(currentTarget.X, currentTarget.Y);
                    var s = _shalloe.GetPredictedPosition(currentTarget.X, currentTarget.Y);
                    return new PointF(s.X, s.Y);
                default:
                    return currentTarget;
            }
        }
    }
}