using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Core_Aim.Services.Configuration;
using KMBox.NET;
using KMBox.NET.Structures;

namespace Core_Aim.Services.Hardware
{
    public enum KmBoxStatus { Disconnected, Connecting, Connected, Error }

    public class KmBoxService : IDisposable
    {
        private readonly AppSettingsService _settings;
        private KmBoxClient?   _client;
        private ReportListener? _listener;

        // Impede connect concorrente (ex.: clicar 2x rápido)
        private int _connectingFlag = 0;

        public KmBoxStatus Status { get; private set; } = KmBoxStatus.Disconnected;
        public Action<KmBoxStatus>? OnStatusChanged;

        public bool IsLeftDown  { get; private set; }
        public bool IsRightDown { get; private set; }

        public KmBoxService(AppSettingsService settings)
        {
            _settings = settings;
        }

        public async Task<bool> ConnectAsync()
        {
            // Garante apenas uma tentativa de conexão por vez
            if (Interlocked.CompareExchange(ref _connectingFlag, 1, 0) != 0)
                return false;

            try
            {
                UpdateStatus(KmBoxStatus.Connecting);

                string ip   = _settings.KmBoxIp;
                int    port = _settings.KmBoxPort;
                string mac  = _settings.KmBoxUuid;

                if (!IPAddress.TryParse(ip, out var ipAddr))
                {
                    System.Diagnostics.Debug.WriteLine("[KMBox] IP inválido.");
                    UpdateStatus(KmBoxStatus.Error);
                    return false;
                }

                // Desconecta sessão anterior se existir
                DisconnectInternal();

                var client      = new KmBoxClient(ipAddr, port, mac);
                var connectTask = client.Connect();

                // Timeout de 3 s — evita travar a UI indefinidamente
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask
                    || !await connectTask)
                {
                    System.Diagnostics.Debug.WriteLine("[KMBox] Timeout/falha ao conectar.");
                    UpdateStatus(KmBoxStatus.Error);
                    return false;
                }

                _client = client;
                StartInputListener();
                UpdateStatus(KmBoxStatus.Connected);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KMBox] Erro: {ex.Message}");
                UpdateStatus(KmBoxStatus.Error);
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _connectingFlag, 0);
            }
        }

        private void StartInputListener()
        {
            if (_client == null) return;
            try
            {
                StopListenerSafe();
                _listener = _client.CreateReportListener();
                _listener.EventListener = report =>
                {
                    var b       = report.MouseReport.Buttons;
                    IsLeftDown  = (b & (byte)MouseButton.MouseLeft)  != 0;
                    IsRightDown = (b & (byte)MouseButton.MouseRight) != 0;
                };
                _listener.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KMBox] Listener erro: {ex.Message}");
            }
        }

        // Para o listener num thread de fundo com timeout de 2 s
        private void StopListenerSafe()
        {
            var l = _listener;
            _listener = null;
            if (l == null) return;

            Task.Run(() =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(2000);
                    var t = Task.Run(() => { try { l.Stop(); } catch { } }, cts.Token);
                    t.Wait(2000);
                }
                catch { }
            }).Wait(2500); // aguarda até 2.5 s antes de continuar
        }

        public void Move(int x, int y)
        {
            var c = _client;
            if (Status != KmBoxStatus.Connected || c == null) return;
            try { _ = c.MouseMoveSimple((short)x, (short)y); } catch { }
        }

        public void Disconnect() => DisconnectInternal();

        private void DisconnectInternal()
        {
            IsLeftDown  = false;
            IsRightDown = false;

            // Para listener em background (não bloqueia quem chamou)
            var l = _listener;
            _listener = null;
            _client   = null;

            if (l != null)
                Task.Run(() => { try { l.Stop(); } catch { } });

            UpdateStatus(KmBoxStatus.Disconnected);
        }

        private void UpdateStatus(KmBoxStatus newStatus)
        {
            Status = newStatus;
            OnStatusChanged?.Invoke(newStatus);
        }

        public void Dispose() => DisconnectInternal();
    }
}
