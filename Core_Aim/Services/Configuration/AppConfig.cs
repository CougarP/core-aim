using System.Text.Json.Serialization;

namespace Core_Aim.Services.Configuration
{
    public class AppConfig
    {
        [JsonPropertyName("selectedModel")]
        public string? SelectedModel { get; set; } = "yolov8n.onnx";

        [JsonPropertyName("confidenceThreshold")]
        public double ConfidenceThreshold { get; set; } = 0.5;

        [JsonPropertyName("inferenceMode")]
        public int InferenceMode { get; set; } = 0;

        [JsonPropertyName("triggerSelection")]
        public int TriggerSelection { get; set; } = 2;

        [JsonPropertyName("saveDetections")]
        public bool SaveDetections { get; set; } = false;

        [JsonPropertyName("saveEnemyOnly")]
        public bool SaveEnemyOnly { get; set; } = true;

        [JsonPropertyName("datasetCropSize")]
        public int DatasetCropSize { get; set; } = 640;

        [JsonPropertyName("saveLabels")]
        public bool SaveLabels { get; set; } = false;

        [JsonPropertyName("saveLabelYolo")]
        public bool SaveLabelYolo { get; set; } = true;

        [JsonPropertyName("saveLabelPixelBot")]
        public bool SaveLabelPixelBot { get; set; } = true;

        [JsonPropertyName("skipBlurryImages")]
        public bool SkipBlurryImages { get; set; } = true;

        [JsonPropertyName("blurThreshold")]
        public double BlurThreshold { get; set; } = 100.0;

        [JsonPropertyName("aimCurveType")]
        public int AimCurveType { get; set; } = 0;

        // Zona morta em pixels reais de tela — ignora micro-jitter
        [JsonPropertyName("aimDeadZone")]
        public double AimDeadZone { get; set; } = 2.0;

        [JsonPropertyName("hardwareType")]
        public int HardwareType { get; set; } = 0;

        [JsonPropertyName("kmBoxIp")]
        public string KmBoxIp { get; set; } = "192.168.2.188";

        [JsonPropertyName("kmBoxPort")]
        public int KmBoxPort { get; set; } = 8888;

        [JsonPropertyName("kmBoxUuid")]
        public string KmBoxUuid { get; set; } = "XXXXXXXX";

        [JsonPropertyName("fovSize")]
        public double FovSize { get; set; } = 300.0;

        [JsonPropertyName("showFov")]
        public bool ShowFov { get; set; } = true;

        [JsonPropertyName("selectedCameraIndex")]
        public int SelectedCameraIndex { get; set; } = 0;

        [JsonPropertyName("aimOffsetY")]
        public double AimOffsetY { get; set; } = 25.0;

        [JsonPropertyName("isIconMode")]
        public bool IsIconMode { get; set; } = false;

        [JsonPropertyName("offsetValuePlayer")]
        public double OffsetValuePlayer { get; set; } = 25.0;

        [JsonPropertyName("offsetValueIcon")]
        public double OffsetValueIcon { get; set; } = 25.0;

        // Kp=1.0 → comportamento idêntico ao sistema proporcional original
        // Ki=0.0 / Kd=0.0 → desativados por padrão, usuário ativa conforme necessário
        [JsonPropertyName("pidKp")]
        public double PidKp { get; set; } = 1.0;

        [JsonPropertyName("pidKi")]
        public double PidKi { get; set; } = 0.0;

        [JsonPropertyName("pidKd")]
        public double PidKd { get; set; } = 0.0;

        // EMA alpha: 0.1 = muito suave/lento, 0.9 = muito reativo/com jitter
        // Exposto na UI como "Suavidade do Alvo"
        [JsonPropertyName("aimResponseCurve")]
        public double AimResponseCurve { get; set; } = 0.40;

        [JsonPropertyName("titanTwoProtocol")]
        public string TitanTwoProtocol { get; set; } = "Auto";

