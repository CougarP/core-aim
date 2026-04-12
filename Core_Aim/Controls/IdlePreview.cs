using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;

namespace Core_Aim.Controls
{
    /// <summary>
    /// Phantom Idle Preview — floating geometric shapes (square / triangle / circle / X)
    /// rising with parallax depth, neon glow and fade in/out. Spec §11.
    /// Runs on a 16ms DispatcherTimer when visible; pauses automatically when hidden.
    /// </summary>
    public class IdlePreview : FrameworkElement
    {
        private readonly List<IdleShape> _shapes = new();
        private readonly Random _rnd = new();
        private DispatcherTimer? _timer;
        private bool _paused;

        private static readonly Color[] COLORS =
        {
            Color.FromRgb(0,   240, 255), // electric
            Color.FromRgb(180, 0,   255), // plasma
            Color.FromRgb(255, 48,  0  ), // inferno
            Color.FromRgb(255, 224, 0  ), // solar
            Color.FromRgb(0,   255, 157)  // pulse
        };

        private static readonly string[] TYPES = { "square", "triangle", "circle", "x" };

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            SizeChanged += (_, __) => Populate();
            IsVisibleChanged += (_, __) =>
            {
                if (IsVisible && !_paused) _timer?.Start(); else _timer?.Stop();
            };

