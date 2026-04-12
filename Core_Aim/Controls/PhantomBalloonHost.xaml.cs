using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using UserControl = System.Windows.Controls.UserControl;
using ScaleTransform = System.Windows.Media.ScaleTransform;

namespace Core_Aim.Controls
{
    /// <summary>
    /// Phantom Balloon Host (embedded) — versão UserControl do PhantomBalloon Window.
    /// Spec phantom_balloons.txt §404-422.
    /// Suporta:
    ///   • Title (DependencyProperty)
    ///   • TabCode (DependencyProperty) — usado pelo CloseTabCommand
    ///   • Content (DependencyProperty padrão)
    ///   • Top line animada (gradient electric→plasma + breathing)
    ///   • Header com título + botão close
    ///   • Corner brackets (TL Electric, BR Plasma)
    ///   • Entrance animation 140ms (fade + scaleY) ao tornar-se visível
    /// </summary>
    [System.Windows.Markup.ContentProperty(nameof(Content))]
    public partial class PhantomBalloonHost : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(PhantomBalloonHost),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TabCodeProperty =
            DependencyProperty.Register(nameof(TabCode), typeof(string), typeof(PhantomBalloonHost),
                new PropertyMetadata(string.Empty));

        public new static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(object), typeof(PhantomBalloonHost),
                new PropertyMetadata(null));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string TabCode
        {
            get => (string)GetValue(TabCodeProperty);
            set => SetValue(TabCodeProperty, value);
        }

        public new object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public PhantomBalloonHost()
        {
            InitializeComponent();
            // Entrance animation cada vez que se torna visível (não só no Loaded inicial)
            IsVisibleChanged += (_, e) =>
            {
                if ((bool)e.NewValue) PlayEntranceAnimation();
            };
        }

        private void PlayEntranceAnimation()
        {
            Root.Opacity = 0;
            EntryScale.ScaleY = 0.94;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            Root.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = ease
            });

            EntryScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = ease
            });
        }
    }
}
