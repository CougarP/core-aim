using Core_Aim.Data;
using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Core_Aim.Services.AI
{
    public class TrackingService : IDisposable
    {
        private readonly AimController _aimController;

        private readonly ConcurrentQueue<List<YoloDetectionResult>> _detectionQueue =
            new ConcurrentQueue<List<YoloDetectionResult>>();

        public TrackingService(AimController aimController)
        {
            _aimController = aimController;
        }

        public void Configure()
        {
            // PID removido. O AimController lê diretamente do AppSettings.
        }

        // ADICIONADO: Método para passar o Offset para o controlador
        public void SetAimOffset(double y)
        {
            _aimController.SetAimOffset(y);
        }

        public void UpdateDetections(List<YoloDetectionResult> detections)
        {
            if (detections == null || detections.Count == 0)
                return;

            while (_detectionQueue.Count > 2)
                _detectionQueue.TryDequeue(out _);

            _detectionQueue.Enqueue(detections);
        }

        public void ProcessDetectionsWithFrame(Mat frame)
        {
            if (frame == null || frame.Empty())
                return;

            List<YoloDetectionResult>? latest = null;

            while (_detectionQueue.TryDequeue(out var d))
                latest = d;

            _aimController.ProcessDetections(latest, frame);
        }

        public void ClearQueue()
        {
            while (_detectionQueue.TryDequeue(out _)) { }
        }

        public void Dispose()
        {
            ClearQueue();
        }
    }
}