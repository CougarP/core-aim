using System.Drawing;

namespace Core_Aim.Data
{
    public struct YoloDetectionResult
    {
        public RectangleF BoundingBox { get; set; }
        public string Label { get; set; }
        public float Confidence { get; set; }

        // NOVO: Identificador numérico da classe
        public int ClassId { get; set; }

        public PointF Center => new PointF(
            BoundingBox.X + (BoundingBox.Width / 2f),
            BoundingBox.Y + (BoundingBox.Height / 2f));
    }
}