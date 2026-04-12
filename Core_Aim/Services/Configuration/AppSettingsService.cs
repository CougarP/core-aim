using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Core_Aim.Services.Configuration
{
    public class AppSettingsService : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly string _configFilePath;
        private AppConfig _config;
        private readonly object _saveLock = new object();

        public AppSettingsService()
        {
            var baseDir = AppContext.BaseDirectory;
            _configFilePath = Path.Combine(baseDir, "config", "appsettings.json");
            _config = new AppConfig();
            LoadConfig();
        }

        public int HardwareType
        {
            get => _config.HardwareType;
            set { if (_config.HardwareType != value) { _config.HardwareType = value; OnPropertyChanged(nameof(HardwareType)); SaveConfigAsync(); } }
        }

        public string KmBoxIp
        {
            get => _config.KmBoxIp;
            set { if (_config.KmBoxIp != value) { _config.KmBoxIp = value; OnPropertyChanged(nameof(KmBoxIp)); SaveConfigAsync(); } }
        }

        public int KmBoxPort
        {
            get => _config.KmBoxPort;
            set { if (_config.KmBoxPort != value) { _config.KmBoxPort = value; OnPropertyChanged(nameof(KmBoxPort)); SaveConfigAsync(); } }
        }

        public string KmBoxUuid
        {
            get => _config.KmBoxUuid;
            set { if (_config.KmBoxUuid != value) { _config.KmBoxUuid = value; OnPropertyChanged(nameof(KmBoxUuid)); SaveConfigAsync(); } }
        }

        public string? SelectedModel
        {
            get => _config.SelectedModel;
            set
            {
                if (_config.SelectedModel != value)
                {
                    _config.SelectedModel = value;
                    OnPropertyChanged(nameof(SelectedModel));
                    SaveConfigAsync();
                }
            }
        }

        public int SelectedCameraIndex
        {
            get => _config.SelectedCameraIndex;
            set
            {
                if (_config.SelectedCameraIndex != value)
                {
                    _config.SelectedCameraIndex = value;
                    OnPropertyChanged(nameof(SelectedCameraIndex));
                    SaveConfigAsync();
                }
            }
        }

        public double ConfidenceThreshold
        {
            get => _config.ConfidenceThreshold;
            set
            {
                if (_config.ConfidenceThreshold != value)
                {
                    _config.ConfidenceThreshold = value;
                    OnPropertyChanged(nameof(ConfidenceThreshold));
                    SaveConfigAsync();
                }
            }
        }

        public int InferenceMode
        {
            get => _config.InferenceMode;
            set
            {
                if (_config.InferenceMode != value)
                {
                    _config.InferenceMode = value;
                    OnPropertyChanged(nameof(InferenceMode));
                    SaveConfigAsync();
                }
            }
        }

        public int TriggerSelection
        {
            get => _config.TriggerSelection;
            set
            {
                if (_config.TriggerSelection != value)
                {
                    _config.TriggerSelection = value;
                    OnPropertyChanged(nameof(TriggerSelection));
                    SaveConfigAsync();
                }
            }
        }

        public double FovSize
        {
            get => _config.FovSize;
            set
            {
                if (_config.FovSize != value)
                {
                    _config.FovSize = value;
                    OnPropertyChanged(nameof(FovSize));
                    SaveConfigAsync();
                }
            }
        }

        public bool ShowFov
        {
            get => _config.ShowFov;
            set
            {
                if (_config.ShowFov != value)
                {
                    _config.ShowFov = value;
                    OnPropertyChanged(nameof(ShowFov));
                    SaveConfigAsync();
                }
            }
        }

        public double PidKp
        {
            get => _config.PidKp;
            set
            {
                if (_config.PidKp != value)
                {
                    _config.PidKp = value;
                    OnPropertyChanged(nameof(PidKp));
                    SaveConfigAsync();
                }
            }
        }

        public double PidKi
        {
            get => _config.PidKi;
            set { if (_config.PidKi != value) { _config.PidKi = value; OnPropertyChanged(nameof(PidKi)); SaveConfigAsync(); } }
        }

        public double PidKd
        {
            get => _config.PidKd;
            set
            {
                if (_config.PidKd != value)
                {
                    _config.PidKd = value;
                    OnPropertyChanged(nameof(PidKd));
                    SaveConfigAsync();
                }
            }
        }

        public double AimResponseCurve
        {
            get => _config.AimResponseCurve;
            set
            {
                if (_config.AimResponseCurve != value)
                {
                    _config.AimResponseCurve = value;
                    OnPropertyChanged(nameof(AimResponseCurve));
                    SaveConfigAsync();
                }
            }
        }

        public double AimOffsetY
        {
            get => _config.AimOffsetY;
            set
            {
                if (_config.AimOffsetY != value)
                {
                    _config.AimOffsetY = value;
                    OnPropertyChanged(nameof(AimOffsetY));
                    SaveConfigAsync();
                }
            }
        }

        public bool IsIconMode
        {
            get => _config.IsIconMode;
            set
            {
                if (_config.IsIconMode != value)
                {
                    _config.IsIconMode = value;
                    OnPropertyChanged(nameof(IsIconMode));
                    SaveConfigAsync();
                }
            }
        }

        public double OffsetValuePlayer
        {
            get => _config.OffsetValuePlayer;
            set
            {
                if (_config.OffsetValuePlayer != value)
                {
                    _config.OffsetValuePlayer = value;
                    OnPropertyChanged(nameof(OffsetValuePlayer));
                    SaveConfigAsync();
                }
            }
        }

        public double OffsetValueIcon
        {
            get => _config.OffsetValueIcon;
            set
            {
                if (_config.OffsetValueIcon != value)
                {
                    _config.OffsetValueIcon = value;
                    OnPropertyChanged(nameof(OffsetValueIcon));
                    SaveConfigAsync();
                }
            }
        }

        public string TitanTwoProtocol
        {
            get => _config.TitanTwoProtocol;
            set
            {
                if (_config.TitanTwoProtocol != value)
                {
                    _config.TitanTwoProtocol = value;
                    OnPropertyChanged(nameof(TitanTwoProtocol));
                    SaveConfigAsync();
                }
            }
        }

        public string TitanTwoInputPollRate
        {
            get => _config.TitanTwoInputPollRate;
            set
            {
                if (_config.TitanTwoInputPollRate != value)
                {
                    _config.TitanTwoInputPollRate = value;
                    OnPropertyChanged(nameof(TitanTwoInputPollRate));
                    SaveConfigAsync();
                }
            }
        }

        public string TitanTwoOutputPollRate
        {
            get => _config.TitanTwoOutputPollRate;
            set
            {
                if (_config.TitanTwoOutputPollRate != value)
                {
                    _config.TitanTwoOutputPollRate = value;
                    OnPropertyChanged(nameof(TitanTwoOutputPollRate));
                    SaveConfigAsync();
                }
            }
        }

        public int TitanTwoSlot
        {
            get => _config.TitanTwoSlot;
            set
            {
                if (_config.TitanTwoSlot != value)
                {
                    _config.TitanTwoSlot = value;
                    OnPropertyChanged(nameof(TitanTwoSlot));
                    SaveConfigAsync();
                }
            }
        }

        // ── Macros GPC ──────────────────────────────────────────────
        public bool MacroRapidFire   { get => _config.MacroRapidFire;   set { if (_config.MacroRapidFire   != value) { _config.MacroRapidFire   = value; OnPropertyChanged(nameof(MacroRapidFire));   SaveConfigAsync(); } } }
        public bool MacroAntiRecoil  { get => _config.MacroAntiRecoil;  set { if (_config.MacroAntiRecoil  != value) { _config.MacroAntiRecoil  = value; OnPropertyChanged(nameof(MacroAntiRecoil));  SaveConfigAsync(); } } }
        public bool MacroDropShot    { get => _config.MacroDropShot;    set { if (_config.MacroDropShot    != value) { _config.MacroDropShot    = value; OnPropertyChanged(nameof(MacroDropShot));    SaveConfigAsync(); } } }
        public bool MacroCrouchPeek  { get => _config.MacroCrouchPeek;  set { if (_config.MacroCrouchPeek  != value) { _config.MacroCrouchPeek  = value; OnPropertyChanged(nameof(MacroCrouchPeek));  SaveConfigAsync(); } } }
        public bool MacroTacSprint   { get => _config.MacroTacSprint;   set { if (_config.MacroTacSprint   != value) { _config.MacroTacSprint   = value; OnPropertyChanged(nameof(MacroTacSprint));   SaveConfigAsync(); } } }
        public bool MacroSlideCancel { get => _config.MacroSlideCancel; set { if (_config.MacroSlideCancel != value) { _config.MacroSlideCancel = value; OnPropertyChanged(nameof(MacroSlideCancel)); SaveConfigAsync(); } } }
        public bool MacroAutoPing    { get => _config.MacroAutoPing;    set { if (_config.MacroAutoPing    != value) { _config.MacroAutoPing    = value; OnPropertyChanged(nameof(MacroAutoPing));    SaveConfigAsync(); } } }

        // Ajustes de macro GPC
        public double MacroArVertical   { get => _config.MacroArVertical;   set { if (_config.MacroArVertical   != value) { _config.MacroArVertical   = value; OnPropertyChanged(nameof(MacroArVertical));   SaveConfigAsync(); } } }
        public double MacroArHorizontal { get => _config.MacroArHorizontal; set { if (_config.MacroArHorizontal != value) { _config.MacroArHorizontal = value; OnPropertyChanged(nameof(MacroArHorizontal)); SaveConfigAsync(); } } }
        public double MacroRapidFireMs  { get => _config.MacroRapidFireMs;  set { if (_config.MacroRapidFireMs  != value) { _config.MacroRapidFireMs  = value; OnPropertyChanged(nameof(MacroRapidFireMs));  SaveConfigAsync(); } } }

        // Stick override
        public double PlayerBlend      { get => _config.PlayerBlend;      set { if (_config.PlayerBlend      != value) { _config.PlayerBlend      = value; OnPropertyChanged(nameof(PlayerBlend));      SaveConfigAsync(); } } }
        public double DisableOnMove    { get => _config.DisableOnMove;    set { if (_config.DisableOnMove    != value) { _config.DisableOnMove    = value; OnPropertyChanged(nameof(DisableOnMove));    SaveConfigAsync(); } } }

        // Timing parameters
        public double QuickscopeDelay  { get => _config.QuickscopeDelay;  set { if (_config.QuickscopeDelay  != value) { _config.QuickscopeDelay  = value; OnPropertyChanged(nameof(QuickscopeDelay));  SaveConfigAsync(); } } }
        public double JumpShotDelay    { get => _config.JumpShotDelay;    set { if (_config.JumpShotDelay    != value) { _config.JumpShotDelay    = value; OnPropertyChanged(nameof(JumpShotDelay));    SaveConfigAsync(); } } }
        public double BunnyHopDelay    { get => _config.BunnyHopDelay;    set { if (_config.BunnyHopDelay    != value) { _config.BunnyHopDelay    = value; OnPropertyChanged(nameof(BunnyHopDelay));    SaveConfigAsync(); } } }
        public double StrafePower      { get => _config.StrafePower;      set { if (_config.StrafePower      != value) { _config.StrafePower      = value; OnPropertyChanged(nameof(StrafePower));      SaveConfigAsync(); } } }

        // Macros v4
        public bool MacroJumpShot       { get => _config.MacroJumpShot;       set { if (_config.MacroJumpShot       != value) { _config.MacroJumpShot       = value; OnPropertyChanged(nameof(MacroJumpShot));       SaveConfigAsync(); } } }
        public bool MacroBunnyHop       { get => _config.MacroBunnyHop;       set { if (_config.MacroBunnyHop       != value) { _config.MacroBunnyHop       = value; OnPropertyChanged(nameof(MacroBunnyHop));       SaveConfigAsync(); } } }
        public bool MacroQuickscope     { get => _config.MacroQuickscope;     set { if (_config.MacroQuickscope     != value) { _config.MacroQuickscope     = value; OnPropertyChanged(nameof(MacroQuickscope));     SaveConfigAsync(); } } }
        public bool MacroAutoBreath     { get => _config.MacroAutoBreath;     set { if (_config.MacroAutoBreath     != value) { _config.MacroAutoBreath     = value; OnPropertyChanged(nameof(MacroAutoBreath));     SaveConfigAsync(); } } }
        public bool MacroAutoReload     { get => _config.MacroAutoReload;     set { if (_config.MacroAutoReload     != value) { _config.MacroAutoReload     = value; OnPropertyChanged(nameof(MacroAutoReload));     SaveConfigAsync(); } } }
        public bool MacroAutoMelee      { get => _config.MacroAutoMelee;      set { if (_config.MacroAutoMelee      != value) { _config.MacroAutoMelee      = value; OnPropertyChanged(nameof(MacroAutoMelee));      SaveConfigAsync(); } } }
        public bool MacroAutoADS        { get => _config.MacroAutoADS;        set { if (_config.MacroAutoADS        != value) { _config.MacroAutoADS        = value; OnPropertyChanged(nameof(MacroAutoADS));        SaveConfigAsync(); } } }
        public bool MacroHairTrigger    { get => _config.MacroHairTrigger;    set { if (_config.MacroHairTrigger    != value) { _config.MacroHairTrigger    = value; OnPropertyChanged(nameof(MacroHairTrigger));    SaveConfigAsync(); } } }
        public bool MacroStrafeShot     { get => _config.MacroStrafeShot;     set { if (_config.MacroStrafeShot     != value) { _config.MacroStrafeShot     = value; OnPropertyChanged(nameof(MacroStrafeShot));     SaveConfigAsync(); } } }
        public bool MacroCookProtection { get => _config.MacroCookProtection; set { if (_config.MacroCookProtection != value) { _config.MacroCookProtection = value; OnPropertyChanged(nameof(MacroCookProtection)); SaveConfigAsync(); } } }
        public bool MacroDeadZoneRemoval{ get => _config.MacroDeadZoneRemoval;set { if (_config.MacroDeadZoneRemoval!= value) { _config.MacroDeadZoneRemoval= value; OnPropertyChanged(nameof(MacroDeadZoneRemoval));SaveConfigAsync(); } } }

        public bool AutoUploadGpc { get => _config.AutoUploadGpc; set { if (_config.AutoUploadGpc != value) { _config.AutoUploadGpc = value; OnPropertyChanged(nameof(AutoUploadGpc)); SaveConfigAsync(); } } }

        public int InferenceIntervalMs
        {
            get => _config.InferenceIntervalMs;
            set
            {
                if (_config.InferenceIntervalMs != value)
                {
                    _config.InferenceIntervalMs = value;
                    OnPropertyChanged(nameof(InferenceIntervalMs));
                    SaveConfigAsync();
                }
            }
        }

        public int TrackingIntervalMs
        {
            get => _config.TrackingIntervalMs;
            set
            {
                if (_config.TrackingIntervalMs != value)
                {
                    _config.TrackingIntervalMs = value;
                    OnPropertyChanged(nameof(TrackingIntervalMs));
                    SaveConfigAsync();
                }
            }
        }

        public int CaptureMode
        {
            get => _config.CaptureMode;
            set
            {
                if (_config.CaptureMode != value)
                {
                    _config.CaptureMode = value;
                    OnPropertyChanged(nameof(CaptureMode));
                    SaveConfigAsync();
                }
            }
        }
        public int CaptureBackend
        {
            // Clamp legacy: o antigo idx 6 (MF Native) foi consolidado em 5
            // após remoção do path WinRT (idx 5 antigo).
            get
            {
                int v = _config.CaptureBackend;
                if (v < 0 || v > 5) return 0;
                return v;
            }
            set
            {
                int v = value < 0 || value > 5 ? 0 : value;
                if (_config.CaptureBackend != v)
                {
                    _config.CaptureBackend = v;
                    OnPropertyChanged(nameof(CaptureBackend));
                    SaveConfigAsync();
                }
            }
        }

        public int CaptureWidth
        {
            get => _config.CaptureWidth;
            set { if (_config.CaptureWidth != value) { _config.CaptureWidth = value; OnPropertyChanged(nameof(CaptureWidth)); SaveConfigAsync(); } }
        }

        public int CaptureHeight
        {
            get => _config.CaptureHeight;
            set { if (_config.CaptureHeight != value) { _config.CaptureHeight = value; OnPropertyChanged(nameof(CaptureHeight)); SaveConfigAsync(); } }
        }

        public int CaptureFps
        {
            get => _config.CaptureFps;
            set { if (_config.CaptureFps != value) { _config.CaptureFps = value; OnPropertyChanged(nameof(CaptureFps)); SaveConfigAsync(); } }
        }

        public string CaptureAdvancedConfig
        {
            get => _config.CaptureAdvancedConfig;
            set { if (_config.CaptureAdvancedConfig != value) { _config.CaptureAdvancedConfig = value; OnPropertyChanged(nameof(CaptureAdvancedConfig)); SaveConfigAsync(); } }
        }

        public bool ShowBoundingBoxes
        {
            get => _config.ShowBoundingBoxes;
            set { if (_config.ShowBoundingBoxes != value) { _config.ShowBoundingBoxes = value; OnPropertyChanged(nameof(ShowBoundingBoxes)); SaveConfigAsync(); } }
        }

        public int BoxStyle
        {
            get => _config.BoxStyle;
            set { if (_config.BoxStyle != value) { _config.BoxStyle = value; OnPropertyChanged(nameof(BoxStyle)); SaveConfigAsync(); } }
        }

        public int BoxThickness
        {
            get => _config.BoxThickness;
            set { if (_config.BoxThickness != value) { _config.BoxThickness = value; OnPropertyChanged(nameof(BoxThickness)); OnPropertyChanged(nameof(BoxThicknessIndex)); SaveConfigAsync(); } }
        }

        // Índice 0/1/2 para ComboBox: mapeia para espessura 1/2/3
        public int BoxThicknessIndex
        {
            get => Math.Clamp(_config.BoxThickness - 1, 0, 2);
            set { BoxThickness = Math.Clamp(value + 1, 1, 3); }
        }

        public bool BoxFillEnabled
        {
            get => _config.BoxFillEnabled;
            set { if (_config.BoxFillEnabled != value) { _config.BoxFillEnabled = value; OnPropertyChanged(nameof(BoxFillEnabled)); SaveConfigAsync(); } }
        }

        public int BoxFillAlpha
        {
            get => _config.BoxFillAlpha;
            set { if (_config.BoxFillAlpha != value) { _config.BoxFillAlpha = value; OnPropertyChanged(nameof(BoxFillAlpha)); SaveConfigAsync(); } }
        }

        public bool ShowFps
        {
            get => _config.ShowFps;
            set
            {
                if (_config.ShowFps != value)
                {
                    _config.ShowFps = value;
                    OnPropertyChanged(nameof(ShowFps));
                    SaveConfigAsync();
                }
            }
        }

        public bool ShowDetectionInfo
        {
            get => _config.ShowDetectionInfo;
            set
            {
                if (_config.ShowDetectionInfo != value)
                {
                    _config.ShowDetectionInfo = value;
                    OnPropertyChanged(nameof(ShowDetectionInfo));
                    SaveConfigAsync();
                }
            }
        }

        public bool ColorFilterEnabled
        {
            get => _config.ColorFilterEnabled;
            set { if (_config.ColorFilterEnabled != value) { _config.ColorFilterEnabled = value; OnPropertyChanged(nameof(ColorFilterEnabled)); SaveConfigAsync(); } }
        }

        public string ColorFilterHex
        {
            get => _config.ColorFilterHex;
            set { if (_config.ColorFilterHex != value) { _config.ColorFilterHex = value; OnPropertyChanged(nameof(ColorFilterHex)); SaveConfigAsync(); } }
        }

        public int ColorTolerance
        {
            get => _config.ColorTolerance;
            set { if (_config.ColorTolerance != value) { _config.ColorTolerance = value; OnPropertyChanged(nameof(ColorTolerance)); SaveConfigAsync(); } }
        }

        public ObservableCollection<string> ModelList { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> CameraList { get; } = new ObservableCollection<string>();

        public void LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var loadedConfig = JsonSerializer.Deserialize<AppConfig>(json);
                    if (loadedConfig != null) _config = loadedConfig;
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);
                    SaveConfig();
                }
            }
            catch { }
        }

        public bool SaveDetections
        {
            get => _config.SaveDetections;
            set
            {
                if (_config.SaveDetections != value)
                {
                    _config.SaveDetections = value;
                    OnPropertyChanged(nameof(SaveDetections));
                    SaveConfigAsync();
                }
            }
        }

        public bool SaveEnemyOnly
        {
            get => _config.SaveEnemyOnly;
            set
            {
                if (_config.SaveEnemyOnly != value)
                {
                    _config.SaveEnemyOnly = value;
                    OnPropertyChanged(nameof(SaveEnemyOnly));
                    SaveConfigAsync();
                }
            }
        }

        public int DatasetCropSize
        {
            get => _config.DatasetCropSize;
            set
            {
                if (_config.DatasetCropSize != value)
                {
                    _config.DatasetCropSize = value;
                    OnPropertyChanged(nameof(DatasetCropSize));
                    SaveConfigAsync();
                }
            }
        }

        public bool SaveLabels
        {
            get => _config.SaveLabels;
            set { if (_config.SaveLabels != value) { _config.SaveLabels = value; OnPropertyChanged(nameof(SaveLabels)); SaveConfigAsync(); } }
        }

        public bool SaveLabelYolo
        {
            get => _config.SaveLabelYolo;
            set { if (_config.SaveLabelYolo != value) { _config.SaveLabelYolo = value; OnPropertyChanged(nameof(SaveLabelYolo)); SaveConfigAsync(); } }
        }

        public bool SaveLabelPixelBot
        {
            get => _config.SaveLabelPixelBot;
            set { if (_config.SaveLabelPixelBot != value) { _config.SaveLabelPixelBot = value; OnPropertyChanged(nameof(SaveLabelPixelBot)); SaveConfigAsync(); } }
        }

        public bool SkipBlurryImages
        {
            get => _config.SkipBlurryImages;
            set { if (_config.SkipBlurryImages != value) { _config.SkipBlurryImages = value; OnPropertyChanged(nameof(SkipBlurryImages)); SaveConfigAsync(); } }
        }

        public double BlurThreshold
        {
            get => _config.BlurThreshold;
            set { if (_config.BlurThreshold != value) { _config.BlurThreshold = value; OnPropertyChanged(nameof(BlurThreshold)); SaveConfigAsync(); } }
        }

        public int AimCurveType
        {
            get => _config.AimCurveType;
            set
            {
                if (_config.AimCurveType != value)
                {
                    _config.AimCurveType = value;
                    OnPropertyChanged(nameof(AimCurveType));
                    SaveConfigAsync();
                }
            }
        }

        public double AimDeadZone
        {
            get => _config.AimDeadZone;
            set
            {
                if (_config.AimDeadZone != value)
                {
                    _config.AimDeadZone = value;
                    OnPropertyChanged(nameof(AimDeadZone));
                    SaveConfigAsync();
                }
            }
        }

        public string SpeedProfile
        {
            get => _config.SpeedProfile ?? "Flat";
            set { if (_config.SpeedProfile != value) { _config.SpeedProfile = value; OnPropertyChanged(nameof(SpeedProfile)); SaveConfigAsync(); } }
        }

        public double TrackingSpeedHIP
        {
            get => _config.TrackingSpeedHIP;
            set { if (_config.TrackingSpeedHIP != value) { _config.TrackingSpeedHIP = value; OnPropertyChanged(nameof(TrackingSpeedHIP)); SaveConfigAsync(); } }
        }

        public double TrackingSpeedADS
        {
            get => _config.TrackingSpeedADS;
            set { if (_config.TrackingSpeedADS != value) { _config.TrackingSpeedADS = value; OnPropertyChanged(nameof(TrackingSpeedADS)); SaveConfigAsync(); } }
        }

        public double PixelBotTrackingSpeed
        {
            get => _config.PixelBotTrackingSpeed;
            set { if (_config.PixelBotTrackingSpeed != value) { _config.PixelBotTrackingSpeed = value; OnPropertyChanged(nameof(PixelBotTrackingSpeed)); SaveConfigAsync(); } }
        }

        public double PixelBotAimResponse
        {
            get => _config.PixelBotAimResponse;
            set { if (_config.PixelBotAimResponse != value) { _config.PixelBotAimResponse = value; OnPropertyChanged(nameof(PixelBotAimResponse)); SaveConfigAsync(); } }
        }

        public double PixelBotDeadZone
        {
            get => _config.PixelBotDeadZone;
            set { if (_config.PixelBotDeadZone != value) { _config.PixelBotDeadZone = value; OnPropertyChanged(nameof(PixelBotDeadZone)); SaveConfigAsync(); } }
        }

        // PB + Titan Two
        public double PixelBotTrackingSpeedTT2
        {
            get => _config.PixelBotTrackingSpeedTT2;
            set { if (_config.PixelBotTrackingSpeedTT2 != value) { _config.PixelBotTrackingSpeedTT2 = value; OnPropertyChanged(nameof(PixelBotTrackingSpeedTT2)); SaveConfigAsync(); } }
        }

        public double PixelBotAimResponseTT2
        {
            get => _config.PixelBotAimResponseTT2;
            set { if (_config.PixelBotAimResponseTT2 != value) { _config.PixelBotAimResponseTT2 = value; OnPropertyChanged(nameof(PixelBotAimResponseTT2)); SaveConfigAsync(); } }
        }

        public double PixelBotDeadZoneTT2
        {
            get => _config.PixelBotDeadZoneTT2;
            set { if (_config.PixelBotDeadZoneTT2 != value) { _config.PixelBotDeadZoneTT2 = value; OnPropertyChanged(nameof(PixelBotDeadZoneTT2)); SaveConfigAsync(); } }
        }

        public int RestrictedTracking
        {
            get => _config.RestrictedTracking;
            set { if (_config.RestrictedTracking != value) { _config.RestrictedTracking = value; OnPropertyChanged(nameof(RestrictedTracking)); SaveConfigAsync(); } }
        }

        public int TrackingDelay
        {
            get => _config.TrackingDelay;
            set { if (_config.TrackingDelay != value) { _config.TrackingDelay = value; OnPropertyChanged(nameof(TrackingDelay)); SaveConfigAsync(); } }
        }

        public bool PredictionEnabled
        {
            get => _config.PredictionEnabled;
            set { if (_config.PredictionEnabled != value) { _config.PredictionEnabled = value; OnPropertyChanged(nameof(PredictionEnabled)); SaveConfigAsync(); } }
        }

        public string PredictionMethod
        {
            get => _config.PredictionMethod ?? "Kalman Filter";
            set { if (_config.PredictionMethod != value) { _config.PredictionMethod = value; OnPropertyChanged(nameof(PredictionMethod)); SaveConfigAsync(); } }
        }

        // ── Recoil KMBox ─────────────────────────────────────────────────
        public bool RecoilEnabledKm
        {
            get => _config.RecoilEnabledKm;
            set { if (_config.RecoilEnabledKm != value) { _config.RecoilEnabledKm = value; OnPropertyChanged(nameof(RecoilEnabledKm)); SaveConfigAsync(); } }
        }
        public int RecoilVerticalKm
        {
            get => _config.RecoilVerticalKm;
            set { if (_config.RecoilVerticalKm != value) { _config.RecoilVerticalKm = value; OnPropertyChanged(nameof(RecoilVerticalKm)); SaveConfigAsync(); } }
        }
        public int RecoilHorizontalKm
        {
            get => _config.RecoilHorizontalKm;
            set { if (_config.RecoilHorizontalKm != value) { _config.RecoilHorizontalKm = value; OnPropertyChanged(nameof(RecoilHorizontalKm)); SaveConfigAsync(); } }
        }
        public int RecoilIntervalKm
        {
            get => _config.RecoilIntervalKm;
            set { if (_config.RecoilIntervalKm != value) { _config.RecoilIntervalKm = value; OnPropertyChanged(nameof(RecoilIntervalKm)); SaveConfigAsync(); } }
        }

        // ── Recoil Titan Two ─────────────────────────────────────────────
        public bool RecoilEnabledTT2
        {
            get => _config.RecoilEnabledTT2;
            set { if (_config.RecoilEnabledTT2 != value) { _config.RecoilEnabledTT2 = value; OnPropertyChanged(nameof(RecoilEnabledTT2)); SaveConfigAsync(); } }
        }
        public int RecoilVerticalTT2
        {
            get => _config.RecoilVerticalTT2;
            set { if (_config.RecoilVerticalTT2 != value) { _config.RecoilVerticalTT2 = value; OnPropertyChanged(nameof(RecoilVerticalTT2)); SaveConfigAsync(); } }
        }
        public int RecoilHorizontalTT2
        {
            get => _config.RecoilHorizontalTT2;
            set { if (_config.RecoilHorizontalTT2 != value) { _config.RecoilHorizontalTT2 = value; OnPropertyChanged(nameof(RecoilHorizontalTT2)); SaveConfigAsync(); } }
        }
        public int RecoilIntervalTT2
        {
            get => _config.RecoilIntervalTT2;
            set { if (_config.RecoilIntervalTT2 != value) { _config.RecoilIntervalTT2 = value; OnPropertyChanged(nameof(RecoilIntervalTT2)); SaveConfigAsync(); } }
        }

        // ── Legacy ───────────────────────────────────────────────────────
        public bool RecoilEnabled
        {
            get => _config.RecoilEnabled;
            set { if (_config.RecoilEnabled != value) { _config.RecoilEnabled = value; OnPropertyChanged(nameof(RecoilEnabled)); SaveConfigAsync(); } }
        }
        public int RecoilHorizontalForce
        {
            get => _config.RecoilHorizontalForce;
            set { if (_config.RecoilHorizontalForce != value) { _config.RecoilHorizontalForce = value; OnPropertyChanged(nameof(RecoilHorizontalForce)); SaveConfigAsync(); } }
        }
        public int RecoilVerticalForce
        {
            get => _config.RecoilVerticalForce;
            set { if (_config.RecoilVerticalForce != value) { _config.RecoilVerticalForce = value; OnPropertyChanged(nameof(RecoilVerticalForce)); SaveConfigAsync(); } }
        }
        public int RecoilInterval
        {
            get => _config.RecoilInterval;
            set { if (_config.RecoilInterval != value) { _config.RecoilInterval = value; OnPropertyChanged(nameof(RecoilInterval)); SaveConfigAsync(); } }
        }

        public int GpuDeviceId
        {
            get => _config.GpuDeviceId;
            set { if (_config.GpuDeviceId != value) { _config.GpuDeviceId = value; OnPropertyChanged(nameof(GpuDeviceId)); SaveConfigAsync(); } }
        }

        public double AdrcB0
        {
            get => _config.AdrcB0;
            set { if (_config.AdrcB0 != value) { _config.AdrcB0 = value; OnPropertyChanged(nameof(AdrcB0)); SaveConfigAsync(); } }
        }

        public bool AutoTuneEnabled
        {
            get => _config.AutoTuneEnabled;
            set { if (_config.AutoTuneEnabled != value) { _config.AutoTuneEnabled = value; OnPropertyChanged(nameof(AutoTuneEnabled)); SaveConfigAsync(); } }
        }

        public string FullscreenKey
        {
            get => _config.FullscreenKey;
            set { if (_config.FullscreenKey != value) { _config.FullscreenKey = value; OnPropertyChanged(nameof(FullscreenKey)); SaveConfigAsync(); } }
        }

        public double TT2SpeedHIP
        {
            get => _config.TT2SpeedHIP;
            set { if (_config.TT2SpeedHIP != value) { _config.TT2SpeedHIP = value; OnPropertyChanged(nameof(TT2SpeedHIP)); SaveConfigAsync(); } }
        }

        public double TT2SpeedADS
        {
            get => _config.TT2SpeedADS;
            set { if (_config.TT2SpeedADS != value) { _config.TT2SpeedADS = value; OnPropertyChanged(nameof(TT2SpeedADS)); SaveConfigAsync(); } }
        }

        public double TT2HardwareSensitivity
        {
            get => _config.TT2HardwareSensitivity;
            set { if (_config.TT2HardwareSensitivity != value) { _config.TT2HardwareSensitivity = value; OnPropertyChanged(nameof(TT2HardwareSensitivity)); SaveConfigAsync(); } }
        }

        public double TT2GameDeadzone
        {
            get => _config.TT2GameDeadzone;
            set { if (_config.TT2GameDeadzone != value) { _config.TT2GameDeadzone = value; OnPropertyChanged(nameof(TT2GameDeadzone)); SaveConfigAsync(); } }
        }

        public double TT2ResponseCurve
        {
            get => _config.TT2ResponseCurve;
            set { if (_config.TT2ResponseCurve != value) { _config.TT2ResponseCurve = value; OnPropertyChanged(nameof(TT2ResponseCurve)); SaveConfigAsync(); } }
        }

        public double TT2Kd
        {
            get => _config.TT2Kd;
            set { if (_config.TT2Kd != value) { _config.TT2Kd = value; OnPropertyChanged(nameof(TT2Kd)); SaveConfigAsync(); } }
        }

        public double TT2Kff
        {
            get => _config.TT2Kff;
            set { if (_config.TT2Kff != value) { _config.TT2Kff = value; OnPropertyChanged(nameof(TT2Kff)); SaveConfigAsync(); } }
        }

        public double TT2MinStick
        {
            get => _config.TT2MinStick;
            set { if (_config.TT2MinStick != value) { _config.TT2MinStick = value; OnPropertyChanged(nameof(TT2MinStick)); SaveConfigAsync(); } }
        }

        public double TT2MaxOutput
        {
            get => _config.TT2MaxOutput;
            set { if (_config.TT2MaxOutput != value) { _config.TT2MaxOutput = value; OnPropertyChanged(nameof(TT2MaxOutput)); SaveConfigAsync(); } }
        }

        public double TT2SpeedBoost
        {
            get => _config.TT2SpeedBoost;
            set { if (_config.TT2SpeedBoost != value) { _config.TT2SpeedBoost = value; OnPropertyChanged(nameof(TT2SpeedBoost)); SaveConfigAsync(); } }
        }

        public int TT2CurveModel
        {
            get => _config.TT2CurveModel;
            set { if (_config.TT2CurveModel != value) { _config.TT2CurveModel = value; OnPropertyChanged(nameof(TT2CurveModel)); SaveConfigAsync(); } }
        }

        // ── PixelBot ──────────────────────────────────────────────────────────
        public bool PixelBotEnabled
        {
            get => _config.PixelBotEnabled;
            set { if (_config.PixelBotEnabled != value) { _config.PixelBotEnabled = value; OnPropertyChanged(nameof(PixelBotEnabled)); SaveConfigAsync(); } }
        }

        public int PixelBotMode
        {
            get => _config.PixelBotMode;
            set { if (_config.PixelBotMode != value) { _config.PixelBotMode = value; OnPropertyChanged(nameof(PixelBotMode)); SaveConfigAsync(); } }
        }

        public string PixelBotColorHex
        {
            get => _config.PixelBotColorHex;
            set { if (_config.PixelBotColorHex != value) { _config.PixelBotColorHex = value; OnPropertyChanged(nameof(PixelBotColorHex)); SaveConfigAsync(); } }
        }

        public int PixelBotColorTolerance
        {
            get => _config.PixelBotColorTolerance;
            set { if (_config.PixelBotColorTolerance != value) { _config.PixelBotColorTolerance = value; OnPropertyChanged(nameof(PixelBotColorTolerance)); SaveConfigAsync(); } }
        }

        public int PixelBotMinArea
        {
            get => _config.PixelBotMinArea;
            set { if (_config.PixelBotMinArea != value) { _config.PixelBotMinArea = value; OnPropertyChanged(nameof(PixelBotMinArea)); SaveConfigAsync(); } }
        }

        public int PixelBotMaxArea
        {
            get => _config.PixelBotMaxArea;
            set { if (_config.PixelBotMaxArea != value) { _config.PixelBotMaxArea = value; OnPropertyChanged(nameof(PixelBotMaxArea)); SaveConfigAsync(); } }
        }

        public double PixelBotMinAspectRatio
        {
            get => _config.PixelBotMinAspectRatio;
            set { if (_config.PixelBotMinAspectRatio != value) { _config.PixelBotMinAspectRatio = value; OnPropertyChanged(nameof(PixelBotMinAspectRatio)); SaveConfigAsync(); } }
        }

        public double PixelBotMinSolidity
        {
            get => _config.PixelBotMinSolidity;
            set { if (_config.PixelBotMinSolidity != value) { _config.PixelBotMinSolidity = value; OnPropertyChanged(nameof(PixelBotMinSolidity)); SaveConfigAsync(); } }
        }

        public double PixelBotMinCircularity
        {
            get => _config.PixelBotMinCircularity;
            set { if (_config.PixelBotMinCircularity != value) { _config.PixelBotMinCircularity = value; OnPropertyChanged(nameof(PixelBotMinCircularity)); SaveConfigAsync(); } }
        }

        public int PixelBotVerticalOffset
        {
            get => _config.PixelBotVerticalOffset;
            set { if (_config.PixelBotVerticalOffset != value) { _config.PixelBotVerticalOffset = value; OnPropertyChanged(nameof(PixelBotVerticalOffset)); SaveConfigAsync(); } }
        }

        public int PixelBotMaxFramesLost
        {
            get => _config.PixelBotMaxFramesLost;
            set { if (_config.PixelBotMaxFramesLost != value) { _config.PixelBotMaxFramesLost = value; OnPropertyChanged(nameof(PixelBotMaxFramesLost)); SaveConfigAsync(); } }
        }

        public int PixelBotSatMin
        {
            get => _config.PixelBotSatMin;
            set { if (_config.PixelBotSatMin != value) { _config.PixelBotSatMin = value; OnPropertyChanged(nameof(PixelBotSatMin)); SaveConfigAsync(); } }
        }

        public int PixelBotValMin
        {
            get => _config.PixelBotValMin;
            set { if (_config.PixelBotValMin != value) { _config.PixelBotValMin = value; OnPropertyChanged(nameof(PixelBotValMin)); SaveConfigAsync(); } }
        }

        public int PixelBotPersistRadius
        {
            get => _config.PixelBotPersistRadius;
            set { if (_config.PixelBotPersistRadius != value) { _config.PixelBotPersistRadius = value; OnPropertyChanged(nameof(PixelBotPersistRadius)); SaveConfigAsync(); } }
        }

        // ── Idioma ────────────────────────────────────────────────────
        public string Language
        {
            get => _config.Language;
            set { if (_config.Language != value) { _config.Language = value; OnPropertyChanged(nameof(Language)); SaveConfigAsync(); } }
        }

        // ── Splash style (Phantom) ──────────────────────────────────
        public string SplashStyle
        {
            get => _config.SplashStyle;
            set { if (_config.SplashStyle != value) { _config.SplashStyle = value; OnPropertyChanged(nameof(SplashStyle)); SaveConfigAsync(); } }
        }

        // ── Métricas overlay ─────────────────────────────────────────
        public bool ShowMetrics
        {
            get => _config.ShowMetrics;
            set { if (_config.ShowMetrics != value) { _config.ShowMetrics = value; OnPropertyChanged(nameof(ShowMetrics)); SaveConfigAsync(); } }
        }

        public int PreviewFps
        {
            get => _config.PreviewFps;
            set { if (_config.PreviewFps != value) { _config.PreviewFps = value; OnPropertyChanged(nameof(PreviewFps)); SaveConfigAsync(); } }
        }

        // ── FOV visual ───────────────────────────────────────────────
        public int FovThickness
        {
            get => _config.FovThickness;
            set { if (_config.FovThickness != value) { _config.FovThickness = value; OnPropertyChanged(nameof(FovThickness)); OnPropertyChanged(nameof(FovThicknessIndex)); SaveConfigAsync(); } }
        }

        public int FovThicknessIndex
        {
            get => Math.Clamp(_config.FovThickness - 1, 0, 2);
            set { FovThickness = Math.Clamp(value + 1, 1, 3); }
        }

        public int FovStyle
        {
            get => _config.FovStyle;
            set { if (_config.FovStyle != value) { _config.FovStyle = value; OnPropertyChanged(nameof(FovStyle)); SaveConfigAsync(); } }
        }

        public string FovColorHex
        {
            get => _config.FovColorHex;
            set { if (_config.FovColorHex != value) { _config.FovColorHex = value; OnPropertyChanged(nameof(FovColorHex)); SaveConfigAsync(); } }
        }

        // Status ao vivo do auto-tuner — não persistido, atualizado pelo KmboxMovementService
        private string _autoTuneStatus = "—";
        public string AutoTuneStatus
        {
            get => _autoTuneStatus;
            set { if (_autoTuneStatus != value) { _autoTuneStatus = value; OnPropertyChanged(nameof(AutoTuneStatus)); } }
        }

        /// <summary>Replaces the entire internal config and notifies all properties.</summary>
        public void ReplaceConfig(AppConfig cfg)
        {
            _config = cfg;
            RefreshAllProperties();
            SaveConfigAsync();
        }

        /// <summary>Returns a deep copy of the current config (serialize → deserialize).</summary>
        public AppConfig GetConfigCopy()
        {
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }

        public void SaveConfig()
        {
            lock (_saveLock)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var json = JsonSerializer.Serialize(_config, options);
                    Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);
                    File.WriteAllText(_configFilePath, json);
                }
                catch { }
            }
        }

        private async void SaveConfigAsync()
        {
            await Task.Run(() => SaveConfig());
        }

        public void RefreshAllProperties()
        {
            var properties = typeof(AppSettingsService).GetProperties();
            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.Name != "ModelList" && prop.Name != "CameraList")
                {
                    OnPropertyChanged(prop.Name);
                }
            }
        }

        // ── Label Maker ──

        public bool LabelShowLabels
        {
            get => _config.LabelShowLabels;
            set { if (_config.LabelShowLabels != value) { _config.LabelShowLabels = value; OnPropertyChanged(nameof(LabelShowLabels)); SaveConfigAsync(); } }
        }

        public bool LabelShowCrosshair
        {
            get => _config.LabelShowCrosshair;
            set { if (_config.LabelShowCrosshair != value) { _config.LabelShowCrosshair = value; OnPropertyChanged(nameof(LabelShowCrosshair)); SaveConfigAsync(); } }
        }

        public int LabelConfThreshold
        {
            get => _config.LabelConfThreshold;
            set { if (_config.LabelConfThreshold != value) { _config.LabelConfThreshold = value; OnPropertyChanged(nameof(LabelConfThreshold)); SaveConfigAsync(); } }
        }

        public int LabelDisplayScale
        {
            get => _config.LabelDisplayScale;
            set { if (_config.LabelDisplayScale != value) { _config.LabelDisplayScale = value; OnPropertyChanged(nameof(LabelDisplayScale)); SaveConfigAsync(); } }
        }

        public int LabelImageIndex
        {
            get => _config.LabelImageIndex;
            set { if (_config.LabelImageIndex != value) { _config.LabelImageIndex = value; OnPropertyChanged(nameof(LabelImageIndex)); SaveConfigAsync(); } }
        }

        public string LabelDatasetDir
        {
            get => _config.LabelDatasetDir;
            set { if (_config.LabelDatasetDir != value) { _config.LabelDatasetDir = value; OnPropertyChanged(nameof(LabelDatasetDir)); SaveConfigAsync(); } }
        }

        public string LabelCheckedClasses
        {
            get => _config.LabelCheckedClasses;
            set { if (_config.LabelCheckedClasses != value) { _config.LabelCheckedClasses = value; OnPropertyChanged(nameof(LabelCheckedClasses)); SaveConfigAsync(); } }
        }

        // ── Training ──

        public int TrainEpochs
        {
            get => _config.TrainEpochs;
            set { if (_config.TrainEpochs != value) { _config.TrainEpochs = value; OnPropertyChanged(nameof(TrainEpochs)); SaveConfigAsync(); } }
        }

        public int TrainBatchSize
        {
            get => _config.TrainBatchSize;
            set { if (_config.TrainBatchSize != value) { _config.TrainBatchSize = value; OnPropertyChanged(nameof(TrainBatchSize)); SaveConfigAsync(); } }
        }

        public int TrainImgSize
        {
            get => _config.TrainImgSize;
            set { if (_config.TrainImgSize != value) { _config.TrainImgSize = value; OnPropertyChanged(nameof(TrainImgSize)); SaveConfigAsync(); } }
        }

        public string TrainBaseModel
        {
            get => _config.TrainBaseModel;
            set { if (_config.TrainBaseModel != value) { _config.TrainBaseModel = value; OnPropertyChanged(nameof(TrainBaseModel)); SaveConfigAsync(); } }
        }

        public int TrainValPercent
        {
            get => _config.TrainValPercent;
            set { if (_config.TrainValPercent != value) { _config.TrainValPercent = value; OnPropertyChanged(nameof(TrainValPercent)); SaveConfigAsync(); } }
        }
    }
}