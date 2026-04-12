using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Core_Aim.Controls
{
    public partial class NumericSlider : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericSlider),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumericSlider),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumericSlider),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(NumericSlider),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(NumericSlider),
                new PropertyMetadata(10.0));

        // Tint = brush usado pelo track decrease (preenchimento) e pelo thumb (stroke + glow).
        // Default: Electric (cyan). Pode ser definido por slider para variar a cor.
        public static readonly DependencyProperty TintProperty =
            DependencyProperty.Register(nameof(Tint), typeof(System.Windows.Media.Brush), typeof(NumericSlider),
                new PropertyMetadata(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF))));

        public double Value       { get => (double)GetValue(ValueProperty);       set => SetValue(ValueProperty, value); }
        public double Minimum     { get => (double)GetValue(MinimumProperty);     set => SetValue(MinimumProperty, value); }
        public double Maximum     { get => (double)GetValue(MaximumProperty);     set => SetValue(MaximumProperty, value); }
        public double SmallChange { get => (double)GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }
        public double LargeChange { get => (double)GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }
        public System.Windows.Media.Brush Tint { get => (System.Windows.Media.Brush)GetValue(TintProperty); set => SetValue(TintProperty, value); }

        public NumericSlider() => InitializeComponent();

        private void BtnMinus_Click(object sender, RoutedEventArgs e)
            => Value = System.Math.Max(Minimum, Value - SmallChange);

        private void BtnPlus_Click(object sender, RoutedEventArgs e)
            => Value = System.Math.Min(Maximum, Value + SmallChange);
    }
}
