using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Core_Aim.Views
{
    public partial class SplashWindow : Window
    {
        public event Action? Completed;

        private readonly DispatcherTimer _phaseTimer = new();
        private static readonly Random   _rng        = new(42);

        private double _cx, _cy;

        // Fragmentos de texto usados na explosão — tamanhos e estilos variados
        private static readonly string[] _words =
        {
            "C","O","R","E","A","I","M",
            "c","o","r","e","a","i","m",
            "core","aim","CORE","AIM",
            "co","re","ai","or","aim",
            "core aim","CORE AIM","aim","core",
            "C","O","R","E","A","I","M",
            "core","aim","re","co","ai"
        };

        public SplashWindow()
        {
            InitializeComponent();
            // Começa invisível — elimina o flash branco do WPF transparent window
            this.Opacity = 0;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _cx = ParticlesCanvas.ActualWidth  / 2;
            _cy = ParticlesCanvas.ActualHeight / 2;
            RunPhase1();
        }

        // ─────────────────────────────────────────────────────────────────
        // Phase 1 — fade in window + elastic grow logo (0 → 0.85 s)
        // ─────────────────────────────────────────────────────────────────
        private void RunPhase1()
        {
            // Fade in da própria janela (elimina o flash branco inicial)
            this.BeginAnimation(OpacityProperty, MakeAnim(0, 1, TimeSpan.FromSeconds(0.2)));

            var dur  = TimeSpan.FromSeconds(0.85);
            var ease = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 5 };

            var scaleX = MakeAnim(0, 1, dur, ease);
            var scaleY = MakeAnim(0, 1, dur, ease);
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

            LogoGroup.BeginAnimation(OpacityProperty, MakeAnim(0, 1, TimeSpan.FromSeconds(0.3)));

            AnimateRing(Ring1Scale, Ring1, 1, 5,   TimeSpan.FromSeconds(0.6),  TimeSpan.FromSeconds(0.0));
            AnimateRing(Ring2Scale, Ring2, 1, 7,   TimeSpan.FromSeconds(0.75), TimeSpan.FromSeconds(0.1));
            AnimateRing(Ring3Scale, Ring3, 1, 9.5, TimeSpan.FromSeconds(0.9),  TimeSpan.FromSeconds(0.2));

            SchedulePhase(RunPhaseHold, 950);
        }

        // ─────────────────────────────────────────────────────────────────
        // Phase Hold — breathing glow + subtitle fade
        // ─────────────────────────────────────────────────────────────────
        private void RunPhaseHold()
        {
            SubText.BeginAnimation(OpacityProperty, MakeAnim(0, 1, TimeSpan.FromSeconds(0.8)));

            var breathe = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(3)) };
            breathe.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,  KeyTime.FromPercent(0.00)));
            breathe.KeyFrames.Add(new EasingDoubleKeyFrame(0.72, KeyTime.FromPercent(0.25)));
            breathe.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,  KeyTime.FromPercent(0.50)));
            breathe.KeyFrames.Add(new EasingDoubleKeyFrame(0.72, KeyTime.FromPercent(0.75)));
            breathe.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,  KeyTime.FromPercent(1.00)));
            LogoGroup.BeginAnimation(OpacityProperty, breathe);

            SchedulePhase(RunPhaseExplosion, 1500);
        }

        // ─────────────────────────────────────────────────────────────────
        // Phase 2 — explosão: logo some, nomes explodem para todos os lados
        // ─────────────────────────────────────────────────────────────────
        private void RunPhaseExplosion()
        {
            // Logo: escala rápida e some
            var explodeScale = MakeAnim(1, 1.3, TimeSpan.FromSeconds(0.3),
                                        new ExponentialEase { EasingMode = EasingMode.EaseIn });
            var explodeFade  = MakeAnim(1, 0, TimeSpan.FromSeconds(0.3),
                                        new ExponentialEase { EasingMode = EasingMode.EaseIn });
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, explodeScale);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, explodeScale.Clone() as DoubleAnimation);
            LogoGroup.BeginAnimation(OpacityProperty, explodeFade);

            // Flash breve
            var flash = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(0.35)) };
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromPercent(0)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.20, KeyTime.FromPercent(0.2)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromPercent(1)));
            FlashOverlay.BeginAnimation(OpacityProperty, flash);

            // Anéis de explosão
            AnimateRing(Ring1Scale, Ring1, 1, 6,   TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0));
            AnimateRing(Ring2Scale, Ring2, 1, 8.5, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.05));
            AnimateRing(Ring3Scale, Ring3, 1, 12,  TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.1));

            // Partículas de texto — o "CORE AIM" explode em fragmentos
            SpawnTextParticles();

            SchedulePhase(RunPhaseFadeOut, 450);
        }

        // ─────────────────────────────────────────────────────────────────
        // Phase 3 — fade out
        // ─────────────────────────────────────────────────────────────────
        private void RunPhaseFadeOut()
        {
            var fade = MakeAnim(1, 0, TimeSpan.FromSeconds(0.55),
                                new ExponentialEase { EasingMode = EasingMode.EaseIn });
            fade.Completed += (_, _) => { Completed?.Invoke(); Close(); };
            RootGrid.BeginAnimation(OpacityProperty, fade);
        }

        // ─────────────────────────────────────────────────────────────────
        // Explosão de texto: fragmentos de "CORE AIM" voam em todas as direções
        // ─────────────────────────────────────────────────────────────────
        private void SpawnTextParticles()
        {
            ParticlesCanvas.Children.Clear();

            // Cores: vermelho, vermelho-escuro, branco
            System.Windows.Media.Color[] palette =
            {
                System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26),
                System.Windows.Media.Color.FromRgb(0xFF, 0x45, 0x45),
                System.Windows.Media.Color.FromRgb(0x7F, 0x1D, 0x1D),
                System.Windows.Media.Color.FromRgb(0xE2, 0xE8, 0xF0),
                System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF),
            };

            int count = _words.Length;
            for (int i = 0; i < count; i++)
            {
                string word    = _words[i];
                double angle   = _rng.NextDouble() * 360.0;
                double dist    = 80 + _rng.NextDouble() * 200;
                double rad     = angle * Math.PI / 180.0;
                double tx      = _cx + Math.Cos(rad) * dist;
                double ty      = _cy + Math.Sin(rad) * dist;
                double gravity = _rng.NextDouble() * 40 - 5; // leve queda
                double fontSize = 7 + _rng.NextDouble() * 22; // 7–29 px
                double rotation = _rng.NextDouble() * 720 - 360; // gira na explosão
                double dur      = 0.65 + _rng.NextDouble() * 0.45; // 0.65–1.1 s

                System.Windows.Media.Color col = palette[_rng.Next(palette.Length)];
                bool bold = _rng.Next(2) == 0;

                var tb = new TextBlock
                {
                    Text       = word,
                    FontSize   = fontSize,
                    FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(col),
                    Opacity    = 1,
                    RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(0)
                };

                // Posição inicial: centro
                Canvas.SetLeft(tb, _cx);
                Canvas.SetTop(tb,  _cy);
                ParticlesCanvas.Children.Add(tb);

                var durTs = TimeSpan.FromSeconds(dur);
                var ease  = new CubicEase { EasingMode = EasingMode.EaseOut };

                // Voa para fora (X)
                tb.BeginAnimation(Canvas.LeftProperty,
                    new DoubleAnimation(_cx, tx, durTs) { EasingFunction = ease });

                // Voa para fora (Y) + gravidade
                tb.BeginAnimation(Canvas.TopProperty,
                    new DoubleAnimation(_cy, ty + gravity, durTs) { EasingFunction = ease });

                // Roda enquanto voa
                var rotAnim = new DoubleAnimation(0, rotation, durTs) { EasingFunction = ease };
                ((RotateTransform)tb.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, rotAnim);

                // Desaparece no final
                tb.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(1, 0, durTs)
                    {
                        BeginTime      = TimeSpan.FromSeconds(dur * 0.4),
                        EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn }
                    });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────
        private static void AnimateRing(ScaleTransform scale, UIElement ring,
                                         double fromScale, double toScale,
                                         TimeSpan dur, TimeSpan delay)
        {
            var opAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration  = new Duration(dur),
                BeginTime = delay
            };
            opAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.8, KeyTime.FromPercent(0.05)));
            opAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0,   KeyTime.FromPercent(1.0)));
            ring.BeginAnimation(OpacityProperty, opAnim);

            var ease   = new ExponentialEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(fromScale, toScale, dur) { BeginTime = delay, EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(fromScale, toScale, dur) { BeginTime = delay, EasingFunction = ease });
        }

        private static DoubleAnimation MakeAnim(double from, double to, TimeSpan dur,
                                                 IEasingFunction? ease = null)
            => new(from, to, dur) { EasingFunction = ease };

        private void SchedulePhase(Action action, int delayMs)
        {
            _phaseTimer.Stop();
            _phaseTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _phaseTimer.Tick    += (_, _) => { _phaseTimer.Stop(); action(); };
            _phaseTimer.Start();
        }
    }
}
