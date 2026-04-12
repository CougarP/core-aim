using System;
using System.Globalization;
using System.Windows;
using M = System.Windows.Media;

namespace Core_Aim.Controls
{
    public class StickVisualizer : FrameworkElement
    {
        public static readonly DependencyProperty StickXProperty =
            DependencyProperty.Register(nameof(StickX), typeof(int), typeof(StickVisualizer),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StickYProperty =
            DependencyProperty.Register(nameof(StickY), typeof(int), typeof(StickVisualizer),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AiXProperty =
            DependencyProperty.Register(nameof(AiX), typeof(int), typeof(StickVisualizer),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AiYProperty =
            DependencyProperty.Register(nameof(AiY), typeof(int), typeof(StickVisualizer),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowAiProperty =
            DependencyProperty.Register(nameof(ShowAi), typeof(bool), typeof(StickVisualizer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(StickVisualizer),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public int    StickX { get => (int)GetValue(StickXProperty);    set => SetValue(StickXProperty, value); }
        public int    StickY { get => (int)GetValue(StickYProperty);    set => SetValue(StickYProperty, value); }
        public int    AiX    { get => (int)GetValue(AiXProperty);       set => SetValue(AiXProperty, value); }
        public int    AiY    { get => (int)GetValue(AiYProperty);       set => SetValue(AiYProperty, value); }
        public bool   ShowAi { get => (bool)GetValue(ShowAiProperty);   set => SetValue(ShowAiProperty, value); }
        public string Label  { get => (string)GetValue(LabelProperty);  set => SetValue(LabelProperty, value); }

        private static readonly M.Pen _circlePen;
        private static readonly M.Pen _crossPen;
        private static readonly M.Pen _physicalPen;
        private static readonly M.Pen _aiPen;
        private static readonly M.Brush _physicalDot;
        private static readonly M.Brush _aiDot;
        private static readonly M.Brush _centerDot;
        private static readonly M.Brush _bgBrush;
        private static readonly M.Typeface _tf = new M.Typeface("Segoe UI");

        static StickVisualizer()
        {
            var circleBrush = new M.SolidColorBrush(M.Color.FromRgb(0x33, 0x41, 0x55));
            circleBrush.Freeze();
            _circlePen = new M.Pen(circleBrush, 1.5); _circlePen.Freeze();

            var crossBrush = new M.SolidColorBrush(M.Color.FromArgb(0x40, 0x94, 0xA3, 0xB8));
            crossBrush.Freeze();
            _crossPen = new M.Pen(crossBrush, 0.5); _crossPen.Freeze();

            var physBrush = new M.SolidColorBrush(M.Color.FromRgb(0x22, 0xD3, 0xEE));
            physBrush.Freeze();
            _physicalPen = new M.Pen(physBrush, 2); _physicalPen.Freeze();
            _physicalDot = physBrush;

            var aiBrush = new M.SolidColorBrush(M.Color.FromRgb(0xFA, 0xCC, 0x15));
            aiBrush.Freeze();
            _aiPen = new M.Pen(aiBrush, 2); _aiPen.Freeze();
            _aiDot = aiBrush;

            var cd = new M.SolidColorBrush(M.Color.FromRgb(0x64, 0x74, 0x8B)); cd.Freeze();
            _centerDot = cd;

            var bg = new M.SolidColorBrush(M.Color.FromRgb(0x0F, 0x17, 0x2A)); bg.Freeze();
            _bgBrush = bg;
        }

        protected override void OnRender(M.DrawingContext dc)
        {
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size < 10) return;

            double cx = ActualWidth / 2;
            double cy = ActualHeight / 2;
            double radius = (size / 2) - 4;

            dc.DrawEllipse(_bgBrush, _circlePen, new System.Windows.Point(cx, cy), radius, radius);
            dc.DrawLine(_crossPen, new System.Windows.Point(cx - radius, cy), new System.Windows.Point(cx + radius, cy));
            dc.DrawLine(_crossPen, new System.Windows.Point(cx, cy - radius), new System.Windows.Point(cx, cy + radius));
            dc.DrawEllipse(_centerDot, null, new System.Windows.Point(cx, cy), 3, 3);

            DrawStick(dc, cx, cy, radius, StickX, StickY, _physicalPen, _physicalDot);
            if (ShowAi)
                DrawStick(dc, cx, cy, radius, AiX, AiY, _aiPen, _aiDot);

            if (!string.IsNullOrEmpty(Label))
            {
                var ft = new M.FormattedText(Label, CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight, _tf, 11,
                    M.Brushes.Gray, M.VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new System.Windows.Point(cx - ft.Width / 2, cy + radius + 4));
            }
        }

        private static void DrawStick(M.DrawingContext dc, double cx, double cy, double radius,
            int x, int y, M.Pen pen, M.Brush dot)
        {
            if (x == 0 && y == 0) return;
            double nx = x / 100.0;
            double ny = y / 100.0;
            double mag = Math.Sqrt(nx * nx + ny * ny);
            if (mag > 1) { nx /= mag; ny /= mag; mag = 1; }
            double endX = cx + nx * radius;
            double endY = cy + ny * radius;
            dc.DrawLine(pen, new System.Windows.Point(cx, cy), new System.Windows.Point(endX, endY));
            dc.DrawEllipse(dot, null, new System.Windows.Point(endX, endY), 4, 4);
        }
    }
}
