using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Core_Aim.Data
{
    /// <summary>
    /// A single annotation bounding box stored in normalized YOLO format.
    /// Coordinates are center-based and normalized to [0,1].
    /// </summary>
    public class LabelBox : INotifyPropertyChanged
    {
        private int    _classId;
        private double _cx, _cy, _w, _h;
        private float  _confidence;

        /// <summary>Class index (0-based).</summary>
        public int ClassId { get => _classId; set { _classId = value; OnPropertyChanged(); } }

        /// <summary>Center X, normalized [0..1].</summary>
        public double CX { get => _cx; set { _cx = value; OnPropertyChanged(); } }

        /// <summary>Center Y, normalized [0..1].</summary>
        public double CY { get => _cy; set { _cy = value; OnPropertyChanged(); } }

        /// <summary>Width, normalized [0..1].</summary>
        public double W { get => _w; set { _w = value; OnPropertyChanged(); } }

        /// <summary>Height, normalized [0..1].</summary>
        public double H { get => _h; set { _h = value; OnPropertyChanged(); } }

        /// <summary>Detection confidence (0 for manual labels).</summary>
        public float Confidence { get => _confidence; set { _confidence = value; OnPropertyChanged(); } }

        // ── Derived (top-left) ──
        public double Left   => CX - W / 2.0;
        public double Top    => CY - H / 2.0;
        public double Right  => CX + W / 2.0;
        public double Bottom => CY + H / 2.0;

        /// <summary>Create from YOLO txt line: classId cx cy w h [conf]</summary>
        public static LabelBox? FromYoloLine(string line)
        {
            var parts = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return null;
            if (!int.TryParse(parts[0], out int cls)) return null;
            if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cx)) return null;
            if (!double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cy)) return null;
            if (!double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double w))  return null;
            if (!double.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h))  return null;

            float conf = 0f;
            if (parts.Length >= 6)
                float.TryParse(parts[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out conf);

            return new LabelBox { ClassId = cls, CX = cx, CY = cy, W = w, H = h, Confidence = conf };
        }

        /// <summary>Serialize to YOLO txt format.</summary>
        public string ToYoloLine()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} {1:F6} {2:F6} {3:F6} {4:F6}", ClassId, CX, CY, W, H);
        }

        /// <summary>Check if a normalized point (0..1) is inside this box.</summary>
        public bool Contains(double nx, double ny)
        {
            return nx >= Left && nx <= Right && ny >= Top && ny <= Bottom;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
