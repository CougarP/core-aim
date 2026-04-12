using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Core_Aim.Services;
using Core_Aim.ViewModels;

namespace Core_Aim
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            SourceInitialized += (_, _) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                HwndSource.FromHwnd(handle)?.AddHook(WndProc);
            };

            DebugConsole.Lines.CollectionChanged += (_, _) =>
            {
                if (_consoleVisible && ConsoleList.Items.Count > 0)
                    ConsoleList.ScrollIntoView(ConsoleList.Items[ConsoleList.Items.Count - 1]);
            };
        }

        // ── WM_GETMINMAXINFO hook — prevent maximized window from covering taskbar ──

        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref mi))
                {
                    var work = mi.rcWork;
                    var mon  = mi.rcMonitor;
                    mmi.ptMaxPosition.x = work.Left - mon.Left;
                    mmi.ptMaxPosition.y = work.Top  - mon.Top;
                    mmi.ptMaxSize.x     = work.Right  - work.Left;
                    mmi.ptMaxSize.y     = work.Bottom - work.Top;
                }
                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }

        // =================================================================
        // Window Loaded
        // =================================================================

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Balões agora são embedded fixos no XAML — sem necessidade de host de Window real.
        }

        // =================================================================
        // Title Bar
        // =================================================================

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // =================================================================
        // Output Log Console
        // =================================================================

        private bool _consoleVisible;

        private void ToggleConsole_Click(object sender, RoutedEventArgs e)
        {
            _consoleVisible = !_consoleVisible;
            ConsoleRowDef.Height = _consoleVisible ? new GridLength(180) : new GridLength(0);
            if (_consoleVisible && ConsoleList.Items.Count > 0)
                ConsoleList.ScrollIntoView(ConsoleList.Items[ConsoleList.Items.Count - 1]);
        }

        private void CopyConsole_Click(object sender, RoutedEventArgs e)
        {
            if (DebugConsole.Lines.Count > 0)
                System.Windows.Clipboard.SetText(string.Join("\n", DebugConsole.Lines));
        }

        // =================================================================
        // Fullscreen (clean view) — hides sidebar + control bar
        // Ctrl + [configured key]  toggles panel visibility
        // =================================================================

        private bool        _isFullscreen;
        private bool        _panelsVisible = true;
        private WindowState _fsWindowState;
        private double      _fsLeft, _fsTop, _fsWidth, _fsHeight;
        private readonly DispatcherTimer _hintFadeTimer = new() { Interval = TimeSpan.FromSeconds(3) };

        private void SetPanelsVisible(bool visible)
        {
            _panelsVisible = visible;
            OverlayCanvas.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ControlRowDef.Height     = visible ? new GridLength(44) : new GridLength(0);
        }

        private void BtnEnterFullscreen_Click(object sender, RoutedEventArgs e) => EnterFullscreen();
        private void BtnExitFullscreen_Click(object sender, RoutedEventArgs e)  => ExitFullscreen();

        private void CameraGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) EnterFullscreen();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Escape always exits fullscreen
            if (e.Key == Key.Escape && _isFullscreen)
            {
                ExitFullscreen();
                e.Handled = true;
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (!ctrl) return;

            var vm = DataContext as MainViewModel;
            string keyStr = vm?.SettingsViewModel.FullscreenKey ?? "F";
            if (!Enum.TryParse<Key>(keyStr, true, out Key fsKey) || e.Key != fsKey) return;

            if (_isFullscreen)
            {
                _panelsVisible = !_panelsVisible;
                OverlayCanvas.Visibility = _panelsVisible ? Visibility.Visible : Visibility.Collapsed;
                // control bar stays hidden in fullscreen — sidebar-only toggle
                if (_panelsVisible) ShowFullscreenHint();
            }
            else
            {
                SetPanelsVisible(!_panelsVisible);
            }
            e.Handled = true;
        }

        private void EnterFullscreen()
        {
            if (_isFullscreen) return;
            _isFullscreen = true;

            _fsWindowState = WindowState;
            _fsLeft  = Left;  _fsTop    = Top;
            _fsWidth = Width; _fsHeight = Height;

            TitleRowDef.Height   = new GridLength(0);
            ControlRowDef.Height = new GridLength(0);
            ConsoleRowDef.Height = new GridLength(0);

            OverlayCanvas.Visibility = Visibility.Collapsed;
            _panelsVisible           = false;
            FsControlBar.Visibility  = Visibility.Visible;

            WindowState = WindowState.Normal;
            Left   = 0;
            Top    = 0;
            Width  = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            ShowFullscreenHint();
        }

        private void ExitFullscreen()
        {
            if (!_isFullscreen) return;
            _isFullscreen = false;

            _hintFadeTimer.Stop();
            FullscreenHint.BeginAnimation(OpacityProperty, null);
            FullscreenHint.Visibility = Visibility.Collapsed;
            FsControlBar.Visibility   = Visibility.Collapsed;

            TitleRowDef.Height   = new GridLength(32);
            ControlRowDef.Height = new GridLength(44);

            _panelsVisible           = true;
            OverlayCanvas.Visibility = Visibility.Visible;

            Left   = _fsLeft;  Top    = _fsTop;
            Width  = _fsWidth; Height = _fsHeight;
            WindowState = _fsWindowState;
        }

        // =================================================================
        // MetricsBar drag
        // =================================================================

        private bool   _metricsDragging;
        private System.Windows.Point  _metricsDragStart;
        private double _metricsLeft, _metricsTop;

        private void MetricsBar_DragStart(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el) return;
            _metricsDragging  = true;
            _metricsDragStart = e.GetPosition(MetricsCanvas);
            _metricsLeft      = System.Windows.Controls.Canvas.GetLeft(MetricsBar);
            _metricsTop       = System.Windows.Controls.Canvas.GetTop(MetricsBar);
            if (double.IsNaN(_metricsLeft))  _metricsLeft  = 12;
            if (double.IsNaN(_metricsTop))   _metricsTop   = 12;
            el.CaptureMouse();
            e.Handled = true;
        }

        private void MetricsBar_DragMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_metricsDragging || sender is not FrameworkElement el) return;
            var pos = e.GetPosition(MetricsCanvas);
            double newLeft = _metricsLeft + (pos.X - _metricsDragStart.X);
            double newTop  = _metricsTop  + (pos.Y - _metricsDragStart.Y);
            newLeft = Math.Max(0, Math.Min(newLeft, MetricsCanvas.ActualWidth  - MetricsBar.ActualWidth));
            newTop  = Math.Max(0, Math.Min(newTop,  MetricsCanvas.ActualHeight - MetricsBar.ActualHeight));
            System.Windows.Controls.Canvas.SetLeft(MetricsBar, newLeft);
            System.Windows.Controls.Canvas.SetTop(MetricsBar,  newTop);
            e.Handled = true;
        }

        private void MetricsBar_DragEnd(object sender, MouseButtonEventArgs e)
        {
            if (!_metricsDragging) return;
            _metricsDragging = false;
            if (sender is FrameworkElement el) el.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void ShowFullscreenHint()
        {
            var vm  = DataContext as MainViewModel;
            string key = vm?.SettingsViewModel.FullscreenKey ?? "F";
            FullscreenHintText.Text   = $"  Ctrl + {key}  — mostrar / ocultar painéis  ";
            FullscreenHint.BeginAnimation(OpacityProperty, null);
            FullscreenHint.Opacity    = 1;
            FullscreenHint.Visibility = Visibility.Visible;

            _hintFadeTimer.Stop();
            _hintFadeTimer.Tick -= HintFade;
            _hintFadeTimer.Tick += HintFade;
            _hintFadeTimer.Start();
        }

        private void HintFade(object? sender, EventArgs e)
        {
            _hintFadeTimer.Stop();
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1));
            anim.Completed += (_, _) =>
            {
                if (FullscreenHint.Opacity == 0)
                    FullscreenHint.Visibility = Visibility.Collapsed;
            };
            FullscreenHint.BeginAnimation(OpacityProperty, anim);
        }

        // =================================================================
        // Cleanup on close
        // =================================================================

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;

            // Hard timeout: se o cleanup não terminar em 8s, força saída
            var hardTimer = new System.Threading.Timer(_ =>
            {
                Console.WriteLine("[OnClosing] Hard timeout 8s — forçando saída");
                Environment.Exit(1);
            }, null, 8000, System.Threading.Timeout.Infinite);

            if (DataContext is MainViewModel vm)
            {
                Task.Run(async () =>
                {
                    try   { await vm.StopSystemAsync(); }
                    catch { }
                    finally
                    {
                        hardTimer.Dispose();
                        Environment.Exit(0);
                    }
                });
            }
            else
            {
                hardTimer.Dispose();
                Environment.Exit(0);
            }
        }
    }
}