            _timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(33),     // ~30fps — animação de fundo, não precisa 60fps
                DispatcherPriority.Background,     // Background não compete com o capture pipeline
                (_, __) => Tick(),
                Dispatcher);
            // Só inicia se já estiver visível — evita gastar CPU em pages colapsadas
            if (IsVisible) _timer.Start();
        }

        /// <summary>Pause animation (e.g. while mouse is active in canvas).</summary>
        public void Pause()
        {
            if (!_paused) { _paused = true; _timer?.Stop(); }
        }

        /// <summary>Resume animation after Pause.</summary>
        public void Resume()
        {
            if (_paused) { _paused = false; if (IsVisible) _timer?.Start(); }
        }

        private void Populate()
        {
            _shapes.Clear();
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            int n = (int)(ActualWidth * ActualHeight / 15000);
            for (int i = 0; i < n; i++)
                _shapes.Add(MakeShape(true));
        }

        private IdleShape MakeShape(bool init)
        {
            double depth = 0.2 + _rnd.NextDouble() * 0.8;
            double size = (6 + _rnd.NextDouble() * 26) * (0.5 + depth * 0.5);

            return new IdleShape
            {
                Type = TYPES[_rnd.Next(TYPES.Length)],
                Color = COLORS[_rnd.Next(COLORS.Length)],
                Size = size,
                Depth = depth,
                X = _rnd.NextDouble() * ActualWidth,
                Y = init
                    ? _rnd.NextDouble() * ActualHeight
                    : ActualHeight + size + 10,
                Vx = (_rnd.NextDouble() - .5) * 0.3,
                Vy = -(0.15 + _rnd.NextDouble() * 0.35) * (0.4 + depth * 0.6),
                Rot = _rnd.NextDouble() * Math.PI * 2,
                VRot = (_rnd.NextDouble() - .5) * 0.007,
                Alpha = 0,
                AlphaT = (0.10 + _rnd.NextDouble() * 0.22) * (0.4 + depth * 0.6),
                FadeIn = true,
                // Pulse: cada shape acende e apaga em velocidade individual
                PulsePhase = _rnd.NextDouble() * Math.PI * 2,
                PulseSpeed = 0.025 + _rnd.NextDouble() * 0.045   // ~1.5–4 ciclos/s a 30fps
            };
        }

        private void Tick()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            foreach (var s in _shapes)
            {
                if (s.FadeIn)
                {
                    s.Alpha = Math.Min(s.Alpha + 0.003, s.AlphaT);
                    if (s.Alpha >= s.AlphaT) s.FadeIn = false;
                }
                if (s.Y < ActualHeight * 0.12)
                {
                    s.Alpha -= 0.006;
                    if (s.Alpha <= 0) s.Dead = true;
                }
                s.X += s.Vx;
                s.Y += s.Vy;
                s.Rot += s.VRot;
                // Pulse: factor 0.25..1.0 baseado em sin(phase)
                s.PulsePhase += s.PulseSpeed;
                s.PulseFactor = 0.25 + 0.75 * (0.5 + 0.5 * Math.Sin(s.PulsePhase));
            }
            _shapes.RemoveAll(s => s.Dead);

            int max = (int)(ActualWidth * ActualHeight / 15000) * 2;
            if (_shapes.Count < max)
                _shapes.Add(MakeShape(false));

            _shapes.Sort((a, b) => a.Depth.CompareTo(b.Depth));

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            // Transparent background — let the 7-layer background show through
            dc.DrawRectangle(
                System.Windows.Media.Brushes.Transparent,
                null,
                new Rect(0, 0, ActualWidth, ActualHeight));

            foreach (var s in _shapes)
            {
                // Pulse: alpha efetivo = alpha base × factor (0.25..1.0). Boost ×1.6
                // para obedecer ao "mais aceso/brilhante" pedido pelo utilizador
                // mantendo a curva do spec.
                double effA = Math.Min(1.0, s.Alpha * s.PulseFactor * 1.6);
                byte a = (byte)Math.Clamp(effA * 255, 0, 255);
                var stroke = Color.FromArgb(a, s.Color.R, s.Color.G, s.Color.B);
                // Stroke 1.4px (era 1) para parecer mais "lit"
                var pen = new Pen(new SolidColorBrush(stroke), 1.4)
                {
                    LineJoin = PenLineJoin.Round
                };

                // Fill um pouco mais opaco (×40 vs ×15 anterior) para o
                // interior dos shapes brilhar com a sua cor
                byte fa = (byte)Math.Clamp(effA * 40, 0, 255);
                var fill = new SolidColorBrush(Color.FromArgb(fa, s.Color.R, s.Color.G, s.Color.B));

                var tg = new TransformGroup();
                tg.Children.Add(new RotateTransform(s.Rot * 180 / Math.PI));
                tg.Children.Add(new TranslateTransform(s.X, s.Y));

                Geometry geom = s.Type switch
                {
                    "circle"   => new EllipseGeometry(new Point(0, 0), s.Size / 2, s.Size / 2),
                    "square"   => new RectangleGeometry(new Rect(-s.Size / 2, -s.Size / 2, s.Size, s.Size)),
                    "triangle" => TriangleGeom(s.Size),
                    "x"        => XGeom(s.Size),
                    _          => new EllipseGeometry(new Point(0, 0), s.Size / 2, s.Size / 2)
                };

                geom.Transform = tg;

                // Halo / glow barato: desenha o mesmo geometry com um pen
                // bem mais grosso e translucido por baixo. Equivale ao
                // DropShadowEffect do spec phantom_docs.txt:1573 mas sem o
                // custo de render (DropShadow em N shapes mata o WPF).
                byte ha = (byte)Math.Clamp(effA * 90, 0, 255);
                var haloPen = new Pen(
                    new SolidColorBrush(Color.FromArgb(ha, s.Color.R, s.Color.G, s.Color.B)),
                    Math.Max(2.5, s.Size * 0.18))
                {
                    LineJoin = PenLineJoin.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                dc.DrawGeometry(null, haloPen, geom);

                dc.DrawGeometry(fill, pen, geom);
            }
        }

        private static PathGeometry TriangleGeom(double s)
        {
            double h = s * 0.866;
            var fig = new PathFigure
            {
                StartPoint = new Point(0, -s * .577),
                IsClosed = true
            };
            fig.Segments.Add(new PolyLineSegment(new[]
            {
                new Point( s / 2,  h * .423),
                new Point(-s / 2,  h * .423)
            }, true));
            return new PathGeometry(new[] { fig });
        }

        private static GeometryGroup XGeom(double s)
        {
            double h = s / 2;
            var g = new GeometryGroup();
            g.Children.Add(new LineGeometry(new Point(-h, -h), new Point(h, h)));
            g.Children.Add(new LineGeometry(new Point(h, -h), new Point(-h, h)));
            return g;
        }
    }

    internal class IdleShape
    {
        public string Type = "circle";
        public Color Color;
        public double Size, Depth, X, Y, Vx, Vy, Rot, VRot, Alpha, AlphaT;
        public double PulsePhase, PulseSpeed;
        public double PulseFactor = 1.0;
        public bool FadeIn, Dead;
    }
}
