using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace Core_Aim.Controls
{
    /// <summary>
    /// Phantom BBox — reticule corners + diagonal-clipped label.
    /// Spec §9. Color is driven by ClassId via the COLORS palette.
    /// </summary>
    public partial class PhantomBBox : System.Windows.Controls.UserControl
    {
        // Palette indexed by class id (Electric, Plasma, Inferno, Solar, Pulse)
        private static readonly Color[] COLORS =
        {
            Color.FromRgb(0,   240, 255), // electric
            Color.FromRgb(180, 0,   255), // plasma
            Color.FromRgb(255, 48,  0  ), // inferno
            Color.FromRgb(255, 224, 0  ), // solar
            Color.FromRgb(0,   255, 157)  // pulse
        };

        public int ClassId
        {
            get => (int)GetValue(ClassIdProperty);
            set => SetValue(ClassIdProperty, value);
        }
        public static readonly DependencyProperty ClassIdProperty =
            DependencyProperty.Register(nameof(ClassId), typeof(int), typeof(PhantomBBox),
                new PropertyMetadata(0, OnClassIdChanged));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(PhantomBBox),
                new PropertyMetadata(string.Empty, OnLabelTextChanged));

        public double Confidence
        {
            get => (double)GetValue(ConfidenceProperty);
            set => SetValue(ConfidenceProperty, value);
        }
        public static readonly DependencyProperty ConfidenceProperty =
            DependencyProperty.Register(nameof(Confidence), typeof(double), typeof(PhantomBBox),
                new PropertyMetadata(0.0, OnLabelTextChanged));

        public PhantomBBox()
        {
            InitializeComponent();
            Loaded += (_, __) => { ApplyColor(); ApplyLabelText(); };
        }

        private static void OnClassIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((PhantomBBox)d).ApplyColor();

        private static void OnLabelTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((PhantomBBox)d).ApplyLabelText();

        private void ApplyColor()
        {
            var c = COLORS[((ClassId % COLORS.Length) + COLORS.Length) % COLORS.Length];
            BgRect.Fill = new SolidColorBrush(Color.FromArgb(0x08, c.R, c.G, c.B));
            BgRect.Stroke = new SolidColorBrush(Color.FromArgb(0xA6, c.R, c.G, c.B));
            LabelBorder.Background = new SolidColorBrush(c);
        }

        private void ApplyLabelText()
        {
            LabelText.Text = string.IsNullOrEmpty(Label)
                ? $"{Confidence * 100:F0}%"
                : $"{Label} {Confidence * 100:F0}%";
        }

        private void LabelBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // polygon(0 0, 100%-5 0, 100% 100%, 0 100%) — diagonal clip on top-right
            double w = e.NewSize.Width;
            double h = e.NewSize.Height;
            if (w <= 0 || h <= 0) return;

            var fig = new PathFigure
            {
                StartPoint = new Point(0, 0),
                IsClosed = true
            };
            fig.Segments.Add(new LineSegment(new Point(w - 5, 0), false));
            fig.Segments.Add(new LineSegment(new Point(w, h), false));
            fig.Segments.Add(new LineSegment(new Point(0, h), false));

            LabelClip.Figures.Clear();
            LabelClip.Figures.Add(fig);
        }
    }
}
