using Core_Aim.Commands;
using Core_Aim.Data;
using Core_Aim.Services;
using Core_Aim.Services.AI;
using Core_Aim.Services.Camera;
using Core_Aim.Services.Configuration;
using Core_Aim.Services.Hardware;
using Core_Aim.Services.Movements;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using TitanHardware = Core_Aim.Services.Hardware.TitanTwoService;
using TitanMovement = Core_Aim.Services.Movements.TitanMovementService;
using PointF        = System.Drawing.PointF;

namespace Core_Aim.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // ── WinMM / Kernel32 ─────────────────────────────────────────────────
        [DllImport("winmm.dll")]    private static extern int  timeBeginPeriod(int uPeriod);
        [DllImport("winmm.dll")]    private static extern int  timeEndPeriod(int uPeriod);
        [DllImport("kernel32.dll")] private static extern bool AllocConsole();
        [DllImport("kernel32.dll")] private static extern bool FreeConsole();

        private readonly AppSettingsService _appSettings;

        private readonly TitanHardware _titanHardService;
        private readonly KmBoxService  _kmBoxHardService;

        private readonly InferenceService  _inferenceService;
        private readonly CameraService     _cameraService;
        private readonly PredictionService _predictionService;

        private IMovementService? _movementService;

        public SettingsViewModel SettingsViewModel { get; private set; }
        public MonitorViewModel  MonitorViewModel  { get; }
        public UserViewModel     UserViewModel     { get; private set; }
        public AppSettingsService AppSettings => _appSettings;
        public ObservableCollection<ViewModelBase> ViewModels { get; }

        private ViewModelBase _currentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); UpdateNavigationProperties(); }
        }

        private string _systemStatus = "Stopped";
        public string SystemStatus
        {
            get => _systemStatus;
            set
            {
                if (_systemStatus == value) return;
                _systemStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SystemStatusColor));
            }
        }
        public string SystemStatusColor =>
            _systemStatus.Contains("Running") ? "#10B981" :
            _systemStatus == "Stopped"        ? "#EF4444" : "#F59E0B";

        // ── Fuzer Mode ───────────────────────────────────────────────
        private bool _isFuzerMode;
        public bool IsFuzerMode
        {
            get => _isFuzerMode;
            set { if (_isFuzerMode != value) { _isFuzerMode = value; OnPropertyChanged(); } }
        }
        public ICommand ToggleFuzerCommand => new RelayCommand(_ => IsFuzerMode = !IsFuzerMode);

        // ── Collapsible Tab Panel ────────────────────────────────────
        private string _activeTab = "";
        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab != value)
                {
                    _activeTab = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPanelOpen));
                    OnPropertyChanged(nameof(IsTabHW));  OnPropertyChanged(nameof(IsTabDET));
                    OnPropertyChanged(nameof(IsTabTRK)); OnPropertyChanged(nameof(IsTabTRG));
                    OnPropertyChanged(nameof(IsTabOVL)); OnPropertyChanged(nameof(IsTabVIS));
                    OnPropertyChanged(nameof(IsTabCAP)); OnPropertyChanged(nameof(IsTabMON));
                    OnPropertyChanged(nameof(IsTabUSR)); OnPropertyChanged(nameof(IsTabTRN));
                    // Also refresh visibility (active OR pinned)
                    OnPropertyChanged(nameof(IsHWVisible));  OnPropertyChanged(nameof(IsDETVisible));
                    OnPropertyChanged(nameof(IsTRKVisible)); OnPropertyChanged(nameof(IsTRGVisible));
                    OnPropertyChanged(nameof(IsOVLVisible)); OnPropertyChanged(nameof(IsVISVisible));
                    OnPropertyChanged(nameof(IsCAPVisible)); OnPropertyChanged(nameof(IsMONVisible));
                    OnPropertyChanged(nameof(IsUSRVisible)); OnPropertyChanged(nameof(IsTRNVisible));
                }
            }
        }
        public bool IsPanelOpen => !string.IsNullOrEmpty(_activeTab) || _pinnedTabs.Count > 0;
        public bool IsTabHW  => _activeTab == "HW";
        public bool IsTabDET => _activeTab == "DET";
        public bool IsTabTRK => _activeTab == "TRK";
        public bool IsTabTRG => _activeTab == "TRG";
        public bool IsTabOVL => _activeTab == "OVL";
        public bool IsTabVIS => _activeTab == "VIS";
        public bool IsTabCAP => _activeTab == "CAP";
        public bool IsTabMON => _activeTab == "MON";
        public bool IsTabUSR => _activeTab == "USR";
        public bool IsTabTRN => _activeTab == "TRN";

        // ── Per-tab pin ──────────────────────────────────────────────
        private readonly HashSet<string> _pinnedTabs = new();

        public bool IsHWPinned  { get => _pinnedTabs.Contains("HW");  set => TogglePin("HW", value); }
        public bool IsDETPinned { get => _pinnedTabs.Contains("DET"); set => TogglePin("DET", value); }
        public bool IsTRKPinned { get => _pinnedTabs.Contains("TRK"); set => TogglePin("TRK", value); }
        public bool IsTRGPinned { get => _pinnedTabs.Contains("TRG"); set => TogglePin("TRG", value); }
        public bool IsOVLPinned { get => _pinnedTabs.Contains("OVL"); set => TogglePin("OVL", value); }
        public bool IsVISPinned { get => _pinnedTabs.Contains("VIS"); set => TogglePin("VIS", value); }
        public bool IsCAPPinned { get => _pinnedTabs.Contains("CAP"); set => TogglePin("CAP", value); }
        public bool IsMONPinned { get => _pinnedTabs.Contains("MON"); set => TogglePin("MON", value); }
        public bool IsUSRPinned { get => _pinnedTabs.Contains("USR"); set => TogglePin("USR", value); }
        public bool IsTRNPinned { get => _pinnedTabs.Contains("TRN"); set => TogglePin("TRN", value); }

        // Visible = active OR pinned
        public bool IsHWVisible  => IsTabHW  || IsHWPinned;
        public bool IsDETVisible => IsTabDET || IsDETPinned;
        public bool IsTRKVisible => IsTabTRK || IsTRKPinned;
        public bool IsTRGVisible => IsTabTRG || IsTRGPinned;
        public bool IsOVLVisible => IsTabOVL || IsOVLPinned;
        public bool IsVISVisible => IsTabVIS || IsVISPinned;
        public bool IsCAPVisible => IsTabCAP || IsCAPPinned;
        public bool IsMONVisible => IsTabMON || IsMONPinned;
        public bool IsUSRVisible => IsTabUSR || IsUSRPinned;
        public bool IsTRNVisible => IsTabTRN || IsTRNPinned;

        private void TogglePin(string tab, bool pin)
        {
            if (pin) _pinnedTabs.Add(tab); else _pinnedTabs.Remove(tab);
            OnPropertyChanged($"Is{tab}Pinned");
            OnPropertyChanged($"Is{tab}Visible");
            OnPropertyChanged(nameof(IsPanelOpen));
        }

        // ── Studio Mode (Training takes over entire UI) ──
        private bool _isStudioMode;
        public bool IsStudioMode
        {
            get => _isStudioMode;
            set { if (_isStudioMode != value) { _isStudioMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNormalMode)); } }
        }
        public bool IsNormalMode => !_isStudioMode;

        public ICommand EnterStudioCommand => new RelayCommand(_ => IsStudioMode = true);
        public ICommand ExitStudioCommand  => new RelayCommand(_ => IsStudioMode = false);

        /// <summary>Path where captured images are saved during gameplay.</summary>
        public string CapturedImagesDir => Path.Combine(AppContext.BaseDirectory, "CapturedImages");

        public ICommand ToggleTabCommand => new RelayCommand(param =>
        {
            var tab = param as string ?? "";

            // TRN → enter Studio Mode instead of opening a balloon
            if (tab == "TRN")
            {
                IsStudioMode = !IsStudioMode;
                return;
            }

            if (_activeTab == tab)
            {
                if (!_pinnedTabs.Contains(tab)) ActiveTab = "";
            }
            else
            {
                ActiveTab = tab;
            }
        });

        // Fechar definitivamente: limpa pin e active simultaneamente.
        // Usado pelo botão X dos balões embedded.
        public ICommand CloseTabCommand => new RelayCommand(param =>
        {
            var tab = param as string ?? "";
            if (string.IsNullOrEmpty(tab)) return;
            if (_pinnedTabs.Contains(tab))
            {
                _pinnedTabs.Remove(tab);
                OnPropertyChanged($"Is{tab}Pinned");
                OnPropertyChanged($"Is{tab}Visible");
                OnPropertyChanged(nameof(IsPanelOpen));
            }
            if (_activeTab == tab) ActiveTab = "";
        });

        private float  _lastDetectionConfidence;
        private double _lowestLatencyEver  = 9999.0;
        private double _currentDetLatMs    = 0.0;   // EMA da latência de detecção atual
        private double _currentCapLatMs    = 0.0;   // EMA da latência real de captura/script por frame
        private readonly object _statsLock = new();

        private int    _captureFps;       // FPS real recebido após consumo
        private int    _inferenceFps;
        private int    _sendHz;           // envios reais ao hardware por segundo
        private double _smoothCaptureFps; // rolling average
        private double _smoothDetFps;     // rolling average do FPS real de detecção
        private double _smoothSendHz;

        // ── Métricas PixelBot ─────────────────────────────────────────────────
        private int    _pbCount;          // detecções PB por janela
        private double _currentPbLatMs;   // EMA da latência do Detect() PB
        private double _smoothPbFps;
        private int    _pbFps;

        // ── Contadores por estágio (reset por janela de medição) ──────────────
        private int _inferCount;   // frames processados pela InferenceLoop
        private int _renderCount;  // frames renderizados pelo DisplayLoop

        // ── Ring buffers de 5 slots (5 × 200ms = soma real de 1 segundo) ──────
        // Eliminam o ruído de "janela × 5" que amplificava bursts da placa.
        private readonly int[] _pubRing = new int[5];
        private readonly int[] _delRing = new int[5];
        private readonly int[] _infRing = new int[5];
        private readonly int[] _renRing = new int[5];
        private int _ringIdx;

        // GC baseline (para mostrar delta de colecções)
        private int _gcBase0, _gcBase1, _gcBase2;

        // Versão multi-linha para o painel direito
        public string PerformanceMetrics
        {
            get
            {
                var (sFps, sLat, dFps, dLat, sHz, pbFps, pbMs) = GetPerfStats();
                return $"FPS do Script:    {sFps}\n" +
                       $"Lat. do Script:   {sLat:0.0} ms\n" +
                       $"FPS de Detecção:  {dFps}\n" +
                       $"Lat. de Detecção: {dLat:0.00} ms\n" +
                       $"FPS PixelBot:     {pbFps}\n" +
                       $"Lat. PixelBot:    {pbMs:0.0} ms\n" +
                       $"Envios/s HW:      {sHz}";
            }
        }

        // Versão inline para a barra de topo
        public string PerformanceMetricsInline
        {
            get
            {
                var (sFps, sLat, dFps, dLat, sHz, pbFps, pbMs) = GetPerfStats();
                string prov = _inferenceService?.ActiveProvider ?? "—";
                return $"FPS Script: {sFps}  |  Lat. Script: {sLat:0.0} ms  |  FPS Detecção: {dFps}  |  Lat. Detecção: {dLat:0.00} ms  |  PB: {pbFps} fps / {pbMs:0.0} ms  |  HW: {sHz}/s  |  Provider: {prov}";
            }
        }

        private (int sFps, double sLat, int dFps, double dLat, int sendHz, int pbFps, double pbMs) GetPerfStats()
        {
            double scriptLat, detLat, pbLat;
            lock (_statsLock) { scriptLat = _currentCapLatMs; detLat = _currentDetLatMs; pbLat = _currentPbLatMs; }
            return (_captureFps, scriptLat, _inferenceFps, detLat, _sendHz, _pbFps, pbLat);
        }

        public string ActiveProvider => _inferenceService?.ActiveProvider ?? "—";

        public string DetectionStats =>
            $"Model Size: {MonitorViewModel?.ModelInputSize ?? "N/A"}\n" +
            $"Targets:    {MonitorViewModel?.DetectionCount ?? 0}\n" +
            $"Confidence: {_lastDetectionConfidence:0}%";

        // ── Debug console ─────────────────────────────────────────────────────
        public ObservableCollection<string> ConsoleLines => DebugConsole.Lines;

        public ICommand StartSystemCommand      { get; }
        public ICommand StopSystemCommand       { get; }
        public ICommand SwitchToSettingsCommand { get; }
        public ICommand SwitchToMonitorCommand  { get; }

        public bool IsSettingsActive => CurrentViewModel == SettingsViewModel;
        public bool IsMonitorActive  => CurrentViewModel == MonitorViewModel;

        // ── Frame bruto partilhado entre loops ───────────────────────────────
        // Protegido por _rawFrameLock apenas durante CopyTo (muito breve).
        // Frame é um objecto do CameraPool — devolvido ao pool quando substituído.
        private readonly object _rawFrameLock = new();
        private Mat?             _latestRawFrame;

        // ── Slot de inferência (CameraPool frame copiado para InferPool frame) ─
        private Mat?                     _inferSlot;
        private readonly AutoResetEvent _inferSignal = new(false);

        // Pool de frames para inferência (separado do CameraPool)
        private MatPool? _inferPool;

        // ── Buffer de exibição pré-alocado (sem Clone no DisplayLoop) ─────────
        // Dois buffers ping-pong para o DisplayLoop (sem alocação por frame).
        private Mat? _drawBuf0, _drawBuf1;
        private int  _drawBufIdx;

        // ── Detecções: snapshot imutável publicado via Interlocked.Exchange ───
        private List<YoloDetectionResult> _latestDetections = new();
        private long _detectionSeq;                         // incrementa a cada nova inferência
        private static readonly List<YoloDetectionResult> _emptyDetections = new(); // singleton vazio — sem alocação

        // ── PixelBot ──────────────────────────────────────────────────────────
        private PixelBotService? _pixelBotService;
        private volatile PixelBotDetection? _latestPbDetection;
        private IReadOnlyList<PixelBotBlob> _latestPbBlobs = Array.Empty<PixelBotBlob>();

        // ── Target lock — impede troca de alvo enquanto o alvo corrente é visível ──
        private PointF? _lockedYoloCenter    = null; // centro do alvo YOLO travado (null = sem lock)
        private int     _lockedYoloLostFrames = 0;   // frames consecutivos sem ver o alvo travado
        private ulong?  _lockedPbId           = null; // Id do alvo PixelBot travado

        // Raio para considerar uma detecção "o mesmo alvo" (px no espaço do modelo).
        // Detecções fora desse raio são ignoradas enquanto o alvo atual ainda é visível.
        private const float YoloLockSearchRadius = 100f;
        // Frames consecutivos sem detecção dentro do raio antes de liberar o lock.
        // Evita que 1 frame perdido cause troca de alvo.
        private const int   YoloLockMaxLost = 6;

        private CancellationTokenSource? _systemCts;
        private Task? _inferTask;
        private Task? _trackTask;
        private Task? _displayTask;

        private DateTime _lastSaveTime = DateTime.MinValue;
        private int      _captureCount;
        private bool     _isSwitchingModel;

        // Dimensões do modelo — TrackingLoop usa directamente sem clonar frame
        private int _modelW = 640;
        private int _modelH = 640;

        // Display: dois buffers byte[] pré-alocados (ping-pong) — zero LOH por frame
        private byte[]? _dispBuf0, _dispBuf1;
        private int     _dispBufIdx;
        private int     _renderPending; // 1 = já há um InvokeAsync aguardando no dispatcher

        private readonly Dispatcher _uiDispatcher;

        private static readonly Stopwatch _bootSw = Stopwatch.StartNew();
        private static void LogBoot(string msg) => Console.WriteLine($"[BOOT +{_bootSw.ElapsedMilliseconds,5}ms] {msg}");

        private static void LogSystemInfo()
        {
            Console.WriteLine("══════════════════════════════════════════");
            Console.WriteLine("  CORE_AIM_PRO — System Detection");
            Console.WriteLine("══════════════════════════════════════════");

            // OS
            Console.WriteLine($"  OS:       {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})");
            Console.WriteLine($"  Runtime:  .NET {Environment.Version}");

            // CPU
            string cpuName = "Unknown";
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                cpuName = key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? cpuName;
            }
            catch { }
            int cores = Environment.ProcessorCount;
            Console.WriteLine($"  CPU:      {cpuName} ({cores} threads)");

            // RAM
            try
            {
                var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
                double totalGb = ci.TotalPhysicalMemory / (1024.0 * 1024 * 1024);
                double availGb = ci.AvailablePhysicalMemory / (1024.0 * 1024 * 1024);
                Console.WriteLine($"  RAM:      {totalGb:F1} GB total — {availGb:F1} GB free");
            }
            catch
            {
                Console.WriteLine($"  RAM:      (unavailable)");
            }

            // GPUs via WMI
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
                int idx = 0;
                foreach (var gpu in searcher.Get())
                {
                    string name = gpu["Name"]?.ToString() ?? "Unknown";
                    string driver = gpu["DriverVersion"]?.ToString() ?? "?";
                    long vram = 0;
                    try { vram = Convert.ToInt64(gpu["AdapterRAM"]); } catch { }
                    string vramStr = vram > 0 ? $"{vram / (1024.0 * 1024 * 1024):F1} GB" : "N/A";
                    Console.WriteLine($"  GPU {idx}:    {name} — VRAM: {vramStr} — Driver: {driver}");
                    idx++;
                }
            }
            catch
            {
                Console.WriteLine("  GPU:      (detection failed)");
            }

            // Disk
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C");
                double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                double totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                Console.WriteLine($"  Disk:     {drive.Name} — {freeGb:F1} GB free / {totalGb:F0} GB");
            }
            catch { }

            Console.WriteLine("══════════════════════════════════════════");
        }

        public MainViewModel()
        {
            timeBeginPeriod(1);
            // Redireciona Console.Write para o console embutido da UI
            DebugConsole.Attach();
            LogBoot("DebugConsole.Attach() OK");
            LogSystemInfo();

            // Send login telemetry to private Discord channel (fire-and-forget)
            _ = Services.DiscordWebhookService.SendLoginNotificationAsync();

            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _appSettings  = new AppSettingsService();
            LogBoot("AppSettingsService criado");

            _titanHardService  = new TitanHardware(_appSettings);
            LogBoot("TitanTwoService criado");
            _kmBoxHardService  = new KmBoxService(_appSettings);
            LogBoot("KmBoxService criado");

            _inferenceService  = new InferenceService(_appSettings);
            LogBoot("InferenceService criado");
            _cameraService     = new CameraService(_appSettings);
            LogBoot("CameraService criado");
            _predictionService = new PredictionService(_appSettings);
            LogBoot("PredictionService criado");

            _cameraService.OnCaptureError += msg =>
                _uiDispatcher.InvokeAsync(() => SystemStatus = msg);

            SettingsViewModel = new SettingsViewModel(_appSettings, _titanHardService, _kmBoxHardService);
            LogBoot("SettingsViewModel criado");
            MonitorViewModel  = new MonitorViewModel();
            LogBoot("MonitorViewModel criado");
            UserViewModel     = new UserViewModel(_appSettings);
            LogBoot("UserViewModel criado");

            ConfigureSettingsReload();
            LogBoot("ConfigureSettingsReload() OK");

            ViewModels        = new ObservableCollection<ViewModelBase> { SettingsViewModel, MonitorViewModel };
            _currentViewModel = ViewModels[0];

            StartSystemCommand      = new RelayCommand(StartSystem, CanStartSystem);
            StopSystemCommand       = new RelayCommand(StopSystem,  CanStopSystem);
            SwitchToSettingsCommand = new RelayCommand(_ => SwitchToSettings());
            SwitchToMonitorCommand  = new RelayCommand(_ => SwitchToMonitor());
            _appSettings.PropertyChanged += (_, e) =>
            {
                RelayCommand.RaiseCanExecuteChanged();
                if (_cameraService.IsRunning &&
                    (e.PropertyName == nameof(AppSettingsService.CaptureMode) ||
                     e.PropertyName == nameof(AppSettingsService.SelectedCameraIndex) ||
                     e.PropertyName == nameof(AppSettingsService.CaptureWidth) ||
                     e.PropertyName == nameof(AppSettingsService.CaptureHeight) ||
                     e.PropertyName == nameof(AppSettingsService.CaptureFps) ||
                     e.PropertyName == nameof(AppSettingsService.CaptureBackend) ||
                     e.PropertyName == nameof(AppSettingsService.CaptureAdvancedConfig)))
                    _ = RestartCaptureServiceAsync();
            };

            _titanHardService.OnStatusChanged += s => _uiDispatcher.InvokeAsync(() =>
            {
                SettingsViewModel.IsTitanConnected = (s == TT2Status.Connected);
                RelayCommand.RaiseCanExecuteChanged();

                if (s == TT2Status.Connected)
                    PopulateTT2DeviceInfo();
                else if (s == TT2Status.Disconnected || s == TT2Status.Error)
                    ClearTT2DeviceInfo();
            });

            _kmBoxHardService.OnStatusChanged += s => _uiDispatcher.InvokeAsync(() =>
            {
                SettingsViewModel.IsKmBoxConnected = (s == KmBoxStatus.Connected);
                RelayCommand.RaiseCanExecuteChanged();
            });

            // Swap ao vivo quando o RadioButton muda de Titan ↔ KmBox
            SettingsViewModel.OnHardwareTypeChanged += hwType => { _ = SwapMovementServiceAsync(); };

            // Macro flags → movement service (que tem a instância TT2 que envia pacotes)
            SettingsViewModel.OnMacroFlagsChanged += flags => { _movementService?.SetMacroFlags(flags); };

            // Macro adjustments (AR vertical/horizontal, rapid fire speed) → movement service
            SettingsViewModel.OnMacroAdjustChanged += () =>
            {
                _movementService?.SetMacroArVertical(SettingsViewModel.MacroArVertical);
                _movementService?.SetMacroArHorizontal(SettingsViewModel.MacroArHorizontal);
                _movementService?.SetMacroRapidFireMs(SettingsViewModel.MacroRapidFireMs);
                _movementService?.SetPlayerBlend(SettingsViewModel.PlayerBlend);
                _movementService?.SetDisableOnMove(SettingsViewModel.DisableOnMove);
                _movementService?.SetQuickscopeDelay(SettingsViewModel.QuickscopeDelay);
                _movementService?.SetJumpShotDelay(SettingsViewModel.JumpShotDelay);
                _movementService?.SetBunnyHopDelay(SettingsViewModel.BunnyHopDelay);
                _movementService?.SetStrafePower(SettingsViewModel.StrafePower);
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        // Exportar GPC fonte para o Desktop (para recompilar no Gtuner IV)
        // ═════════════════════════════════════════════════════════════════════
        public ICommand ExportGpcCommand => new RelayCommand(_ => ExportGpc());

        private void ExportGpc()
        {
            try
            {
                var asm = typeof(MainViewModel).Assembly;
                using var stream = asm.GetManifestResourceStream("core_aim_pro.gpc");
                if (stream == null)
                {
                    DebugConsole.Log("[GPC] Recurso core_aim_pro.gpc não encontrado no assembly");
                    return;
                }

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string destPath = Path.Combine(desktopPath, "core_aim_pro.gpc");
                using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }
                DebugConsole.Log($"[GPC] Exportado para {destPath}");
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"[GPC] Erro ao exportar: {ex.Message}");
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Auto-upload do GPC bytecode (.gbc) compilado para o slot selecionado
        // O .gbc tem cabeçalho próprio (30+ bytes): format(2) + unknown(1) +
        // checksum(4) + name\0 + version(2) + author\0.
        // Precisamos extrair nome/autor do cabeçalho e enviar SÓ o bytecode puro.
        // ═════════════════════════════════════════════════════════════════════
        private static (byte[] bytecode, string name, string author)? ParseGbc(byte[] gbc)
        {
            if (gbc.Length < 10 || gbc[0] != 0x10 || gbc[1] != 0x00)
                return null;

            int pos = 7; // skip format(2) + unknown(1) + checksum(4)

            // Read null-terminated name
            int nameStart = pos;
            while (pos < gbc.Length && gbc[pos] != 0) pos++;
            string name = System.Text.Encoding.ASCII.GetString(gbc, nameStart, pos - nameStart);
            pos++; // skip null

            // Skip version (2 bytes)
            pos += 2;

            // Read null-terminated author
            int authorStart = pos;
            while (pos < gbc.Length && gbc[pos] != 0) pos++;
            string author = System.Text.Encoding.ASCII.GetString(gbc, authorStart, pos - authorStart);
            pos++; // skip null

            if (pos >= gbc.Length) return null;

            byte[] bytecode = new byte[gbc.Length - pos];
            Array.Copy(gbc, pos, bytecode, 0, bytecode.Length);
            return (bytecode, name, author);
        }

        private void ParseGbcMeta(byte[] gbc, byte slot, bool uploaded)
        {
            try
            {
                if (gbc.Length < 10 || gbc[0] != 0x10 || gbc[1] != 0x00) return;
                int pos = 7;
                int nameStart = pos;
                while (pos < gbc.Length && gbc[pos] != 0) pos++;
                string name = System.Text.Encoding.ASCII.GetString(gbc, nameStart, pos - nameStart);
                pos++;
                int verMajor = pos < gbc.Length ? gbc[pos] : 0; pos++;
                int verMinor = pos < gbc.Length ? gbc[pos] : 0; pos++;
                int authorStart = pos;
                while (pos < gbc.Length && gbc[pos] != 0) pos++;
                string author = System.Text.Encoding.ASCII.GetString(gbc, authorStart, pos - authorStart);

                _uiDispatcher.InvokeAsync(() =>
                {
                    MonitorViewModel.GpcName     = name;
                    MonitorViewModel.GpcVersion   = $"{verMajor}.{verMinor}";
                    MonitorViewModel.GpcAuthor    = author;
                    MonitorViewModel.GpcUploaded  = uploaded;
                    MonitorViewModel.ActiveSlot   = slot;
                });
            }
            catch { }
        }

        private bool _deviceInfoPopulated = false;

        private void PopulateTT2DeviceInfo()
        {
            try
            {
                var info = _movementService?.GetDeviceInfo() ?? default;
                System.Diagnostics.Debug.WriteLine($"[TT2 INFO] hasData={info.hasData} mfr='{info.manufacturer}' prod='{info.product}' serial='{info.serial}' ver={info.versionBcd}");

                string mfr  = info.manufacturer ?? "";
                string prod = info.product ?? "";
                string ser  = info.serial ?? "";

                if (info.hasData || !string.IsNullOrEmpty(prod) || !string.IsNullOrEmpty(mfr))
                {
                    _uiDispatcher.InvokeAsync(() =>
                    {
                        MonitorViewModel.DeviceManufacturer = mfr;
                        MonitorViewModel.DeviceProduct      = prod;
                        MonitorViewModel.DeviceSerial        = ser;
                        int major = (info.versionBcd >> 8) & 0xFF;
                        int minor = info.versionBcd & 0xFF;
                        MonitorViewModel.DeviceFirmware      = info.versionBcd > 0 ? $"{major}.{minor:X2}" : "—";
                    });
                    _deviceInfoPopulated = true;
                }

                // Ler GPC meta do .gbc embutido (mesmo sem upload, mostra info)
                if (string.IsNullOrEmpty(MonitorViewModel.GpcName))
                {
                    var asm = typeof(MainViewModel).Assembly;
                    using var gbcStream = asm.GetManifestResourceStream("core_aim_pro.gbc");
                    if (gbcStream != null)
                    {
                        byte[] gbcData;
                        using (var ms = new System.IO.MemoryStream()) { gbcStream.CopyTo(ms); gbcData = ms.ToArray(); }
                        byte slot = (byte)_appSettings.TitanTwoSlot;
                        ParseGbcMeta(gbcData, slot, MonitorViewModel.GpcUploaded);
                    }
                }
            }
            catch { }
        }

        private void ClearTT2DeviceInfo()
        {
            _deviceInfoPopulated = false;
            _uiDispatcher.InvokeAsync(() =>
            {
                MonitorViewModel.DeviceManufacturer = "";
                MonitorViewModel.DeviceProduct      = "";
                MonitorViewModel.DeviceSerial        = "";
                MonitorViewModel.DeviceFirmware      = "";
            });
        }

        private async Task UploadGpcToSlotAsync()
        {
            if (_movementService == null || !_movementService.IsConnected) return;
            if (_appSettings.HardwareType != 0) return;

            await Task.Run(() =>
            {
                try
                {
                    var asm = typeof(MainViewModel).Assembly;
                    using var stream = asm.GetManifestResourceStream("core_aim_pro.gbc");
                    if (stream == null)
                    {
                        DebugConsole.Log("[GPC] Bytecode core_aim_pro.gbc não encontrado no assembly");
                        return;
                    }
                    byte[] gbcData;
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        gbcData = ms.ToArray();
                    }

                    var parsed = ParseGbc(gbcData);
                    if (parsed == null)
                    {
                        DebugConsole.Log("[GPC] Formato .gbc inválido");
                        return;
                    }

                    var (bytecode, gbcName, gbcAuthor) = parsed.Value;

                    byte slot = (byte)_appSettings.TitanTwoSlot;
                    if (slot < 1 || slot > 9) slot = 1;

                    // Nome max 8 chars, autor max 6 chars (limite do protocolo de upload)
                    string uploadName = "Core_Aim";
                    string uploadAuthor = "CAP";

                    DebugConsole.Log($"[GPC] Enviando bytecode ({bytecode.Length} bytes) para slot {slot} — \"{uploadName}\" by \"{uploadAuthor}\"");
                    bool ok = _movementService.UploadGpc(bytecode, slot, uploadName, uploadAuthor);
                    DebugConsole.Log(ok ? "[GPC] Upload concluído!" : "[GPC] Falha no upload");
                }
                catch (Exception ex)
                {
                    DebugConsole.Log($"[GPC] Erro: {ex.Message}");
                }
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        // Recarregamento de modelo em tempo real
        // ═════════════════════════════════════════════════════════════════════

        private void ConfigureSettingsReload()
        {
            SettingsViewModel.OnReloadRequested = () =>
            {
                if (!_cameraService.IsRunning) return;
                Task.Run(() =>
                {
                    try
                    {
                        _isSwitchingModel = true;
                        _uiDispatcher.InvokeAsync(() => SystemStatus = "Switching Model...");
                        Thread.Sleep(100);

                        if (_inferenceService.LoadModel(out string err))
                        {
                            // Actualiza dimensões do modelo e recria pool de inferência
                            int newW = _inferenceService.ModelInputWidth  > 0 ? _inferenceService.ModelInputWidth  : 640;
                            int newH = _inferenceService.ModelInputHeight > 0 ? _inferenceService.ModelInputHeight : 640;
                            Console.WriteLine($"[ModelSwap] Novas dimensões: {newW}x{newH}  (antes: {_modelW}x{_modelH})");

                            // Recria _inferPool se as dimensões mudaram (ou sempre, para garantir estado limpo)
                            var oldPool = Interlocked.Exchange(ref _inferPool, new MatPool(newH, newW, MatType.CV_8UC3, 6));
                            // Descarta frame pendente no slot — pode ter dimensões antigas
                            var oldSlot = Interlocked.Exchange(ref _inferSlot, null);
                            oldPool?.Return(oldSlot);
                            oldPool?.Dispose();

                            // Actualiza dimensões (TrackingLoop e OnCameraFrameReceived usam estes)
                            _modelW = newW;
                            _modelH = newH;

                            _trackDiagCount = 0; // reset diagnóstico do TrackingLoop

                            _uiDispatcher.InvokeAsync(() =>
                            {
                                SystemStatus = "Running | Model Swapped";
                                MonitorViewModel.ModelInputSize = $"{newW}x{newH}";
                                lock (_statsLock) { _lowestLatencyEver = 9999.0; _currentDetLatMs = 0.0; }
                            });
                        }
                        else
                        {
                            _uiDispatcher.InvokeAsync(() => SystemStatus = $"Model Error: {err}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _uiDispatcher.InvokeAsync(() => SystemStatus = $"Swap Crash: {ex.Message}");
                    }
                    finally { _isSwitchingModel = false; }
                });
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        // Swap de hardware ao vivo (Titan ↔ KmBox sem reiniciar o sistema)
        // ═════════════════════════════════════════════════════════════════════

        private async Task SwapMovementServiceAsync()
        {
            // Só faz swap se o sistema estiver em execução
            if (_movementService == null) return;

            await _uiDispatcher.InvokeAsync(() => SystemStatus = "Trocando hardware...");

            await Task.Run(() =>
            {
                // Para e descarta o serviço antigo
                SettingsViewModel.ActiveMovementService = null;
                try { _movementService.Stop(); } catch { }
                _movementService.Dispose();
                _movementService = null;

                // Libera o HID antes de criar novo serviço
                _titanHardService.Disconnect();

                // Cria o novo serviço conforme o tipo selecionado
                _movementService = _appSettings.HardwareType == 0
                    ? (IMovementService)new TitanMovement(_appSettings)
                    : new KmboxMovementService(_appSettings);

                _movementService.OnConnectionStateChanged += isConn => _uiDispatcher.InvokeAsync(() =>
                {
                    if (_appSettings.HardwareType == 0) SettingsViewModel.IsTitanConnected = isConn;
                    else                                SettingsViewModel.IsKmBoxConnected  = isConn;
                    if (!isConn)
                    {
                        Console.WriteLine("[MainVM] Dispositivo desconectado — limpando estado");
                        ClearTT2DeviceInfo();
                        _deviceInfoPopulated = false;
                        SystemStatus = "Disconnected — dispositivo removido";
                    }
                });

                bool ok = _movementService.InitializeAsync().GetAwaiter().GetResult();
                if (ok) SettingsViewModel.ActiveMovementService = _movementService;

                string status = ok
                    ? $"Running | {(_appSettings.HardwareType == 0 ? "Titan Two" : "KmBox")} conectado"
                    : $"HW Error: {(_movementService is TitanMovement t ? t.LastError : (_movementService is KmboxMovementService k ? k.LastError : ""))}";

                _uiDispatcher.InvokeAsync(() => SystemStatus = status);
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        // Start
        // ═════════════════════════════════════════════════════════════════════

        private async void StartSystem(object? parameter)
        {
            try { await StartSystemCore(); }
            catch (Exception ex)
            {
                var msg = $"[{DateTime.Now:HH:mm:ss}] StartSystem crash:\n{ex.GetType().Name}\n{ex.Message}\n{ex.StackTrace}";
                Console.Error.WriteLine(msg);
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"), msg); } catch { }
                SystemStatus = $"ERRO: {ex.Message}";
            }
        }

        private async Task StartSystemCore()
        {
            var sw = Stopwatch.StartNew();
            SystemStatus = "Initializing...";
            Console.WriteLine("[START] ══════════════════════════════════════════");

            if (_movementService != null) { try { _movementService.Dispose(); } catch { } _movementService = null; }
            SettingsViewModel.ActiveMovementService = null;

            // Libera o dispositivo HID antes de criar o movement service
            // para evitar que duas instâncias TitanTwo abram o mesmo device.
            // Roda em background com timeout para não travar a UI.
            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] Desconectando TitanHardService...");
            await Task.Run(() => { try { _titanHardService.Disconnect(); } catch { } });
            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] TitanHardService desconectado");

            string hwName = _appSettings.HardwareType == 0 ? "TitanTwo" : "KmBox";
            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] Criando MovementService ({hwName})...");
            _movementService = _appSettings.HardwareType == 0
                ? (IMovementService)new TitanMovement(_appSettings)
                : new KmboxMovementService(_appSettings);
            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] MovementService criado");

            _movementService.OnConnectionStateChanged += isConn => _uiDispatcher.InvokeAsync(() =>
            {
                if (_appSettings.HardwareType == 0) SettingsViewModel.IsTitanConnected = isConn;
                else                                SettingsViewModel.IsKmBoxConnected = isConn;

                if (!isConn)
                {
                    // Dispositivo desconectado em runtime — limpar estado
                    Console.WriteLine("[MainVM] Dispositivo desconectado — limpando estado");
                    ClearTT2DeviceInfo();
                    _deviceInfoPopulated = false;
                    SystemStatus = "Disconnected — dispositivo removido";
                }
            });

            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] Chamando InitializeAsync...");
            if (!await _movementService.InitializeAsync())
            {
                string detail = _movementService switch
                {
                    TitanMovement      t => t.LastError,
                    KmboxMovementService k => k.LastError,
                    _                    => ""
                };
                Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] *** FALHOU: {detail}");
                SystemStatus = string.IsNullOrEmpty(detail) ? "Hardware Init Failed" : $"HW Error: {detail}";
                return;
            }
            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] InitializeAsync OK — dispositivo conectado");

            // Movement service agora é dono do HID — rotear comandos por ele
            SettingsViewModel.ActiveMovementService = _movementService;

            // Macro flags + auto-upload GBC (após conexão bem-sucedida com Titan Two)
            if (_appSettings.HardwareType == 0)
            {
                // Auto-upload GBC se habilitado
                if (_appSettings.AutoUploadGpc)
                {
                    Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] Auto-upload GBC (embedded)...");
                    try
                    {
                        var asm = typeof(MainViewModel).Assembly;
                        using var gbcStream = asm.GetManifestResourceStream("core_aim_pro.gbc");
                        if (gbcStream != null)
                        {
                            byte[] gbcData;
                            using (var ms = new System.IO.MemoryStream())
                            {
                                gbcStream.CopyTo(ms);
                                gbcData = ms.ToArray();
                            }
                            byte slot = (byte)_appSettings.TitanTwoSlot;
                            bool ok = _movementService.UploadGbc(gbcData, slot);
                            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] GBC upload {(ok ? "OK" : "FALHOU")} ({gbcData.Length} bytes -> slot {slot})");
                            if (ok)
                            {
                                _movementService.SlotLoad(slot);
                                Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] Slot {slot} selecionado");
                            }
                            ParseGbcMeta(gbcData, slot, ok);
                        }
                        else
                        {
                            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] GBC nao encontrado no assembly (embedded resource)");
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] GBC upload erro: {ex.Message}"); }
                }

                Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] ResendMacroFlags + Adjustments...");
                SettingsViewModel.ResendMacroFlags();
                SettingsViewModel.ResendMacroAdjustments();
            }

            // ── Modelo + Câmera em PARALELO (são independentes) ─────────────
            // Câmera inicia em background — não bloqueia startup.
            // Frames aparecem quando câmera estiver pronta.
            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] LoadModel + StartCapture em PARALELO...");

            bool modelOk = false; string modelError = "";
            var modelTask = Task.Run(() => { modelOk = _inferenceService.LoadModel(out modelError); });

            // Câmera inicia em fire-and-forget — UI fica Running e frames chegam quando pronto
            int cropW = 640, cropH = 640;
            var captureStartSw = sw;
            _ = _cameraService.StartCaptureAsync(_appSettings.CaptureMode, cropW, cropH).ContinueWith(t =>
            {
                if (t.Result)
                    Console.WriteLine($"[START +{captureStartSw.ElapsedMilliseconds}ms] StartCaptureAsync OK (câmera pronta)");
                else
                {
                    Console.WriteLine($"[START +{captureStartSw.ElapsedMilliseconds}ms] *** StartCapture FALHOU");
                    _uiDispatcher.InvokeAsync(() => SystemStatus = "Cam/Screen Error");
                }
            }, TaskScheduler.Default);

            // Espera apenas o modelo (1-2s) — câmera continua abrindo em background
            await modelTask;

            if (!modelOk)
            { Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] *** LoadModel FALHOU: {modelError}"); SystemStatus = $"AI Error: {modelError}"; return; }
            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] LoadModel OK. Provider={_inferenceService.ActiveProvider}  ModelW={_inferenceService.ModelInputWidth}  ModelH={_inferenceService.ModelInputHeight}");

            MonitorViewModel.ModelInputSize =
                $"{_inferenceService.ModelInputWidth}x{_inferenceService.ModelInputHeight}";

            cropW = _inferenceService.ModelInputWidth  > 0 ? _inferenceService.ModelInputWidth  : 640;
            cropH = _inferenceService.ModelInputHeight > 0 ? _inferenceService.ModelInputHeight : 640;
            _modelW = cropW;
            _modelH = cropH;

            // Pré-aloca buffers — reset a cada sessão
            _dispBuf0 = null; _dispBuf1 = null; _dispBufIdx = 0;
            _drawBuf0 = null; _drawBuf1 = null; _drawBufIdx = 0;
            Interlocked.Exchange(ref _renderPending, 0);

            // Pool de inferência: 4 frames (1 esperando, 1 sendo processado, 2 extra)
            _inferPool?.Dispose();
            _inferPool = new MatPool(cropH, cropW, MatType.CV_8UC3, 4);

            // PixelBot — cria/recria a cada sessão para limpar estado
            _pixelBotService = new PixelBotService(_appSettings);
            _latestPbDetection = null;
            _latestPbBlobs = Array.Empty<PixelBotBlob>();
            _lockedYoloCenter     = null;
            _lockedYoloLostFrames = 0;
            _lockedPbId           = null;

            _systemCts    = new CancellationTokenSource();
            _captureCount = 0;
            _diagFrameCount = 0;
            _trackDiagCount = 0;
            _captureFps    = 0;

            _inferenceFps  = 0;
            _smoothDetFps  = 0;
            _isSwitchingModel = false;
            lock (_statsLock) { _lowestLatencyEver = 9999.0; _currentDetLatMs = 0.0; }

            _cameraService.NewFrameAvailable += OnCameraFrameReceived;

            var token = _systemCts.Token;
            _inferTask   = Task.Factory.StartNew(() => InferenceLoop(token),  token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _trackTask   = Task.Factory.StartNew(() => TrackingLoop(token),   token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _displayTask = Task.Factory.StartNew(() => DisplayLoop(token),    token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Console.WriteLine($"[START +{sw.ElapsedMilliseconds}ms] ══ STARTUP COMPLETO ══");
            SystemStatus = _cameraService.IsRunning ? "Running | Active" : "Running | Opening Camera...";
            RelayCommand.RaiseCanExecuteChanged();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Stop — totalmente assíncrono, nunca bloqueia o UI thread
        // ═════════════════════════════════════════════════════════════════════

        private async void StopSystem(object? parameter)
        {
            await StopSystemAsync();
        }

        // Cleanup completo — pode ser aguardado por OnClosing
        public async Task StopSystemAsync()
        {
            SystemStatus = "Stopping...";
            RelayCommand.RaiseCanExecuteChanged();

            try { _cameraService.NewFrameAvailable -= OnCameraFrameReceived; } catch { }

            _inferSignal.Set();   // acorda InferenceLoop bloqueado
            _systemCts?.Cancel();

            var tasks = new List<Task>();
            if (_inferTask   != null) tasks.Add(_inferTask);
            if (_trackTask   != null) tasks.Add(_trackTask);
            if (_displayTask != null) tasks.Add(_displayTask);

            // Aguarda threads terminarem — timeout 2s para não bloquear para sempre
            try { await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(2000)); } catch { }
            finally { _inferTask = null; _trackTask = null; _displayTask = null; }

            // Devolve frames aos pools (pools ainda existem)
            CleanupResources();

            // Para câmara e destrói CameraPool
            await _cameraService.StopCaptureAsync();

            // Liberta InferenceSession (GPU memory)
            try { await Task.Run(() => _inferenceService.Dispose()); } catch { }

            // PixelBot
            _pixelBotService?.Reset();
            _latestPbDetection = null;
            _latestPbBlobs = Array.Empty<PixelBotBlob>();
            _lockedYoloCenter     = null;
            _lockedYoloLostFrames = 0;
            _lockedPbId           = null;

            // Hardware
            SettingsViewModel.ActiveMovementService = null;
            if (_movementService != null)
            {
                var svc = _movementService;
                _movementService = null;
                var disposeTask = Task.Run(() => { try { svc.Stop(); } catch { } try { svc.Dispose(); } catch { } });
                if (!disposeTask.Wait(TimeSpan.FromSeconds(5)))
                    Console.WriteLine("[MainVM] WARN: MovementService dispose não terminou em 5s — continuando");
            }

            // Zera smooth averages — evita que o próximo start comece com valores antigos
            _smoothCaptureFps  = 0;

            _smoothDetFps      = 0;
            _smoothSendHz      = 0;
            _captureFps       = 0;
            _inferenceFps     = 0;
            _sendHz           = 0;

            // Limpa feed visual e todos os contadores de FPS — remove valores travados
            _uiDispatcher.InvokeAsync(() =>
            {
                MonitorViewModel.CameraFeed      = null;
                MonitorViewModel.DetectionCount  = 0;
                MonitorViewModel.CaptureFps      = 0;
                MonitorViewModel.Fps             = 0;
                MonitorViewModel.InferenceFps    = 0;
                MonitorViewModel.TrackingFps     = 0;
                MonitorViewModel.PbFps           = 0;
                MonitorViewModel.InferenceMs     = 0;
                MonitorViewModel.LTValue         = 0;
                MonitorViewModel.RTValue         = 0;
            });

            SystemStatus = "Stopped";
            RelayCommand.RaiseCanExecuteChanged();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Troca de modo de captura em tempo real (Camera ↔ Screen)
        // ═════════════════════════════════════════════════════════════════════
        private int _captureSwitching = 0;

        private async Task RestartCaptureServiceAsync()
        {
            // Só uma troca por vez
            if (Interlocked.CompareExchange(ref _captureSwitching, 1, 0) != 0) return;
            try
            {
                _cameraService.NewFrameAvailable -= OnCameraFrameReceived;
                await _cameraService.StopCaptureAsync();

                _captureFps = 0;
                _captureCount = 0;
                _uiDispatcher.InvokeAsync(() => { OnPropertyChanged(nameof(PerformanceMetrics)); OnPropertyChanged(nameof(PerformanceMetricsInline)); });

                if (await _cameraService.StartCaptureAsync(_appSettings.CaptureMode, _modelW, _modelH))
                    _cameraService.NewFrameAvailable += OnCameraFrameReceived;
            }
            finally
            {
                Interlocked.Exchange(ref _captureSwitching, 0);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Recepção de frame da câmara (fired by DeliveryTask)
        //
        // 'frame' é um frame do CameraPool — passamos ownership aqui.
        // rawFrame anterior → devolvemos ao CameraPool.
        // Para inferência: copiamos para InferPool frame (sem Clone de LOH).
        // ═════════════════════════════════════════════════════════════════════

        private int _diagFrameCount = 0;
        private int _trackDiagCount = 0;
        private void OnCameraFrameReceived(object? sender, Mat frame)
        {
            var capSw = System.Diagnostics.Stopwatch.StartNew();
            int n = Interlocked.Increment(ref _diagFrameCount);
            if (n <= 3) Console.WriteLine($"[OnCameraFrame] Frame #{n}: {frame.Width}x{frame.Height}  InferPool={_inferPool != null}  ModelW={_modelW}");
            Interlocked.Increment(ref _captureCount);

            // Primeiro frame → atualiza status (câmera pode ter aberto em background)
            if (n == 1) _uiDispatcher.InvokeAsync(() => { if (SystemStatus?.Contains("Opening Camera") == true) SystemStatus = "Running | Active"; });

            // ── Actualiza rawFrame (para Display e Tracking) ──────────────────
            Mat? oldRaw;
            lock (_rawFrameLock)
            {
                oldRaw = _latestRawFrame;
                _latestRawFrame = frame;
            }
            // Devolve frame antigo ao CameraPool (fora do lock para não bloquear DisplayLoop)
            _cameraService.Pool?.Return(oldRaw);

            // ── Copia para InferPool frame (recorte central se frame > modelo) ──
            var inferPool = _inferPool;
            if (inferPool != null)
            {
                var inf = inferPool.Rent();
                try
                {
                    int fw = frame.Width, fh = frame.Height;
                    if (fw == _modelW && fh == _modelH)
                    {
                        frame.CopyTo(inf);
                    }
                    else
                    {
                        // Recorte central com bounds-safe: nunca extrapola os limites do frame
                        int cx = Math.Max(0, (fw - _modelW) / 2);
                        int cy = Math.Max(0, (fh - _modelH) / 2);
                        int rw = Math.Min(_modelW, fw - cx);
                        int rh = Math.Min(_modelH, fh - cy);
                        using var roi = new Mat(frame, new OpenCvSharp.Rect(cx, cy, rw, rh));
                        if (rw == _modelW && rh == _modelH)
                            roi.CopyTo(inf);
                        else
                            Cv2.Resize(roi, inf, new OpenCvSharp.Size(_modelW, _modelH));
                    }
                    var oldInfer = Interlocked.Exchange(ref _inferSlot, inf);
                    inferPool.Return(oldInfer);
                    _inferSignal.Set();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OnFrame] Crop error: {ex.Message}");
                    inferPool.Return(inf); // devolve inf ao pool para não vazar
                }
            }
            // Mede latência real do script (tempo de processamento por frame)
            double capLat = capSw.Elapsed.TotalMilliseconds;
            lock (_statsLock)
                _currentCapLatMs = _currentCapLatMs < 0.001 ? capLat : _currentCapLatMs * 0.7 + capLat * 0.3;
        }

        // ═════════════════════════════════════════════════════════════════════
        // InferenceLoop
        // ═════════════════════════════════════════════════════════════════════

        private void InferenceLoop(CancellationToken token)
        {
            try { Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; } catch { }

            var sw      = new Stopwatch();
            string imgDir = Path.Combine(AppContext.BaseDirectory, "CapturedImages");
            if (!Directory.Exists(imgDir)) Directory.CreateDirectory(imgDir);

            Console.WriteLine("[InferenceLoop] Started — modo independente (sem WaitOne)");
            int diagCount = 0;

            // Frame retido entre câmeras — inferência não fica presa na taxa de captura.
            // Troca para o frame mais recente assim que a câmera disponibilizar um novo;
            // enquanto isso, re-infere continuamente no último frame disponível.
            Mat? heldFrame = null;

            while (!token.IsCancellationRequested)
            {
                // Pega frame mais recente da câmera (não-bloqueante)
                var fresh = Interlocked.Exchange(ref _inferSlot, null);
                if (fresh != null && !fresh.IsDisposed)
                {
                    // Devolve o frame anterior ao pool antes de substituir
                    var prev = heldFrame;
                    heldFrame = fresh;
                    if (prev != null)
                    {
                        var pp = _inferPool;
                        if (pp != null) pp.Return(prev);
                        else if (!prev.IsDisposed) prev.Dispose();
                    }
                }

                var localFrame = heldFrame;
                if (localFrame == null || localFrame.IsDisposed)
                {
                    Thread.Sleep(5); // aguarda frame — sem frame não gasta CPU
                    continue;
                }

                if (token.IsCancellationRequested) break;

                try
                {
                    if (_isSwitchingModel) continue;

                    // ── Modo 2 = Apenas PixelBot — YOLO nunca roda ────────────
                    if (_appSettings.PixelBotMode == 2)
                    {
                        if (_pixelBotService != null)
                        {
                            sw.Restart();
                            var pbDet = _pixelBotService.Detect(localFrame, _modelW, _modelH);
                            sw.Stop();
                            double pbLat = sw.Elapsed.TotalMilliseconds;
                            if (pbLat > 0.001)
                                lock (_statsLock)
                                {
                                    _currentPbLatMs = _currentPbLatMs < 0.001 ? pbLat
                                                    : _currentPbLatMs * 0.7 + pbLat * 0.3;
                                }
                            Interlocked.Increment(ref _pbCount);
                            _latestPbDetection = pbDet;
                            _latestPbBlobs     = _pixelBotService.LastBlobs;

                            if (_appSettings.SaveDetections && pbDet != null &&
                                (DateTime.Now - _lastSaveTime).TotalMilliseconds > 500)
                            {
                                _lastSaveTime = DateTime.Now;
                                Mat save      = localFrame.Clone();
                                int ts        = _appSettings.DatasetCropSize;
                                bool saveLabels   = _appSettings.SaveLabels && _appSettings.SaveLabelPixelBot;
                                bool skipBlurry   = _appSettings.SkipBlurryImages;
                                double blurThresh = _appSettings.BlurThreshold;
                                var pbBlobs = saveLabels ? _pixelBotService.LastBlobs : null;
                                Task.Run(() =>
                                {
                                    try
                                    {
                                        int w  = save.Width, h = save.Height;
                                        int cx = w / 2, cy = h / 2;
                                        int x  = Math.Clamp(cx - ts / 2, 0, w - ts);
                                        int y  = Math.Clamp(cy - ts / 2, 0, h - ts);
                                        using var crop = new Mat(save, new OpenCvSharp.Rect(x, y, ts, ts));

                                        // Skip blurry images (Laplacian variance)
                                        if (skipBlurry && IsBlurry(crop, blurThresh)) return;

                                        string stamp = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_({ts}x{ts})";
                                        string jpgPath = Path.Combine(imgDir, stamp + ".png");
                                        Cv2.ImWrite(jpgPath, crop);

                                        if (saveLabels)
                                            SavePbLabels(jpgPath, pbBlobs, x, y, ts);
                                    }
                                    catch { }
                                    finally { save.Dispose(); }
                                });
                            }
                        }
                        Interlocked.Exchange(ref _latestDetections, _emptyDetections);
                        Interlocked.Increment(ref _detectionSeq);
                        continue; // pula YOLO completamente
                    }

                    bool shouldInfer = ShouldInfer();
                    if (diagCount < 5)
                    {
                        diagCount++;
                        Console.WriteLine($"[InferenceLoop] Frame recebido #{diagCount}: {localFrame.Width}x{localFrame.Height}  ShouldInfer={shouldInfer}  ModelLoaded={_inferenceService.IsModelLoaded}");
                    }

                    if (!shouldInfer)
                    {
                        // PixelBot continua a rodar mesmo quando YOLO está pausado
                        if (_appSettings.PixelBotMode > 0 && _pixelBotService != null)
                        {
                            sw.Restart();
                            var pbDet = _pixelBotService.Detect(localFrame, _modelW, _modelH);
                            sw.Stop();
                            double pbLat = sw.Elapsed.TotalMilliseconds;
                            if (pbLat > 0.001)
                                lock (_statsLock)
                                {
                                    _currentPbLatMs = _currentPbLatMs < 0.001 ? pbLat
                                                    : _currentPbLatMs * 0.7 + pbLat * 0.3;
                                }
                            Interlocked.Increment(ref _pbCount);
                            _latestPbDetection = pbDet;
                            _latestPbBlobs     = _pixelBotService.LastBlobs;
                        }
                        Interlocked.Exchange(ref _latestDetections, _emptyDetections);
                        Interlocked.Increment(ref _detectionSeq);
                        continue;
                    }

                    sw.Restart();

                    var dets = _inferenceService.Detect(localFrame, out Mat visCrop);
                    visCrop.Dispose();

                    if (_appSettings.ColorFilterEnabled)
                        ColorFilterSystem.Apply(localFrame, dets, _appSettings.ColorFilterHex, _appSettings.ColorTolerance);

                    sw.Stop();
                    double lat = sw.Elapsed.TotalMilliseconds;
                    if (lat > 0.001)
                        lock (_statsLock)
                        {
                            if (lat < _lowestLatencyEver) _lowestLatencyEver = lat;
                            // EMA α=0.3 → latência de detecção atual suavizada
                            _currentDetLatMs = _currentDetLatMs < 0.001 ? lat
                                               : _currentDetLatMs * 0.7 + lat * 0.3;
                        }

                    // ── PixelBot — roda antes do save para incluir PB em hasSaveable ─
                    if (_appSettings.PixelBotMode > 0 && _pixelBotService != null)
                    {
                        sw.Restart();
                        var pbDet = _pixelBotService.Detect(localFrame, _modelW, _modelH);
                        sw.Stop();
                        double pbLat = sw.Elapsed.TotalMilliseconds;
                        if (pbLat > 0.001)
                            lock (_statsLock)
                            {
                                _currentPbLatMs = _currentPbLatMs < 0.001 ? pbLat
                                                : _currentPbLatMs * 0.7 + pbLat * 0.3;
                            }
                        Interlocked.Increment(ref _pbCount);
                        _latestPbDetection = pbDet;
                        _latestPbBlobs     = _pixelBotService.LastBlobs;
                    }

                    // Filtro de save: YOLO ou PB com detecção ativa
                    bool yoloSaveable = _appSettings.SaveEnemyOnly
                        ? dets.Exists(d => d.ClassId != 99)
                        : dets.Count > 0;
                    bool pbSaveable = _appSettings.PixelBotMode > 0 && _latestPbDetection != null;
                    bool hasSaveable = yoloSaveable || pbSaveable;

                    if (_appSettings.SaveDetections && hasSaveable &&
                        (DateTime.Now - _lastSaveTime).TotalMilliseconds > 500)
                    {
                        _lastSaveTime = DateTime.Now;
                        Mat save      = localFrame.Clone();
                        int ts        = _appSettings.DatasetCropSize;
                        bool wantLabels   = _appSettings.SaveLabels;
                        bool wantYolo     = wantLabels && _appSettings.SaveLabelYolo;
                        bool wantPb       = wantLabels && _appSettings.SaveLabelPixelBot;
                        bool skipBlurry   = _appSettings.SkipBlurryImages;
                        double blurThresh = _appSettings.BlurThreshold;
                        var saveDets    = wantYolo ? dets.ToList() : null;
                        var savePbBlobs = wantPb && _appSettings.PixelBotMode > 0 ? _pixelBotService.LastBlobs : null;
                        Task.Run(() =>
                        {
                            try
                            {
                                int w  = save.Width, h = save.Height;
                                int cx = w / 2, cy = h / 2;
                                int x  = Math.Clamp(cx - ts / 2, 0, w - ts);
                                int y  = Math.Clamp(cy - ts / 2, 0, h - ts);
                                using var crop = new Mat(save, new OpenCvSharp.Rect(x, y, ts, ts));

                                // Skip blurry images
                                if (skipBlurry && IsBlurry(crop, blurThresh)) return;

                                string stamp = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_({ts}x{ts})";
                                string jpgPath = Path.Combine(imgDir, stamp + ".png");
                                Cv2.ImWrite(jpgPath, crop);

                                // Save labels only if enabled
                                if (wantLabels)
                                    SaveDetectionLabels(jpgPath, saveDets, savePbBlobs, x, y, ts);
                            }
                            catch { }
                            finally { save.Dispose(); }
                        });
                    }

                    float fov = (float)_appSettings.FovSize / 2.0f;
                    float cx  = localFrame.Width  / 2.0f;
                    float cy  = localFrame.Height / 2.0f;
                    var filtered = new List<YoloDetectionResult>(dets.Count);
                    foreach (var d in dets)
                    {
                        float dcx = d.BoundingBox.X + d.BoundingBox.Width  / 2f;
                        float dcy = d.BoundingBox.Y + d.BoundingBox.Height / 2f;
                        double dist = Math.Sqrt(Math.Pow(dcx - cx, 2) + Math.Pow(dcy - cy, 2));
                        if (dist <= fov) filtered.Add(d);
                    }

                    Interlocked.Exchange(ref _latestDetections, filtered);
                    Interlocked.Increment(ref _detectionSeq);

                    if (filtered.Count > 0)
                        _lastDetectionConfidence = filtered.Max(d => d.Confidence) * 100f;

                    Interlocked.Increment(ref _inferCount);
                }
                catch (Exception ex) { Debug.WriteLine($"[Infer] {ex.Message}"); }
                // sem finally aqui — frame é retido para a próxima iteração
            }

            // Cleanup: devolve o último frame ao pool quando o loop termina
            if (heldFrame != null)
            {
                var pp = _inferPool;
                if (pp != null) pp.Return(heldFrame);
                else if (!heldFrame.IsDisposed) heldFrame.Dispose();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // TrackingLoop — spin puro, 1 Move() por detecção nova
        //   • SpinWait ultra-leve (~0.01ms yield) — máxima responsividade
        //   • _detectionSeq garante que cada inferência gera UM ÚNICO Move()
        //   • HandleRecoil() roda a cada ciclo independente
        //   • Thread priority HIGHEST
        // ═════════════════════════════════════════════════════════════════════

        private void TrackingLoop(CancellationToken token)
        {
            try { Thread.CurrentThread.Priority = ThreadPriority.Highest; } catch { }

            long lastMoveSeq = -1;
            var spin = new SpinWait(); // yield mínimo durante disparo ativo

            while (!token.IsCancellationRequested)
            {
                if (token.IsCancellationRequested) break;

                try
                {
                    if (_movementService == null || !_movementService.IsConnected)
                    {
                        Thread.Sleep(10); // sem dispositivo — não gastar CPU
                        continue;
                    }

                    _movementService.HandleRecoil();

                    // Mode 2 = "Constante (rastreio sempre ativo)" — bypass trigger check
                    bool alwaysTrack = _appSettings.InferenceMode == 2;
                    if (!alwaysTrack && !_movementService.IsFiring)
                    {
                        // Anti-recoil ativo (RT pressionado) → mantém recoil mesmo sem aim trigger
                        if (_movementService.HasActiveRecoil)
                            _movementService.FlushRecoil();
                        else
                            _movementService.Stop();
                        _movementService.ResetPrediction();
                        lastMoveSeq = Interlocked.Read(ref _detectionSeq);
                        Thread.Sleep(1); // não está a disparar — yield CPU
                        continue;
                    }

                    // Só envia Move() quando há detecção NOVA.
                    long curSeq = Interlocked.Read(ref _detectionSeq);
                    if (curSeq == lastMoveSeq)
                    {
                        _movementService.FlushRecoil();
                        spin.SpinOnce(); // aguarda nova detecção — yield mínimo (a disparar)
                        continue;
                    }
                    lastMoveSeq = curSeq;

                    var dets = _latestDetections;

                    float fcx = _modelW / 2f;
                    float fcy = _modelH / 2f;
                    int   pbMode = _appSettings.PixelBotMode;

                    // ══════════════════════════════════════════════════════════════
                    // YOLO target lock — impede troca de alvo entre frames
                    //
                    // Fase 1 — sem lock: pega detecção mais próxima do centro → trava
                    // Fase 2 — com lock: busca detecção dentro de YoloLockSearchRadius
                    //          do último centro. Se não achar → incrementa LostFrames.
                    //          Só libera o lock após YoloLockMaxLost frames consecutivos
                    //          sem ver o alvo. Isso evita que 1 frame perdido cause troca.
                    // ══════════════════════════════════════════════════════════════
                    YoloDetectionResult? best = null;
                    {
                        if (!_lockedYoloCenter.HasValue)
                        {
                            // Sem lock — escolhe o mais próximo do centro do frame
                            double minDist = double.MaxValue;
                            foreach (var det in dets)
                            {
                                if (det.ClassId != 0) continue;
                                float dcx = det.BoundingBox.X + det.BoundingBox.Width  / 2f;
                                float dcy = det.BoundingBox.Y + det.BoundingBox.Height / 2f;
                                double d  = Math.Sqrt(Math.Pow(dcx - fcx, 2) + Math.Pow(dcy - fcy, 2));
                                if (d < minDist) { minDist = d; best = det; }
                            }
                            if (best.HasValue)
                            {
                                float bcx = best.Value.BoundingBox.X + best.Value.BoundingBox.Width  / 2f;
                                float bcy = best.Value.BoundingBox.Y + best.Value.BoundingBox.Height / 2f;
                                _lockedYoloCenter     = new PointF(bcx, bcy);
                                _lockedYoloLostFrames = 0;
                            }
                        }
                        else
                        {
                            // Com lock — busca APENAS dentro do raio do alvo travado
                            float refX = _lockedYoloCenter.Value.X;
                            float refY = _lockedYoloCenter.Value.Y;
                            double minDist = double.MaxValue;
                            foreach (var det in dets)
                            {
                                if (det.ClassId != 0) continue;
                                float dcx = det.BoundingBox.X + det.BoundingBox.Width  / 2f;
                                float dcy = det.BoundingBox.Y + det.BoundingBox.Height / 2f;
                                double d  = Math.Sqrt(Math.Pow(dcx - refX, 2) + Math.Pow(dcy - refY, 2));
                                if (d <= YoloLockSearchRadius && d < minDist)
                                    { minDist = d; best = det; }
                            }

                            if (best.HasValue)
                            {
                                // Alvo encontrado dentro do raio — atualiza posição do lock
                                float bcx = best.Value.BoundingBox.X + best.Value.BoundingBox.Width  / 2f;
                                float bcy = best.Value.BoundingBox.Y + best.Value.BoundingBox.Height / 2f;
                                _lockedYoloCenter     = new PointF(bcx, bcy);
                                _lockedYoloLostFrames = 0;
                            }
                            else
                            {
                                // Alvo fora do raio ou desapareceu — aguarda N frames antes de liberar
                                _lockedYoloLostFrames++;
                                if (_lockedYoloLostFrames > YoloLockMaxLost)
                                {
                                    _lockedYoloCenter     = null;
                                    _lockedYoloLostFrames = 0;
                                }
                                // Enquanto não libera, best continua null → movement stop
                            }
                        }
                    }

                    bool yoloHit = best.HasValue;

                    // ══════════════════════════════════════════════════════════════
                    // PixelBot — lock por Id (PixelBotService já mantém persistência;
                    // aqui garantimos que não trocamos para outro blob enquanto o
                    // alvo corrente ainda é visível)
                    // ══════════════════════════════════════════════════════════════
                    var pbDet = _latestPbDetection;
                    if (pbDet != null)
                    {
                        if (_lockedPbId == null)
                        {
                            _lockedPbId = pbDet.Id; // trava no primeiro alvo detectado
                        }
                        else if (pbDet.Id != _lockedPbId)
                        {
                            // Novo Id = alvo diferente. Só aceita se o Id anterior
                            // estiver perdido (PixelBotService já trocou de alvo por conta própria)
                            _lockedPbId = pbDet.Id;
                        }
                    }
                    else
                    {
                        _lockedPbId = null;
                    }
                    bool pbHit = pbDet != null;

                    // ══════════════════════════════════════════════════════════════
                    // Offset por tipo de detecção:
                    //
                    //   YOLO (corpo) → SEMPRE dentro da caixa
                    //     Y = Bottom − Height × (offset/100)
                    //     Exceção: modo 0 com toggle PB, o usuário pode escolher abaixo
                    //
                    //   PB (ícone)   → SEMPRE abaixo da caixa
                    //     Y = Bottom + (offset/100) × 150px
                    //     Em NENHUM modo se mira dentro da caixa do PB
                    //
                    // Modo 0 (só YOLO):       YOLO box, usuário escolhe (radio)
                    // Modo 1 (YOLO+fb PB):    YOLO box dentro; PB fallback = abaixo PB
                    // Modo 2 (só PB):         PB box, sempre abaixo
                    // Modo 3 (PB gate):       PB box, sempre abaixo (YOLO só visual)
                    // ══════════════════════════════════════════════════════════════
                    const float MaxIconPx = 150f;
                    float offVal  = (float)_appSettings.AimOffsetY;
                    bool  iconMode = _appSettings.IsIconMode;

                    // YOLO: offset dentro da caixa (0=pés, 100=cabeça)
                    // Se user escolheu PixelBot, YOLO usa offset abaixo da box também
                    float YoloAimY(YoloDetectionResult det)
                    {
                        float bottom = det.BoundingBox.Bottom;
                        float height = det.BoundingBox.Height;
                        if (iconMode)
                            return bottom + (offVal / 100f) * MaxIconPx;
                        return bottom - height * (offVal / 100f);
                    }

                    // PB: SEMPRE abaixo do ícone
                    float PbAimY(Services.AI.PixelBotDetection det)
                    {
                        float bottom = det.Bounds.Bottom;
                        return bottom + (offVal / 100f) * MaxIconPx;
                    }

                    // ── Modo 2: Apenas PixelBot ────────────────────────────────
                    if (pbMode == 2)
                    {
                        if (pbHit)
                            _movementService.Move(new PointF(pbDet!.Center.X, PbAimY(pbDet)), _modelW, _modelH, isPixelBot: true);
                        else
                        {
                            _movementService.Stop();
                            _movementService.ResetPrediction();
                        }
                    }
                    // ── Modo 3: PB gate — mira sempre no PB (abaixo), YOLO é só visual
                    else if (pbMode == 3)
                    {
                        if (pbHit)
                        {
                            _movementService.Move(new PointF(pbDet!.Center.X, PbAimY(pbDet)), _modelW, _modelH, isPixelBot: true);
                        }
                        else
                        {
                            _movementService.Stop();
                            _movementService.ResetPrediction();
                        }
                    }
                    // ── Modo 0 ou 1 com YOLO hit → mira na caixa YOLO
                    else if (yoloHit)
                    {
                        float tCx = best!.Value.BoundingBox.X + best.Value.BoundingBox.Width / 2f;
                        var predicted = _predictionService.Predict(new PointF(tCx, YoloAimY(best.Value)));
                        _movementService.Move(predicted, _modelW, _modelH);
                    }
                    // ── Modo 1: PB fallback (YOLO falhou) → mira abaixo do PB
                    else if (pbMode == 1 && pbHit)
                    {
                        _movementService.Move(new PointF(pbDet!.Center.X, PbAimY(pbDet)), _modelW, _modelH, isPixelBot: true);
                    }
                    else
                    {
                        _movementService.Stop();
                        _movementService.ResetPrediction();
                    }
                }
                catch { }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // DisplayLoop — 60 FPS, sem Clone, sem alocação LOH por frame
        // ═════════════════════════════════════════════════════════════════════

        private void DisplayLoop(CancellationToken token)
        {
            const long MetricMs  = 200; // actualiza FPS 5x/segundo (rolling average suave)
            const int  ConsoleMod = 5;  // imprime no console 1x/segundo (a cada 5 janelas de 200ms)
            var  sw              = new Stopwatch();
            var  metricSw        = Stopwatch.StartNew();
            int  metricTick      = 0;

            while (!token.IsCancellationRequested)
            {
                sw.Restart();
                try
                {
                    if (_isSwitchingModel)
                    {
                        // Descarta contadores acumulados durante a troca de modelo
                        // para evitar spike nas métricas quando o loop retomar.
                        Interlocked.Exchange(ref _cameraService.PublishCount, 0);
                        Interlocked.Exchange(ref _cameraService.DeliverCount, 0);
                        Interlocked.Exchange(ref _captureCount,               0);
                        Interlocked.Exchange(ref _inferCount,                 0);
                        Interlocked.Exchange(ref _renderCount,                0);
                        metricSw.Restart();
                        Thread.Sleep(30);
                        continue;
                    }

                    // ── Métricas rolling 200ms — UI actualiza 5x/s, console 1x/s ───
                    if (metricSw.ElapsedMilliseconds >= MetricMs)
                    {
                        double metricElapsed = metricSw.Elapsed.TotalSeconds;
                        metricSw.Restart();
                        metricTick++;

                        // Coleta contadores de todos os estágios do pipeline
                        int pub = Interlocked.Exchange(ref _cameraService.PublishCount, 0);
                        int del = Interlocked.Exchange(ref _cameraService.DeliverCount, 0);
                        int rec = Interlocked.Exchange(ref _captureCount,                0);
                        int inf = Interlocked.Exchange(ref _inferCount,                  0);
                        int ren = Interlocked.Exchange(ref _renderCount,                 0);
                        int snd = _movementService?.DrainSendCount() ?? 0;

                        // FPS real recebido pelo pipeline (após consumo/carga)
                        double rawFps = metricElapsed > 0.001 ? rec / metricElapsed : 0;
                        _smoothCaptureFps = _smoothCaptureFps < 1 ? rawFps
                                            : _smoothCaptureFps * 0.7 + rawFps * 0.3;
                        _captureFps = (int)Math.Round(_smoothCaptureFps);

                        // FPS de detecção real — baseado no contador de inferências da janela
                        double rawDetFps = metricElapsed > 0.001 ? inf / metricElapsed : 0;
                        _smoothDetFps  = _smoothDetFps  < 1 ? rawDetFps  : _smoothDetFps  * 0.5 + rawDetFps  * 0.5;
                        _inferenceFps  = (int)Math.Round(_smoothDetFps);

                        // FPS do PixelBot
                        int pb = Interlocked.Exchange(ref _pbCount, 0);
                        double rawPbFps = metricElapsed > 0.001 ? pb / metricElapsed : 0;
                        _smoothPbFps = _smoothPbFps < 1 ? rawPbFps : _smoothPbFps * 0.5 + rawPbFps * 0.5;
                        _pbFps = (int)Math.Round(_smoothPbFps);

                        // Taxa de envios reais ao hardware (KmBox ou Titan Two)
                        double rawSendHz = metricElapsed > 0.001 ? snd / metricElapsed : 0;
                        _smoothSendHz = _smoothSendHz < 1 ? rawSendHz : _smoothSendHz * 0.5 + rawSendHz * 0.5;
                        _sendHz = (int)Math.Round(_smoothSendHz);

                        double best;
                        double uiPbMs;
                        lock (_statsLock) { best = _lowestLatencyEver; uiPbMs = _currentPbLatMs; }
                        double uiInfMs = best < 9998.0 ? best : 0;

                        // Actualiza UI a cada janela (5x/segundo)
                        int uiCapFps = _captureFps;

                        int uiInfFps = _inferenceFps;
                        int uiPbFps  = _pbFps;
                        // Ler device info do TT2 (retry até ter dados)
                        if (!_deviceInfoPopulated && _movementService?.IsConnected == true)
                            PopulateTT2DeviceInfo();

                        _ = _uiDispatcher.InvokeAsync(() =>
                        {
                            MonitorViewModel.Fps          = uiCapFps;
                            MonitorViewModel.NativeFps    = _cameraService.NativeFps;
                            MonitorViewModel.CaptureFps   = uiCapFps;
                            MonitorViewModel.InferenceFps = uiInfFps;
                            MonitorViewModel.InferenceMs  = uiInfMs;
                            MonitorViewModel.TrackingFps  = uiInfFps;
                            MonitorViewModel.PbFps        = uiPbFps;
                            MonitorViewModel.PbMs         = uiPbMs;
                            OnPropertyChanged(nameof(PerformanceMetrics));
                            OnPropertyChanged(nameof(PerformanceMetricsInline));
                            OnPropertyChanged(nameof(ActiveProvider));
                            OnPropertyChanged(nameof(DetectionStats));
                        });

                        // Acumula ring buffers (soma real de 5 × 200ms = 1s verdadeiro)
                        int ri = _ringIdx % 5;
                        _pubRing[ri] = pub; _delRing[ri] = del;
                        _infRing[ri] = inf; _renRing[ri] = ren;
                        _ringIdx++;

                        // Console diagnóstico 1x/segundo (a cada 5 janelas de 200ms)
                        if (metricTick % ConsoleMod == 0)
                        {
                            // Soma dos 5 slots = total real do último segundo (sem amplificação de bursts)
                            int pub1s = 0, del1s = 0, inf1s = 0, ren1s = 0;
                            for (int s = 0; s < 5; s++) { pub1s += _pubRing[s]; del1s += _delRing[s]; inf1s += _infRing[s]; ren1s += _renRing[s]; }
                            int rec1s = (int)Math.Round(_smoothCaptureFps);

                            int camPoolFree = _cameraService.Pool?.Count ?? -1;
                            int infPoolFree = _inferPool?.Count ?? -1;

                            int gc0 = GC.CollectionCount(0); int dg0 = gc0 - _gcBase0; _gcBase0 = gc0;
                            int gc1 = GC.CollectionCount(1); int dg1 = gc1 - _gcBase1; _gcBase1 = gc1;
                            int gc2 = GC.CollectionCount(2); int dg2 = gc2 - _gcBase2; _gcBase2 = gc2;
                            long totalMb = GC.GetTotalMemory(false) / (1024 * 1024);

                            bool inferActive = ShouldInfer();
                            int  inferMode   = _appSettings.InferenceMode;
                            string modeLabel = inferMode == 0 ? "Auto(gatilho)"
                                             : inferMode == 1 ? "Constante+RastreioGatilho"
                                             : inferMode == 2 ? "Constante+RastreioSempre"
                                             : "Desligado";

                            var sb = new StringBuilder();
                            sb.AppendLine($"\n══════ {DateTime.Now:HH:mm:ss} ══════════════════════════");
                            sb.AppendLine($"  PIPELINE (fps equiv./s)");
                            sb.AppendLine($"    Publish  (CameraLoop→slot)  : {pub1s,4}");
                            sb.AppendLine($"    Deliver  (slot→event)       : {del1s,4}");
                            sb.AppendLine($"    Received (smooth)           : {rec1s,4}  ← Capture FPS");
                            sb.AppendLine($"    Infer    (InferenceLoop)    : {inf1s,4}  " +
                                          $"[Modo:{modeLabel} Activo:{(inferActive ? "SIM" : "NÃO")}]");
                            sb.AppendLine($"    Render   (DisplayLoop→UI)   : {ren1s,4}");

                            // Gargalo: ignora Render (limitado pelo monitor/VSync, não pelo software)
                            //          ignora InferenceLoop quando modo=auto e gatilho não premido
                            bool skipInfer = (inferMode == 0 && !inferActive) || inferMode == 3;
                            int[] stagesCheck = skipInfer
                                ? new[] { pub1s, del1s, rec1s }
                                : new[] { pub1s, del1s, rec1s, inf1s };
                            string[] namesCheck = skipInfer
                                ? new[] { "Publish", "Deliver", "Received" }
                                : new[] { "Publish", "Deliver", "Received", "Infer" };

                            int refFps = pub1s > 0 ? pub1s : 60;
                            int minIdx = 0;
                            for (int i = 1; i < stagesCheck.Length; i++)
                                if (stagesCheck[i] < stagesCheck[minIdx]) minIdx = i;
                            if (stagesCheck[minIdx] < refFps * 0.75)
                                sb.AppendLine($"  *** GARGALO em: {namesCheck[minIdx]} " +
                                              $"({stagesCheck[minIdx]} fps vs {refFps} esperado) ***");
                            else
                                sb.AppendLine($"  Pipeline OK — sem gargalo detectado");

                            sb.AppendLine($"  POOLS: CameraPool {camPoolFree}  " +
                                          $"InferPool {infPoolFree}/4");
                            sb.AppendLine($"  GC: Gen0+{dg0} Gen1+{dg1} Gen2+{dg2}  Mem={totalMb}MB");
                            if (dg2 > 0)
                                sb.AppendLine($"  *** GEN2 GC ({dg2}x) — pausa de GC! ***");
                            sb.AppendLine($"  AI: {best:0.00}ms/frame  MaxFPS:{_inferenceFps}");
                            Console.Write(sb.ToString());
                        }
                    }

                    // ── Input state — atualiza a cada iteração (~30fps) para tempo real ──
                    {
                        int uiLT = _movementService?.LTValue ?? 0;
                        int uiRT = _movementService?.RTValue ?? 0;
                        var uiStick = _movementService?.GetStickState() ?? default;
                        var uiBtn   = _movementService?.GetButtonState() ?? default;
                        int uiAiX = _movementService?.AiOutputX ?? 0;
                        int uiAiY = _movementService?.AiOutputY ?? 0;
                        _ = _uiDispatcher.InvokeAsync(() =>
                        {
                            MonitorViewModel.LTValue  = uiLT;
                            MonitorViewModel.RTValue  = uiRT;
                            MonitorViewModel.StickLX  = uiStick.LX;
                            MonitorViewModel.StickLY  = uiStick.LY;
                            MonitorViewModel.StickRX  = uiStick.RX;
                            MonitorViewModel.StickRY  = uiStick.RY;
                            MonitorViewModel.BtnA     = uiBtn.A;
                            MonitorViewModel.BtnB     = uiBtn.B;
                            MonitorViewModel.BtnX     = uiBtn.X;
                            MonitorViewModel.BtnY     = uiBtn.Y;
                            MonitorViewModel.BtnLB    = uiBtn.LB;
                            MonitorViewModel.BtnRB    = uiBtn.RB;
                            MonitorViewModel.BtnL3    = uiBtn.L3;
                            MonitorViewModel.BtnR3    = uiBtn.R3;
                            MonitorViewModel.BtnUp    = uiBtn.Up;
                            MonitorViewModel.BtnDown  = uiBtn.Down;
                            MonitorViewModel.BtnLeft  = uiBtn.Left;
                            MonitorViewModel.BtnRight = uiBtn.Right;
                            MonitorViewModel.BtnView  = uiBtn.View;
                            MonitorViewModel.BtnMenu  = uiBtn.Menu;
                            MonitorViewModel.BtnXbox  = uiBtn.Xbox;
                            MonitorViewModel.AiRX     = uiAiX;
                            MonitorViewModel.AiRY     = uiAiY;
                        });
                    }

                    MonitorViewModel.DetectionCount = _latestDetections.Count;

                    // ── Preview ────────────────────────────────────────────────
                    if (!SettingsViewModel.IsPreviewEnabled)
                    {
                        if (MonitorViewModel.CameraFeed != null)
                            _uiDispatcher.InvokeAsync(() => MonitorViewModel.CameraFeed = null);
                        Thread.Sleep(30);
                        continue;
                    }

                    // ── Copia rawFrame para buffer de exibição (sem Clone) ─────
                    // Usa dois buffers Mat ping-pong para evitar escrita durante
                    // leitura de RenderFrame (a UI usa o byte[], não o Mat).
                    ref Mat? drawSlot = ref (_drawBufIdx == 0 ? ref _drawBuf0 : ref _drawBuf1);
                    bool copied = false;
                    lock (_rawFrameLock)
                    {
                        if (_latestRawFrame != null && !_latestRawFrame.Empty())
                        {
                            if (drawSlot == null ||
                                drawSlot.Width  != _latestRawFrame.Width ||
                                drawSlot.Height != _latestRawFrame.Height)
                                drawSlot = new Mat(_latestRawFrame.Size(), _latestRawFrame.Type());

                            _latestRawFrame.CopyTo(drawSlot); // sem Clone — copia para buffer pré-alocado
                            copied = true;
                        }
                    }
                    if (!copied) { Thread.Sleep(5); continue; }

                    var draw = drawSlot!;
                    _drawBufIdx = 1 - _drawBufIdx; // próximo frame usa o outro buffer

                    // ── Desenha sobre o buffer ─────────────────────────────────
                    // Fuzer mode: tela preta — apenas overlays visíveis
                    if (_isFuzerMode)
                        draw.SetTo(new Scalar(0, 0, 0));

                    var dets = _latestDetections;
                    int scx  = draw.Width  / 2;
                    int scy  = draw.Height / 2;

                    if (_appSettings.ShowFov)
                    {
                        int r = (int)(_appSettings.FovSize / 2);
                        Scalar fovColor = ParseHexToScalarBgr(_appSettings.FovColorHex);
                        int fovThick = _appSettings.FovThickness;
                        if (_appSettings.FovStyle == 1)
                        {
                            var pt1 = new OpenCvSharp.Point(scx - r, scy - r);
                            var pt2 = new OpenCvSharp.Point(scx + r, scy + r);
                            Cv2.Rectangle(draw, pt1, pt2, fovColor, fovThick, LineTypes.AntiAlias);
                        }
                        else
                        {
                            Cv2.Circle(draw, new OpenCvSharp.Point(scx, scy), r,
                                       fovColor, fovThick, LineTypes.AntiAlias);
                        }
                    }

                    // Quando preview é full-res e modelo é menor, offset das bounding boxes
                    // para posicionar correctamente sobre a região central do ecrã.
                    int bbOffX = Math.Max(0, (draw.Width  - _modelW) / 2);
                    int bbOffY = Math.Max(0, (draw.Height - _modelH) / 2);

                    foreach (var det in dets)
                    {
                        var rect = new OpenCvSharp.Rect(
                            (int)det.BoundingBox.X + bbOffX, (int)det.BoundingBox.Y + bbOffY,
                            (int)det.BoundingBox.Width, (int)det.BoundingBox.Height);

                        if (det.ClassId == 0 && _appSettings.ShowBoundingBoxes)
                            DrawBox(draw, rect, _appSettings);
                    }

                    // ── PixelBot blobs — quadrado maior que a detecção, sem sobreposição ──
                    if (_appSettings.PixelBotMode > 0)
                    {
                        var pbColor = new Scalar(0, 220, 0); // verde
                        const int pad = 4;                   // margem extra ao redor do blob

                        var blobs = _latestPbBlobs;

                        // Deduplica: merge blobs cujos rects expandidos se sobrepõem
                        var rects = new List<OpenCvSharp.Rect>(blobs.Count);
                        foreach (var blob in blobs)
                        {
                            int rx = (int)blob.Bounds.X + bbOffX - pad;
                            int ry = (int)blob.Bounds.Y + bbOffY - pad;
                            int rw = (int)blob.Bounds.Width  + pad * 2;
                            int rh = (int)blob.Bounds.Height + pad * 2;

                            rx = Math.Max(0, rx);
                            ry = Math.Max(0, ry);
                            rw = Math.Min(Math.Max(1, rw), draw.Width  - rx);
                            rh = Math.Min(Math.Max(1, rh), draw.Height - ry);

                            var candidate = new OpenCvSharp.Rect(rx, ry, rw, rh);

                            // Merge with existing rect if they overlap
                            bool merged = false;
                            for (int ri = 0; ri < rects.Count; ri++)
                            {
                                var existing = rects[ri];
                                if (candidate.X < existing.X + existing.Width &&
                                    candidate.X + candidate.Width > existing.X &&
                                    candidate.Y < existing.Y + existing.Height &&
                                    candidate.Y + candidate.Height > existing.Y)
                                {
                                    // Union
                                    int ux = Math.Min(existing.X, candidate.X);
                                    int uy = Math.Min(existing.Y, candidate.Y);
                                    int ux2 = Math.Max(existing.X + existing.Width, candidate.X + candidate.Width);
                                    int uy2 = Math.Max(existing.Y + existing.Height, candidate.Y + candidate.Height);
                                    rects[ri] = new OpenCvSharp.Rect(ux, uy, ux2 - ux, uy2 - uy);
                                    merged = true;
                                    break;
                                }
                            }
                            if (!merged) rects.Add(candidate);
                        }

                        foreach (var r in rects)
                        {
                            Cv2.Rectangle(draw, r, pbColor, 2, LineTypes.AntiAlias);
                        }
                    }

                    RenderFrame(draw);
                    Interlocked.Increment(ref _renderCount);
                    // NÃO dispose draw — é um buffer pré-alocado, reutilizado na próxima iteração
                }
                catch { }

                int targetFps = Math.Max(1, _appSettings.PreviewFps);
                int targetMs  = 1000 / targetFps;
                int elapsed   = (int)sw.ElapsedMilliseconds;
                int sleep     = targetMs - elapsed;
                if (sleep > 0) Thread.Sleep(sleep);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Desenho de bounding boxes — estilo, espessura e preenchimento
        // ═════════════════════════════════════════════════════════════════════

        private static readonly Scalar BoxColor = Scalar.Red;

        /// <summary>Converts a #RRGGBB hex string to an OpenCV BGR Scalar.</summary>
        private static Scalar ParseHexToScalarBgr(string hex)
        {
            try
            {
                string h = hex.TrimStart('#');
                if (h.Length == 6)
                {
                    byte r = Convert.ToByte(h.Substring(0, 2), 16);
                    byte g = Convert.ToByte(h.Substring(2, 2), 16);
                    byte b = Convert.ToByte(h.Substring(4, 2), 16);
                    return new Scalar(b, g, r);   // OpenCV uses BGR
                }
            }
            catch { }
            return new Scalar(246, 130, 59);  // fallback: #3B82F6 in BGR
        }

        private static void DrawBox(Mat img, OpenCvSharp.Rect rect,
            Core_Aim.Services.Configuration.AppSettingsService cfg)
        {
            int thick = Math.Clamp(cfg.BoxThickness, 1, 3);

            // ── Preenchimento semi-transparente (independente do estilo) ──────
            if (cfg.BoxFillEnabled)
            {
                var cr = OpenCvSharp.Rect.Intersect(rect,
                    new OpenCvSharp.Rect(0, 0, img.Width, img.Height));
                if (cr.Width > 0 && cr.Height > 0)
                {
                    using var roi     = new Mat(img, cr);
                    using var overlay = new Mat(roi.Size(), roi.Type(), BoxColor);
                    double alpha = Math.Clamp(cfg.BoxFillAlpha, 5, 200) / 255.0;
                    Cv2.AddWeighted(roi, 1.0 - alpha, overlay, alpha, 0, roi);
                }
            }

            // ── Contorno ──────────────────────────────────────────────────────
            switch (cfg.BoxStyle)
            {
                case 1: DrawCorners(img, rect, BoxColor, thick);     break;
                case 2: DrawRoundedRect(img, rect, BoxColor, thick); break;
                default: Cv2.Rectangle(img, rect, BoxColor, thick, LineTypes.AntiAlias); break;
            }
        }

        // Apenas os 4 cantos em L
        private static void DrawCorners(Mat img, OpenCvSharp.Rect r, Scalar c, int t)
        {
            int lx = Math.Max(r.Width  / 5, 6);
            int ly = Math.Max(r.Height / 5, 6);
            int x = r.X, y = r.Y, x2 = r.X + r.Width, y2 = r.Y + r.Height;

            Cv2.Line(img, new OpenCvSharp.Point(x,      y),  new OpenCvSharp.Point(x + lx, y),  c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x,      y),  new OpenCvSharp.Point(x,  y + ly), c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x2,     y),  new OpenCvSharp.Point(x2 - lx, y), c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x2,     y),  new OpenCvSharp.Point(x2, y + ly), c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x,      y2), new OpenCvSharp.Point(x + lx, y2), c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x,      y2), new OpenCvSharp.Point(x, y2 - ly), c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x2,     y2), new OpenCvSharp.Point(x2 - lx, y2),c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x2,     y2), new OpenCvSharp.Point(x2, y2 - ly),c, t);
        }

        // Retângulo com cantos arredondados (linhas retas + arcos nos cantos)
        private static void DrawRoundedRect(Mat img, OpenCvSharp.Rect r, Scalar c, int t)
        {
            int rad = Math.Max(Math.Min(r.Width, r.Height) / 6, 4);
            int x = r.X, y = r.Y, w = r.Width, h = r.Height;
            var axes = new OpenCvSharp.Size(rad, rad);

            Cv2.Line(img, new OpenCvSharp.Point(x + rad, y),     new OpenCvSharp.Point(x + w - rad, y),     c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x + rad, y + h), new OpenCvSharp.Point(x + w - rad, y + h), c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x, y + rad),     new OpenCvSharp.Point(x, y + h - rad),     c, t);
            Cv2.Line(img, new OpenCvSharp.Point(x + w, y + rad), new OpenCvSharp.Point(x + w, y + h - rad), c, t);

            Cv2.Ellipse(img, new OpenCvSharp.Point(x + rad,     y + rad    ), axes, 0, 180, 270, c, t, LineTypes.AntiAlias);
            Cv2.Ellipse(img, new OpenCvSharp.Point(x + w - rad, y + rad    ), axes, 0, 270, 360, c, t, LineTypes.AntiAlias);
            Cv2.Ellipse(img, new OpenCvSharp.Point(x + rad,     y + h - rad), axes, 0,  90, 180, c, t, LineTypes.AntiAlias);
            Cv2.Ellipse(img, new OpenCvSharp.Point(x + w - rad, y + h - rad), axes, 0,   0,  90, c, t, LineTypes.AntiAlias);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Renderização na UI — byte[] double-buffer pré-alocado (zero LOH)
        // ═════════════════════════════════════════════════════════════════════

        private void RenderFrame(Mat bgr)
        {
            if (_systemCts == null || _systemCts.IsCancellationRequested) return;
            if (bgr == null || bgr.Empty()) return;

            // Descarta se já há um frame aguardando no dispatcher — evita saturar a UI
            if (Interlocked.Exchange(ref _renderPending, 1) == 1) return;

            try
            {
                Mat finalMat     = bgr;
                bool needDispose = false;
                if (bgr.Type() != MatType.CV_8UC3)
                {
                    finalMat    = new Mat();
                    bgr.ConvertTo(finalMat, MatType.CV_8UC3);
                    needDispose = true;
                }

                int w      = finalMat.Width;
                int h      = finalMat.Height;
                int stride = (int)finalMat.Step();
                int bytes  = stride * h;

                ref byte[]? slot = ref (_dispBufIdx == 0 ? ref _dispBuf0 : ref _dispBuf1);
                if (slot == null || slot.Length != bytes) slot = new byte[bytes];
                byte[] buf = slot;

                Marshal.Copy(finalMat.Data, buf, 0, bytes);
                if (needDispose) finalMat.Dispose();

                _dispBufIdx = 1 - _dispBufIdx;

                int fw = w, fh = h, fs = stride;
                _ = _uiDispatcher.InvokeAsync(() =>
                {
                    Interlocked.Exchange(ref _renderPending, 0);
                    if (MonitorViewModel.CameraFeed == null ||
                        MonitorViewModel.CameraFeed.PixelWidth  != fw ||
                        MonitorViewModel.CameraFeed.PixelHeight != fh)
                    {
                        MonitorViewModel.CameraFeed = new WriteableBitmap(
                            fw, fh, 96, 96, PixelFormats.Bgr24, null);
                    }
                    MonitorViewModel.CameraFeed.WritePixels(
                        new System.Windows.Int32Rect(0, 0, fw, fh), buf, fs, 0);
                }, DispatcherPriority.Render);
            }
            catch { Interlocked.Exchange(ref _renderPending, 0); }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Cleanup — devolve frames aos pools; pools ainda devem estar vivos
        // ═════════════════════════════════════════════════════════════════════

        private void CleanupResources()
        {
            // Devolve inferSlot ao InferPool
            var inferPool = _inferPool;
            _inferPool = null;
            var oldInfer = Interlocked.Exchange(ref _inferSlot, null);
            if (inferPool != null) { inferPool.Return(oldInfer); inferPool.Dispose(); }
            else oldInfer?.Dispose();

            _inferSignal.Reset();

            // Devolve rawFrame ao CameraPool (CameraPool ainda está vivo — StopCaptureAsync vem depois)
            Mat? rawFrame;
            lock (_rawFrameLock) { rawFrame = _latestRawFrame; _latestRawFrame = null; }
            _cameraService.Pool?.Return(rawFrame);

            // Nula draw buffers (não faz Dispose — podem ainda estar em uso por InvokeAsync)
            _drawBuf0 = null; _drawBuf1 = null; _drawBufIdx = 0;

            Interlocked.Exchange(ref _latestDetections, _emptyDetections);
            _systemCts?.Dispose();
            _systemCts = null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helpers
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Check if image is blurry using Laplacian variance.</summary>
        private static bool IsBlurry(Mat img, double threshold)
        {
            using var gray = new Mat();
            if (img.Channels() > 1)
                Cv2.CvtColor(img, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
            else
                img.CopyTo(gray);

            using var lap = new Mat();
            Cv2.Laplacian(gray, lap, OpenCvSharp.MatType.CV_64F);
            Cv2.MeanStdDev(lap, out _, out var stddev);
            double variance = stddev.Val0 * stddev.Val0;
            return variance < threshold;
        }

        /// <summary>Save PixelBot blobs as YOLO labels (class 0) relative to the crop region.</summary>
        /// <summary>
        /// Convert PB blobs to padded + dedup'd label lines (class 0).
        /// Same 8px padding and union-merge as the display drawing.
        /// </summary>
        private static List<string> PbBlobsToLabelLines(IReadOnlyList<Services.AI.PixelBotBlob> blobs, int cropX, int cropY, int cropSize)
        {
            const int pad = 4; // same as display drawing

            // Build expanded rects + merge overlapping (same logic as DisplayLoop)
            var rects = new List<(float x, float y, float w, float h)>();
            foreach (var blob in blobs)
            {
                float bx = blob.Bounds.X - cropX - pad;
                float by = blob.Bounds.Y - cropY - pad;
                float bw = blob.Bounds.Width  + pad * 2;
                float bh = blob.Bounds.Height + pad * 2;

                bool merged = false;
                for (int i = 0; i < rects.Count; i++)
                {
                    var (ex, ey, ew, eh) = rects[i];
                    if (bx < ex + ew && bx + bw > ex && by < ey + eh && by + bh > ey)
                    {
                        float ux  = Math.Min(ex, bx);
                        float uy  = Math.Min(ey, by);
                        float ux2 = Math.Max(ex + ew, bx + bw);
                        float uy2 = Math.Max(ey + eh, by + bh);
                        rects[i] = (ux, uy, ux2 - ux, uy2 - uy);
                        merged = true;
                        break;
                    }
                }
                if (!merged) rects.Add((bx, by, bw, bh));
            }

            var lines = new List<string>();
            foreach (var (rx, ry, rw, rh) in rects)
            {
                float ncx = (rx + rw / 2f) / cropSize;
                float ncy = (ry + rh / 2f) / cropSize;
                float nw  = rw / cropSize;
                float nh  = rh / cropSize;

                if (ncx < 0 || ncx > 1 || ncy < 0 || ncy > 1) continue;
                nw = Math.Min(nw, 1f); nh = Math.Min(nh, 1f);

                lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "0 {0:F6} {1:F6} {2:F6} {3:F6}", ncx, ncy, nw, nh));
            }
            return lines;
        }

        private static void SavePbLabels(string jpgPath, IReadOnlyList<Services.AI.PixelBotBlob>? blobs, int cropX, int cropY, int cropSize)
        {
            if (blobs == null || blobs.Count == 0) return;
            var lines = PbBlobsToLabelLines(blobs, cropX, cropY, cropSize);
            if (lines.Count > 0)
                File.WriteAllLines(Path.ChangeExtension(jpgPath, ".txt"), lines);
        }

        /// <summary>Save YOLO + PixelBot detections as YOLO labels relative to the crop region.</summary>
        private static void SaveDetectionLabels(string jpgPath, List<YoloDetectionResult>? dets,
            IReadOnlyList<Services.AI.PixelBotBlob>? pbBlobs, int cropX, int cropY, int cropSize)
        {
            var txtPath = Path.ChangeExtension(jpgPath, ".txt");
            var lines = new List<string>();

            // YOLO detections
            if (dets != null)
            foreach (var d in dets)
            {
                float bx = d.BoundingBox.X - cropX;
                float by = d.BoundingBox.Y - cropY;
                float bw = d.BoundingBox.Width;
                float bh = d.BoundingBox.Height;

                float ncx = (bx + bw / 2f) / cropSize;
                float ncy = (by + bh / 2f) / cropSize;
                float nw  = bw / cropSize;
                float nh  = bh / cropSize;

                if (ncx < 0 || ncx > 1 || ncy < 0 || ncy > 1) continue;
                nw = Math.Min(nw, 1f); nh = Math.Min(nh, 1f);

                lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0} {1:F6} {2:F6} {3:F6} {4:F6}", d.ClassId, ncx, ncy, nw, nh));
            }

            // PixelBot blobs (class 0) — padded + dedup'd, same as display
            if (pbBlobs != null && pbBlobs.Count > 0)
                lines.AddRange(PbBlobsToLabelLines(pbBlobs, cropX, cropY, cropSize));

            if (lines.Count > 0)
                File.WriteAllLines(txtPath, lines);
        }

        private bool ShouldInfer()
        {
            int mode = _appSettings.InferenceMode;
            if (mode == 3) return false;
            if (mode == 1 || mode == 2) return true;
            return _movementService?.IsConnected == true && _movementService.IsFiring;
        }

        private void SwitchToSettings() => CurrentViewModel = SettingsViewModel;
        private void SwitchToMonitor()  => CurrentViewModel = MonitorViewModel;
        private void UpdateNavigationProperties()
        {
            OnPropertyChanged(nameof(IsSettingsActive));
            OnPropertyChanged(nameof(IsMonitorActive));
        }
        private bool CanStartSystem(object? p) => !_cameraService.IsRunning;
        private bool CanStopSystem(object? p)  =>  _cameraService.IsRunning;
    }
}
