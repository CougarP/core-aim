using System;
using System.Drawing;
using System.Threading.Tasks;
using Core_Aim.Services.Hardware;

namespace Core_Aim.Services.Movements
{
    public interface IMovementService : IDisposable
    {
        bool IsConnected { get; }
        bool IsFiring { get; }

        int LTValue { get; }
        int RTValue { get; }

        TT2StickState GetStickState();
        TT2ButtonState GetButtonState();
        TT2Info GetDeviceInfo();

        // Evento para atualizar a UI (Verde/Vermelho)
        event Action<bool> OnConnectionStateChanged;

        Task<bool> InitializeAsync();

        // isPixelBot=true → usa PixelBotTrackingSpeed/AimResponse/DeadZone
        // isPixelBot=false → usa TrackingSpeedHIP/AimResponseCurve/AimDeadZone (YOLO)
        void Move(PointF rawTarget, int modelWidth, int modelHeight, bool isPixelBot = false);
        void HandleRecoil();

        // Flush de recoil pendente sem alterar o vetor de rastreio.
        // Chamado quando não há detecção nova mas recoil pode estar pendente.
        // KMBox: no-op. TitanTwo: envia recoil acumulado.
        void FlushRecoil();

        // Indica se há offset de anti-recoil ativo (RT pressionado).
        bool HasActiveRecoil { get; }

        // OBRIGATÓRIO: Enviar 0,0 para o hardware
        void Stop();

        void ResetPrediction();

        // Log de diagnóstico — registra erro/stick por frame no CSV
        bool IsLogging { get; }
        void StartLogging();
        void StopLogging();

        // Define os flags de macro GPC (bitmask)
        void SetMacroFlags(uint flags);

        // Ajustes de macro GPC
        void SetMacroArVertical(double val);
        void SetMacroArHorizontal(double val);
        void SetMacroRapidFireMs(double val);
        void SetPlayerBlend(double val);
        void SetDisableOnMove(double val);
        void SetQuickscopeDelay(double val);
        void SetJumpShotDelay(double val);
        void SetBunnyHopDelay(double val);
        void SetStrafePower(double val);

        // Upload de GPC bytecode para um slot
        bool UploadGpc(byte[] data, byte slot, string name, string author);
        bool UploadGbc(byte[] gbcData, byte slot);

        // ── Slot management (delegado ao TitanTwo quando disponível) ──
        bool SlotLoad(byte slot);
        bool SlotUp();
        bool SlotDown();
        bool SlotUnload();

        // ── Config passthrough (aplicar settings no dispositivo ativo) ──
        bool SetOutputProtocol(TT2_Proto protocol);
        bool SetOutputPolling(TT2_Poll poll);
        bool SetInputPolling(TT2_Poll poll);
        bool SetHighSpeedMode(bool enable);
        void SetInvertX(bool value);
        void SetInvertY(bool value);
        void SetClampMagnitude(double value);

        // AI output stick values (-100..+100) — última saída enviada ao hardware
        int AiOutputX { get; }
        int AiOutputY { get; }

        // Retorna e zera o contador de envios reais ao hardware desde a última chamada.
        // Usado pelo loop de métricas para calcular taxa de envio (sends/s).
        int DrainSendCount();
    }
}