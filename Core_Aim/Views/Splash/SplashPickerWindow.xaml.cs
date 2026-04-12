using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Core_Aim.Services.Configuration;

namespace Core_Aim.Views.Splash
{
    /// <summary>
    /// Lets the user pick (and preview) a Phantom splash style.
    /// Persists the selection to AppSettingsService.SplashStyle and exposes
    /// SelectedStyle so the caller can spawn the chosen splash next.
    /// </summary>
    public partial class SplashPickerWindow : Window
    {
        private readonly AppSettingsService _settings;
        public string SelectedStyle { get; private set; }

        public SplashPickerWindow()
        {
            InitializeComponent();
            _settings = new AppSettingsService();

            // Default selection — if user already had one saved, preselect it.
            SelectedStyle = string.IsNullOrEmpty(_settings.SplashStyle)
                ? SplashStyles.NeuralBoot
                : _settings.SplashStyle;

            UpdateStatus();
            HighlightCard(SelectedStyle);
        }

        // ─── Window chrome ───────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Close();
        }

        // ─── Preview buttons (run the splash without closing picker) ─
        private void PreviewNeural_Click(object sender, RoutedEventArgs e)  => RunPreview(new SplashNeuralBootWindow());
        private void PreviewReactor_Click(object sender, RoutedEventArgs e) => RunPreview(new SplashReactorWindow());
        private void PreviewHolo_Click(object sender, RoutedEventArgs e)    => RunPreview(new SplashHoloWindow());

        private void RunPreview(ISplashWindow splash)
        {
            // Modal-ish: hide picker, show splash, restore picker on completed
            this.Opacity = 0.05;
            splash.Completed += () =>
            {
                Dispatcher.Invoke(() => this.Opacity = 1);
            };
            splash.Show();
        }

        // ─── Select buttons (persist + highlight + status) ───────────
        private void SelectNeural_Click(object sender, RoutedEventArgs e)  => Select(SplashStyles.NeuralBoot);
        private void SelectReactor_Click(object sender, RoutedEventArgs e) => Select(SplashStyles.ReactorIgnition);
        private void SelectHolo_Click(object sender, RoutedEventArgs e)    => Select(SplashStyles.HolographicAssembly);

        private void Select(string style)
        {
            SelectedStyle = style;
            _settings.SplashStyle = style;
            UpdateStatus();
            HighlightCard(style);
        }

        private void UpdateStatus()
        {
            string label = SelectedStyle switch
            {
                SplashStyles.NeuralBoot          => "NEURAL BOOT",
                SplashStyles.ReactorIgnition     => "REACTOR IGNITION",
                SplashStyles.HolographicAssembly => "HOLOGRAPHIC ASSEMBLY",
                _                                 => "NEURAL BOOT",
            };
            StatusText.Text = "// CURRENT: " + label;
        }

        private void HighlightCard(string style)
        {
            var soft = (System.Windows.Media.Brush)FindResource("StrokeSoftBrush");
            var hot  = (System.Windows.Media.Brush)FindResource("ElectricBrush");

            CardA.BorderBrush = (style == SplashStyles.NeuralBoot)          ? hot : soft;
            CardB.BorderBrush = (style == SplashStyles.ReactorIgnition)     ? hot : soft;
            CardC.BorderBrush = (style == SplashStyles.HolographicAssembly) ? hot : soft;
        }
    }
}
