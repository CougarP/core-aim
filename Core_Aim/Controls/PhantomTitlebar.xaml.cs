using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Core_Aim.Controls
{
    /// <summary>
    /// Phantom Titlebar — 36px chromeless header with logo, status chips, START/STOP, window buttons.
    /// Drag area = full bar minus buttons. Raises StartRequested/StopRequested for the host VM.
    /// </summary>
    public partial class PhantomTitlebar : System.Windows.Controls.UserControl
    {
        public event EventHandler? StartRequested;
        public event EventHandler? StopRequested;

        public PhantomTitlebar()
        {
            InitializeComponent();
        }

        public void SetStatus(int targets, double fps, double latencyMs, bool live)
        {
            CapText.Text = $"CAP {targets} TGT";
            FpsText.Text = $"FPS {fps:F0}";
            LatencyText.Text = $"{latencyMs:F1}ms";
            LiveText.Text = live ? "LIVE" : "IDLE";
            LiveText.Foreground = live
                ? System.Windows.Media.Brushes.LimeGreen
                : System.Windows.Media.Brushes.DimGray;
        }

        private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            var win = Window.GetWindow(this);
            if (win == null) return;

            if (e.ClickCount == 2)
            {
                win.WindowState = win.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }
            try { win.DragMove(); } catch { }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
            => StartRequested?.Invoke(this, EventArgs.Empty);

        private void Stop_Click(object sender, RoutedEventArgs e)
            => StopRequested?.Invoke(this, EventArgs.Empty);

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w != null) w.WindowState = WindowState.Minimized;
        }

        private void Max_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w == null) return;
            w.WindowState = w.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            w?.Close();
        }
    }
}