        [JsonPropertyName("titanTwoInputPollRate")]
        public string TitanTwoInputPollRate { get; set; } = "Default";

        [JsonPropertyName("titanTwoOutputPollRate")]
        public string TitanTwoOutputPollRate { get; set; } = "Default";

        [JsonPropertyName("titanTwoSlot")]
        public int TitanTwoSlot { get; set; } = 1;

        // ── Macros GPC (flags enviados via GCV ao Titan Two) ─────────
        [JsonPropertyName("macroRapidFire")]
        public bool MacroRapidFire { get; set; } = false;

        [JsonPropertyName("macroAntiRecoil")]
        public bool MacroAntiRecoil { get; set; } = false;

        [JsonPropertyName("macroDropShot")]
        public bool MacroDropShot { get; set; } = false;

        [JsonPropertyName("macroCrouchPeek")]
        public bool MacroCrouchPeek { get; set; } = false;

        [JsonPropertyName("macroTacSprint")]
        public bool MacroTacSprint { get; set; } = false;

        [JsonPropertyName("macroSlideCancel")]
        public bool MacroSlideCancel { get; set; } = false;

        [JsonPropertyName("macroAutoPing")]
        public bool MacroAutoPing { get; set; } = false;

        [JsonPropertyName("macroArVertical")]
        public double MacroArVertical { get; set; } = 15.0;

        [JsonPropertyName("macroArHorizontal")]
        public double MacroArHorizontal { get; set; } = 0.0;

        [JsonPropertyName("macroRapidFireMs")]
        public double MacroRapidFireMs { get; set; } = 40.0;

        [JsonPropertyName("playerBlend")]
        public double PlayerBlend { get; set; } = 0.2;

        [JsonPropertyName("disableOnMove")]
        public double DisableOnMove { get; set; } = 70.0;

        [JsonPropertyName("quickscopeDelay")]
        public double QuickscopeDelay { get; set; } = 500.0;

        [JsonPropertyName("jumpShotDelay")]
        public double JumpShotDelay { get; set; } = 300.0;

        [JsonPropertyName("bunnyHopDelay")]
        public double BunnyHopDelay { get; set; } = 100.0;

        [JsonPropertyName("strafePower")]
        public double StrafePower { get; set; } = 40.0;

        // ── Novas macros v4 ──
        [JsonPropertyName("macroJumpShot")]
        public bool MacroJumpShot { get; set; } = false;

        [JsonPropertyName("macroBunnyHop")]
        public bool MacroBunnyHop { get; set; } = false;

        [JsonPropertyName("macroQuickscope")]
        public bool MacroQuickscope { get; set; } = false;

        [JsonPropertyName("macroAutoBreath")]
        public bool MacroAutoBreath { get; set; } = false;

        [JsonPropertyName("macroAutoReload")]
        public bool MacroAutoReload { get; set; } = false;

        [JsonPropertyName("macroAutoMelee")]
        public bool MacroAutoMelee { get; set; } = false;

        [JsonPropertyName("macroAutoADS")]
        public bool MacroAutoADS { get; set; } = false;

        [JsonPropertyName("macroHairTrigger")]
        public bool MacroHairTrigger { get; set; } = false;

        [JsonPropertyName("macroStrafeShot")]
        public bool MacroStrafeShot { get; set; } = false;

        [JsonPropertyName("macroCookProtection")]
        public bool MacroCookProtection { get; set; } = false;

        [JsonPropertyName("macroDeadZoneRemoval")]
        public bool MacroDeadZoneRemoval { get; set; } = false;

        [JsonPropertyName("autoUploadGpc")]
        public bool AutoUploadGpc { get; set; } = true;

        [JsonPropertyName("inferenceIntervalMs")]
        public int InferenceIntervalMs { get; set; } = 10;

        [JsonPropertyName("trackingIntervalMs")]
        public int TrackingIntervalMs { get; set; } = 12;

        [JsonPropertyName("showBoundingBoxes")]
        public bool ShowBoundingBoxes { get; set; } = true;

