using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Core_Aim.Commands;
using Core_Aim.Data;

namespace Core_Aim.ViewModels
{
    public class ClassItem : ViewModelBase
    {
        private string _name = "";
        private bool   _isChecked = true;
        private bool   _isSelected;

        public int    Id        { get; set; }
        public string Name      { get => _name;      set { _name = value;      OnPropertyChanged(); } }
        public bool   IsChecked { get => _isChecked;  set { _isChecked = value;  OnPropertyChanged(); } }
        public bool   IsSelected{ get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        public SolidColorBrush Color { get; set; } = System.Windows.Media.Brushes.Cyan;
    }

    public class ImageEntry : ViewModelBase
    {
        public string FullPath { get; set; } = "";
        public string FileName => Path.GetFileName(FullPath);

        private bool _hasLabel;
        public bool HasLabel { get => _hasLabel; set { _hasLabel = value; OnPropertyChanged(); } }
    }

    public class TrainingViewModel : ViewModelBase
    {
        // ── Class color palette (Phantom) ──
        private static readonly System.Windows.Media.Color[] Palette =
        {
            System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF), // Electric
            System.Windows.Media.Color.FromRgb(0xB4, 0x00, 0xFF), // Plasma
            System.Windows.Media.Color.FromRgb(0xFF, 0xE0, 0x00), // Solar
            System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x9D), // Pulse
            System.Windows.Media.Color.FromRgb(0xFF, 0x30, 0x00), // Inferno
            System.Windows.Media.Color.FromRgb(0xFF, 0x88, 0x00), // Orange
            System.Windows.Media.Color.FromRgb(0x80, 0xE8, 0xFF), // Ice
            System.Windows.Media.Color.FromRgb(0xFF, 0x00, 0x80), // Pink
            System.Windows.Media.Color.FromRgb(0x80, 0xFF, 0x00), // Lime
            System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF), // White
        };

        public static SolidColorBrush GetClassBrush(int classId)
        {
            var c = Palette[classId % Palette.Length];
            return new SolidColorBrush(c);
        }

        public static System.Windows.Media.Color GetClassColor(int classId) => Palette[classId % Palette.Length];

        // ══════════════ Collections ══════════════

        public ObservableCollection<ImageEntry> Images { get; } = new();
        public ObservableCollection<LabelBox>   Boxes  { get; } = new();
        public ObservableCollection<ClassItem>  Classes{ get; } = new();

        // ══════════════ State ══════════════

        private string _datasetDir = "";
        public string DatasetDir { get => _datasetDir; set { _datasetDir = value; OnPropertyChanged(); } }

        private int _imageIndex = -1;
        public int ImageIndex
        {
            get => _imageIndex;
            set
            {
                if (value < -1) value = -1;
                if (value >= Images.Count) value = Images.Count - 1;
                _imageIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentImage));
                OnPropertyChanged(nameof(CurrentFileName));
                OnPropertyChanged(nameof(ImageCounter));
                LoadCurrentImage();
            }
        }

        public ImageEntry? CurrentImage => _imageIndex >= 0 && _imageIndex < Images.Count ? Images[_imageIndex] : null;
        public string CurrentFileName => CurrentImage?.FileName ?? "—";
        public string ImageCounter => Images.Count > 0 ? $"{_imageIndex + 1} / {Images.Count}" : "0 / 0";

        private BitmapSource? _displayImage;
        public BitmapSource? DisplayImage { get => _displayImage; set { _displayImage = value; OnPropertyChanged(); } }

        private int _imgWidth, _imgHeight;
        public int ImgWidth  { get => _imgWidth;  set { _imgWidth = value;  OnPropertyChanged(); OnPropertyChanged(nameof(ImageDimensions)); } }
        public int ImgHeight { get => _imgHeight; set { _imgHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(ImageDimensions)); } }
        public string ImageDimensions => _imgWidth > 0 ? $"{_imgWidth}x{_imgHeight}" : "";

        private int _selectedClassId;
        public int SelectedClassId
        {
            get => _selectedClassId;
            set { _selectedClassId = value; OnPropertyChanged(); }
        }

        // ── Display options ──
        private bool _showLabels = true;
        public bool ShowLabels { get => _showLabels; set { _showLabels = value; OnPropertyChanged(); } }

        private bool _showCrosshair = true;
        public bool ShowCrosshair { get => _showCrosshair; set { _showCrosshair = value; OnPropertyChanged(); } }

        // ── Confidence threshold (user-adjustable) ──
        private float _confThreshold = 0.50f;
        public float ConfThreshold
        {
            get => _confThreshold;
            set { _confThreshold = Math.Clamp(value, 0.05f, 1.0f); OnPropertyChanged(); OnPropertyChanged(nameof(ConfThresholdInt)); OnPropertyChanged(nameof(ConfThresholdPercent)); }
        }
        public int ConfThresholdInt
        {
            get => (int)(_confThreshold * 100);
            set { ConfThreshold = value / 100f; }
        }
        public string ConfThresholdPercent => $"{ConfThresholdInt}%";

        // ── Display scale (100% = real size, up to 200%) ──
        private int _displayScale = 100;
        public int DisplayScale
        {
            get => _displayScale;
            set { _displayScale = Math.Clamp(value, 100, 200); OnPropertyChanged(); OnPropertyChanged(nameof(DisplayScaleText)); }
        }
        public string DisplayScaleText => $"{_displayScale}%";

        // ── Zoom ──
        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set { _zoomLevel = Math.Clamp(value, 1.0, 5.0); OnPropertyChanged(); }
        }

        private double _zoomCenterX = 0.5, _zoomCenterY = 0.5;
        public double ZoomCenterX { get => _zoomCenterX; set { _zoomCenterX = value; OnPropertyChanged(); } }
        public double ZoomCenterY { get => _zoomCenterY; set { _zoomCenterY = value; OnPropertyChanged(); } }

        // ── Model state ──
        private string _modelPath = "";
        public string ModelPath { get => _modelPath; set { _modelPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModelStatus)); } }
        public string ModelStatus => _onnxSession != null ? Path.GetFileName(_modelPath) : "No model";

        // Stats
        public int TotalImages  => Images.Count;
        public int LabeledCount => Images.Count(i => i.HasLabel);
        public int UnlabeledCount => Images.Count - LabeledCount;

        private bool _isDirty;
        public bool IsDirty { get => _isDirty; set { _isDirty = value; OnPropertyChanged(); } }

        private string _statusText = "";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

        private bool _isLabeling;
        public bool IsLabeling { get => _isLabeling; set { _isLabeling = value; OnPropertyChanged(); } }

        private CancellationTokenSource? _labelAllCts;

        // ══════════════ Commands ══════════════

        public ICommand OpenFolderCommand     { get; }
        public ICommand LoadModelCommand      { get; }
        public ICommand SaveCommand           { get; }
        public ICommand LabelThisImageCommand { get; }
        public ICommand LabelAllImagesCommand { get; }
        public ICommand StopLabelCommand      { get; }
        public ICommand ClearBoxesCommand     { get; }
        public ICommand DeleteImageCommand    { get; }
        public ICommand CleanDatasetCommand   { get; }
        public ICommand PrevImageCommand      { get; }
        public ICommand NextImageCommand      { get; }
        public ICommand SelectClassCommand    { get; }
        public ICommand EditClassesCommand    { get; }
        public ICommand ResetZoomCommand      { get; }

        // Training commands
        public ICommand PrepareDatasetCommand  { get; }
        public ICommand StartTrainingCommand   { get; }
        public ICommand StopTrainingCommand    { get; }
        public ICommand InstallModelCommand    { get; }

        // ══════════════ Constructor ══════════════

        public TrainingViewModel()
        {
            OpenFolderCommand     = new RelayCommand(_ => OpenFolder());
            LoadModelCommand      = new RelayCommand(_ => LoadModel());
            SaveCommand           = new RelayCommand(_ => { IsDirty = true; SaveCurrentLabels(); });
            LabelThisImageCommand = new RelayCommand(_ => RunAutoLabel(), _ => _onnxSession != null && CurrentImage != null);
            LabelAllImagesCommand = new RelayCommand(_ => LabelAllImages(), _ => _onnxSession != null && Images.Count > 0 && !IsLabeling);
            StopLabelCommand      = new RelayCommand(_ => StopLabeling(), _ => IsLabeling);
            ClearBoxesCommand     = new RelayCommand(_ => { Boxes.Clear(); IsDirty = true; });
            DeleteImageCommand    = new RelayCommand(_ => DeleteCurrentImage(), _ => CurrentImage != null);
            CleanDatasetCommand   = new RelayCommand(_ => CleanDataset(), _ => !string.IsNullOrEmpty(DatasetDir));
            PrevImageCommand      = new RelayCommand(_ => GoToPrevImage(), _ => _imageIndex > 0);
            NextImageCommand      = new RelayCommand(_ => GoToNextImage(), _ => _imageIndex < Images.Count - 1);
            SelectClassCommand    = new RelayCommand(p => { if (p is int id) SelectedClassId = id; });
            EditClassesCommand    = new RelayCommand(_ => EditClasses(), _ => !string.IsNullOrEmpty(DatasetDir));
            ResetZoomCommand      = new RelayCommand(_ => { ZoomLevel = 1.0; ZoomCenterX = 0.5; ZoomCenterY = 0.5; });

            // Training
            PrepareDatasetCommand  = new RelayCommand(_ => PrepareDataset(), _ => !string.IsNullOrEmpty(DatasetDir) && Images.Count > 0 && !IsTraining);
            StartTrainingCommand   = new RelayCommand(_ => StartTraining(), _ => _isDatasetReady && !IsTraining);
            StopTrainingCommand    = new RelayCommand(_ => StopTraining(), _ => IsTraining);
            InstallModelCommand    = new RelayCommand(_ => InstallOnnxModel(), _ => !string.IsNullOrEmpty(_lastTrainedOnnx) && File.Exists(_lastTrainedOnnx));
        }

        private Services.Configuration.AppSettingsService? _appSettings;

        /// <summary>Initialize: load CoreAim model + CapturedImages. Call after DataContext is set.</summary>
        public void Initialize(Services.Configuration.AppSettingsService? appSettings = null)
        {
            _appSettings = appSettings;

            // Restore persisted settings from AppConfig
            if (_appSettings != null)
            {
                _showLabels = _appSettings.LabelShowLabels;
                _showCrosshair = _appSettings.LabelShowCrosshair;
                _confThreshold = Math.Clamp(_appSettings.LabelConfThreshold / 100f, 0.05f, 1.0f);
                _displayScale = Math.Clamp(_appSettings.LabelDisplayScale, 100, 200);
                OnPropertyChanged(nameof(ShowLabels));
                OnPropertyChanged(nameof(ShowCrosshair));
                OnPropertyChanged(nameof(ConfThreshold));
                OnPropertyChanged(nameof(ConfThresholdInt));
                OnPropertyChanged(nameof(ConfThresholdPercent));
                OnPropertyChanged(nameof(DisplayScale));
                OnPropertyChanged(nameof(DisplayScaleText));
            }

            // Restore training settings
            if (_appSettings != null)
            {
                _trainEpochs = Math.Clamp(_appSettings.TrainEpochs, 10, 1000);
                _trainBatchSize = _appSettings.TrainBatchSize;
                _trainImgSize = _appSettings.TrainImgSize;
                _trainBaseModel = _appSettings.TrainBaseModel ?? "yolov8n.pt";
                _trainValPercent = Math.Clamp(_appSettings.TrainValPercent, 5, 50);
                OnPropertyChanged(nameof(TrainEpochs));
                OnPropertyChanged(nameof(TrainBatchIndex));
                OnPropertyChanged(nameof(TrainImgSizeIndex));
                OnPropertyChanged(nameof(TrainBaseModelIndex));
                OnPropertyChanged(nameof(TrainValPercent));
            }

            // Load dataset: prefer saved dir, fallback to CapturedImages
            string savedDir = _appSettings?.LabelDatasetDir ?? "";
            string capturedDir = Path.Combine(AppContext.BaseDirectory, "CapturedImages");

            if (Images.Count == 0)
            {
                if (!string.IsNullOrEmpty(savedDir) && Directory.Exists(savedDir))
                    LoadDatasetFromPath(savedDir);
                else if (Directory.Exists(capturedDir))
                    LoadDatasetFromPath(capturedDir);
            }

            // Restore checked classes
            if (_appSettings != null && !string.IsNullOrEmpty(_appSettings.LabelCheckedClasses))
            {
                var checkedIds = _appSettings.LabelCheckedClasses.Split(',')
                    .Select(s => int.TryParse(s, out int v) ? v : -1).ToHashSet();
                foreach (var c in Classes)
                    c.IsChecked = checkedIds.Contains(c.Id);
            }

            // Restore image index
            if (_appSettings != null && _appSettings.LabelImageIndex >= 0 && _appSettings.LabelImageIndex < Images.Count)
                ImageIndex = _appSettings.LabelImageIndex;

            // Load model from CoreAim config (same SelectedModel)
            if (_onnxSession == null)
                TryLoadCoreAimModel();
        }

        /// <summary>Try to load the same ONNX model that CoreAim is using.</summary>
        private void TryLoadCoreAimModel()
        {
            try
            {
                string? modelName = _appSettings?.SelectedModel;
                if (string.IsNullOrEmpty(modelName))
                    return;

                string fullPath = Path.Combine(AppContext.BaseDirectory, "Models", modelName);
                if (!File.Exists(fullPath)) return;

                LoadModelFromPath(fullPath);
            }
            catch { }
        }

        private void LoadModelFromPath(string path)
        {
            try
            {
                _onnxSession?.Dispose();
                _onnxSession = null;

                int gpuId = _appSettings?.GpuDeviceId ?? 0;
                string provider = "CPU";

                // DirectML first (same package the project uses)
                // Fresh SessionOptions for each attempt — avoids corrupted state
                Microsoft.ML.OnnxRuntime.SessionOptions so;

                try
                {
                    so = new Microsoft.ML.OnnxRuntime.SessionOptions();
                    so.ExecutionMode = Microsoft.ML.OnnxRuntime.ExecutionMode.ORT_SEQUENTIAL;
                    so.GraphOptimizationLevel = Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_ENABLE_ALL;
                    so.AppendExecutionProvider_DML(gpuId);
                    so.IntraOpNumThreads = 1;
                    so.InterOpNumThreads = 1;
                    provider = $"DirectML GPU{gpuId}";
                }
                catch
                {
                    // DirectML unavailable — CPU fallback
                    so = new Microsoft.ML.OnnxRuntime.SessionOptions();
                    so.ExecutionMode = Microsoft.ML.OnnxRuntime.ExecutionMode.ORT_SEQUENTIAL;
                    so.GraphOptimizationLevel = Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_ENABLE_ALL;
                    provider = "CPU";
                }

                _onnxSession = new Microsoft.ML.OnnxRuntime.InferenceSession(path, so);
                _modelInputName = _onnxSession.InputMetadata.Keys.First();

                var shape = _onnxSession.InputMetadata[_modelInputName].Dimensions;
                if (shape.Length >= 4) _modelInputSize = shape[2] > 0 ? shape[2] : 640;

                // Warmup — compile DirectML shaders before real use
                try
                {
                    var warmup = new float[3 * _modelInputSize * _modelInputSize];
                    var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(
                        warmup, new[] { 1, 3, _modelInputSize, _modelInputSize });
                    var inputs = new[] { Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor(_modelInputName, tensor) };
                    for (int w = 0; w < 2; w++)
                    {
                        using var r = _onnxSession.Run(inputs);
                    }
                }
                catch { }

                ModelPath = path;
                StatusText = $"Model: {Path.GetFileName(path)} [{provider}]";
            }
            catch (Exception ex)
            {
                _onnxSession = null;
                StatusText = $"Model error: {ex.Message}";
            }

            RelayCommand.RaiseCanExecuteChanged();
        }

        // ══════════════ Open Folder ══════════════

        private static readonly string[] ImgExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };

        public void OpenFolder()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Dataset Folder" };
            if (dlg.ShowDialog() != true) return;
            LoadDatasetFromPath(dlg.FolderName);
        }

        public void LoadDatasetFromPath(string path)
        {
            if (!Directory.Exists(path)) return;
            SaveCurrentLabels();

            DatasetDir = path;
            Images.Clear();

            var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => ImgExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            foreach (var f in files)
            {
                var txt = Path.ChangeExtension(f, ".txt");
                Images.Add(new ImageEntry
                {
                    FullPath = f,
                    HasLabel = File.Exists(txt) && new FileInfo(txt).Length > 0
                });
            }

            LoadClasses();
            RefreshStats();
            StatusText = $"Loaded {Images.Count} images";

            // Check if dataset is already prepared
            IsDatasetReady = File.Exists(Path.Combine(path, "data.yaml"));
            if (IsDatasetReady)
                TrainStatus = "Dataset ready (data.yaml found)";

            ImageIndex = Images.Count > 0 ? 0 : -1;
            RelayCommand.RaiseCanExecuteChanged();
        }

        // ══════════════ Classes ══════════════

        private void LoadClasses()
        {
            Classes.Clear();
            var classesFile = Path.Combine(DatasetDir, "classes.txt");
            string[] names;
            if (File.Exists(classesFile))
                names = File.ReadAllLines(classesFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            else
                names = Enumerable.Range(0, 10).Select(i => i.ToString()).ToArray();

            for (int i = 0; i < names.Length; i++)
            {
                Classes.Add(new ClassItem
                {
                    Id = i,
                    Name = names[i].Trim(),
                    Color = GetClassBrush(i),
                    IsChecked = true
                });
            }

            if (SelectedClassId >= Classes.Count)
                SelectedClassId = 0;
        }

        public void EditClasses()
        {
            if (string.IsNullOrEmpty(DatasetDir)) return;

            var classesFile = Path.Combine(DatasetDir, "classes.txt");

            var dlg = new System.Windows.Window
            {
                Title = "Edit Classes",
                Width = 350, Height = 450,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x03, 0x07, 0x0F)),
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStyle = System.Windows.WindowStyle.ToolWindow
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(12) };

            var lbl = new System.Windows.Controls.TextBlock
            {
                Text = "One class per line:",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0xAA, 0xBB)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Margin = new System.Windows.Thickness(0, 0, 0, 6)
            };

            var txt = new System.Windows.Controls.TextBox
            {
                Height = 320,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0A, 0x10, 0x1A)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x2E, 0x42)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Padding = new System.Windows.Thickness(6)
            };

            if (File.Exists(classesFile))
                txt.Text = File.ReadAllText(classesFile);
            else
                txt.Text = string.Join("\n", Classes.Select(c => c.Name));

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(0, 10, 0, 0)
            };

            var btnSave = new System.Windows.Controls.Button
            {
                Content = "Save",
                Width = 80, Height = 28,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x60, 0x40)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x9D)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 0, 6, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnSave.Click += (_, _) =>
            {
                File.WriteAllText(classesFile, txt.Text.TrimEnd());
                dlg.DialogResult = true;
                dlg.Close();
            };

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 80, Height = 28,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x28, 0x28)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0xAA, 0xBB)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (_, _) => dlg.Close();

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(lbl);
            stack.Children.Add(txt);
            stack.Children.Add(btnPanel);
            dlg.Content = stack;

            var owner = System.Windows.Application.Current.MainWindow;
            if (owner != null) dlg.Owner = owner;

            if (dlg.ShowDialog() == true)
            {
                LoadClasses();
                RefreshStats();
                StatusText = "Classes updated";
            }
        }

        public string GetClassName(int classId)
        {
            if (classId >= 0 && classId < Classes.Count)
                return Classes[classId].Name;
            return classId.ToString();
        }

        public void ChangeClassSelection(int direction)
        {
            int newIdx = SelectedClassId + direction;
            if (newIdx >= 0 && newIdx < Classes.Count)
                SelectedClassId = newIdx;
        }

        // ══════════════ Image Loading ══════════════

        private void LoadCurrentImage()
        {
            if (CurrentImage == null)
            {
                DisplayImage = null;
                Boxes.Clear();
                return;
            }

            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(CurrentImage.FullPath);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                DisplayImage = bi;
                ImgWidth = bi.PixelWidth;
                ImgHeight = bi.PixelHeight;
            }
            catch
            {
                DisplayImage = null;
                ImgWidth = 0;
                ImgHeight = 0;
            }

            LoadLabelsForCurrent();
            IsDirty = false;

            // Reset zoom on image change
            ZoomLevel = 1.0;
            ZoomCenterX = 0.5;
            ZoomCenterY = 0.5;
        }

        // ══════════════ YOLO txt I/O ══════════════

        private void LoadLabelsForCurrent()
        {
            Boxes.Clear();
            if (CurrentImage == null) return;

            var txtPath = Path.ChangeExtension(CurrentImage.FullPath, ".txt");
            if (!File.Exists(txtPath)) return;

            var lines = File.ReadAllLines(txtPath);
            foreach (var line in lines)
            {
                var box = LabelBox.FromYoloLine(line);
                if (box != null) Boxes.Add(box);
            }
        }

        public void SaveCurrentLabels()
        {
            if (CurrentImage == null || !IsDirty) return;

            var txtPath = Path.ChangeExtension(CurrentImage.FullPath, ".txt");

            if (Boxes.Count == 0)
            {
                if (File.Exists(txtPath)) File.Delete(txtPath);
                CurrentImage.HasLabel = false;
            }
            else
            {
                var lines = Boxes.Select(b => b.ToYoloLine()).ToArray();
                File.WriteAllLines(txtPath, lines);
                CurrentImage.HasLabel = true;
            }

            IsDirty = false;
            RefreshStats();
        }

        // ══════════════ Navigation ══════════════

        public void GoToNextImage()
        {
            SaveCurrentLabels();
            if (_imageIndex < Images.Count - 1) ImageIndex++;
        }

        public void GoToPrevImage()
        {
            SaveCurrentLabels();
            if (_imageIndex > 0) ImageIndex--;
        }

        // ══════════════ Box operations ══════════════

        public void AddBox(double cx, double cy, double w, double h)
        {
            double left = Math.Max(0, cx - w / 2);
            double top  = Math.Max(0, cy - h / 2);
            double right  = Math.Min(1, cx + w / 2);
            double bottom = Math.Min(1, cy + h / 2);

            var box = new LabelBox
            {
                ClassId = SelectedClassId,
                CX = (left + right) / 2,
                CY = (top + bottom) / 2,
                W  = right - left,
                H  = bottom - top,
                Confidence = 0f
            };

            if (box.W < 0.005 || box.H < 0.005) return;

            if (SelectedClassId < Classes.Count && !Classes[SelectedClassId].IsChecked)
            {
                var firstChecked = Classes.FirstOrDefault(c => c.IsChecked);
                if (firstChecked != null)
                    box.ClassId = firstChecked.Id;
            }

            Boxes.Add(box);
            IsDirty = true;
        }

        public LabelBox? FindBoxAt(double nx, double ny)
        {
            LabelBox? best = null;
            double bestArea = double.MaxValue;
            foreach (var b in Boxes)
            {
                if (b.Contains(nx, ny))
                {
                    double area = b.W * b.H;
                    if (area < bestArea) { best = b; bestArea = area; }
                }
            }
            return best;
        }

        public void RemoveBox(LabelBox box)
        {
            Boxes.Remove(box);
            IsDirty = true;
        }

        // ══════════════ Delete image ══════════════

        private void DeleteCurrentImage()
        {
            if (CurrentImage == null) return;

            var path = CurrentImage.FullPath;
            var txt  = Path.ChangeExtension(path, ".txt");

            int idx = _imageIndex;
            Images.RemoveAt(idx);

            try { if (File.Exists(path)) File.Delete(path); } catch { }
            try { if (File.Exists(txt))  File.Delete(txt);  } catch { }

            RefreshStats();
            ImageIndex = Math.Min(idx, Images.Count - 1);
            StatusText = "Image deleted";
        }

        // ══════════════ Clean dataset ══════════════

        private void CleanDataset()
        {
            SaveCurrentLabels();
            if (string.IsNullOrEmpty(DatasetDir)) return;

            var blankDir = Path.Combine(DatasetDir, "blank");
            Directory.CreateDirectory(blankDir);

            int moved = 0;
            for (int i = Images.Count - 1; i >= 0; i--)
            {
                if (!Images[i].HasLabel)
                {
                    var src = Images[i].FullPath;
                    var dst = Path.Combine(blankDir, Path.GetFileName(src));
                    try
                    {
                        File.Move(src, dst, true);
                        var srcTxt = Path.ChangeExtension(src, ".txt");
                        if (File.Exists(srcTxt)) File.Delete(srcTxt);
                        Images.RemoveAt(i);
                        moved++;
                    }
                    catch { }
                }
            }

            RefreshStats();
            if (_imageIndex >= Images.Count) ImageIndex = Images.Count - 1;
            StatusText = $"{moved} blank images moved";
            System.Windows.MessageBox.Show($"{moved} files moved to 'blank' folder.", "Clean Dataset",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        // ══════════════ ONNX Auto-Label ══════════════

        private Microsoft.ML.OnnxRuntime.InferenceSession? _onnxSession;
        private string? _modelInputName;
        private int _modelInputSize = 640;

        private void LoadModel()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select ONNX Model",
                Filter = "ONNX Models (*.onnx)|*.onnx",
                InitialDirectory = Path.Combine(AppContext.BaseDirectory, "Models")
            };
            if (dlg.ShowDialog() != true) return;
            LoadModelFromPath(dlg.FileName);
        }

        /// <summary>Preprocess image into ONNX input tensor. Uses Marshal.Copy (single P/Invoke, not per-pixel).</summary>
        private static float[] PreprocessImage(string path, int sz)
        {
            using var mat = OpenCvSharp.Cv2.ImRead(path);
            using var resized = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Resize(mat, resized, new OpenCvSharp.Size(sz, sz));

            // Single bulk copy instead of 1.2M individual At<Vec3b> calls
            int totalPixels = sz * sz;
            byte[] pixels = new byte[totalPixels * 3];
            System.Runtime.InteropServices.Marshal.Copy(resized.Data, pixels, 0, pixels.Length);

            float[] input = new float[3 * totalPixels];
            const float scale = 1f / 255f;
            for (int i = 0; i < totalPixels; i++)
            {
                int pi = i * 3; // BGR layout
                input[i]                    = pixels[pi + 2] * scale; // R
                input[totalPixels + i]      = pixels[pi + 1] * scale; // G
                input[2 * totalPixels + i]  = pixels[pi]     * scale; // B
            }
            return input;
        }

        /// <summary>Run ONNX inference — thread-safe for DML provider.</summary>
        private List<LabelBox> RunInference(float[] input, int sz)
        {
            var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(input, new[] { 1, 3, sz, sz });
            var inputs = new[] { Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor(_modelInputName!, tensor) };

            using var results = _onnxSession!.Run(inputs);
            var output = results.First().AsTensor<float>();
            return ParseDetections(output, sz);
        }

        /// <summary>Load BitmapImage on background thread (Freeze for cross-thread).</summary>
        private static BitmapImage? LoadBitmapFrozen(string path)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        public async void RunAutoLabel()
        {
            if (_onnxSession == null || DisplayImage == null || CurrentImage == null) return;

            var src = CurrentImage.FullPath;
            int sz = _modelInputSize;
            StatusText = "Labeling...";

            try
            {
                // Preprocess + inference on background thread
                var detections = await Task.Run(() =>
                {
                    var input = PreprocessImage(src, sz);
                    return RunInference(input, sz);
                });

                // Update UI on main thread
                Boxes.Clear();
                foreach (var b in detections) Boxes.Add(b);
                StatusText = $"Label: {Boxes.Count} detections";
            }
            catch (Exception ex)
            {
                StatusText = $"Inference error: {ex.Message}";
            }

            IsDirty = true;
        }

        private void StopLabeling()
        {
            _labelAllCts?.Cancel();
        }

        /// <summary>
        /// Label ALL images.
        /// Per-image: preprocess + inference + file save on background thread (await Task.Run).
        /// The await returns to UI thread between images — gives GPU a break so WPF can render.
        /// Preview (bitmap + boxes) only every 20 images. StatusText every image.
        /// </summary>
        private async void LabelAllImages()
        {
            if (_onnxSession == null || Images.Count == 0) return;

            if (!ShowLabelAllConfirmation()) return;

            SaveCurrentLabels();
            IsLabeling = true;
            RelayCommand.RaiseCanExecuteChanged();

            _labelAllCts = new CancellationTokenSource();
            var ct = _labelAllCts.Token;

            int total = Images.Count;
            int sz = _modelInputSize;
            int labeled = 0;
            bool stopped = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                for (int i = 0; i < total; i++)
                {
                    if (ct.IsCancellationRequested) { stopped = true; break; }

                    var img = Images[i];
                    var path = img.FullPath;

                    // Preprocess + inference + file save + bitmap — all on background thread
                    var (dets, bitmap) = await Task.Run(() =>
                    {
                        var input = PreprocessImage(path, sz);
                        var d = RunInference(input, sz);

                        var txtPath = Path.ChangeExtension(path, ".txt");
                        if (d.Count == 0)
                        {
                            if (File.Exists(txtPath)) File.Delete(txtPath);
                        }
                        else
                        {
                            var lines = new string[d.Count];
                            for (int j = 0; j < d.Count; j++)
                                lines[j] = d[j].ToYoloLine();
                            File.WriteAllLines(txtPath, lines);
                        }

                        // Bitmap loaded in parallel with inference (already on bg thread)
                        var bi = LoadBitmapFrozen(path);
                        return (d, bi);
                    }, ct);

                    // --- Back on UI thread (lightweight) ---
                    img.HasLabel = dets.Count > 0;
                    if (dets.Count > 0) labeled++;

                    double ips = sw.Elapsed.TotalSeconds > 0 ? (i + 1) / sw.Elapsed.TotalSeconds : 0;
                    StatusText = $"Labeling {i + 1}/{total} — {ips:F1} img/s";

                    // Show every image — bitmap already loaded on bg thread
                    _imageIndex = i;
                    OnPropertyChanged(nameof(ImageIndex));
                    OnPropertyChanged(nameof(CurrentImage));
                    OnPropertyChanged(nameof(ImageCounter));

                    if (bitmap != null)
                    {
                        DisplayImage = bitmap;
                        ImgWidth = bitmap.PixelWidth;
                        ImgHeight = bitmap.PixelHeight;
                    }

                    Boxes.Clear();
                    foreach (var b in dets) Boxes.Add(b);
                }
            }
            catch (OperationCanceledException) { stopped = true; }
            finally
            {
                IsLabeling = false;
                IsDirty = false;
                _labelAllCts?.Dispose();
                _labelAllCts = null;
            }

            // Back to first image
            if (Images.Count > 0)
            {
                ImageIndex = 0;
                LoadCurrentImage();
            }

            RefreshStats();

            int blank = total - labeled;
            double totalSec = sw.Elapsed.TotalSeconds;
            double finalIps = totalSec > 0 ? total / totalSec : 0;

            string summary = stopped
                ? $"Stopped: {labeled}/{total} labeled"
                : $"Auto-Label Complete";

            ShowLabelAllResult(summary, total, labeled, blank, finalIps, totalSec);

            StatusText = stopped
                ? $"Stopped: {labeled}/{total} labeled ({finalIps:F1} img/s)"
                : $"Done: {labeled}/{total} — {labeled} boxes, {blank} blank ({finalIps:F1} img/s)";
            RelayCommand.RaiseCanExecuteChanged();
        }

        /// <summary>Phantom-styled result dialog after Label All completes.</summary>
        private void ShowLabelAllResult(string title, int totalImages, int labeled, int blank, double ips, double seconds)
        {
            var dlg = new System.Windows.Window
            {
                Width = 420, Height = 260,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x03, 0x07, 0x0F)),
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStyle = System.Windows.WindowStyle.None,
                AllowsTransparency = true,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x2E, 0x42)),
                BorderThickness = new System.Windows.Thickness(1)
            };

            var stack = new System.Windows.Controls.StackPanel
            {
                Margin = new System.Windows.Thickness(24, 20, 24, 20)
            };

            var titleTb = new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x9D)),
                Margin = new System.Windows.Thickness(0, 0, 0, 16)
            };

            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            string timeStr = mins > 0 ? $"{mins}m {secs}s" : $"{secs}s";

            var details = new System.Windows.Controls.TextBlock
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0xAA, 0xBB)),
                LineHeight = 20,
                Text = $"Images:    {totalImages}\n" +
                       $"Labeled:   {labeled}\n" +
                       $"Blank:     {blank}\n" +
                       $"Speed:     {ips:F1} img/s\n" +
                       $"Time:      {timeStr}",
                Margin = new System.Windows.Thickness(0, 0, 0, 20)
            };

            var btnOk = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 120, Height = 32,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1E, 0x28)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnOk.Click += (_, _) => dlg.Close();

            stack.Children.Add(titleTb);
            stack.Children.Add(details);
            stack.Children.Add(btnOk);
            dlg.Content = stack;

            var owner = System.Windows.Application.Current.MainWindow;
            if (owner != null) dlg.Owner = owner;

            dlg.ShowDialog();
        }

        /// <summary>Phantom-styled confirmation dialog for Label All.</summary>
        private bool ShowLabelAllConfirmation()
        {
            var dlg = new System.Windows.Window
            {
                Width = 400, Height = 210,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x03, 0x07, 0x0F)),
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStyle = System.Windows.WindowStyle.None,
                AllowsTransparency = true,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x2E, 0x42)),
                BorderThickness = new System.Windows.Thickness(1)
            };

            var stack = new System.Windows.Controls.StackPanel
            {
                Margin = new System.Windows.Thickness(24, 20, 24, 20)
            };

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "\u26A0  LABEL ALL IMAGES",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xE0, 0x00)),
                Margin = new System.Windows.Thickness(0, 0, 0, 14)
            };

            var msg = new System.Windows.Controls.TextBlock
            {
                Text = $"Auto-label ALL {Images.Count} images?\nExisting labels will be overwritten.",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0xAA, 0xBB)),
                TextWrapping = System.Windows.TextWrapping.Wrap,
                LineHeight = 18,
                Margin = new System.Windows.Thickness(0, 0, 0, 24)
            };

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var btnConfirm = new System.Windows.Controls.Button
            {
                Content = "CONFIRM",
                Width = 110, Height = 32,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x40, 0x10, 0x00)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x30, 0x00)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new System.Windows.Thickness(0, 0, 8, 0)
            };
            btnConfirm.Click += (_, _) => { dlg.DialogResult = true; dlg.Close(); };

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "CANCEL",
                Width = 110, Height = 32,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1E, 0x28)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0xAA, 0xBB)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (_, _) => { dlg.DialogResult = false; dlg.Close(); };

            btnPanel.Children.Add(btnConfirm);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(title);
            stack.Children.Add(msg);
            stack.Children.Add(btnPanel);
            dlg.Content = stack;

            var owner = System.Windows.Application.Current.MainWindow;
            if (owner != null) dlg.Owner = owner;

            return dlg.ShowDialog() == true;
        }

        /// <summary>Parse detections from output tensor without modifying Boxes collection.</summary>
        private List<LabelBox> ParseDetections(Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> output, int modelSize)
        {
            int dims = output.Dimensions[1];
            int rows = output.Dimensions[2];
            int numClasses = dims - 4;

            var detections = new List<LabelBox>();

            for (int r = 0; r < rows; r++)
            {
                float cx = output[0, 0, r] / modelSize;
                float cy = output[0, 1, r] / modelSize;
                float w  = output[0, 2, r] / modelSize;
                float h  = output[0, 3, r] / modelSize;

                int bestClass = -1;
                float bestConf = 0;
                for (int c = 0; c < numClasses; c++)
                {
                    float conf = output[0, c + 4, r];
                    if (conf > bestConf) { bestConf = conf; bestClass = c; }
                }

                if (bestConf < _confThreshold) continue;
                if (bestClass >= Classes.Count) continue;
                if (!Classes[bestClass].IsChecked) continue;

                detections.Add(new LabelBox
                {
                    ClassId = bestClass,
                    CX = cx, CY = cy, W = w, H = h,
                    Confidence = bestConf
                });
            }

            // NMS
            detections = detections.OrderByDescending(d => d.Confidence).ToList();
            var keep = new List<LabelBox>();
            foreach (var d in detections)
            {
                bool suppress = false;
                foreach (var k in keep)
                {
                    if (IoU(d, k) > 0.45f) { suppress = true; break; }
                }
                if (!suppress) keep.Add(d);
            }

            return keep;
        }

        private void ParseAndAddDetections(Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> output, int modelSize)
        {
            var keep = ParseDetections(output, modelSize);
            Boxes.Clear();
            foreach (var b in keep) Boxes.Add(b);
        }

        private static double IoU(LabelBox a, LabelBox b)
        {
            double x1 = Math.Max(a.Left, b.Left);
            double y1 = Math.Max(a.Top,  b.Top);
            double x2 = Math.Min(a.Right, b.Right);
            double y2 = Math.Min(a.Bottom, b.Bottom);

            if (x2 <= x1 || y2 <= y1) return 0;

            double inter = (x2 - x1) * (y2 - y1);
            double union  = a.W * a.H + b.W * b.H - inter;
            return union > 0 ? inter / union : 0;
        }

        // ══════════════ Config Persistence (via AppSettingsService) ══════════════

        public void SaveConfig()
        {
            if (_appSettings == null) return;

            _appSettings.LabelShowLabels = _showLabels;
            _appSettings.LabelShowCrosshair = _showCrosshair;
            _appSettings.LabelConfThreshold = ConfThresholdInt;
            _appSettings.LabelDisplayScale = _displayScale;
            _appSettings.LabelImageIndex = _imageIndex;
            _appSettings.LabelDatasetDir = DatasetDir;

            var checkedIds = Classes.Where(c => c.IsChecked).Select(c => c.Id.ToString());
            _appSettings.LabelCheckedClasses = string.Join(",", checkedIds);

            // Training settings
            _appSettings.TrainEpochs = _trainEpochs;
            _appSettings.TrainBatchSize = _trainBatchSize;
            _appSettings.TrainImgSize = _trainImgSize;
            _appSettings.TrainBaseModel = _trainBaseModel;
            _appSettings.TrainValPercent = _trainValPercent;
        }

        // ══════════════ Auto-Refresh (new images from gameplay) ══════════════

        /// <summary>
        /// Scans the current dataset directory (or CapturedImages) for new images
        /// that were added since the last load. Called every time the page becomes visible.
        /// </summary>
        public void RefreshDataset()
        {
            // If no dataset loaded yet, try CapturedImages
            if (string.IsNullOrEmpty(DatasetDir) || !Directory.Exists(DatasetDir))
            {
                string capturedDir = Path.Combine(AppContext.BaseDirectory, "CapturedImages");
                if (Directory.Exists(capturedDir))
                    LoadDatasetFromPath(capturedDir);
                return;
            }

            // Incremental scan — only add files not already in the list
            var existing = new HashSet<string>(Images.Select(i => i.FullPath), StringComparer.OrdinalIgnoreCase);

            var allFiles = Directory.EnumerateFiles(DatasetDir, "*.*", SearchOption.AllDirectories)
                .Where(f => ImgExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            int added = 0;
            foreach (var f in allFiles)
            {
                if (existing.Contains(f)) continue;

                var txt = Path.ChangeExtension(f, ".txt");
                Images.Add(new ImageEntry
                {
                    FullPath = f,
                    HasLabel = File.Exists(txt) && new FileInfo(txt).Length > 0
                });
                added++;
            }

            if (added > 0)
            {
                RefreshStats();
                StatusText = $"+{added} new images ({Images.Count} total)";

                // If no image was selected, go to first
                if (_imageIndex < 0 && Images.Count > 0)
                    ImageIndex = 0;
            }
        }

        // ══════════════ Stats ══════════════

        private void RefreshStats()
        {
            OnPropertyChanged(nameof(TotalImages));
            OnPropertyChanged(nameof(LabeledCount));
            OnPropertyChanged(nameof(UnlabeledCount));
            OnPropertyChanged(nameof(ImageCounter));
        }

        // ══════════════════════════════════════════════════════════════════
        //  TRAINING MODULE
        // ══════════════════════════════════════════════════════════════════

        // ── Training state ──

        private bool _isTraining;
        public bool IsTraining
        {
            get => _isTraining;
            set { _isTraining = value; OnPropertyChanged(); RelayCommand.RaiseCanExecuteChanged(); }
        }

        private bool _isDatasetReady;
        public bool IsDatasetReady
        {
            get => _isDatasetReady;
            set { _isDatasetReady = value; OnPropertyChanged(); RelayCommand.RaiseCanExecuteChanged(); }
        }

        // ── Training settings ──

        private int _trainEpochs = 100;
        public int TrainEpochs
        {
            get => _trainEpochs;
            set { _trainEpochs = Math.Clamp(value, 10, 1000); OnPropertyChanged(); }
        }

        private int _trainBatchSize = 16;
        public int TrainBatchSize
        {
            get => _trainBatchSize;
            set { _trainBatchSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(TrainBatchIndex)); }
        }

        private int _trainImgSize = 640;
        public int TrainImgSize
        {
            get => _trainImgSize;
            set { _trainImgSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(TrainImgSizeIndex)); }
        }

        private string _trainBaseModel = "yolov8n.pt";
        public string TrainBaseModel
        {
            get => _trainBaseModel;
            set { _trainBaseModel = value ?? "yolov8n.pt"; OnPropertyChanged(); OnPropertyChanged(nameof(TrainBaseModelIndex)); }
        }

        private int _trainValPercent = 20;
        public int TrainValPercent
        {
            get => _trainValPercent;
            set { _trainValPercent = Math.Clamp(value, 5, 50); OnPropertyChanged(); }
        }

        // ── Index-based ComboBox helpers ──

        private static readonly int[] BatchSizeOptions = { -1, 2, 4, 8, 16, 32 };
        public int TrainBatchIndex
        {
            get { int idx = Array.IndexOf(BatchSizeOptions, _trainBatchSize); return idx >= 0 ? idx : 4; }
            set { if (value >= 0 && value < BatchSizeOptions.Length) { _trainBatchSize = BatchSizeOptions[value]; OnPropertyChanged(); OnPropertyChanged(nameof(TrainBatchSize)); } }
        }

        private static readonly int[] ImgSizeOptions = { 320, 416, 480, 640, 960, 1280 };
        public int TrainImgSizeIndex
        {
            get { int idx = Array.IndexOf(ImgSizeOptions, _trainImgSize); return idx >= 0 ? idx : 3; }
            set { if (value >= 0 && value < ImgSizeOptions.Length) { _trainImgSize = ImgSizeOptions[value]; OnPropertyChanged(); OnPropertyChanged(nameof(TrainImgSize)); } }
        }

        private static readonly string[] BaseModelOptions = { "yolov8n.pt", "yolov8s.pt", "yolov8m.pt", "yolov8l.pt", "yolo11n.pt", "yolo11s.pt", "yolo11m.pt" };
        public int TrainBaseModelIndex
        {
            get { int idx = Array.IndexOf(BaseModelOptions, _trainBaseModel); return idx >= 0 ? idx : 0; }
            set { if (value >= 0 && value < BaseModelOptions.Length) { _trainBaseModel = BaseModelOptions[value]; OnPropertyChanged(); OnPropertyChanged(nameof(TrainBaseModel)); } }
        }

        // ── Training metrics ──

        private int _trainEpochCurrent;
        public int TrainEpochCurrent
        {
            get => _trainEpochCurrent;
            set { _trainEpochCurrent = value; OnPropertyChanged(); OnPropertyChanged(nameof(TrainEpochText)); }
        }
        public string TrainEpochText => _trainEpochCurrent > 0 ? $"{_trainEpochCurrent}/{_trainEpochs}" : "—";

        private double _trainLossBox;
        public double TrainLossBox { get => _trainLossBox; set { _trainLossBox = value; OnPropertyChanged(); } }

        private double _trainLossCls;
        public double TrainLossCls { get => _trainLossCls; set { _trainLossCls = value; OnPropertyChanged(); } }

        private double _trainLossDfl;
        public double TrainLossDfl { get => _trainLossDfl; set { _trainLossDfl = value; OnPropertyChanged(); } }

        private double _trainMap50;
        public double TrainMap50 { get => _trainMap50; set { _trainMap50 = value; OnPropertyChanged(); } }

        private double _trainMap5095;
        public double TrainMap5095 { get => _trainMap5095; set { _trainMap5095 = value; OnPropertyChanged(); } }

        private string _trainLog = "";
        public string TrainLog { get => _trainLog; set { _trainLog = value; OnPropertyChanged(); } }

        private string _trainStatus = "";
        public string TrainStatus { get => _trainStatus; set { _trainStatus = value; OnPropertyChanged(); } }

        private string _lastTrainedOnnx = "";
        public string LastTrainedOnnx
        {
            get => _lastTrainedOnnx;
            set { _lastTrainedOnnx = value; OnPropertyChanged(); RelayCommand.RaiseCanExecuteChanged(); }
        }

        // ── Process + log ──

        private System.Diagnostics.Process? _trainProcess;
        private readonly Queue<string> _logLines = new();
        private const int MaxLogLines = 100;

        // ══════════════ Find Python ══════════════

        private static string? FindPython()
        {
            foreach (string name in new[] { "python", "python3", "py" })
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = name,
                        Arguments = "--version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    if (p == null) continue;
                    p.WaitForExit(5000);
                    if (p.ExitCode == 0) return name;
                }
                catch { }
            }

            // Common Windows install locations
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new[]
            {
                Path.Combine(localApp, "Programs", "Python"),
                @"C:\Python3",
                @"C:\Python"
            };

            foreach (var basePath in candidates)
            {
                if (!Directory.Exists(basePath)) continue;
                try
                {
                    foreach (var dir in Directory.GetDirectories(basePath, "Python*"))
                    {
                        var exe = Path.Combine(dir, "python.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }
                catch { }
            }

            return null;
        }

        // ══════════════ Prepare Dataset (train/val split) ══════════════

        private void PrepareDataset()
        {
            if (string.IsNullOrEmpty(DatasetDir)) return;
            SaveCurrentLabels();

            var labeled = Images.Where(i => i.HasLabel).ToList();
            if (labeled.Count < 2)
            {
                TrainStatus = "Need at least 2 labeled images.";
                return;
            }

            TrainStatus = "Preparing dataset...";

            try
            {
                // YOLO directory structure
                string imgTrain = Path.Combine(DatasetDir, "images", "train");
                string imgVal   = Path.Combine(DatasetDir, "images", "val");
                string lblTrain = Path.Combine(DatasetDir, "labels", "train");
                string lblVal   = Path.Combine(DatasetDir, "labels", "val");

                Directory.CreateDirectory(imgTrain);
                Directory.CreateDirectory(imgVal);
                Directory.CreateDirectory(lblTrain);
                Directory.CreateDirectory(lblVal);

                // Shuffle + split
                var rng = new Random();
                var shuffled = labeled.OrderBy(_ => rng.Next()).ToList();
                int valCount = Math.Max(1, (int)(shuffled.Count * (_trainValPercent / 100.0)));
                int trainCount = shuffled.Count - valCount;

                for (int i = 0; i < shuffled.Count; i++)
                {
                    var img = shuffled[i];
                    var txt = Path.ChangeExtension(img.FullPath, ".txt");
                    if (!File.Exists(txt)) continue;

                    bool isTrain = i < trainCount;
                    string destImg = isTrain ? imgTrain : imgVal;
                    string destLbl = isTrain ? lblTrain : lblVal;

                    string fn = Path.GetFileName(img.FullPath);
                    string ln = Path.GetFileNameWithoutExtension(img.FullPath) + ".txt";

                    File.Copy(img.FullPath, Path.Combine(destImg, fn), true);
                    File.Copy(txt, Path.Combine(destLbl, ln), true);
                }

                // Read classes
                var classesFile = Path.Combine(DatasetDir, "classes.txt");
                string[] classNames;
                if (File.Exists(classesFile))
                    classNames = File.ReadAllLines(classesFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                else
                    classNames = Classes.Select(c => c.Name).ToArray();

                // Generate data.yaml
                var namesJoined = string.Join(", ", classNames.Select(n => $"'{n.Trim()}'"));
                var yaml = $"path: {DatasetDir.Replace("\\", "/")}\n" +
                           $"train: images/train\n" +
                           $"val: images/val\n" +
                           $"nc: {classNames.Length}\n" +
                           $"names: [{namesJoined}]\n";

                File.WriteAllText(Path.Combine(DatasetDir, "data.yaml"), yaml);

                IsDatasetReady = true;
                TrainStatus = $"Dataset: {trainCount} train, {valCount} val, {classNames.Length} classes";
            }
            catch (Exception ex)
            {
                TrainStatus = $"Error: {ex.Message}";
            }
        }

        // ══════════════ Start Training ══════════════

        private async void StartTraining()
        {
            if (IsTraining || string.IsNullOrEmpty(DatasetDir)) return;

            // Check Python
            string? python = FindPython();
            if (python == null)
            {
                TrainStatus = "Python not found. Install Python 3.8+ and: pip install ultralytics";
                return;
            }

            // Check data.yaml
            string dataYaml = Path.Combine(DatasetDir, "data.yaml");
            if (!File.Exists(dataYaml))
            {
                TrainStatus = "Click 'Preparar Dataset' first.";
                return;
            }

            IsTraining = true;
            _trainLog = "";
            _logLines.Clear();
            TrainEpochCurrent = 0;
            TrainLossBox = 0; TrainLossCls = 0; TrainLossDfl = 0;
            TrainMap50 = 0; TrainMap5095 = 0;
            LastTrainedOnnx = "";
            TrainStatus = "Starting training...";

            // Generate Python script
            string runsDir = Path.Combine(DatasetDir, "runs").Replace("\\", "/");
            string scriptPath = Path.Combine(DatasetDir, "_train.py");
            string yamlEscaped = dataYaml.Replace("\\", "/");

            string script =
$@"import sys, os
os.environ['YOLO_VERBOSE'] = 'True'
try:
    from ultralytics import YOLO
except ImportError:
    print('[CA] ERROR: ultralytics not installed. Run: pip install ultralytics')
    sys.exit(1)

print('[CA] Loading model: {_trainBaseModel}')
sys.stdout.flush()
model = YOLO('{_trainBaseModel}')

print(f'[CA] Training: epochs={_trainEpochs} batch={_trainBatchSize} imgsz={_trainImgSize}')
sys.stdout.flush()

results = model.train(
    data=r'{yamlEscaped}',
    epochs={_trainEpochs},
    batch={_trainBatchSize},
    imgsz={_trainImgSize},
    project=r'{runsDir}',
    name='train',
    exist_ok=True,
    verbose=True,
    device=0
)

print('[CA] Training complete!')
best = str(model.trainer.best)
print(f'[CA] Best: {{best}}')
sys.stdout.flush()

print('[CA] Exporting ONNX...')
sys.stdout.flush()
onnx_path = model.export(format='onnx', imgsz={_trainImgSize})
print(f'[CA] ONNX: {{onnx_path}}')
print('[CA] DONE')
sys.stdout.flush()
";

            File.WriteAllText(scriptPath, script);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = python,
                Arguments = $"-u \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = DatasetDir
            };

            try
            {
                _trainProcess = System.Diagnostics.Process.Start(psi);
                if (_trainProcess == null)
                {
                    TrainStatus = "Failed to start Python.";
                    IsTraining = false;
                    return;
                }

                TrainStatus = "Training started...";

                // Read stdout/stderr on background threads
                var stdoutTask = Task.Run(() => ReadTrainStream(_trainProcess.StandardOutput));
                var stderrTask = Task.Run(() => ReadTrainStream(_trainProcess.StandardError));

                await Task.Run(() => _trainProcess.WaitForExit());

                // Wait for output tasks to finish
                await Task.WhenAll(stdoutTask, stderrTask);

                int exitCode = _trainProcess.ExitCode;
                _trainProcess.Dispose();
                _trainProcess = null;

                if (exitCode == 0)
                {
                    TrainStatus = "Training complete!";

                    // Find exported ONNX
                    string trainDir = Path.Combine(DatasetDir, "runs", "train");
                    if (Directory.Exists(trainDir))
                    {
                        var onnxFiles = Directory.GetFiles(trainDir, "*.onnx", SearchOption.AllDirectories)
                            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                            .ToArray();
                        if (onnxFiles.Length > 0)
                            LastTrainedOnnx = onnxFiles[0];
                    }
                }
                else
                {
                    TrainStatus = $"Training failed (exit {exitCode})";
                }
            }
            catch (Exception ex)
            {
                TrainStatus = $"Error: {ex.Message}";
                _trainProcess?.Dispose();
                _trainProcess = null;
            }
            finally
            {
                IsTraining = false;
                try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
            }
        }

        private void ReadTrainStream(StreamReader reader)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string captured = line;
                dispatcher.BeginInvoke(() => ProcessTrainLine(captured));
            }
        }

        private void ProcessTrainLine(string line)
        {
            // Append to log
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines) _logLines.Dequeue();
            TrainLog = string.Join("\n", _logLines);

            // Parse epoch + losses:   1/100   1.7G   1.287   3.203   1.789   95   640:
            var epochMatch = System.Text.RegularExpressions.Regex.Match(line,
                @"\s+(\d+)/(\d+)\s+[\d.]+G?\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)");
            if (epochMatch.Success)
            {
                if (int.TryParse(epochMatch.Groups[1].Value, out int ep))
                    TrainEpochCurrent = ep;
                if (double.TryParse(epochMatch.Groups[3].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lb))
                    TrainLossBox = lb;
                if (double.TryParse(epochMatch.Groups[4].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lc))
                    TrainLossCls = lc;
                if (double.TryParse(epochMatch.Groups[5].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ld))
                    TrainLossDfl = ld;
                TrainStatus = $"Epoch {_trainEpochCurrent}/{_trainEpochs}";
            }

            // Parse mAP:   all  40  40  0.456  0.389  0.381  0.238
            var mapMatch = System.Text.RegularExpressions.Regex.Match(line,
                @"\s+all\s+\d+\s+\d+\s+[\d.]+\s+[\d.]+\s+([\d.]+)\s+([\d.]+)");
            if (mapMatch.Success)
            {
                if (double.TryParse(mapMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double m50))
                    TrainMap50 = m50;
                if (double.TryParse(mapMatch.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double m5095))
                    TrainMap5095 = m5095;
            }

            // Parse ONNX export path
            if (line.Contains("[CA] ONNX:"))
            {
                var p = line.Substring(line.IndexOf("[CA] ONNX:") + 10).Trim();
                if (File.Exists(p)) LastTrainedOnnx = p;
            }

            // Parse errors
            if (line.Contains("[CA] ERROR:"))
                TrainStatus = line.Replace("[CA] ERROR:", "").Trim();
        }

        // ══════════════ Stop Training ══════════════

        private void StopTraining()
        {
            if (_trainProcess != null && !_trainProcess.HasExited)
            {
                try
                {
                    _trainProcess.Kill(true);
                    TrainStatus = "Training stopped.";
                }
                catch { }
            }
        }

        // ══════════════ Install Trained Model ══════════════

        private void InstallOnnxModel()
        {
            if (string.IsNullOrEmpty(_lastTrainedOnnx) || !File.Exists(_lastTrainedOnnx)) return;

            string modelsDir = Path.Combine(AppContext.BaseDirectory, "Models");
            Directory.CreateDirectory(modelsDir);

            string destName = $"custom_{DateTime.Now:yyyyMMdd_HHmm}.onnx";
            string destPath = Path.Combine(modelsDir, destName);

            try
            {
                File.Copy(_lastTrainedOnnx, destPath, true);
                TrainStatus = $"Installed: {destName}";

                // Load the new model in the label maker
                LoadModelFromPath(destPath);
            }
            catch (Exception ex)
            {
                TrainStatus = $"Copy error: {ex.Message}";
            }
        }
    }
}
