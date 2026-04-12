using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Core_Aim.Views.Splash
{
    /// <summary>
    /// Conceito C — HOLOGRAPHIC ASSEMBLY.
    /// Sequência: brackets desenham → hex grid scan in random + HUD → CA wireframe
    /// stroke draw → liquid fill bottom-up → swap pra wordmark com underline plasma → fim.
    /// </summary>
    public partial class SplashHoloWindow : Window, ISplashWindow
    {
        public event Action? Completed;

        public SplashHoloWindow()
        {
            InitializeComponent();
            this.Opacity = 0;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.BeginAnimation(OpacityProperty, MakeAnim(0, 1, TimeSpan.FromSeconds(0.18)));
            RunPhase1_Brackets();
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 1 — corner brackets fade in clockwise (TL → TR → BR → BL)
        // ─────────────────────────────────────────────────────────────
        private void RunPhase1_Brackets()
        {
            FadeBracket(BrkTL, 0);
            FadeBracket(BrkTR, 100);
            FadeBracket(BrkBR, 200);
            FadeBracket(BrkBL, 300);

            SchedulePhase(RunPhase2_HexAndHud, 480);
        }

        private static void FadeBracket(Path brk, int delayMs)
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.35))
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            brk.BeginAnimation(OpacityProperty, anim);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 2 — hex grid fades in + HUD telemetry pops in at corners
        // ─────────────────────────────────────────────────────────────
        private void RunPhase2_HexAndHud()
        {
            HexLayer.BeginAnimation(OpacityProperty,
                MakeAnim(0, 0.55, TimeSpan.FromSeconds(0.65)));

            FadeHud(HudTL, 0);
            FadeHud(HudTR, 80);
            FadeHud(HudBL, 160);
            FadeHud(HudBR, 240);

            SchedulePhase(RunPhase3_Monogram, 640);
        }

        private static void FadeHud(System.Windows.Controls.TextBlock tb, int delayMs)
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.35))
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs)
            };
            tb.BeginAnimation(OpacityProperty, anim);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 3 — CA wireframe draws stroke-by-stroke (using opacity proxy)
        // ─────────────────────────────────────────────────────────────
        private void RunPhase3_Monogram()
        {
            // Both glyphs start invisible, fade in with stagger to simulate stroke draw
            GlyphC.Opacity = 0;
            GlyphA.Opacity = 0;

            GlyphC.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.45))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            GlyphA.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.45))
                {
                    BeginTime = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            SchedulePhase(RunPhase4_LiquidFill, 720);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 4 — liquid fill rises from bottom of monogram canvas
        // ─────────────────────────────────────────────────────────────
        private void RunPhase4_LiquidFill()
        {
            LiquidFill.Opacity = 1;

            // The fill rect grows in height from 0 → 140 (canvas height)
            // and Canvas.Top moves down so it appears to fill upward.
            const double canvasHeight = 140;

            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            // We animate Height upward, anchored to bottom by adjusting Canvas.Top
            var heightAnim = new DoubleAnimation(0, canvasHeight, TimeSpan.FromSeconds(0.85)) { EasingFunction = ease };
            var topAnim    = new DoubleAnimation(canvasHeight, 0, TimeSpan.FromSeconds(0.85)) { EasingFunction = ease };

            LiquidFill.BeginAnimation(HeightProperty, heightAnim);
            LiquidFill.BeginAnimation(System.Windows.Controls.Canvas.TopProperty, topAnim);

            // Glow blooms during fill
            var bloomBlur = new BlurEffect { Radius = 0 };
            GlyphC.Effect = bloomBlur;
            GlyphA.Effect = bloomBlur;
            bloomBlur.BeginAnimation(BlurEffect.RadiusProperty,
                new DoubleAnimation(0, 8, TimeSpan.FromSeconds(0.85)));

            SchedulePhase(RunPhase5_Swap, 900);
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 5 — fade out monogram, fade in wordmark with plasma underline
        // ─────────────────────────────────────────────────────────────
        private void RunPhase5_Swap()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.30));
            GlyphC.BeginAnimation(OpacityProperty, fadeOut);
            GlyphA.BeginAnimation(OpacityProperty, fadeOut.Clone() as DoubleAnimation);
            LiquidFill.BeginAnimation(OpacityProperty, fadeOut.Clone() as DoubleAnimation);

            WordmarkGroup.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.40))
                {
                    BeginTime = TimeSpan.FromMilliseconds(120)
                });

            // Underline draws left → right
            PlasmaUnderline.BeginAnimation(WidthProperty,
                new DoubleAnimation(0, 280, TimeSpan.FromSeconds(0.55))
                {
                    BeginTime = TimeSpan.FromMilliseconds(280),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            // Subtitle types in
            SchedulePhase(StartSubtitleType, 480);
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
                    SchedulePhase(RunPhase6_FadeOut, 600);
                    return;
                }
                WordmarkSub.Text = SubtitleText.Substring(0, _subChar);
            };
            _subTimer.Start();
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 6 — final fade out
        // ─────────────────────────────────────────────────────────────
        private void RunPhase6_FadeOut()
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

        private void SchedulePhase(Action next, int delayMs)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            t.Tick += (_, _) => { t.Stop(); next(); };
            t.Start();
        }
    }
}