        // 0 = quadrado  1 = apenas cantos  2 = arredondado
        [JsonPropertyName("boxStyle")]
        public int BoxStyle { get; set; } = 0;

        // Espessura da linha: 1, 2 ou 3
        [JsonPropertyName("boxThickness")]
        public int BoxThickness { get; set; } = 2;

        // Preenchimento semi-transparente independente do estilo
        [JsonPropertyName("boxFillEnabled")]
        public bool BoxFillEnabled { get; set; } = false;

        // Opacidade do preenchimento: 10–200 (escala 0–255)
        [JsonPropertyName("boxFillAlpha")]
        public int BoxFillAlpha { get; set; } = 40;

        [JsonPropertyName("showFps")]
        public bool ShowFps { get; set; } = true;

        [JsonPropertyName("showDetectionInfo")]
        public bool ShowDetectionInfo { get; set; } = true;

        [JsonPropertyName("colorFilterEnabled")]
        public bool ColorFilterEnabled { get; set; } = false;

        [JsonPropertyName("colorFilterHex")]
        public string ColorFilterHex { get; set; } = "#FF0000";

        [JsonPropertyName("colorTolerance")]
        public int ColorTolerance { get; set; } = 15;

        [JsonPropertyName("speedProfile")]
        public string? SpeedProfile { get; set; } = "Flat";

        // Velocidade de rastreio: fração do erro corrigida por frame (0.05–0.88)
        // Com speed < 1.0 é matematicamente impossível ultrapassar o alvo
        [JsonPropertyName("trackingSpeedHIP")]
        public double TrackingSpeedHIP { get; set; } = 0.35;

        [JsonPropertyName("trackingSpeedADS")]
        public double TrackingSpeedADS { get; set; } = 0.3;

        // ── PB + KmBox ────────────────────────────────────────────────────────
        // Velocidade: escala inteira 1–100, interno /1000 (→ 0.001–0.100 px/frame)
        // Suavidade:  escala inteira 1–99,  alpha = 1 − (valor/100)
        [JsonPropertyName("pixelBotTrackingSpeed")]
        public double PixelBotTrackingSpeed { get; set; } = 35;

        [JsonPropertyName("pixelBotAimResponse")]
        public double PixelBotAimResponse { get; set; } = 60;

        [JsonPropertyName("pixelBotDeadZone")]
        public double PixelBotDeadZone { get; set; } = 2.0;

        // ── PB + Titan Two ────────────────────────────────────────────────────
        // Velocidade: escala natural do TT2 (0.10–3.00), igual ao TT2SpeedHIP
        // Suavidade:  escala inteira 1–99,  alpha = 1 − (valor/100)
        [JsonPropertyName("pixelBotTrackingSpeedTT2")]
        public double PixelBotTrackingSpeedTT2 { get; set; } = 0.35;

        [JsonPropertyName("pixelBotAimResponseTT2")]
        public double PixelBotAimResponseTT2 { get; set; } = 60;

        [JsonPropertyName("pixelBotDeadZoneTT2")]
        public double PixelBotDeadZoneTT2 { get; set; } = 2.0;

        [JsonPropertyName("restrictedTracking")]
        public int RestrictedTracking { get; set; } = 100;

        [JsonPropertyName("trackingDelay")]
        public int TrackingDelay { get; set; } = 0;

        [JsonPropertyName("predictionEnabled")]
        public bool PredictionEnabled { get; set; } = false;

        [JsonPropertyName("predictionMethod")]
        public string? PredictionMethod { get; set; } = "Kalman Filter";

        // ── Recoil KMBox ─────────────────────────────────────────────────
        [JsonPropertyName("recoilEnabledKm")]
        public bool RecoilEnabledKm { get; set; } = false;

        [JsonPropertyName("recoilVerticalKm")]
        public int RecoilVerticalKm { get; set; } = 0;

        [JsonPropertyName("recoilHorizontalKm")]
        public int RecoilHorizontalKm { get; set; } = 0;

