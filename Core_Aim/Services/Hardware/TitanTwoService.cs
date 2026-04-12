using System;
using System.Threading;
using System.Threading.Tasks;
using Core_Aim.Services.Configuration;

namespace Core_Aim.Services.Hardware
{
    public class TitanTwoService : IDisposable
    {
        private readonly AppSettingsService _appSettings;
        private TitanTwo? _titanTwo;
        private TT2Config _config;
        private readonly object _sync = new object();
        private bool _disposed;

        // Status polling
        private System.Threading.Timer? _statusTimer;
        private TT2Status _lastPolledStatus = TT2Status.Disconnected;

        public event EventHandler<TT2ButtonEvent>? OnButtonEvent;
        public event Action<TT2Status>? OnStatusChanged;

        public TitanTwoService(AppSettingsService appSettings)
        {
            _appSettings = appSettings;
            _config = new TT2Config
            {
                exclusiveHandle = false,
                rateHz = 1000,
                invertX = false,
                invertY = false,
                clampMagnitude = 1.0,
                enableInputRead = true
            };
        }

        public TT2Status Status
        {
            get
            {
                lock (_sync)
                {
                    try { return _titanTwo?.Status ?? TT2Status.Disconnected; }
                    catch { return TT2Status.Disconnected; }
                }
            }
        }

        public bool IsConnected => Status == TT2Status.Connected;

        public TT2Info GetInfo()
        {
            lock (_sync)
            {
                try { return _titanTwo?.GetInfo() ?? default; }
                catch { return default; }
            }
        }

        public bool Connect()
        {
            lock (_sync)
            {
                OnStatusChanged?.Invoke(TT2Status.Connecting);
                try
                {
                    if (_titanTwo != null)
                    {
                        _titanTwo.OnButtonEvent -= InternalOnButtonEvent;
                        _titanTwo.Dispose();
                        _titanTwo = null;
                    }

                    _titanTwo = new TitanTwo(_config);
                    _titanTwo.OnButtonEvent += InternalOnButtonEvent;

                    bool started = _titanTwo.Start();
                    if (!started)
                    {
                        _titanTwo.Dispose();
                        _titanTwo = null;
                        OnStatusChanged?.Invoke(TT2Status.Error);
                        return false;
                    }

                    // Start() apenas inicializa o scan USB — não garante dispositivo presente.
                    // Emitir Connecting e deixar o timer detectar o Connected real.
                    _lastPolledStatus = TT2Status.Connecting;
                    OnStatusChanged?.Invoke(TT2Status.Connecting);
                    StartStatusPolling();
                    return true;
                }
                catch
                {
                    _titanTwo?.Dispose();
                    _titanTwo = null;
                    OnStatusChanged?.Invoke(TT2Status.Error);
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            StopStatusPolling();
            lock (_sync)
            {
                try
                {
                    if (_titanTwo == null)
                    {
                        _lastPolledStatus = TT2Status.Disconnected;
                        OnStatusChanged?.Invoke(TT2Status.Disconnected);
                        return;
                    }

                    _titanTwo.OnButtonEvent -= InternalOnButtonEvent;
                    _titanTwo.Stop();
                    _titanTwo.Dispose();
                    _titanTwo = null;
                    _lastPolledStatus = TT2Status.Disconnected;
                    OnStatusChanged?.Invoke(TT2Status.Disconnected);
                }
                catch { }
            }
        }

        private void StartStatusPolling()
        {
            _statusTimer?.Dispose();
            _statusTimer = new System.Threading.Timer(PollStatus, null, 500, 500);
        }

        private void StopStatusPolling()
        {
            _statusTimer?.Dispose();
            _statusTimer = null;
        }

        private void PollStatus(object? _)
        {
            TT2Status current;
            lock (_sync)
            {
                if (_titanTwo == null) return;
                try { current = _titanTwo.Status; }
                catch { return; }
            }

            if (current != _lastPolledStatus)
            {
                _lastPolledStatus = current;
                OnStatusChanged?.Invoke(current);

                // Dispositivo desconectado fisicamente — parar polling e limpar
                if (current == TT2Status.Disconnected || current == TT2Status.Error)
                    StopStatusPolling();
            }
        }

        public bool SetOutputProtocol(TT2_Proto protocol)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return false;
                try { return _titanTwo.SetOutputProtocol(protocol); }
                catch { return false; }
            }
        }

