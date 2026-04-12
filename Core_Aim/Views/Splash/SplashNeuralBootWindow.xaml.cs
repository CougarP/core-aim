using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Core_Aim.Views.Splash
{
    /// <summary>
    /// Conceito A — NEURAL BOOT.
    /// Sequência: blink central → hex grid radial reveal → boot log digitando →
    /// glitch RGB-split flash → reveal do logo → hold com brackets → wipe → fim.
    /// </summary>
    public partial class SplashNeuralBootWindow : Window, ISplashWindow
    {
        public event Action? Completed;

        // Boot log lines (typed character-by-character)
        private static readonly (string Text, TextBlock? _)[] _logLinesTemplate =
        {
            ("> NEURAL CORE ............ ONLINE",       null),
            ("> TENSOR BUFFER .......... 4096/4096",    null),
            ("> HID BRIDGE ............. TT2 LOCKED",   null),
            ("> AIM SOLVER ............. v9.0.0",       null),
            ("> CALIBRATION ............ NOMINAL",      null),
        };

        private TextBlock[] _bootBlocks = Array.Empty<TextBlock>();
        private readonly DispatcherTimer _typeTimer = new();
        private int _currentLine;
        private int _currentChar;

        public SplashNeuralBootWindow()
        {
            InitializeComponent();
            this.Opacity = 0;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _bootBlocks = new[] { BootLine0, BootLine1, BootLine2, BootLine3, BootLine4 };
            this.BeginAnimation(OpacityProperty, MakeAnim(0, 1, TimeSpan.FromSeconds(0.18)));
            RunPhase1_Blink();
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 1 — central dot blinks twice + scanline sweep down
        // ─────────────────────────────────────────────────────────────
        private void RunPhase1_Blink()
        {
            var blink = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(0.55)) };
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(0,   KeyTime.FromPercent(0.00)));
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(1,   KeyTime.FromPercent(0.10)));
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(0,   KeyTime.FromPercent(0.30)));
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(1,   KeyTime.FromPercent(0.50)));
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(0,   KeyTime.FromPercent(0.85)));
            BootDot.BeginAnimation(OpacityProperty, blink);

            // Scanline sweep top → bottom
            Scanline.BeginAnimation(OpacityProperty,
                MakeKeyFrames(0, 0.85, 0, TimeSpan.FromSeconds(0.55)));
            ScanlineY.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, this.Height, TimeSpan.FromSeconds(0.55))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });

            SchedulePhase(RunPhase2_HexReveal, 600);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 2 — hex grid materializes radially from center
        // ─────────────────────────────────────────────────────────────
        private void RunPhase2_HexReveal()
        {
            HexLayer.BeginAnimation(OpacityProperty,
                MakeAnim(0, 0.65, TimeSpan.FromSeconds(0.9)));

            var radiusEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            HexMask.BeginAnimation(RadialGradientBrush.RadiusXProperty,
                new DoubleAnimation(0.05, 1.4, TimeSpan.FromSeconds(0.9)) { EasingFunction = radiusEase });
            HexMask.BeginAnimation(RadialGradientBrush.RadiusYProperty,
                new DoubleAnimation(0.05, 1.4, TimeSpan.FromSeconds(0.9)) { EasingFunction = radiusEase });

            // Boot log container fades in
            BootLog.BeginAnimation(OpacityProperty, MakeAnim(0, 1, TimeSpan.FromSeconds(0.3)));

            SchedulePhase(RunPhase3_BootLog, 350);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 3 — boot log types in line by line
        // ─────────────────────────────────────────────────────────────
        private void RunPhase3_BootLog()
        {
            _currentLine = 0;
            _currentChar = 0;
            _typeTimer.Interval = TimeSpan.FromMilliseconds(18);
            _typeTimer.Tick += TypeTick;
            _typeTimer.Start();
        }

        private void TypeTick(object? sender, EventArgs e)
        {
            if (_currentLine >= _logLinesTemplate.Length)
            {
                _typeTimer.Stop();
                _typeTimer.Tick -= TypeTick;
                SchedulePhase(RunPhase4_Glitch, 220);
                return;
            }

            string fullLine = _logLinesTemplate[_currentLine].Text;
            _currentChar++;

            if (_currentChar > fullLine.Length)
            {
                _currentLine++;
                _currentChar = 0;
                return;
            }

            _bootBlocks[_currentLine].Text = fullLine.Substring(0, _currentChar);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 4 — glitch RGB-split flash, then logo snap
        // ─────────────────────────────────────────────────────────────
        private void RunPhase4_Glitch()
        {
            // Brief cyan flash
            var flash = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(0.32)) };
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromPercent(0.00)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(0.18)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.10, KeyTime.FromPercent(0.45)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromPercent(1.00)));
            GlitchFlash.BeginAnimation(OpacityProperty, flash);

            // Logo group: slide up + fade in (with magenta/cyan ghosts already at 0.6)
            LogoGroup.BeginAnimation(OpacityProperty, MakeAnim(0, 1, TimeSpan.FromSeconds(0.25)));
            LogoOffset.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(-12, 0, TimeSpan.FromSeconds(0.35))
                {
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
                });

            // Brackets fade in
            var bracketAnim = MakeAnim(0, 1, TimeSpan.FromSeconds(0.45));
            BrkTL.BeginAnimation(OpacityProperty, bracketAnim);
            BrkTR.BeginAnimation(OpacityProperty, bracketAnim.Clone());
            BrkBL.BeginAnimation(OpacityProperty, bracketAnim.Clone());
            BrkBR.BeginAnimation(OpacityProperty, bracketAnim.Clone());

            // Subtitle types in
            SchedulePhase(StartSubtitleType, 280);
        }

        private const string SubtitleText = "ASSISTIVE  TARGETING  SYSTEM";
        private int _subChar;
        private readonly DispatcherTimer _subTimer = new();

        private void StartSubtitleType()
        {
            _subChar = 0;
            _subTimer.Interval = TimeSpan.FromMilliseconds(28);
            _subTimer.Tick += (_, _) =>
            {
                _subChar++;
                if (_subChar > SubtitleText.Length)
                {
                    _subTimer.Stop();
                    SchedulePhase(RunPhase5_Hold, 750);
                    return;
                }
                SubLine.Text = SubtitleText.Substring(0, _subChar);
            };
            _subTimer.Start();
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 5 — hold with breathing glow
        // ─────────────────────────────────────────────────────────────
        private void RunPhase5_Hold()
        {
            var breathe = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(1.0)) };
            breathe.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,  KeyTime.FromPercent(0.0)));
            breathe.KeyFrames.Add(new EasingDoubleKeyFrame(0.78, KeyTime.FromPercent(0.5)));
            breathe.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,  KeyTime.FromPercent(1.0)));
            LogoCore.BeginAnimation(OpacityProperty, breathe);

            SchedulePhase(RunPhase6_Wipe, 850);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 6 — final void wipe + fade out
        // ─────────────────────────────────────────────────────────────
        private void RunPhase6_Wipe()
        {
            var fade = MakeAnim(1, 0, TimeSpan.FromSeconds(0.45),
                                new ExponentialEase { EasingMode = EasingMode.EaseIn });
            fade.Completed += (_, _) => { Completed?.Invoke(); Close(); };
            Root.BeginAnimation(OpacityProperty, fade);
        }

        // ─── helpers ─────────────────────────────────────────────────
        private static DoubleAnimation MakeAnim(double from, double to, TimeSpan dur,
                                                 IEasingFunction? ease = null)
            => new(from, to, dur) { EasingFunction = ease };

        private static DoubleAnimationUsingKeyFrames MakeKeyFrames(double a, double b, double c, TimeSpan dur)
        {
            var anim = new DoubleAnimationUsingKeyFrames { Duration = new Duration(dur) };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(a, KeyTime.FromPercent(0.0)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(b, KeyTime.FromPercent(0.4)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(c, KeyTime.FromPercent(1.0)));
            return anim;
        }

        private void SchedulePhase(Action next, int delayMs)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            t.Tick += (_, _) => { t.Stop(); next(); };
            t.Start();
        }
    }
}