        [JsonPropertyName("recoilIntervalKm")]
        public int RecoilIntervalKm { get; set; } = 35;

        // ── Recoil Titan Two ─────────────────────────────────────────────
        [JsonPropertyName("recoilEnabledTT2")]
        public bool RecoilEnabledTT2 { get; set; } = false;

        [JsonPropertyName("recoilVerticalTT2")]
        public int RecoilVerticalTT2 { get; set; } = 0;

        [JsonPropertyName("recoilHorizontalTT2")]
        public int RecoilHorizontalTT2 { get; set; } = 0;

        [JsonPropertyName("recoilIntervalTT2")]
        public int RecoilIntervalTT2 { get; set; } = 35;

        // ── Legacy (mantido só para compatibilidade JSON) ─────────────────
        [JsonPropertyName("recoilEnabled")]
        public bool RecoilEnabled { get; set; } = false;

        [JsonPropertyName("recoilHorizontalForce")]
        public int RecoilHorizontalForce { get; set; } = 0;

        [JsonPropertyName("recoilVerticalForce")]
        public int RecoilVerticalForce { get; set; } = 0;

        [JsonPropertyName("recoilInterval")]
        public int RecoilInterval { get; set; } = 35;

        // ── Idioma / Localização ──────────────────────────────────────
        [JsonPropertyName("language")]
        public string Language { get; set; } = "pt";

        // ── Splash style (Phantom): "" = não escolhido (abre picker)
        //    "neural"  → Neural Boot      (telemetry boot log + glitch reveal)
        //    "reactor" → Reactor Ignition (rotating rings + collapse flash)
        //    "holo"    → Holographic Assembly (wireframe CA monogram fill)
        [JsonPropertyName("splashStyle")]
        public string SplashStyle { get; set; } = "";

        // ── Métricas overlay ─────────────────────────────────────────
        [JsonPropertyName("showMetrics")]
        public bool ShowMetrics { get; set; } = true;

        // ── Preview FPS (30 | 60 | 120 | 240) ────────────────────────
        [JsonPropertyName("previewFps")]
        public int PreviewFps { get; set; } = 60;

        // ── FOV visual ───────────────────────────────────────────────
        // Espessura: 1, 2 ou 3
        [JsonPropertyName("fovThickness")]
        public int FovThickness { get; set; } = 2;

        // Estilo: 0 = círculo, 1 = quadrado
        [JsonPropertyName("fovStyle")]
        public int FovStyle { get; set; } = 0;

        // Cor do FOV em hex (#RRGGBB)
        [JsonPropertyName("fovColorHex")]
        public string FovColorHex { get; set; } = "#3B82F6";

        [JsonPropertyName("captureMode")]
        public int CaptureMode { get; set; } = 0;

        // 0 = Auto | 1 = DSHOW | 2 = DSHOW+MJPEG | 3 = MSMF | 4 = MSMF+MJPEG
        [JsonPropertyName("captureBackend")]
        public int CaptureBackend { get; set; } = 0;

        // Resolução alvo — livre, suporta qualquer placa (SD, HD, 4K, 8K…)
        [JsonPropertyName("captureWidth")]
        public int CaptureWidth  { get; set; } = 1920;

        [JsonPropertyName("captureHeight")]
        public int CaptureHeight { get; set; } = 1080;

        // FPS alvo — livre (1-480+); placa de captura pode ir a 240fps ou mais
        [JsonPropertyName("captureFps")]
        public int CaptureFps { get; set; } = 60;

        // Configurações avançadas OpenCV (key=value por linha)
        // Ex: CAP_PROP_FPS=60   CAP_PROP_FOURCC=MJPG
        [JsonPropertyName("captureAdvancedConfig")]
        public string CaptureAdvancedConfig { get; set; } = "CAP_PROP_FPS=60";

