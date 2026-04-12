using Core_Aim.Commands;
using Core_Aim.Services.Camera;
using Core_Aim.Services.Configuration;
using Core_Aim.Services.Hardware;
using Core_Aim.Services.Movements;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Media = System.Windows.Media;

namespace Core_Aim.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        public string PageName { get; } = "Settings";
        public AppSettingsService Settings { get; }
        private readonly TitanTwoService _titanTwoService;
        private readonly KmBoxService _kmBoxService;

        /// <summary>
        /// Quando o sistema está rodando, o movimento é dono do dispositivo HID.
        /// Slot/config commands devem ir pelo movement service — não pelo TitanTwoService.
        /// </summary>
        public IMovementService? ActiveMovementService { get; set; }

        public ICommand RefreshModelsCommand { get; }
        public ICommand RefreshCamerasCommand { get; }
        public ICommand TestMovementCommand { get; }
        public ICommand SlotUpCommand { get; }
        public ICommand SlotDownCommand { get; }
        public ICommand SlotUnloadCommand { get; }
        public ICommand ToggleTitanConnectionCommand { get; }
        public ICommand ToggleKmBoxConnectionCommand { get; }
        public ICommand PickColorCommand { get; }
        public ICommand PickPixelBotColorCommand { get; }
        public ICommand ResetPixelBotCommand     { get; }
        public ICommand PickFovColorCommand      { get; }
        public ICommand TogglePreviewCommand     { get; }

        public Action? OnReloadRequested { get; set; }

        public ObservableCollection<string> InferenceModeLabels { get; } = new ObservableCollection<string>
        {
            "Auto (inferência só no gatilho)",    // 0
            "Constante (rastreio só no gatilho)", // 1 ← inferência sempre ativa
            "Constante (rastreio sempre ativo)",  // 2
            "Desligado"                           // 3
        };

        public ObservableCollection<string> TriggerLabels { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ProtocolLabels { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PollRateLabels { get; } = new ObservableCollection<string>();
        public ObservableCollection<int> SlotNumbers { get; } = new ObservableCollection<int>();

        public ObservableCollection<string> PredictionMethods { get; } = new ObservableCollection<string>
        {
            "Kalman Filter", "WiseTheFox", "Shalloe"
        };

        public ObservableCollection<string> SpeedProfiles { get; } = new ObservableCollection<string>();

        private bool _isPreviewEnabled = true;
        public bool IsPreviewEnabled
        {
            get => _isPreviewEnabled;
            set
            {
                if (_isPreviewEnabled != value)
                {
                    _isPreviewEnabled = value;
                    OnPropertyChanged(nameof(IsPreviewEnabled));
                }
            }
        }

        public ObservableCollection<Media.SolidColorBrush> ColorSlots { get; } = new ObservableCollection<Media.SolidColorBrush>();

        public bool ColorFilterEnabled
        {
            get => Settings.ColorFilterEnabled;
            set
            {
                if (Settings.ColorFilterEnabled != value)
                {
                    Settings.ColorFilterEnabled = value;
                    OnPropertyChanged(nameof(ColorFilterEnabled));
                }
            }
        }

        public int ColorTolerance
        {
            get => Settings.ColorTolerance;
            set
            {
                if (Settings.ColorTolerance != value)
                {
                    Settings.ColorTolerance = value;
                    OnPropertyChanged(nameof(ColorTolerance));
                }
            }
        }

        public string ColorFilterHex
        {
            get => Settings.ColorFilterHex;
            set
            {
                if (Settings.ColorFilterHex != value)
                {
                    Settings.ColorFilterHex = value;
                    OnPropertyChanged(nameof(ColorFilterHex));
                    LoadColorsFromSettingsString();
                }
            }
        }

        public double FovSize
        {
            get => Settings.FovSize;
            set
            {
                if (Math.Abs(Settings.FovSize - value) > 0.1)
                {
                    Settings.FovSize = value;
                    OnPropertyChanged(nameof(FovSize));
                }
            }
        }

        public bool ShowFov
        {
            get => Settings.ShowFov;
            set
            {
                if (Settings.ShowFov != value)
                {
                    Settings.ShowFov = value;
                    OnPropertyChanged(nameof(ShowFov));
                }
            }
        }

        public string? SelectedModel
        {
            get => Settings.SelectedModel;
            set
            {
                if (Settings.SelectedModel != value)
                {
                    Settings.SelectedModel = value;
                    OnPropertyChanged(nameof(SelectedModel));
                    RelayCommand.RaiseCanExecuteChanged();
                    ReloadModel();
                }
            }
        }

        public int SelectedInferenceMode
        {
            get => Settings.InferenceMode;
            set { if (Settings.InferenceMode != value) { Settings.InferenceMode = value; OnPropertyChanged(nameof(SelectedInferenceMode)); } }
        }

        public int SelectedTrigger
        {
            get => Settings.TriggerSelection;
            set { if (Settings.TriggerSelection != value) { Settings.TriggerSelection = value; OnPropertyChanged(nameof(SelectedTrigger)); } }
        }

        private bool _isIconMode;
        public bool IsIconMode
        {
            get => _isIconMode;
            set
            {
                if (_isIconMode != value)
                {
                    _isIconMode = value;
                    Settings.IsIconMode = value;
                    OnPropertyChanged(nameof(IsIconMode));
                    OnPropertyChanged(nameof(IsPlayerMode));

                    if (value)
                    {
                        AimOffsetY = Settings.OffsetValueIcon;
                    }
                    else
                    {
                        AimOffsetY = Settings.OffsetValuePlayer;
                    }
                }
            }
        }

        public bool IsPlayerMode
        {
            get => !_isIconMode;
            set
            {
                bool newIconMode = !value;
                if (_isIconMode != newIconMode)
                {
                    _isIconMode = newIconMode;
                    Settings.IsIconMode = newIconMode;
                    OnPropertyChanged(nameof(IsPlayerMode));
                    OnPropertyChanged(nameof(IsIconMode));

                    if (value)
                    {
                        AimOffsetY = Settings.OffsetValuePlayer;
                    }
                    else
                    {
                        AimOffsetY = Settings.OffsetValueIcon;
                    }
                }
            }
        }

        public double SliderPlayerValue
        {
            get => Settings.OffsetValuePlayer;
            set
            {
                if (Settings.OffsetValuePlayer != value)
                {
                    Settings.OffsetValuePlayer = value;
                    OnPropertyChanged(nameof(SliderPlayerValue));
                    if (IsPlayerMode) AimOffsetY = value;
                }
            }
        }

        public double SliderIconValue
        {
            get => Settings.OffsetValueIcon;
            set
            {
                if (Settings.OffsetValueIcon != value)
                {
                    Settings.OffsetValueIcon = value;
                    OnPropertyChanged(nameof(SliderIconValue));
                    if (IsIconMode) AimOffsetY = value;
                }
            }
        }

        public double AimOffsetY
        {
            get => Settings.AimOffsetY;
            set
            {
                if (Math.Abs(Settings.AimOffsetY - value) > 0.001)
                {
                    Settings.AimOffsetY = value;
                    OnPropertyChanged(nameof(AimOffsetY));
                }
            }
        }

        public string SelectedSpeedProfile
        {
            get => Settings.SpeedProfile;
            set { if (Settings.SpeedProfile != value) { Settings.SpeedProfile = value; OnPropertyChanged(nameof(SelectedSpeedProfile)); } }
        }

        public double TrackingSpeedHIP
        {
            get => Settings.TrackingSpeedHIP;
            set { if (Settings.TrackingSpeedHIP != value) { Settings.TrackingSpeedHIP = value; OnPropertyChanged(nameof(TrackingSpeedHIP)); } }
        }

        public bool IsCameraMode
        {
            get => Settings.CaptureMode == 0;
            set { if (value) { Settings.CaptureMode = 0; OnPropertyChanged(); OnPropertyChanged(nameof(IsScreenMode)); } }
        }

        public bool IsScreenMode
        {
            get => Settings.CaptureMode == 1;
            set { if (value) { Settings.CaptureMode = 1; OnPropertyChanged(); OnPropertyChanged(nameof(IsCameraMode)); } }
        }

        // ── Plugin / Backend de captura ───────────────────────────────────────
        public static List<string> CaptureBackendOptions { get; } = new()
        {
            "Automatic Detection",
            "MS Media Foundation",
            "MS DirectShow",
            "MS DirectShow + MJPEG",
            "MS Media Foundation + MJPEG",
            "Media Foundation Native ★ 240fps",   // idx 5 — IMFSourceReader, NV12/YUY2, MF_LOW_LATENCY
        };

        public int SelectedCaptureBackend
        {
            get => Settings.CaptureBackend;
            set { if (Settings.CaptureBackend != value) { Settings.CaptureBackend = value; OnPropertyChanged(nameof(SelectedCaptureBackend)); } }
        }

        // ── Resolução livre (suporta SD, HD, 4K, 8K, EDID…) ─────────────────
        public int CaptureWidth
        {
            get => Settings.CaptureWidth;
            set { if (Settings.CaptureWidth != value) { Settings.CaptureWidth = value; OnPropertyChanged(nameof(CaptureWidth)); } }
        }

        public int CaptureHeight
        {
            get => Settings.CaptureHeight;
            set { if (Settings.CaptureHeight != value) { Settings.CaptureHeight = value; OnPropertyChanged(nameof(CaptureHeight)); } }
        }

        // ── FPS livre (1-480+) ────────────────────────────────────────────────
        public int CaptureFps
        {
            get => Settings.CaptureFps;
            set { if (Settings.CaptureFps != value) { Settings.CaptureFps = value; OnPropertyChanged(nameof(CaptureFps)); } }
        }

        // ── Configurações avançadas OpenCV (key=value por linha) ──────────────
        public string CaptureAdvancedConfig
        {
            get => Settings.CaptureAdvancedConfig;
            set { if (Settings.CaptureAdvancedConfig != value) { Settings.CaptureAdvancedConfig = value; OnPropertyChanged(nameof(CaptureAdvancedConfig)); } }
        }

        public double TrackingSpeedADS
        {
            get => Settings.TrackingSpeedADS;
            set { if (Settings.TrackingSpeedADS != value) { Settings.TrackingSpeedADS = value; OnPropertyChanged(nameof(TrackingSpeedADS)); } }
        }

        public double PixelBotTrackingSpeed
        {
            get => Settings.PixelBotTrackingSpeed;
            set { if (Settings.PixelBotTrackingSpeed != value) { Settings.PixelBotTrackingSpeed = value; OnPropertyChanged(nameof(PixelBotTrackingSpeed)); } }
        }

        public double PixelBotAimResponse
        {
            get => Settings.PixelBotAimResponse;
            set { if (Settings.PixelBotAimResponse != value) { Settings.PixelBotAimResponse = value; OnPropertyChanged(nameof(PixelBotAimResponse)); } }
        }

        public double PixelBotDeadZone
        {
            get => Settings.PixelBotDeadZone;
            set { if (Settings.PixelBotDeadZone != value) { Settings.PixelBotDeadZone = value; OnPropertyChanged(nameof(PixelBotDeadZone)); } }
        }

        // PB + Titan Two
        public double PixelBotTrackingSpeedTT2
        {
            get => Settings.PixelBotTrackingSpeedTT2;
            set { if (Settings.PixelBotTrackingSpeedTT2 != value) { Settings.PixelBotTrackingSpeedTT2 = value; OnPropertyChanged(nameof(PixelBotTrackingSpeedTT2)); } }
        }

        public double PixelBotAimResponseTT2
        {
            get => Settings.PixelBotAimResponseTT2;
            set { if (Settings.PixelBotAimResponseTT2 != value) { Settings.PixelBotAimResponseTT2 = value; OnPropertyChanged(nameof(PixelBotAimResponseTT2)); } }
        }

        public double PixelBotDeadZoneTT2
        {
            get => Settings.PixelBotDeadZoneTT2;
            set { if (Settings.PixelBotDeadZoneTT2 != value) { Settings.PixelBotDeadZoneTT2 = value; OnPropertyChanged(nameof(PixelBotDeadZoneTT2)); } }
        }

        public double AimResponseCurve
        {
            get => Settings.AimResponseCurve;
            set { if (Settings.AimResponseCurve != value) { Settings.AimResponseCurve = value; OnPropertyChanged(nameof(AimResponseCurve)); } }
        }

        public int RestrictedTracking
        {
            get => Settings.RestrictedTracking;
            set { if (Settings.RestrictedTracking != value) { Settings.RestrictedTracking = value; OnPropertyChanged(nameof(RestrictedTracking)); } }
        }

        public int TrackingDelay
        {
            get => Settings.TrackingDelay;
            set { if (Settings.TrackingDelay != value) { Settings.TrackingDelay = value; OnPropertyChanged(nameof(TrackingDelay)); } }
        }

        public bool PredictionEnabled
        {
            get => Settings.PredictionEnabled;
            set { if (Settings.PredictionEnabled != value) { Settings.PredictionEnabled = value; OnPropertyChanged(nameof(PredictionEnabled)); } }
        }

        public string SelectedPredictionMethod
        {
            get => Settings.PredictionMethod ?? "Kalman Filter";
            set { if (Settings.PredictionMethod != value) { Settings.PredictionMethod = value; OnPropertyChanged(nameof(SelectedPredictionMethod)); } }
        }

        // ── TRACKING CONTROL (sem expor "PID" na UI) ──────────────────────────

        /// <summary>Resposta — equivale ao Kp proporcional (velocidade de rastreio)</summary>
        public double PidKp
        {
            get => Settings.PidKp;
            set { if (Math.Abs(Settings.PidKp - value) > 0.0001) { Settings.PidKp = value; OnPropertyChanged(nameof(PidKp)); } }
        }

        /// <summary>Correção — ADRC b0 (força de rejeição de distúrbios)</summary>
        public double AdrcB0
        {
            get => Settings.AdrcB0;
            set { if (Math.Abs(Settings.AdrcB0 - value) > 0.0001) { Settings.AdrcB0 = value; OnPropertyChanged(nameof(AdrcB0)); } }
        }

        public double AimDeadZonePx
        {
            get => Settings.AimDeadZone;
            set { if (Settings.AimDeadZone != value) { Settings.AimDeadZone = value; OnPropertyChanged(nameof(AimDeadZonePx)); } }
        }

        public bool AutoTuneEnabled
        {
            get => Settings.AutoTuneEnabled;
            set { if (Settings.AutoTuneEnabled != value) { Settings.AutoTuneEnabled = value; OnPropertyChanged(nameof(AutoTuneEnabled)); } }
        }

        public string AutoTuneStatus => Settings.AutoTuneStatus;

        public string FullscreenKey
        {
            get => Settings.FullscreenKey;
            set { if (Settings.FullscreenKey != value) { Settings.FullscreenKey = value; OnPropertyChanged(nameof(FullscreenKey)); } }
        }

        // ── PixelBot settings ─────────────────────────────────────────────────
        public bool PixelBotEnabled
        {
            get => Settings.PixelBotEnabled;
            set { if (Settings.PixelBotEnabled != value) { Settings.PixelBotEnabled = value; OnPropertyChanged(nameof(PixelBotEnabled)); } }
        }

        public int PixelBotMode
        {
            get => Settings.PixelBotMode;
            set
            {
                if (Settings.PixelBotMode != value)
                {
                    Settings.PixelBotMode    = value;
                    Settings.PixelBotEnabled = value > 0;
                    OnPropertyChanged(nameof(PixelBotMode));
                    OnPropertyChanged(nameof(PixelBotIsActive));
                    OnPropertyChanged(nameof(ShowYoloAimControls));
                    OnPropertyChanged(nameof(ShowPixelBotAimControls));
                    OnPropertyChanged(nameof(ShowOffsetRadio));
                }
            }
        }

        // Visibilidade dos sliders de mira na seção TRACKING CONTROL
        // Modo 0 → só YOLO | Modo 2 → só PB | Modos 1 e 3 → ambos
        public bool ShowYoloAimControls      => Settings.PixelBotMode != 2;  // 0, 1, 3
        public bool ShowPixelBotAimControls  => Settings.PixelBotMode > 0;   // 1, 2, 3
        // Radio YOLO/PB só aparece no modo 0 (só YOLO) — nos outros o offset é automático
        public bool ShowOffsetRadio          => Settings.PixelBotMode == 0;

        // Visibilidade dos controles PB — true quando modo != "Apenas YOLO"
        public bool PixelBotIsActive => Settings.PixelBotMode > 0;

        public string PixelBotColorHex
        {
            get => Settings.PixelBotColorHex;
            set { if (Settings.PixelBotColorHex != value) { Settings.PixelBotColorHex = value; OnPropertyChanged(nameof(PixelBotColorHex)); OnPropertyChanged(nameof(PixelBotColorBrush)); } }
        }

        public Media.SolidColorBrush PixelBotColorBrush
        {
            get
            {
                try { return new Media.SolidColorBrush((Media.Color)Media.ColorConverter.ConvertFromString(Settings.PixelBotColorHex)); }
                catch { return new Media.SolidColorBrush(Media.Colors.Red); }
            }
        }

        public int PixelBotColorTolerance
        {
            get => Settings.PixelBotColorTolerance;
            set { if (Settings.PixelBotColorTolerance != value) { Settings.PixelBotColorTolerance = value; OnPropertyChanged(nameof(PixelBotColorTolerance)); } }
        }

        public int PixelBotMinArea
        {
            get => Settings.PixelBotMinArea;
            set { if (Settings.PixelBotMinArea != value) { Settings.PixelBotMinArea = value; OnPropertyChanged(nameof(PixelBotMinArea)); } }
        }

        public int PixelBotMaxArea
        {
            get => Settings.PixelBotMaxArea;
            set { if (Settings.PixelBotMaxArea != value) { Settings.PixelBotMaxArea = value; OnPropertyChanged(nameof(PixelBotMaxArea)); } }
        }

        public double PixelBotMinAspectRatio
        {
            get => Settings.PixelBotMinAspectRatio;
            set { if (Settings.PixelBotMinAspectRatio != value) { Settings.PixelBotMinAspectRatio = value; OnPropertyChanged(nameof(PixelBotMinAspectRatio)); } }
        }

        public double PixelBotMinSolidity
        {
            get => Settings.PixelBotMinSolidity;
            set { if (Settings.PixelBotMinSolidity != value) { Settings.PixelBotMinSolidity = value; OnPropertyChanged(nameof(PixelBotMinSolidity)); } }
        }

        public double PixelBotMinCircularity
        {
            get => Settings.PixelBotMinCircularity;
            set { if (Settings.PixelBotMinCircularity != value) { Settings.PixelBotMinCircularity = value; OnPropertyChanged(nameof(PixelBotMinCircularity)); } }
        }

        public int PixelBotVerticalOffset
        {
            get => Settings.PixelBotVerticalOffset;
            set { if (Settings.PixelBotVerticalOffset != value) { Settings.PixelBotVerticalOffset = value; OnPropertyChanged(nameof(PixelBotVerticalOffset)); } }
        }

        public int PixelBotMaxFramesLost
        {
            get => Settings.PixelBotMaxFramesLost;
            set { if (Settings.PixelBotMaxFramesLost != value) { Settings.PixelBotMaxFramesLost = value; OnPropertyChanged(nameof(PixelBotMaxFramesLost)); } }
        }

        public int PixelBotSatMin
        {
            get => Settings.PixelBotSatMin;
            set { if (Settings.PixelBotSatMin != value) { Settings.PixelBotSatMin = value; OnPropertyChanged(nameof(PixelBotSatMin)); } }
        }

        public int PixelBotValMin
        {
            get => Settings.PixelBotValMin;
            set { if (Settings.PixelBotValMin != value) { Settings.PixelBotValMin = value; OnPropertyChanged(nameof(PixelBotValMin)); } }
        }

        public int PixelBotPersistRadius
        {
            get => Settings.PixelBotPersistRadius;
            set { if (Settings.PixelBotPersistRadius != value) { Settings.PixelBotPersistRadius = value; OnPropertyChanged(nameof(PixelBotPersistRadius)); } }
        }

        public static string[] PixelBotModeLabels { get; } = new[]
        {
            "0 — Apenas YOLO",
            "1 — Prioridade (YOLO → PB fallback)",
            "2 — Apenas PixelBot"
        };

        // ── Titan Two tracking settings ───────────────────────────────────────
        public double TT2SpeedHIP
        {
            get => Settings.TT2SpeedHIP;
            set { if (Settings.TT2SpeedHIP != value) { Settings.TT2SpeedHIP = value; OnPropertyChanged(nameof(TT2SpeedHIP)); } }
        }

        public double TT2SpeedADS
        {
            get => Settings.TT2SpeedADS;
            set { if (Settings.TT2SpeedADS != value) { Settings.TT2SpeedADS = value; OnPropertyChanged(nameof(TT2SpeedADS)); } }
        }

        public double TT2HardwareSensitivity
        {
            get => Settings.TT2HardwareSensitivity;
            set { if (Settings.TT2HardwareSensitivity != value) { Settings.TT2HardwareSensitivity = value; OnPropertyChanged(nameof(TT2HardwareSensitivity)); } }
        }

        public double TT2GameDeadzone
        {
            get => Settings.TT2GameDeadzone;
            set { if (Settings.TT2GameDeadzone != value) { Settings.TT2GameDeadzone = value; OnPropertyChanged(nameof(TT2GameDeadzone)); } }
        }

        public double TT2ResponseCurve
        {
            get => Settings.TT2ResponseCurve;
            set { if (Settings.TT2ResponseCurve != value) { Settings.TT2ResponseCurve = value; OnPropertyChanged(nameof(TT2ResponseCurve)); } }
        }

        public double TT2Kd
        {
            get => Settings.TT2Kd;
            set { if (Settings.TT2Kd != value) { Settings.TT2Kd = value; OnPropertyChanged(nameof(TT2Kd)); } }
        }

        public double TT2Kff
        {
            get => Settings.TT2Kff;
            set { if (Settings.TT2Kff != value) { Settings.TT2Kff = value; OnPropertyChanged(nameof(TT2Kff)); } }
        }

        public double TT2MinStick
        {
            get => Settings.TT2MinStick;
            set { if (Settings.TT2MinStick != value) { Settings.TT2MinStick = value; OnPropertyChanged(nameof(TT2MinStick)); } }
        }

        public double TT2MaxOutput
        {
            get => Settings.TT2MaxOutput;
            set { if (Settings.TT2MaxOutput != value) { Settings.TT2MaxOutput = value; OnPropertyChanged(nameof(TT2MaxOutput)); } }
        }

        public double TT2SpeedBoost
        {
            get => Settings.TT2SpeedBoost;
            set { if (Settings.TT2SpeedBoost != value) { Settings.TT2SpeedBoost = value; OnPropertyChanged(nameof(TT2SpeedBoost)); } }
        }

        public int TT2CurveModel
        {
            get => Settings.TT2CurveModel;
            set { if (Settings.TT2CurveModel != value) { Settings.TT2CurveModel = value; OnPropertyChanged(nameof(TT2CurveModel)); } }
        }

        public static string[] TT2CurveModelLabels { get; } = new[]
        {
            "0 — Linear (padrão)",
            "1 — Quadrático (lento perto)",
            "2 — Raiz Quadrada (rápido perto)",
            "3 — Suave / S-Curve",
            "4 — Exponencial (agressivo)",
            "5 — Bang + Linear",
            "6 — Cúbico (extremo lento perto)"
        };

        public string[] FullscreenKeyOptions { get; } = new[]
        {
            "F","G","H","J","K","L","M",
            "Z","X","C","V","B","N",
            "F11","F12","Insert","Home","End","PageUp","PageDown","Pause"
        };

        // ── Recoil KMBox ─────────────────────────────────────────────────
        public bool RecoilEnabledKm
        {
            get => Settings.RecoilEnabledKm;
            set { if (Settings.RecoilEnabledKm != value) { Settings.RecoilEnabledKm = value; OnPropertyChanged(nameof(RecoilEnabledKm)); } }
        }
        public int RecoilVerticalKm
        {
            get => Settings.RecoilVerticalKm;
            set { if (Settings.RecoilVerticalKm != value) { Settings.RecoilVerticalKm = value; OnPropertyChanged(nameof(RecoilVerticalKm)); } }
        }
        public int RecoilHorizontalKm
        {
            get => Settings.RecoilHorizontalKm;
            set { if (Settings.RecoilHorizontalKm != value) { Settings.RecoilHorizontalKm = value; OnPropertyChanged(nameof(RecoilHorizontalKm)); } }
        }
        public int RecoilIntervalKm
        {
            get => Settings.RecoilIntervalKm;
            set { if (Settings.RecoilIntervalKm != value) { Settings.RecoilIntervalKm = value; OnPropertyChanged(nameof(RecoilIntervalKm)); } }
        }

        // ── Recoil Titan Two ─────────────────────────────────────────────
        public bool RecoilEnabledTT2
        {
            get => Settings.RecoilEnabledTT2;
            set { if (Settings.RecoilEnabledTT2 != value) { Settings.RecoilEnabledTT2 = value; OnPropertyChanged(nameof(RecoilEnabledTT2)); } }
        }
        public int RecoilVerticalTT2
        {
            get => Settings.RecoilVerticalTT2;
            set { if (Settings.RecoilVerticalTT2 != value) { Settings.RecoilVerticalTT2 = value; OnPropertyChanged(nameof(RecoilVerticalTT2)); } }
        }
        public int RecoilHorizontalTT2
        {
            get => Settings.RecoilHorizontalTT2;
            set { if (Settings.RecoilHorizontalTT2 != value) { Settings.RecoilHorizontalTT2 = value; OnPropertyChanged(nameof(RecoilHorizontalTT2)); } }
        }
        public int RecoilIntervalTT2
        {
            get => Settings.RecoilIntervalTT2;
            set { if (Settings.RecoilIntervalTT2 != value) { Settings.RecoilIntervalTT2 = value; OnPropertyChanged(nameof(RecoilIntervalTT2)); } }
        }

        // ── Legacy (não exposto na UI, mantido para compat.) ──────────────
        public bool RecoilEnabled
        {
            get => Settings.RecoilEnabled;
            set { if (Settings.RecoilEnabled != value) { Settings.RecoilEnabled = value; OnPropertyChanged(nameof(RecoilEnabled)); } }
        }
        public int RecoilHorizontal
        {
            get => Settings.RecoilHorizontalForce;
            set { if (Settings.RecoilHorizontalForce != value) { Settings.RecoilHorizontalForce = value; OnPropertyChanged(nameof(RecoilHorizontal)); } }
        }
        public int RecoilVertical
        {
            get => Settings.RecoilVerticalForce;
            set { if (Settings.RecoilVerticalForce != value) { Settings.RecoilVerticalForce = value; OnPropertyChanged(nameof(RecoilVertical)); } }
        }
        public int RecoilInterval
        {
            get => Settings.RecoilInterval;
            set { if (Settings.RecoilInterval != value) { Settings.RecoilInterval = value; OnPropertyChanged(nameof(RecoilInterval)); } }
        }

        // ── Splash Style ──────────────────────────────────────────────
        // Estilo de splash exibido no startup (escolha do utilizador na aba User).
        // Persistido em AppSettings.SplashStyle. Códigos em Views.Splash.SplashStyles.
        private static readonly string[] _splashCodes =
        {
            Core_Aim.Views.Splash.SplashStyles.NeuralBoot,
            Core_Aim.Views.Splash.SplashStyles.ReactorIgnition,
            Core_Aim.Views.Splash.SplashStyles.HolographicAssembly
        };
        public string[] SplashStyleOptions { get; } =
        {
            "Neural Boot",
            "Reactor Ignition",
            "Holographic Assembly"
        };

        public int SelectedSplashStyleIndex
        {
            get
            {
                int idx = Array.IndexOf(_splashCodes, Settings.SplashStyle);
                return idx >= 0 ? idx : 0;
            }
            set
            {
                string style = (value >= 0 && value < _splashCodes.Length) ? _splashCodes[value] : _splashCodes[0];
                if (Settings.SplashStyle != style)
                {
                    Settings.SplashStyle = style;
                    OnPropertyChanged(nameof(SelectedSplashStyleIndex));
                }
            }
        }

        // ── Idioma / Localização ──────────────────────────────────────
        private static readonly string[] _langCodes = { "pt", "en", "fr", "es", "it", "zh" };
        public static string[] LanguageOptions { get; } = { "Português", "English", "Français", "Español", "Italiano", "中文" };

        public int SelectedLanguageIndex
        {
            get
            {
                int idx = Array.IndexOf(_langCodes, Settings.Language);
                return idx >= 0 ? idx : 0;
            }
            set
            {
                string lang = (value >= 0 && value < _langCodes.Length) ? _langCodes[value] : "pt";
                if (Settings.Language != lang)
                {
                    Settings.Language = lang;
                    Core_Aim.Services.LocalizationManager.Instance.SetLanguage(lang);
                    OnPropertyChanged(nameof(SelectedLanguageIndex));
                    UpdateTriggerOptions();
                }
            }
        }

        // ── Overlay — métricas ────────────────────────────────────────
        public bool ShowMetrics
        {
            get => Settings.ShowMetrics;
            set { if (Settings.ShowMetrics != value) { Settings.ShowMetrics = value; OnPropertyChanged(nameof(ShowMetrics)); } }
        }

        // ── Preview FPS ───────────────────────────────────────────────
        // Índice do ComboBox: 0=30  1=60  2=120  3=240
        private static readonly int[] _previewFpsOptions = { 30, 60, 120, 240 };
        public int PreviewFpsIndex
        {
            get
            {
                int idx = Array.IndexOf(_previewFpsOptions, Settings.PreviewFps);
                return idx >= 0 ? idx : 1; // default: 60
            }
            set
            {
                int fps = (value >= 0 && value < _previewFpsOptions.Length)
                    ? _previewFpsOptions[value] : 60;
                if (Settings.PreviewFps != fps)
                {
                    Settings.PreviewFps = fps;
                    OnPropertyChanged(nameof(PreviewFpsIndex));
                }
            }
        }

        // ── Overlay — FOV ─────────────────────────────────────────────
        public int FovThicknessIndex
        {
            get => Settings.FovThicknessIndex;
            set { if (Settings.FovThicknessIndex != value) { Settings.FovThicknessIndex = value; OnPropertyChanged(nameof(FovThicknessIndex)); } }
        }

        public int FovStyleIndex
        {
            get => Settings.FovStyle;
            set { if (Settings.FovStyle != value) { Settings.FovStyle = value; OnPropertyChanged(nameof(FovStyleIndex)); } }
        }

        public string FovColorHex
        {
            get => Settings.FovColorHex;
            set
            {
                if (Settings.FovColorHex != value)
                {
                    Settings.FovColorHex = value;
                    OnPropertyChanged(nameof(FovColorHex));
                    OnPropertyChanged(nameof(FovColorBrush));
                }
            }
        }

        public Media.SolidColorBrush FovColorBrush
        {
            get
            {
                try { return new Media.SolidColorBrush((Media.Color)Media.ColorConverter.ConvertFromString(Settings.FovColorHex)); }
                catch { return new Media.SolidColorBrush(Media.Colors.DodgerBlue); }
            }
        }

        public string SelectedProtocol
        {
            get => Settings.TitanTwoProtocol;
            set
            {
                if (Settings.TitanTwoProtocol != value)
                {
                    Settings.TitanTwoProtocol = value;
                    OnPropertyChanged(nameof(SelectedProtocol));
                    if (IsTitanConnected && Enum.TryParse<TT2_Proto>(value, out var proto))
                        _titanTwoService.SetOutputProtocol(proto);
                }
            }
        }

        public string SelectedInputPollRate
        {
            get => Settings.TitanTwoInputPollRate;
            set
            {
                if (Settings.TitanTwoInputPollRate != value)
                {
                    Settings.TitanTwoInputPollRate = value;
                    OnPropertyChanged(nameof(SelectedInputPollRate));
                    if (IsTitanConnected && Enum.TryParse<TT2_Poll>(value, out var poll))
                        _titanTwoService.SetInputPolling(poll);
                }
            }
        }

        public string SelectedOutputPollRate
        {
            get => Settings.TitanTwoOutputPollRate;
            set
            {
                if (Settings.TitanTwoOutputPollRate != value)
                {
                    Settings.TitanTwoOutputPollRate = value;
                    OnPropertyChanged(nameof(SelectedOutputPollRate));
                    if (IsTitanConnected && Enum.TryParse<TT2_Poll>(value, out var poll))
                        _titanTwoService.SetOutputPolling(poll);
                }
            }
        }

        public int SelectedSlot
        {
            get => Settings.TitanTwoSlot;
            set
            {
                if (Settings.TitanTwoSlot != value)
                {
                    Settings.TitanTwoSlot = value;
                    OnPropertyChanged(nameof(SelectedSlot));
                    if (IsTitanConnected)
                    {
                        if (ActiveMovementService != null)
                            ActiveMovementService.SlotLoad((byte)value);
                        else
                            _titanTwoService.SelectSlot(value);
                    }
                }
            }
        }

        private bool _invertX = false;
        public bool InvertX { get => _invertX; set { _invertX = value; OnPropertyChanged(nameof(InvertX)); if (IsTitanConnected) { if (ActiveMovementService != null) ActiveMovementService.SetInvertX(value); else _titanTwoService.SetInvertX(value); } } }
        private bool _invertY = false;
        public bool InvertY { get => _invertY; set { _invertY = value; OnPropertyChanged(nameof(InvertY)); if (IsTitanConnected) { if (ActiveMovementService != null) ActiveMovementService.SetInvertY(value); else _titanTwoService.SetInvertY(value); } } }

        // ── Macros GPC (controlados via GCV flags) ──────────────────
        public bool MacroRapidFire   { get => Settings.MacroRapidFire;   set { if (Settings.MacroRapidFire   != value) { Settings.MacroRapidFire   = value; OnPropertyChanged(nameof(MacroRapidFire));   SendMacroFlags(); } } }
        public bool MacroAntiRecoil  { get => Settings.MacroAntiRecoil;  set { if (Settings.MacroAntiRecoil  != value) { Settings.MacroAntiRecoil  = value; OnPropertyChanged(nameof(MacroAntiRecoil));  SendMacroFlags(); } } }
        public bool MacroDropShot    { get => Settings.MacroDropShot;    set { if (Settings.MacroDropShot    != value) { Settings.MacroDropShot    = value; OnPropertyChanged(nameof(MacroDropShot));    SendMacroFlags(); } } }
        public bool MacroCrouchPeek  { get => Settings.MacroCrouchPeek;  set { if (Settings.MacroCrouchPeek  != value) { Settings.MacroCrouchPeek  = value; OnPropertyChanged(nameof(MacroCrouchPeek));  SendMacroFlags(); } } }
        public bool MacroTacSprint   { get => Settings.MacroTacSprint;   set { if (Settings.MacroTacSprint   != value) { Settings.MacroTacSprint   = value; OnPropertyChanged(nameof(MacroTacSprint));   SendMacroFlags(); } } }
        public bool MacroSlideCancel { get => Settings.MacroSlideCancel; set { if (Settings.MacroSlideCancel != value) { Settings.MacroSlideCancel = value; OnPropertyChanged(nameof(MacroSlideCancel)); SendMacroFlags(); } } }
        public bool MacroAutoPing    { get => Settings.MacroAutoPing;    set { if (Settings.MacroAutoPing    != value) { Settings.MacroAutoPing    = value; OnPropertyChanged(nameof(MacroAutoPing));    SendMacroFlags(); } } }

        // ── Ajustes de macro GPC (sliders enviados via GCV) ────────────
        public double MacroArVertical
        {
            get => Settings.MacroArVertical;
            set { if (Settings.MacroArVertical != value) { Settings.MacroArVertical = value; OnPropertyChanged(nameof(MacroArVertical)); OnMacroAdjustChanged?.Invoke(); } }
        }
        public double MacroArHorizontal
        {
            get => Settings.MacroArHorizontal;
            set { if (Settings.MacroArHorizontal != value) { Settings.MacroArHorizontal = value; OnPropertyChanged(nameof(MacroArHorizontal)); OnMacroAdjustChanged?.Invoke(); } }
        }
        public double MacroRapidFireMs
        {
            get => Settings.MacroRapidFireMs;
            set { if (Settings.MacroRapidFireMs != value) { Settings.MacroRapidFireMs = value; OnPropertyChanged(nameof(MacroRapidFireMs)); OnMacroAdjustChanged?.Invoke(); } }
        }

        public double PlayerBlend
        {
            get => Settings.PlayerBlend;
            set { if (Settings.PlayerBlend != value) { Settings.PlayerBlend = value; OnPropertyChanged(nameof(PlayerBlend)); OnMacroAdjustChanged?.Invoke(); } }
        }
        public double DisableOnMove
        {
            get => Settings.DisableOnMove;
            set { if (Settings.DisableOnMove != value) { Settings.DisableOnMove = value; OnPropertyChanged(nameof(DisableOnMove)); OnMacroAdjustChanged?.Invoke(); } }
        }

        public double QuickscopeDelay
        {
            get => Settings.QuickscopeDelay;
            set { if (Settings.QuickscopeDelay != value) { Settings.QuickscopeDelay = value; OnPropertyChanged(nameof(QuickscopeDelay)); OnMacroAdjustChanged?.Invoke(); } }
        }
        public double JumpShotDelay
        {
            get => Settings.JumpShotDelay;
            set { if (Settings.JumpShotDelay != value) { Settings.JumpShotDelay = value; OnPropertyChanged(nameof(JumpShotDelay)); OnMacroAdjustChanged?.Invoke(); } }
        }
        public double BunnyHopDelay
        {
            get => Settings.BunnyHopDelay;
            set { if (Settings.BunnyHopDelay != value) { Settings.BunnyHopDelay = value; OnPropertyChanged(nameof(BunnyHopDelay)); OnMacroAdjustChanged?.Invoke(); } }
        }
        public double StrafePower
        {
            get => Settings.StrafePower;
            set { if (Settings.StrafePower != value) { Settings.StrafePower = value; OnPropertyChanged(nameof(StrafePower)); OnMacroAdjustChanged?.Invoke(); } }
        }

        // ── Novas macros v4 ──
        public bool MacroJumpShot { get => Settings.MacroJumpShot; set { if (Settings.MacroJumpShot != value) { Settings.MacroJumpShot = value; OnPropertyChanged(nameof(MacroJumpShot)); SendMacroFlags(); } } }
        public bool MacroBunnyHop { get => Settings.MacroBunnyHop; set { if (Settings.MacroBunnyHop != value) { Settings.MacroBunnyHop = value; OnPropertyChanged(nameof(MacroBunnyHop)); SendMacroFlags(); } } }
        public bool MacroQuickscope { get => Settings.MacroQuickscope; set { if (Settings.MacroQuickscope != value) { Settings.MacroQuickscope = value; OnPropertyChanged(nameof(MacroQuickscope)); SendMacroFlags(); } } }
        public bool MacroAutoBreath { get => Settings.MacroAutoBreath; set { if (Settings.MacroAutoBreath != value) { Settings.MacroAutoBreath = value; OnPropertyChanged(nameof(MacroAutoBreath)); SendMacroFlags(); } } }
        public bool MacroAutoReload { get => Settings.MacroAutoReload; set { if (Settings.MacroAutoReload != value) { Settings.MacroAutoReload = value; OnPropertyChanged(nameof(MacroAutoReload)); SendMacroFlags(); } } }
        public bool MacroAutoMelee { get => Settings.MacroAutoMelee; set { if (Settings.MacroAutoMelee != value) { Settings.MacroAutoMelee = value; OnPropertyChanged(nameof(MacroAutoMelee)); SendMacroFlags(); } } }
        public bool MacroAutoADS { get => Settings.MacroAutoADS; set { if (Settings.MacroAutoADS != value) { Settings.MacroAutoADS = value; OnPropertyChanged(nameof(MacroAutoADS)); SendMacroFlags(); } } }
        public bool MacroHairTrigger { get => Settings.MacroHairTrigger; set { if (Settings.MacroHairTrigger != value) { Settings.MacroHairTrigger = value; OnPropertyChanged(nameof(MacroHairTrigger)); SendMacroFlags(); } } }
        public bool MacroStrafeShot { get => Settings.MacroStrafeShot; set { if (Settings.MacroStrafeShot != value) { Settings.MacroStrafeShot = value; OnPropertyChanged(nameof(MacroStrafeShot)); SendMacroFlags(); } } }
        public bool MacroCookProtection { get => Settings.MacroCookProtection; set { if (Settings.MacroCookProtection != value) { Settings.MacroCookProtection = value; OnPropertyChanged(nameof(MacroCookProtection)); SendMacroFlags(); } } }
        public bool MacroDeadZoneRemoval { get => Settings.MacroDeadZoneRemoval; set { if (Settings.MacroDeadZoneRemoval != value) { Settings.MacroDeadZoneRemoval = value; OnPropertyChanged(nameof(MacroDeadZoneRemoval)); SendMacroFlags(); } } }

        public bool AutoUploadGpc
        {
            get => Settings.AutoUploadGpc;
            set { if (Settings.AutoUploadGpc != value) { Settings.AutoUploadGpc = value; OnPropertyChanged(nameof(AutoUploadGpc)); } }
        }

        public event Action? OnMacroAdjustChanged;

        public void ResendMacroFlags() => SendMacroFlags();

        /// <summary>Reenvia todos os ajustes de macro (flags + valores) para o movement service.</summary>
        public void ResendMacroAdjustments()
        {
            OnMacroAdjustChanged?.Invoke();
        }

        private void SendMacroFlags()
        {
            uint flags = 0;
            if (Settings.MacroRapidFire)      flags |= 1;
            if (Settings.MacroAntiRecoil)     flags |= 2;
            if (Settings.MacroDropShot)       flags |= 4;
            if (Settings.MacroCrouchPeek)     flags |= 8;
            if (Settings.MacroTacSprint)      flags |= 16;
            if (Settings.MacroSlideCancel)    flags |= 32;
            if (Settings.MacroAutoPing)       flags |= 64;
            // v4 bits
            if (Settings.MacroJumpShot)       flags |= 128;
            if (Settings.MacroBunnyHop)       flags |= 256;
            if (Settings.MacroQuickscope)     flags |= 512;
            if (Settings.MacroAutoBreath)     flags |= 1024;
            if (Settings.MacroAutoReload)     flags |= 2048;
            if (Settings.MacroAutoMelee)      flags |= 4096;
            if (Settings.MacroAutoADS)        flags |= 8192;
            if (Settings.MacroHairTrigger)    flags |= 16384;
            if (Settings.MacroStrafeShot)     flags |= 32768;
            if (Settings.MacroCookProtection) flags |= 65536;
            if (Settings.MacroDeadZoneRemoval)flags |= 131072;
            OnMacroFlagsChanged?.Invoke(flags);
        }
        private double _clampMagnitude = 1.0;
        public double ClampMagnitude { get => _clampMagnitude; set { _clampMagnitude = value; OnPropertyChanged(nameof(ClampMagnitude)); if (IsTitanConnected) { if (ActiveMovementService != null) ActiveMovementService.SetClampMagnitude(value); else _titanTwoService.SetClampMagnitude(value); } } }
        private bool _highSpeedMode = false;
        public bool HighSpeedMode { get => _highSpeedMode; set { _highSpeedMode = value; OnPropertyChanged(nameof(HighSpeedMode)); if (IsTitanConnected) { if (ActiveMovementService != null) ActiveMovementService.SetHighSpeedMode(value); else _titanTwoService.SetHighSpeedMode(value); } } }

        private bool _isTitanConnected;
        public bool IsTitanConnected
        {
            get => _isTitanConnected;
            set
            {
                if (_isTitanConnected != value)
                {
                    _isTitanConnected = value;
                    OnPropertyChanged();
                    RelayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // Disparado sempre que o tipo de hardware muda — MainViewModel usa para swap ao vivo
        public event Action<int>? OnHardwareTypeChanged;
        public event Action<uint>? OnMacroFlagsChanged;

        public bool IsTitanSelected
        {
            get => Settings.HardwareType == 0;
            set
            {
                if (value && Settings.HardwareType != 0)
                {
                    Settings.HardwareType = 0;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsKmBoxSelected));
                    UpdateTriggerOptions();
                    OnHardwareTypeChanged?.Invoke(0);
                }
            }
        }

        public bool IsKmBoxSelected
        {
            get => Settings.HardwareType == 1;
            set
            {
                if (value && Settings.HardwareType != 1)
                {
                    Settings.HardwareType = 1;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTitanSelected));
                    UpdateTriggerOptions();
                    OnHardwareTypeChanged?.Invoke(1);
                }
            }
        }

        private bool _isKmBoxConnected;
        public bool IsKmBoxConnected
        {
            get => _isKmBoxConnected;
            set
            {
                if (_isKmBoxConnected != value)
                {
                    _isKmBoxConnected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(KmBoxStatusText));
                    OnPropertyChanged(nameof(KmBoxStatusColor));
                }
            }
        }

        public string KmBoxStatusText => IsKmBoxConnected ? "CONNECTED" : "DISCONNECTED";

        public Media.SolidColorBrush KmBoxStatusColor => IsKmBoxConnected
            ? new Media.SolidColorBrush(Media.Color.FromRgb(16, 185, 129))
            : new Media.SolidColorBrush(Media.Color.FromRgb(239, 68, 68));

        public string KmBoxIp { get => Settings.KmBoxIp; set => Settings.KmBoxIp = value; }
        public int KmBoxPort { get => Settings.KmBoxPort; set => Settings.KmBoxPort = value; }
        public string KmBoxUuid { get => Settings.KmBoxUuid; set => Settings.KmBoxUuid = value; }

        public SettingsViewModel(AppSettingsService appSettings, TitanTwoService titanTwoService, KmBoxService kmBoxService)
        {
            Settings = appSettings;
            _titanTwoService = titanTwoService;
            _kmBoxService = kmBoxService;

            _isIconMode = Settings.IsIconMode;

            RefreshModelsCommand = new RelayCommand(_ => RefreshModelList());
            RefreshCamerasCommand = new RelayCommand(_ => RefreshCameraList());
            TestMovementCommand = new RelayCommand(_ => TestMovement(), _ => IsTitanConnected);
            SlotUpCommand = new RelayCommand(_ => SlotUp(), _ => IsTitanConnected);
            SlotDownCommand = new RelayCommand(_ => SlotDown(), _ => IsTitanConnected);
            SlotUnloadCommand = new RelayCommand(_ => SlotUnload(), _ => IsTitanConnected);

            ToggleTitanConnectionCommand = new RelayCommand(
                _ => {
                    // Sistema rodando → movement service é dono do HID, não permitir Connect/Disconnect manual
                    if (ActiveMovementService != null) return;
                    if (IsTitanConnected || _titanTwoService.Status == TT2Status.Connecting) _titanTwoService.Disconnect(); else _titanTwoService.Connect();
                },
                _ => ActiveMovementService == null && _titanTwoService.Status != TT2Status.Finalizing
            );

            ToggleKmBoxConnectionCommand = new RelayCommand(async _ =>
            {
                if (IsKmBoxConnected) _kmBoxService.Disconnect(); else await _kmBoxService.ConnectAsync();
            });

            PickColorCommand = new RelayCommand(param =>
            {
                if (param is string indexStr && int.TryParse(indexStr, out int index)) OpenColorPicker(index);
            });

            PickPixelBotColorCommand = new RelayCommand(_ => OpenPixelBotColorPicker());
            ResetPixelBotCommand     = new RelayCommand(_ => ResetPixelBotDefaults());
            PickFovColorCommand      = new RelayCommand(_ => OpenFovColorPicker());
            TogglePreviewCommand     = new RelayCommand(_ => IsPreviewEnabled = !IsPreviewEnabled);

            // Inicializa o idioma salvo
            Core_Aim.Services.LocalizationManager.Instance.SetLanguage(Settings.Language);

            _titanTwoService.OnStatusChanged += OnTitanTwoStatusChanged;
            _kmBoxService.OnStatusChanged += status => { IsKmBoxConnected = (status == KmBoxStatus.Connected); };

            // Propaga mudanças feitas pelo auto-tuner (escreve direto em Settings) para a UI
            Settings.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(AppSettingsService.TrackingSpeedHIP):  OnPropertyChanged(nameof(TrackingSpeedHIP));  break;
                    case nameof(AppSettingsService.PidKp):             OnPropertyChanged(nameof(PidKp));             break;
                    case nameof(AppSettingsService.AdrcB0):            OnPropertyChanged(nameof(AdrcB0));            break;
                    case nameof(AppSettingsService.AutoTuneStatus):    OnPropertyChanged(nameof(AutoTuneStatus));    break;
                    // Profile swap: quando ReplaceConfig muda o HardwareType, propaga para a UI e reconecta
                    case nameof(AppSettingsService.HardwareType):
                        OnPropertyChanged(nameof(IsTitanSelected));
                        OnPropertyChanged(nameof(IsKmBoxSelected));
                        UpdateTriggerOptions();
                        OnHardwareTypeChanged?.Invoke(Settings.HardwareType);
                        break;
                }
            };

            PopulateEnumLists();
            LoadSpeedProfiles();
            UpdateTriggerOptions();

            RefreshModelList();
            RefreshCameraList();
            InitializeColorSlots();

            IsTitanConnected = (_titanTwoService.Status == TT2Status.Connected);
            IsKmBoxConnected = (_kmBoxService.Status == KmBoxStatus.Connected);

            OnPropertyChanged(nameof(IsTitanSelected));
            OnPropertyChanged(nameof(IsKmBoxSelected));
        }

        private void LoadSpeedProfiles()
        {
            SpeedProfileManager.Load();
            var profiles = SpeedProfileManager.GetProfileNames();
            SpeedProfiles.Clear();
            foreach (var p in profiles) SpeedProfiles.Add(p);
            if (!SpeedProfiles.Contains(SelectedSpeedProfile)) SelectedSpeedProfile = SpeedProfiles.FirstOrDefault() ?? "Flat";
        }

        private void UpdateTriggerOptions()
        {
            int currentIndex = Settings.TriggerSelection;
            TriggerLabels.Clear();

            if (IsTitanSelected)
            {
                TriggerLabels.Add("LT (Left Trigger)");
                TriggerLabels.Add("RT (Right Trigger)");
                TriggerLabels.Add("Both Triggers");
            }
            else
            {
                TriggerLabels.Add("Mouse Left Click");
                TriggerLabels.Add("Mouse Right Click");
                TriggerLabels.Add("Both Buttons");
            }

            if (currentIndex >= 0 && currentIndex < TriggerLabels.Count)
            {
                SelectedTrigger = currentIndex;
            }
            else
            {
                SelectedTrigger = 0;
            }
        }

        private void InitializeColorSlots()
        {
            for (int i = 0; i < 5; i++) ColorSlots.Add(new Media.SolidColorBrush(Media.Colors.Black));
            LoadColorsFromSettingsString();
        }

        private void LoadColorsFromSettingsString()
        {
            try
            {
                string hexString = Settings.ColorFilterHex ?? "";
                var parts = hexString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < 5; i++)
                {
                    if (i < parts.Length)
                    {
                        try { var wpfColor = (Media.Color)Media.ColorConverter.ConvertFromString(parts[i]); ColorSlots[i] = new Media.SolidColorBrush(wpfColor); }
                        catch { ColorSlots[i] = new Media.SolidColorBrush(Media.Colors.Black); }
                    }
                    else { ColorSlots[i] = new Media.SolidColorBrush(Media.Colors.Black); }
                }
            }
            catch { }
        }

        private void OpenColorPicker(int index)
        {
            if (index < 0 || index >= 5) return;
            Media.Color startColor = ColorSlots[index].Color;
            var pickerWindow = new Core_Aim.Views.ModernColorPickerWindow(startColor);
            if (pickerWindow.ShowDialog() == true)
            {
                Media.Color selectedColor = pickerWindow.SelectedColor;
                ColorSlots[index] = new Media.SolidColorBrush(selectedColor);
                UpdateSettingsStringFromSlots();
            }
        }

        private void UpdateSettingsStringFromSlots()
        {
            var hexColors = ColorSlots.Select(brush => $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}").ToList();
            Settings.ColorFilterHex = string.Join(",", hexColors);
            OnPropertyChanged(nameof(ColorFilterHex));
        }

        // Valores de referência: C:\Projeto-ONNX-DML\configs\Core_Aim_MAX.ini
        private void ResetPixelBotDefaults()
        {
            // Idêntico aos padrões do Python PixelBot
            PixelBotColorHex       = "#921b7d";
            PixelBotColorTolerance = 20;
            PixelBotMinArea        = 25;
            PixelBotMaxArea        = 1500;
            PixelBotMinAspectRatio = 0.800;
            PixelBotMinSolidity    = 0.900;
            PixelBotMinCircularity = 0.650;
            PixelBotSatMin         = 50;
            PixelBotValMin         = 50;
            PixelBotVerticalOffset = 30;
            PixelBotPersistRadius  = 35;
            PixelBotMaxFramesLost  = 6;
            // Controles de mira PB + KmBox
            PixelBotTrackingSpeed  = 35;   // escala inteira, interno: /1000 = 0.035 px/frame
            PixelBotAimResponse    = 60;   // escala inteira, alpha = 1 − 0.60 = 0.40
            PixelBotDeadZone       = 2.0;
            // Controles de mira PB + Titan Two
            PixelBotTrackingSpeedTT2 = 0.35;  // escala natural TT2 (igual TT2SpeedHIP)
            PixelBotAimResponseTT2   = 60;    // escala inteira, alpha = 1 − 0.60 = 0.40
            PixelBotDeadZoneTT2      = 2.0;
        }

        private void OpenFovColorPicker()
        {
            try
            {
                Media.Color startColor;
                try { startColor = (Media.Color)Media.ColorConverter.ConvertFromString(Settings.FovColorHex); }
                catch { startColor = Media.Colors.DodgerBlue; }

                var pickerWindow = new Core_Aim.Views.ModernColorPickerWindow(startColor);
                if (pickerWindow.ShowDialog() == true)
                {
                    var c = pickerWindow.SelectedColor;
                    FovColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
            }
            catch { }
        }

        private void OpenPixelBotColorPicker()
        {
            try
            {
                Media.Color startColor;
                try { startColor = (Media.Color)Media.ColorConverter.ConvertFromString(Settings.PixelBotColorHex); }
                catch { startColor = Media.Colors.Red; }

                var pickerWindow = new Core_Aim.Views.ModernColorPickerWindow(startColor);
                if (pickerWindow.ShowDialog() == true)
                {
                    var c = pickerWindow.SelectedColor;
                    PixelBotColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
            }
            catch { }
        }

        private void OnTitanTwoStatusChanged(TT2Status status)
        {
            IsTitanConnected = (status == TT2Status.Connected);
            if (IsTitanConnected) { Task.Run(async () => { await Task.Delay(500); ApplyHardwareSettings(); }); }
        }

        private void ApplyHardwareSettings()
        {
            try
            {
                var ms = ActiveMovementService;
                if (ms != null)
                {
                    // Sistema rodando → enviar via movement service (dono do HID)
                    if (Enum.TryParse<TT2_Proto>(Settings.TitanTwoProtocol, out var proto)) ms.SetOutputProtocol(proto);
                    if (Enum.TryParse<TT2_Poll>(Settings.TitanTwoInputPollRate, out var pollIn)) ms.SetInputPolling(pollIn);
                    if (Enum.TryParse<TT2_Poll>(Settings.TitanTwoOutputPollRate, out var pollOut)) ms.SetOutputPolling(pollOut);
                    ms.SlotLoad((byte)Settings.TitanTwoSlot);
                    ms.SetInvertX(InvertX); ms.SetInvertY(InvertY);
                    ms.SetClampMagnitude(ClampMagnitude); ms.SetHighSpeedMode(HighSpeedMode);
                }
                else
                {
                    // Sistema parado → enviar via TitanTwoService
                    if (Enum.TryParse<TT2_Proto>(Settings.TitanTwoProtocol, out var proto)) _titanTwoService.SetOutputProtocol(proto);
                    if (Enum.TryParse<TT2_Poll>(Settings.TitanTwoInputPollRate, out var pollIn)) _titanTwoService.SetInputPolling(pollIn);
                    if (Enum.TryParse<TT2_Poll>(Settings.TitanTwoOutputPollRate, out var pollOut)) _titanTwoService.SetOutputPolling(pollOut);
                    _titanTwoService.SelectSlot(Settings.TitanTwoSlot);
                    _titanTwoService.SetInvertX(InvertX); _titanTwoService.SetInvertY(InvertY);
                    _titanTwoService.SetClampMagnitude(ClampMagnitude); _titanTwoService.SetHighSpeedMode(HighSpeedMode);
                }
                SendMacroFlags();
            }
            catch { }
        }

        public ObservableCollection<int> DatasetSizes { get; } = new ObservableCollection<int> { 320, 416, 640 };

        public bool SaveDetections { get => Settings.SaveDetections; set { if (Settings.SaveDetections != value) { Settings.SaveDetections = value; OnPropertyChanged(nameof(SaveDetections)); } } }
        public bool SaveEnemyOnly  { get => Settings.SaveEnemyOnly;  set { if (Settings.SaveEnemyOnly  != value) { Settings.SaveEnemyOnly  = value; OnPropertyChanged(nameof(SaveEnemyOnly));  } } }
        public int SelectedDatasetSize { get => Settings.DatasetCropSize; set { if (Settings.DatasetCropSize != value) { Settings.DatasetCropSize = value; OnPropertyChanged(nameof(SelectedDatasetSize)); } } }
        public bool SaveLabels { get => Settings.SaveLabels; set { if (Settings.SaveLabels != value) { Settings.SaveLabels = value; OnPropertyChanged(nameof(SaveLabels)); } } }
        public bool SaveLabelYolo { get => Settings.SaveLabelYolo; set { if (Settings.SaveLabelYolo != value) { Settings.SaveLabelYolo = value; OnPropertyChanged(nameof(SaveLabelYolo)); } } }
        public bool SaveLabelPixelBot { get => Settings.SaveLabelPixelBot; set { if (Settings.SaveLabelPixelBot != value) { Settings.SaveLabelPixelBot = value; OnPropertyChanged(nameof(SaveLabelPixelBot)); } } }
        public bool SkipBlurryImages { get => Settings.SkipBlurryImages; set { if (Settings.SkipBlurryImages != value) { Settings.SkipBlurryImages = value; OnPropertyChanged(nameof(SkipBlurryImages)); } } }
        public double BlurThreshold { get => Settings.BlurThreshold; set { if (Settings.BlurThreshold != value) { Settings.BlurThreshold = value; OnPropertyChanged(nameof(BlurThreshold)); } } }

        public ObservableCollection<string> CurveLabels { get; } = new ObservableCollection<string> { "Linear", "Exponential", "Flat / Snappy" };
        public int SelectedCurveType { get => Settings.AimCurveType; set { if (Settings.AimCurveType != value) { Settings.AimCurveType = value; OnPropertyChanged(nameof(SelectedCurveType)); } } }

        private void PopulateEnumLists()
        {
            ProtocolLabels.Clear(); foreach (var name in Enum.GetNames(typeof(TT2_Proto))) ProtocolLabels.Add(name); OnPropertyChanged(nameof(SelectedProtocol));
            PollRateLabels.Clear(); foreach (var name in Enum.GetNames(typeof(TT2_Poll))) PollRateLabels.Add(name); OnPropertyChanged(nameof(SelectedInputPollRate)); OnPropertyChanged(nameof(SelectedOutputPollRate));
            SlotNumbers.Clear(); for (int i = 1; i <= 9; i++) SlotNumbers.Add(i); OnPropertyChanged(nameof(SelectedSlot));
        }

        private async void TestMovement() => await _titanTwoService.TestMovementAsync();
        private void SlotUp()     { if (ActiveMovementService != null) ActiveMovementService.SlotUp();     else _titanTwoService.SlotUp(); }
        private void SlotDown()   { if (ActiveMovementService != null) ActiveMovementService.SlotDown();   else _titanTwoService.SlotDown(); }
        private void SlotUnload() { if (ActiveMovementService != null) ActiveMovementService.SlotUnload(); else _titanTwoService.SlotUnload(); }
        private void ReloadModel() => OnReloadRequested?.Invoke();

        public void RefreshModelList()
        {
            try
            {
                string modelsDirectory = Path.Combine(AppContext.BaseDirectory, "Models");
                if (!Directory.Exists(modelsDirectory)) Directory.CreateDirectory(modelsDirectory);
                var modelFiles = Directory.EnumerateFiles(modelsDirectory, "*.onnx").Select(Path.GetFileName).ToList();
                Settings.ModelList.Clear(); foreach (var m in modelFiles) Settings.ModelList.Add(m);
                if (Settings.SelectedModel == null || !Settings.ModelList.Contains(Settings.SelectedModel)) { Settings.SelectedModel = Settings.ModelList.FirstOrDefault(); OnPropertyChanged(nameof(SelectedModel)); }
            }
            catch { }
        }

        public void RefreshCameraList()
        {
            // Atualiza placeholder imediatamente para UI não parecer travada
            if (Settings.CameraList.Count == 0)
                Settings.CameraList.Add("Verificando câmeras...");

            Task.Run(() =>
            {
                try
                {
                    var cams = CameraService.GetAvailableCameras();
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        Settings.CameraList.Clear();
                        foreach (var c in cams) Settings.CameraList.Add(c);
                        if (Settings.SelectedCameraIndex < 0 || Settings.SelectedCameraIndex >= Settings.CameraList.Count)
                            Settings.SelectedCameraIndex = 0;
                    });
                }
                catch { }
            });
        }

        public void Dispose() { _titanTwoService.OnStatusChanged -= OnTitanTwoStatusChanged; }
    }
}