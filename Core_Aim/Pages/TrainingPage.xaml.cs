using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Core_Aim.Data;
using Core_Aim.ViewModels;
using WInput = System.Windows.Input;
using WShapes = System.Windows.Shapes;

namespace Core_Aim.Pages
{
    // ═══════════════ Crosshair — DrawingVisual (bypasses WPF layout entirely) ═══════════════

    internal sealed class CrosshairOverlay : FrameworkElement
    {
        private readonly DrawingVisual _visual = new();
        private double _mx = -1, _my = -1;
        private Rect _clip = Rect.Empty;
        private static readonly System.Windows.Media.Pen _pen;

        static CrosshairOverlay()
        {
            _pen = new System.Windows.Media.Pen(
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x22, 0x00)), 1);
            _pen.Freeze();
        }

        public CrosshairOverlay()
        {
            IsHitTestVisible = false;
            AddVisualChild(_visual);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _visual;

        public void SetClip(Rect r) { _clip = r; }

        public void MoveTo(double x, double y)
        {
            _mx = x; _my = y;
            Redraw();
        }

        public void Hide()
        {
            if (_mx >= 0) { _mx = -1; Redraw(); }
        }

        private void Redraw()
        {
            using var dc = _visual.RenderOpen();
            if (_mx < 0 || _clip.IsEmpty) return;
            if (_mx < _clip.X || _mx > _clip.Right || _my < _clip.Y || _my > _clip.Bottom) return;

            dc.DrawLine(_pen, new System.Windows.Point(_clip.X, _my),
                              new System.Windows.Point(_clip.Right, _my));
            dc.DrawLine(_pen, new System.Windows.Point(_mx, _clip.Y),
                              new System.Windows.Point(_mx, _clip.Bottom));
        }

        protected override System.Windows.Size MeasureOverride(System.Windows.Size a)
            => new(double.IsInfinity(a.Width) ? 0 : a.Width,
                   double.IsInfinity(a.Height) ? 0 : a.Height);

        protected override System.Windows.Size ArrangeOverride(System.Windows.Size s) => s;
    }

    // ═══════════════ TrainingPage ═══════════════

    public partial class TrainingPage : System.Windows.Controls.UserControl
    {
        private TrainingViewModel VM => (TrainingViewModel)DataContext;
        private bool _eventsWired;

        // Drawing state
        private bool _isDrawing;
        private System.Windows.Point _drawStart;

        // Crosshair (DrawingVisual — bypasses WPF layout)
        private readonly CrosshairOverlay _crosshair;
        private bool _mouseInCanvas;

        // Cached image screen rect (updated on zoom/resize/image change)
        private Rect _imgScreenRect;

        public TrainingPage()
        {
            InitializeComponent();

            _crosshair = new CrosshairOverlay();
            CanvasArea.Children.Add(_crosshair);

            IsVisibleChanged += OnVisibilityChanged;
        }

        /// <summary>Fires every frame before composition. Polls mouse position at render time.</summary>
        private void OnCompositionRendering(object? sender, EventArgs e)
        {
            if (!_mouseInCanvas) return;

            var pos = WInput.Mouse.GetPosition(CanvasArea);

            if (VM.ShowCrosshair)
                _crosshair.MoveTo(pos.X, pos.Y);
            else
                _crosshair.Hide();
        }

        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue == true)
            {
                CompositionTarget.Rendering += OnCompositionRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnCompositionRendering;
                _mouseInCanvas = false;
                _crosshair.Hide();
                return;
            }

            if (!_eventsWired)
            {
                _eventsWired = true;
                VM.Boxes.CollectionChanged += (_, _) => RenderBoxes();
                VM.PropertyChanged += (s, ev) =>
                {
                    switch (ev.PropertyName)
                    {
                        case nameof(VM.DisplayImage):
                            ApplyDisplayScale();
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                                () => { InvalidateCachedRect(); RenderBoxes(); });
                            break;
                        case nameof(VM.ShowLabels):
                            RenderBoxes();
                            break;
                        case nameof(VM.ShowCrosshair):
                            if (!VM.ShowCrosshair) _crosshair.Hide();
                            break;
                        case nameof(VM.DisplayScale):
                            ApplyDisplayScale();
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                                () => { InvalidateCachedRect(); RenderBoxes(); });
                            break;
                        case nameof(VM.ZoomLevel):
                        case nameof(VM.ZoomCenterX):
                        case nameof(VM.ZoomCenterY):
                            ApplyZoom();
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                                InvalidateCachedRect);
                            break;
                        case nameof(VM.IsLabeling):
                            BtnLabelAll.Visibility  = VM.IsLabeling ? Visibility.Collapsed : Visibility.Visible;
                            BtnStopLabel.Visibility = VM.IsLabeling ? Visibility.Visible   : Visibility.Collapsed;
                            break;
                        case nameof(VM.IsTraining):
                            BtnTrain.Visibility        = VM.IsTraining ? Visibility.Collapsed : Visibility.Visible;
                            BtnStopTrain.Visibility    = VM.IsTraining ? Visibility.Visible   : Visibility.Collapsed;
                            TrainMetricsPanel.Visibility = VM.IsTraining ? Visibility.Visible  : TrainMetricsPanel.Visibility;
                            TrainLogBox.Visibility       = VM.IsTraining ? Visibility.Visible  : TrainLogBox.Visibility;
                            break;
                        case nameof(VM.LastTrainedOnnx):
                            BtnInstallModel.Visibility = string.IsNullOrEmpty(VM.LastTrainedOnnx)
                                ? Visibility.Collapsed : Visibility.Visible;
                            break;
                        case nameof(VM.TrainLog):
                            TrainLogBox.ScrollToEnd();
                            break;
                    }
                };

                Services.Configuration.AppSettingsService? appSettings = null;
                if (Window.GetWindow(this)?.DataContext is MainViewModel mainVm)
                    appSettings = mainVm.AppSettings;
                VM.Initialize(appSettings);
            }
            else
            {
                // Already initialized — refresh for new images captured during gameplay
                VM.RefreshDataset();
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                ApplyDisplayScale();
                InvalidateCachedRect();
                RenderBoxes();
                Focus();
                WInput.Keyboard.Focus(this);
            });
        }

        // ═══════════════ Back button ═══════════════

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            VM.SaveCurrentLabels();
            VM.SaveConfig();
            if (Window.GetWindow(this)?.DataContext is MainViewModel mainVm)
                mainVm.IsStudioMode = false;
        }

        private void CanvasArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyDisplayScale();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                () => { InvalidateCachedRect(); RenderBoxes(); });
        }

        // ═══════════════ Display Scale ═══════════════

        private void ApplyDisplayScale()
        {
            if (VM.DisplayImage == null || VM.ImgWidth <= 0) return;

            double scaledW = VM.ImgWidth * (VM.DisplayScale / 100.0);
            double scaledH = VM.ImgHeight * (VM.DisplayScale / 100.0);

            CanvasImage.Width = scaledW;
            CanvasImage.Height = scaledH;
        }

        // ═══════════════ Zoom ═══════════════

        private void Canvas_MouseWheel(object sender, WInput.MouseWheelEventArgs e)
        {
            // Capture cursor position in image coords BEFORE changing zoom
            var pos = e.GetPosition(CanvasArea);
            var (nx, ny, valid) = ScreenToNorm(pos);
            if (!valid) { nx = 0.5; ny = 0.5; }

            if (e.Delta > 0)
                VM.ZoomLevel = Math.Min(VM.ZoomLevel + 0.25, 5.0);
            else
                VM.ZoomLevel = Math.Max(VM.ZoomLevel - 0.25, 1.0);

            VM.ZoomCenterX = nx;
            VM.ZoomCenterY = ny;

            ApplyZoom();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                InvalidateCachedRect);
            e.Handled = true;
        }

        private void ApplyZoom()
        {
            double z = VM.ZoomLevel;
            ZoomTransform.ScaleX = z;
            ZoomTransform.ScaleY = z;

            if (z <= 1.0)
            {
                ZoomTranslate.X = 0;
                ZoomTranslate.Y = 0;
            }
            else
            {
                double w = CanvasHost.ActualWidth;
                double h = CanvasHost.ActualHeight;
                if (w <= 0 || h <= 0) return;

                // With RenderTransformOrigin=0.5,0.5: scale from center
                double tx = -w * (VM.ZoomCenterX - 0.5) * (z - 1);
                double ty = -h * (VM.ZoomCenterY - 0.5) * (z - 1);

                double maxTx = (w / 2) * (z - 1);
                double maxTy = (h / 2) * (z - 1);
                tx = Math.Clamp(tx, -maxTx, maxTx);
                ty = Math.Clamp(ty, -maxTy, maxTy);

                ZoomTranslate.X = tx;
                ZoomTranslate.Y = ty;
            }
        }

        // ═══════════════ Coordinate helpers ═══════════════

        /// <summary>Image rect in CanvasHost local coordinates (for RenderBoxes).</summary>
        private Rect GetImageRect()
        {
            double iw = CanvasImage.ActualWidth;
            double ih = CanvasImage.ActualHeight;
            if (iw <= 0 || ih <= 0) return Rect.Empty;
            return new Rect(0, 0, iw, ih);
        }

        /// <summary>Image rect in screen space (CanvasArea coords) via TranslatePoint.</summary>
        private Rect GetImageScreenRect()
        {
            double iw = CanvasHost.ActualWidth;
            double ih = CanvasHost.ActualHeight;
            if (iw <= 0 || ih <= 0) return Rect.Empty;

            var topLeft = CanvasHost.TranslatePoint(new System.Windows.Point(0, 0), CanvasArea);
            var botRight = CanvasHost.TranslatePoint(new System.Windows.Point(iw, ih), CanvasArea);

            return new Rect(topLeft.X, topLeft.Y,
                            botRight.X - topLeft.X, botRight.Y - topLeft.Y);
        }

        /// <summary>Recache image screen rect + update crosshair clip bounds.</summary>
        private void InvalidateCachedRect()
        {
            _imgScreenRect = GetImageScreenRect();
            _crosshair.SetClip(_imgScreenRect);
        }

        /// <summary>Convert screen position to normalized image coords.</summary>
        private (double nx, double ny, bool valid) ScreenToNorm(System.Windows.Point screen)
        {
            if (_imgScreenRect.IsEmpty) return (0, 0, false);

            double nx = (screen.X - _imgScreenRect.X) / _imgScreenRect.Width;
            double ny = (screen.Y - _imgScreenRect.Y) / _imgScreenRect.Height;
            return (nx, ny, nx >= 0 && nx <= 1 && ny >= 0 && ny <= 1);
        }

        // ═══════════════ Mouse — Draw bboxes ═══════════════

        private void Canvas_MouseLeftButtonDown(object sender, WInput.MouseButtonEventArgs e)
        {
            if (VM.DisplayImage == null) return;

            _drawStart = e.GetPosition(CanvasArea);
            _isDrawing = true;
            CanvasArea.CaptureMouse();

            DragRect.Visibility = Visibility.Visible;
            DragRect.Width = 0;
            DragRect.Height = 0;
            DragRect.Margin = new Thickness(_drawStart.X, _drawStart.Y, 0, 0);

            e.Handled = true;
        }

        private void Canvas_MouseMove(object sender, WInput.MouseEventArgs e)
        {
            _mouseInCanvas = true;

            // Drag rect only — crosshair is handled by CompositionTarget.Rendering
            if (_isDrawing)
            {
                var pos = e.GetPosition(CanvasArea);
                double x = Math.Min(pos.X, _drawStart.X);
                double y = Math.Min(pos.Y, _drawStart.Y);
                double w = Math.Abs(pos.X - _drawStart.X);
                double h = Math.Abs(pos.Y - _drawStart.Y);

                DragRect.Margin = new Thickness(x, y, 0, 0);
                DragRect.Width = w;
                DragRect.Height = h;

                var clr = TrainingViewModel.GetClassColor(VM.SelectedClassId);
                DragRect.BorderBrush = new SolidColorBrush(clr);
                DragRect.Background = new SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0x2A, clr.R, clr.G, clr.B));
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, WInput.MouseButtonEventArgs e)
        {
            if (!_isDrawing) return;
            _isDrawing = false;
            CanvasArea.ReleaseMouseCapture();
            DragRect.Visibility = Visibility.Collapsed;

            var pos = e.GetPosition(CanvasArea);
            if (_imgScreenRect.IsEmpty) return;

            if (Math.Abs(pos.X - _drawStart.X) < 5 && Math.Abs(pos.Y - _drawStart.Y) < 5) return;

            double nx1 = (_drawStart.X - _imgScreenRect.X) / _imgScreenRect.Width;
            double ny1 = (_drawStart.Y - _imgScreenRect.Y) / _imgScreenRect.Height;
            double nx2 = (pos.X - _imgScreenRect.X) / _imgScreenRect.Width;
            double ny2 = (pos.Y - _imgScreenRect.Y) / _imgScreenRect.Height;

            nx1 = Math.Clamp(nx1, 0, 1); ny1 = Math.Clamp(ny1, 0, 1);
            nx2 = Math.Clamp(nx2, 0, 1); ny2 = Math.Clamp(ny2, 0, 1);

            double left   = Math.Min(nx1, nx2), top    = Math.Min(ny1, ny2);
            double right  = Math.Max(nx1, nx2), bottom = Math.Max(ny1, ny2);
            double w = right - left, h = bottom - top;
            double cx = left + w / 2, cy = top + h / 2;

            VM.AddBox(cx, cy, w, h);
        }

        private void Canvas_MouseRightButtonDown(object sender, WInput.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(CanvasArea);
            var (nx, ny, ok) = ScreenToNorm(pos);
            if (!ok) return;

            var box = VM.FindBoxAt(nx, ny);
            if (box != null) VM.RemoveBox(box);
            e.Handled = true;
        }

        private void Canvas_MouseLeave(object sender, WInput.MouseEventArgs e)
        {
            _mouseInCanvas = false;
            _crosshair.Hide();
        }

        // ═══════════════ Render bboxes on canvas ═══════════════

        private void RenderBoxes()
        {
            BBoxCanvas.Children.Clear();
            var r = GetImageRect();
            if (r.IsEmpty) return;

            // Blank indicator — green bar at bottom when no detections
            if (VM.Boxes.Count == 0 && VM.DisplayImage != null)
            {
                var bar = new Border
                {
                    Height = 22,
                    Width = r.Width,
                    Background = new SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0xCC, 0x00, 0x22, 0x10)),
                    BorderBrush = new SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x9D)),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Child = new TextBlock
                    {
                        Text = "BLANK",
                        FontFamily = (System.Windows.Media.FontFamily)FindResource("MonoFont"),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x9D)),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    }
                };
                Canvas.SetLeft(bar, 0);
                Canvas.SetTop(bar, r.Height - 22);
                BBoxCanvas.Children.Add(bar);
                return;
            }

            foreach (var box in VM.Boxes)
            {
                double x = box.Left * r.Width;
                double y = box.Top  * r.Height;
                double w = box.W * r.Width;
                double h = box.H * r.Height;

                var clr = TrainingViewModel.GetClassColor(box.ClassId);
                var pen = new SolidColorBrush(clr);

                var rect = new WShapes.Rectangle
                {
                    Width = w, Height = h,
                    Stroke = pen,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0x1A, clr.R, clr.G, clr.B)),
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                BBoxCanvas.Children.Add(rect);

                if (VM.ShowLabels)
                {
                    string labelText = VM.GetClassName(box.ClassId);
                    if (box.Confidence > 0)
                        labelText += $" {box.Confidence:F2}";

                    var label = new Border
                    {
                        Background = new SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(0xCC, 0x03, 0x07, 0x0F)),
                        Padding = new Thickness(3, 1, 3, 1),
                        Child = new TextBlock
                        {
                            Text = labelText,
                            FontFamily = (System.Windows.Media.FontFamily)FindResource("MonoFont"),
                            FontSize = 9,
                            FontWeight = FontWeights.Bold,
                            Foreground = pen
                        }
                    };
                    Canvas.SetLeft(label, x);
                    Canvas.SetTop(label, Math.Max(0, y - 16));
                    BBoxCanvas.Children.Add(label);
                }
            }
        }

        // ═══════════════ Class selection ═══════════════

        private void ClassItem_Click(object sender, WInput.MouseButtonEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is ClassItem cls)
            {
                VM.SelectedClassId = cls.Id;
            }
        }

        // ═══════════════ Keyboard shortcuts ═══════════════

        protected override void OnKeyDown(WInput.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            bool ctrl = (WInput.Keyboard.Modifiers & WInput.ModifierKeys.Control) != 0;

            if (ctrl)
            {
                switch (e.Key)
                {
                    case WInput.Key.S:
                        VM.IsDirty = true;
                        VM.SaveCurrentLabels();
                        VM.StatusText = "Labels saved";
                        e.Handled = true;
                        return;
                    case WInput.Key.O:
                        VM.OpenFolder();
                        e.Handled = true;
                        return;
                    case WInput.Key.M:
                        VM.LoadModelCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case WInput.Key.C:
                        if (VM.DeleteImageCommand.CanExecute(null))
                            VM.DeleteImageCommand.Execute(null);
                        e.Handled = true;
                        return;
                }
            }

            switch (e.Key)
            {
                case WInput.Key.D:
                case WInput.Key.Right:
                    VM.GoToNextImage();
                    e.Handled = true;
                    break;
                case WInput.Key.A:
                case WInput.Key.Left:
                    VM.GoToPrevImage();
                    e.Handled = true;
                    break;
                case WInput.Key.W:
                    VM.ChangeClassSelection(-1);
                    e.Handled = true;
                    break;
                case WInput.Key.S:
                    if (!ctrl) VM.ChangeClassSelection(+1);
                    e.Handled = true;
                    break;
                case WInput.Key.C:
                    if (!ctrl)
                    {
                        VM.ClearBoxesCommand.Execute(null);
                        VM.StatusText = "Boxes cleared";
                    }
                    e.Handled = true;
                    break;
                case WInput.Key.R:
                    if (VM.LabelThisImageCommand.CanExecute(null))
                        VM.LabelThisImageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case WInput.Key.T:
                    if (VM.DeleteImageCommand.CanExecute(null))
                        VM.DeleteImageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case WInput.Key.Delete:
                    VM.ClearBoxesCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }
}