        // Índice da GPU para inferência (0 = primeira GPU, 1 = segunda GPU, etc.)
        [JsonPropertyName("gpuDeviceId")]
        public int GpuDeviceId { get; set; } = 0;

        // ADRC — ganho do canal de controle (força de rejeição de distúrbios)
        // Padrão 21.7 = valor do projeto referência C:\Projeto-ONNX-DML\src
        [JsonPropertyName("adrcB0")]
        public double AdrcB0 { get; set; } = 21.7;

        // Auto-Tune — ajuste automático de Kp e b0 em tempo real
        [JsonPropertyName("autoTuneEnabled")]
        public bool AutoTuneEnabled { get; set; } = true;

        // Tecla para sair do modo tela cheia (combinada com Ctrl)
        [JsonPropertyName("fullscreenKey")]
        public string FullscreenKey { get; set; } = "F";

        // ── Configurações exclusivas do Titan Two ─────────────────────────────
        // Separadas das config do KMBox para não conflitar
        // KMBox: trabalha em pixels de tela reais
        // TT2  : trabalha em valores normalizados de stick (-100 a 100)
        [JsonPropertyName("tt2SpeedHIP")]
        public double TT2SpeedHIP { get; set; } = 0.5;

        [JsonPropertyName("tt2SpeedADS")]
        public double TT2SpeedADS { get; set; } = 0.3;

        // Multiplicador aplicado sobre o output final — força a mira a entrar na deadzone
        // mesmo quando perto do alvo. 1.0 = neutro · 3.0 = padrão recomendado
        [JsonPropertyName("tt2SpeedBoost")]
        public double TT2SpeedBoost { get; set; } = 3.0;

        // Multiplicador de hardware — escala o delta do modelo para o range do stick
        [JsonPropertyName("tt2HardwareSensitivity")]
        public double TT2HardwareSensitivity { get; set; } = 1.0;

        // Deadzone do jogo — compensação para evitar que o stick fique preso no centro
        // 0 = desligado (padrão); só ativar se o jogo ignorar valores pequenos de stick
        [JsonPropertyName("tt2GameDeadzone")]
        public double TT2GameDeadzone { get; set; } = 0.0;

        // Curva de resposta (1.0 = linear, >1.0 = agressivo nos erros grandes)
        [JsonPropertyName("tt2ResponseCurve")]
        public double TT2ResponseCurve { get; set; } = 1.0;

        [JsonPropertyName("tt2Kd")]
        public double TT2Kd { get; set; } = 0.0;

        // Modelo de curva de resposta do TT2 (0=Linear, 1=Quadrático, 2=Raiz, 3=S-Curve,
        //                                      4=Exponencial, 5=Bang+Linear, 6=Cúbico)
        [JsonPropertyName("tt2CurveModel")]
        public int TT2CurveModel { get; set; } = 0;

        // Feedforward de velocidade: aplica a velocidade do alvo diretamente ao stick,
        // independente da distância. Evita desaceleração próxima ao alvo e mantém a mira
        // travada num alvo em movimento. 0 = desligado · 2.5 = padrão recomendado.
        [JsonPropertyName("tt2Kff")]
        public double TT2Kff { get; set; } = 2.5;

        // Força mínima de stick enviada ao TT2 quando há erro fora da deadzone do modelo.
        // Supera a deadzone interna do jogo que ignora valores pequenos de stick.
        // Log mostrou erro travado em 7-9px porque stickX=14 está abaixo da deadzone do jogo.
        // 0 = desligado · 20 = padrão recomendado · range 0–50 (% de stick, 0–100)
        [JsonPropertyName("tt2MinStick")]
        public double TT2MinStick { get; set; } = 20.0;

        // Velocidade máxima permitida (% de stick, 0–100).
        // Separa o ganho (Kp) da velocidade máxima: aumenta Kp sem causar wobble.
        // Longe do alvo: clamped ao máximo → velocidade constante (bang).
        // Perto do alvo: saída proporcional < máximo → convergência suave sem overshoot.
        // Padrão 60 = bom equilíbrio. Reduzir se ainda balançar; aumentar para mais velocidade.
        [JsonPropertyName("tt2MaxOutput")]
        public double TT2MaxOutput { get; set; } = 60.0;

