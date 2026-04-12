using System.Windows.Media.Imaging; // For WriteableBitmap

namespace Core_Aim.ViewModels
{
    public class MonitorViewModel : ViewModelBase
    {
        // PageName for navigation (TRANSLATED)
        public string PageName { get; } = "Monitor";

        // --- Camera Image ---
        private WriteableBitmap? _cameraFeed;
        public WriteableBitmap? CameraFeed
        {
            get => _cameraFeed;
            set
            {
                _cameraFeed = value;
                OnPropertyChanged();
            }
        }

        // --- Statistics ---
        private int _detectionCount = 0;
        public int DetectionCount
        {
            get => _detectionCount;
            set
            {
                _detectionCount = value;
                OnPropertyChanged();
            }
        }

        private double _fps = 0;
        public double Fps
        {
            get => _fps;
            set
            {
                _fps = value;
                OnPropertyChanged();
            }
        }

        private string _modelInputSize = "N/A";
        public string ModelInputSize
        {
            get => _modelInputSize;
            set { _modelInputSize = value; OnPropertyChanged(); }
        }

        // ── Stats para o overlay de fullscreen ────────────────────────────────
        private int _captureFps;
        public int CaptureFps
        {
            get => _captureFps;
            set { _captureFps = value; OnPropertyChanged(); }
        }

        private int _nativeFps;
        /// <summary>FPS nativo negociado pelo driver da placa de captura.</summary>
        public int NativeFps
        {
            get => _nativeFps;
            set { _nativeFps = value; OnPropertyChanged(); }
        }

        private int _inferenceFps;
        public int InferenceFps
        {
            get => _inferenceFps;
            set { _inferenceFps = value; OnPropertyChanged(); }
        }

        private double _inferenceMs;
        public double InferenceMs
        {
            get => _inferenceMs;
            set { _inferenceMs = value; OnPropertyChanged(); }
        }

        private int _trackingFps;
        public int TrackingFps
        {
            get => _trackingFps;
            set { _trackingFps = value; OnPropertyChanged(); }
        }

        private int _pbFps;
        public int PbFps
        {
            get => _pbFps;
            set { _pbFps = value; OnPropertyChanged(); }
        }

        private double _pbMs;
        public double PbMs
        {
            get => _pbMs;
            set { _pbMs = value; OnPropertyChanged(); }
        }

        // ── Gatilhos TT2 (0‒100) ─────────────────────────────────────────────
        private int _ltValue;
        public int LTValue
        {
            get => _ltValue;
            set { _ltValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(LTPercent)); }
        }

        private int _rtValue;
        public int RTValue
        {
            get => _rtValue;
            set { _rtValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(RTPercent)); }
        }

        // 0.0–1.0 para ProgressBar
        public double LTPercent => _ltValue / 100.0;
        public double RTPercent => _rtValue / 100.0;

        // ── Sticks (-100..+100) ──────────────────────────────────────────────
        private int _stickLX, _stickLY, _stickRX, _stickRY;
        public int StickLX { get => _stickLX; set { _stickLX = value; OnPropertyChanged(); } }
        public int StickLY { get => _stickLY; set { _stickLY = value; OnPropertyChanged(); } }
        public int StickRX { get => _stickRX; set { _stickRX = value; OnPropertyChanged(); } }
        public int StickRY { get => _stickRY; set { _stickRY = value; OnPropertyChanged(); } }

        // ── AI Output Sticks (-100..+100) ────────────────────────────────────
        private int _aiRX, _aiRY;
        public int AiRX { get => _aiRX; set { _aiRX = value; OnPropertyChanged(); } }
        public int AiRY { get => _aiRY; set { _aiRY = value; OnPropertyChanged(); } }

        // ── Device Info ──────────────────────────────────────────────────────
        private string _deviceManufacturer = "";
        public string DeviceManufacturer { get => _deviceManufacturer; set { _deviceManufacturer = value; OnPropertyChanged(); } }

        private string _deviceProduct = "";
        public string DeviceProduct { get => _deviceProduct; set { _deviceProduct = value; OnPropertyChanged(); } }

        private string _deviceSerial = "";
        public string DeviceSerial { get => _deviceSerial; set { _deviceSerial = value; OnPropertyChanged(); } }

        private string _deviceFirmware = "";
        public string DeviceFirmware { get => _deviceFirmware; set { _deviceFirmware = value; OnPropertyChanged(); } }

        // ── GPC Info ─────────────────────────────────────────────────────────
        private string _gpcName = "";
        public string GpcName { get => _gpcName; set { _gpcName = value; OnPropertyChanged(); } }

        private string _gpcVersion = "";
        public string GpcVersion { get => _gpcVersion; set { _gpcVersion = value; OnPropertyChanged(); } }

        private string _gpcAuthor = "";
        public string GpcAuthor { get => _gpcAuthor; set { _gpcAuthor = value; OnPropertyChanged(); } }

        private bool _gpcUploaded;
        public bool GpcUploaded { get => _gpcUploaded; set { _gpcUploaded = value; OnPropertyChanged(); } }

        private byte _activeSlot;
        public byte ActiveSlot { get => _activeSlot; set { _activeSlot = value; OnPropertyChanged(); } }

        // ── Botoes (bool) ────────────────────────────────────────────────────
        private bool _btnA, _btnB, _btnX, _btnY, _btnLB, _btnRB, _btnL3, _btnR3;
        private bool _btnUp, _btnDown, _btnLeft, _btnRight, _btnView, _btnMenu, _btnXbox;

        public bool BtnA     { get => _btnA;     set { _btnA = value; OnPropertyChanged(); } }
        public bool BtnB     { get => _btnB;     set { _btnB = value; OnPropertyChanged(); } }
        public bool BtnX     { get => _btnX;     set { _btnX = value; OnPropertyChanged(); } }
        public bool BtnY     { get => _btnY;     set { _btnY = value; OnPropertyChanged(); } }
        public bool BtnLB    { get => _btnLB;    set { _btnLB = value; OnPropertyChanged(); } }
        public bool BtnRB    { get => _btnRB;    set { _btnRB = value; OnPropertyChanged(); } }
        public bool BtnL3    { get => _btnL3;    set { _btnL3 = value; OnPropertyChanged(); } }
        public bool BtnR3    { get => _btnR3;    set { _btnR3 = value; OnPropertyChanged(); } }
        public bool BtnUp    { get => _btnUp;    set { _btnUp = value; OnPropertyChanged(); } }
        public bool BtnDown  { get => _btnDown;  set { _btnDown = value; OnPropertyChanged(); } }
        public bool BtnLeft  { get => _btnLeft;  set { _btnLeft = value; OnPropertyChanged(); } }
        public bool BtnRight { get => _btnRight; set { _btnRight = value; OnPropertyChanged(); } }
        public bool BtnView  { get => _btnView;  set { _btnView = value; OnPropertyChanged(); } }
        public bool BtnMenu  { get => _btnMenu;  set { _btnMenu = value; OnPropertyChanged(); } }
        public bool BtnXbox  { get => _btnXbox;  set { _btnXbox = value; OnPropertyChanged(); } }
    }
}