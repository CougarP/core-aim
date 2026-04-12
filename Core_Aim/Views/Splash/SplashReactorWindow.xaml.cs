using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Core_Aim.Views.Splash
{
    /// <summary>
    /// Conceito B — REACTOR IGNITION.
    /// Sequência: pixel central → 6 anéis explodem rotativos → orbs entram drift →
    /// hex grid radial reveal → anéis colapsam → flash branco → reveal logo + underline → fim.
    /// </summary>
    public partial class SplashReactorWindow : Window, ISplashWindow
    {
        public event Action? Completed;

        private (Ellipse Ring, ScaleTransform Scale, RotateTransform Rot, double TargetRot)[] _rings = null!;

        public SplashReactorWindow()
        {
            InitializeComponent();
            this.Opacity = 0;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _rings = new[]
            {
                (Ring1, Ring1Scale, Ring1Rot,  120.0),
                (Ring2, Ring2Scale, Ring2Rot, -160.0),
                (Ring3, Ring3Scale, Ring3Rot,  220.0),
                (Ring4, Ring4Scale, Ring4Rot, -200.0),
                (Ring5, Ring5Scale, Ring5Rot,  260.0),
                (Ring6, Ring6Scale, Ring6Rot, -300.0),
            };

            this.BeginAnimation(OpacityProperty, MakeAnim(0, 1, TimeSpan.FromSeconds(0.18)));
            RunPhase1_CoreIgnition();
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 1 — central pixel grows + 6 rings explode + orbs drift in
        // ─────────────────────────────────────────────────────────────
        private void RunPhase1_CoreIgnition()
        {
            // Core dot inhale
            CoreDot.BeginAnimation(OpacityProperty,
                MakeKeyFrames(0, 1, 0.5, TimeSpan.FromSeconds(0.45)));

            // Rings: opacity in + scale 0.05 → 1 + start spinning
            for (int i = 0; i < _rings.Length; i++)
            {
                var (ring, scale, rot, target) = _rings[i];
                var delay = TimeSpan.FromMilliseconds(40 * i);

                ring.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 0.85, TimeSpan.FromSeconds(0.55))
                    {
                        BeginTime = delay,
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });

                var grow = new DoubleAnimation(0.05, 1.0, TimeSpan.FromSeconds(0.85))
                {
                    BeginTime = delay,
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow.Clone() as DoubleAnimation);

                // Continuous rotation through phases 1-3 (until collapse)
                rot.BeginAnimation(RotateTransform.AngleProperty,
                    new DoubleAnimation(0, target, TimeSpan.FromSeconds(2.4))
                    {
                        BeginTime = delay
                    });
            }

            // Orbs drift in
            OrbA.BeginAnimation(OpacityProperty, MakeAnim(0, 0.55, TimeSpan.FromSeconds(0.9)));
            OrbB.BeginAnimation(OpacityProperty, MakeAnim(0, 0.55, TimeSpan.FromSeconds(0.9)));

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            OrbAOffset.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(-360, -90, TimeSpan.FromSeconds(1.4)) { EasingFunction = ease });
            OrbAOffset.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(-180, -90, TimeSpan.FromSeconds(1.4)) { EasingFunction = ease });
            OrbBOffset.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(360, 90, TimeSpan.FromSeconds(1.4)) { EasingFunction = ease });
            OrbBOffset.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(180, 90, TimeSpan.FromSeconds(1.4)) { EasingFunction = ease });

            SchedulePhase(RunPhase2_HexReveal, 350);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 2 — hex grid materializes radially synced with rings
        // ─────────────────────────────────────────────────────────────
        private void RunPhase2_HexReveal()
        {
            HexLayer.BeginAnimation(OpacityProperty,
                MakeAnim(0, 0.45, TimeSpan.FromSeconds(1.0)));

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            HexMask.BeginAnimation(RadialGradientBrush.RadiusXProperty,
                new DoubleAnimation(0.05, 1.4, TimeSpan.FromSeconds(1.0)) { EasingFunction = ease });
            HexMask.BeginAnimation(RadialGradientBrush.RadiusYProperty,
                new DoubleAnimation(0.05, 1.4, TimeSpan.FromSeconds(1.0)) { EasingFunction = ease });

            SchedulePhase(RunPhase3_Collapse, 1000);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 3 — rings collapse inward simultaneously
        // ─────────────────────────────────────────────────────────────
        private void RunPhase3_Collapse()
        {
            for (int i = 0; i < _rings.Length; i++)
            {
                var (ring, scale, _, _) = _rings[i];
                var collapse = new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(0.35))
                {
                    EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn }
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, collapse);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, collapse.Clone() as DoubleAnimation);

                ring.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0.85, 0, TimeSpan.FromSeconds(0.35)));
            }

            // Orbs collapse too
            OrbA.BeginAnimation(OpacityProperty, MakeAnim(0.55, 0, TimeSpan.FromSeconds(0.35)));
            OrbB.BeginAnimation(OpacityProperty, MakeAnim(0.55, 0, TimeSpan.FromSeconds(0.35)));

            SchedulePhase(RunPhase4_Flash, 360);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 4 — full white flash, then logo emerges
        // ─────────────────────────────────────────────────────────────
        private void RunPhase4_Flash()
        {
            var flash = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(0.40)) };
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromPercent(0.00)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.95, KeyTime.FromPercent(0.10)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromPercent(1.00)));
            Flash.BeginAnimation(OpacityProperty, flash);

            // Logo fades in mid-flash
            var logoFade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.35))
            {
                BeginTime = TimeSpan.FromMilliseconds(80),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoGroup.BeginAnimation(OpacityProperty, logoFade);

            // Underline draws left → right
            var underline = new DoubleAnimation(0, 320, TimeSpan.FromSeconds(0.55))
            {
                BeginTime = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            UnderlineBar.BeginAnimation(WidthProperty, underline);

            SchedulePhase(RunPhase5_Hold, 950);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 5 — hold + fade out
        // ─────────────────────────────────────────────────────────────
        private void RunPhase5_Hold()
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                var fade = MakeAnim(1, 0, TimeSpan.FromSeconds(0.45),
                                    new ExponentialEase { EasingMode = EasingMode.EaseIn });
                fade.Completed += (_, _) => { Completed?.Invoke(); Close(); };
                Root.BeginAnimation(OpacityProperty, fade);
            };
            t.Start();
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