        // ── PixelBot — detecção de alvos por cor (HSV) ────────────────────────
        // Modo 0 = Apenas YOLO  (PixelBot desativado)
        // Modo 1 = Prioridade   (YOLO principal, PB como fallback)
        // Modo 2 = Apenas PB    (ignora YOLO para rastreio)
        [JsonPropertyName("pixelBotEnabled")]
        public bool PixelBotEnabled { get; set; } = false;   // legado — modo > 0 implica enabled

        [JsonPropertyName("pixelBotMode")]
        public int PixelBotMode { get; set; } = 0;

        // Valores padrão = idênticos ao Python PixelBot
        [JsonPropertyName("pixelBotColorHex")]
        public string PixelBotColorHex { get; set; } = "#921b7d";

        [JsonPropertyName("pixelBotColorTolerance")]
        public int PixelBotColorTolerance { get; set; } = 20;

        [JsonPropertyName("pixelBotMinArea")]
        public int PixelBotMinArea { get; set; } = 25;

        [JsonPropertyName("pixelBotMaxArea")]
        public int PixelBotMaxArea { get; set; } = 1500;

        [JsonPropertyName("pixelBotMinAspectRatio")]
        public double PixelBotMinAspectRatio { get; set; } = 0.800;

        [JsonPropertyName("pixelBotMinSolidity")]
        public double PixelBotMinSolidity { get; set; } = 0.900;

        [JsonPropertyName("pixelBotMinCircularity")]
        public double PixelBotMinCircularity { get; set; } = 0.650;

        [JsonPropertyName("pixelBotVerticalOffset")]
        public int PixelBotVerticalOffset { get; set; } = 25;  // percentual (0-100)

        [JsonPropertyName("pixelBotMaxFramesLost")]
        public int PixelBotMaxFramesLost { get; set; } = 6;

        // HSV — min_s e min_v: Python usa max(valor-70, 50), logo mínimo efetivo = 50
        [JsonPropertyName("pixelBotSatMin")]
        public int PixelBotSatMin { get; set; } = 50;

        [JsonPropertyName("pixelBotValMin")]
        public int PixelBotValMin { get; set; } = 50;

        // Raio de persistência (px): novo blob dentro desse raio = mesmo alvo.
        [JsonPropertyName("pixelBotPersistRadius")]
        public int PixelBotPersistRadius { get; set; } = 35;

        // ── Label Maker ──
        [JsonPropertyName("labelShowLabels")]
        public bool LabelShowLabels { get; set; } = true;

        [JsonPropertyName("labelShowCrosshair")]
        public bool LabelShowCrosshair { get; set; } = true;

        [JsonPropertyName("labelConfThreshold")]
        public int LabelConfThreshold { get; set; } = 50;

        [JsonPropertyName("labelDisplayScale")]
        public int LabelDisplayScale { get; set; } = 100;

        [JsonPropertyName("labelImageIndex")]
        public int LabelImageIndex { get; set; } = 0;

        [JsonPropertyName("labelDatasetDir")]
        public string LabelDatasetDir { get; set; } = "";

        [JsonPropertyName("labelCheckedClasses")]
        public string LabelCheckedClasses { get; set; } = "";

        // ── Training ──
        [JsonPropertyName("trainEpochs")]
        public int TrainEpochs { get; set; } = 100;

        [JsonPropertyName("trainBatchSize")]
        public int TrainBatchSize { get; set; } = 16;

        [JsonPropertyName("trainImgSize")]
        public int TrainImgSize { get; set; } = 640;

        [JsonPropertyName("trainBaseModel")]
        public string TrainBaseModel { get; set; } = "yolov8n.pt";

        [JsonPropertyName("trainValPercent")]
        public int TrainValPercent { get; set; } = 20;
    }
}