        public bool SetOutputPolling(TT2_Poll poll)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return false;
                try { return _titanTwo.SetOutputPolling(poll); }
                catch { return false; }
            }
        }

        public bool SetInputPolling(TT2_Poll poll)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return false;
                try { return _titanTwo.SetInputPolling(poll); }
                catch { return false; }
            }
        }

        public bool SetHighSpeedMode(bool enable)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return false;
                try { return _titanTwo.SetHighSpeedMode(enable); }
                catch { return false; }
            }
        }

        public bool SelectSlot(int slot)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return false;
                try { return _titanTwo.SlotLoad((byte)slot); }
                catch { return false; }
            }
        }

        public bool SlotUp()
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return false;
                try { return _titanTwo.SlotUp(); }
                catch { return false; }
            }
        }

        public bool SlotDown()
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return false;
                try { return _titanTwo.SlotDown(); }
                catch { return false; }
            }
        }

        public bool SlotUnload()
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return false;
                try { return _titanTwo.SlotUnload(); }
                catch { return false; }
            }
        }

        public void SendAimVector(double x, double y)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return;
                try { _titanTwo.SendAimVector(x, y); }
                catch { }
            }
        }

        public void SendAimPixels(double dx, double dy, int width, int height)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return;
                try { _titanTwo.SendAimPixels(dx, dy, width, height); }
                catch { }
            }
        }

        public void SetInvertX(bool value)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return;
                try { _titanTwo.SetInvertX(value); }
                catch { }
            }
        }

        public void SetInvertY(bool value)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return;
                try { _titanTwo.SetInvertY(value); }
                catch { }
            }
        }

        public void SetRateHz(int value)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return;
                try { _titanTwo.SetRateHz(value); }
                catch { }
            }
        }

        public void SetExclusiveHandle(bool value)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return;
                try { _titanTwo.SetExclusiveHandle(value); }
                catch { }
            }
        }

        public void SetClampMagnitude(double value)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return;
                try { _titanTwo.SetClampMagnitude(value); }
                catch { }
            }
        }

        public bool IsLTDown
        {
            get { lock (_sync) { try { return _titanTwo?.IsLTDown ?? false; } catch { return false; } } }
        }

        public bool IsRTDown
        {
            get { lock (_sync) { try { return _titanTwo?.IsRTDown ?? false; } catch { return false; } } }
        }

        /// <summary>Valor analógico do LT: 0 (solto) … 100 (fundo).</summary>
        public int LTValue
        {
            get { lock (_sync) { try { return _titanTwo?.LTValue ?? 0; } catch { return 0; } } }
        }

        /// <summary>Valor analógico do RT: 0 (solto) … 100 (fundo).</summary>
        public int RTValue
        {
            get { lock (_sync) { try { return _titanTwo?.RTValue ?? 0; } catch { return 0; } } }
        }

        public async Task TestMovementAsync()
        {
            lock (_sync)
            {
                if (_titanTwo == null) return;
            }

            var tests = new (double x, double y)[]
            {
                ( 0.5,  0.0), (-0.5,  0.0), ( 0.0,  0.5), ( 0.0, -0.5),
                ( 1.0,  0.0), (-1.0,  0.0), ( 0.0,  1.0), ( 0.0, -1.0)
            };

            foreach (var t in tests)
            {
                SendAimVector(t.x, t.y);
                await Task.Delay(120);
                await Task.Delay(120);
            }
        }

        public void SendAim(double nx, double ny)
        {
            lock (_sync)
            {
                if (_titanTwo == null) return;
                try { _titanTwo.SendAimVector(nx, ny); }
                catch { }
            }
        }

        public void SetMacroFlags(uint flags)
        {
            lock (_sync)
            {
                if (!IsConnected || _titanTwo == null) return;
                try { _titanTwo.SetMacroFlags(flags); }
                catch { }
            }
        }

        private void InternalOnButtonEvent(object? sender, TT2ButtonEvent evt)
        {
            OnButtonEvent?.Invoke(this, evt);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing) { StopStatusPolling(); }
            try { Disconnect(); } catch { }
            _disposed = true;
        }

        public void Dispose() { Dispose(true); }
        ~TitanTwoService() { Dispose(false); }
    }
}