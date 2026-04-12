using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Color = System.Windows.Media.Color;

namespace Core_Aim.Controls
{
    /// <summary>
    /// Phantom Balloon — floating draggable window with pin + corner brackets + entrance animation.
    /// Spec §7. Hide() instead of Close() to preserve state.
    /// Close/Pin actions são propagadas para o host (BalloonHostManager) via callbacks
    /// para manter o ViewModel em sincronia (sidebar pulse, IsXXPinned, etc).
    /// </summary>
    public partial class PhantomBalloon : Window
    {
        public bool IsPinned { get; private set; }

        // Callbacks para o host (BalloonHostManager)
        public Action? OnCloseRequested;
        public Action<bool>? OnPinChanged;

        // Estado original do efeito de sombra (preservado para restauração após drag)
        private Effect? _savedShadowEffect;

        public PhantomBalloon(Window owner, string title, UIElement content)
        {
            InitializeComponent();
            Owner = owner;
            TitleText.Text = (title ?? string.Empty).ToUpperInvariant();
            BalloonContent.Content = content;
            AddCornerBrackets();
            Loaded += (_, __) => PlayEntranceAnimation();
        }

        /// <summary>Sincroniza visualmente o estado do botão Pin sem disparar o callback.</summary>
        public void SetPinnedSilently(bool pinned)
        {
            PinBtn.Checked   -= Pin_Checked;
            PinBtn.Unchecked -= Pin_Unchecked;
            PinBtn.IsChecked = pinned;
            ApplyPinVisual(pinned);
            IsPinned = pinned;
            Topmost  = pinned;
            PinBtn.Checked   += Pin_Checked;
            PinBtn.Unchecked += Pin_Unchecked;
        }

        // ── DRAG (com efeito de sombra desabilitado durante o arrasto p/ fluidez) ──
        private void Header_MouseDown(object s, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            // Desliga shadow durante drag (DropShadow + AllowsTransparency = killer de FPS no DragMove)
            _savedShadowEffect = Root.Effect;
            Root.Effect = null;
            try
            {
                DragMove();
            }
            catch { }
            finally
            {
                // Restaura shadow assim que o drag termina
                Root.Effect = _savedShadowEffect;
                _savedShadowEffect = null;
            }
        }

        // ── PIN ──
        private void Pin_Checked(object s, RoutedEventArgs e)
        {
            IsPinned = true;
            Topmost  = true;
            ApplyPinVisual(true);
            OnPinChanged?.Invoke(true);
        }

        private void Pin_Unchecked(object s, RoutedEventArgs e)
        {
            IsPinned = false;
            Topmost  = false;
            ApplyPinVisual(false);
            OnPinChanged?.Invoke(false);
        }

        private void ApplyPinVisual(bool pinned)
        {
            PinBtn.Content   = pinned ? "◈ FIXADO" : "◈ FIXAR";
            Root.BorderBrush = new SolidColorBrush(pinned
                ? Color.FromRgb(0, 240, 255)
                : Color.FromRgb(30, 46, 66));
        }

        // ── CLOSE — apenas notifica host; o host decide Hide() e atualiza VM ──
        private void Close_Click(object s, RoutedEventArgs e)
        {
            OnCloseRequested?.Invoke();
        }

        // ── ENTRANCE ANIMATION (140ms fade + scaleY) ──
        private void PlayEntranceAnimation()
        {
            Root.Opacity      = 0;
            EntryScale.ScaleY = 0.94;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fadeIn = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = ease
            };
            Root.BeginAnimation(OpacityProperty, fadeIn);

            var scaleIn = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = ease
            };
            EntryScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
        }

        // ── CORNER BRACKETS (TL Electric, BR Plasma) ──
        private void AddCornerBrackets()
        {
            var electricBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255));
            var plasmaBrush   = new SolidColorBrush(Color.FromRgb(180, 0, 255));

            var tl = new Border
            {
                Width = 12,
                Height = 12,
                BorderBrush = electricBrush,
                BorderThickness = new Thickness(1, 1, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                IsHitTestVisible = false
            };

            var br = new Border
            {
                Width = 12,
                Height = 12,
                BorderBrush = plasmaBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                IsHitTestVisible = false
            };

            Grid.SetRowSpan(tl, 3);
            Grid.SetRowSpan(br, 3);
            RootGrid.Children.Add(tl);
            RootGrid.Children.Add(br);
        }
    }
}
