using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using Core_Aim.Controls;
using Core_Aim.Pages;
using Core_Aim.ViewModels;

namespace Core_Aim.Services
{
    /// <summary>
    /// Phantom Balloon host manager — spec §7.
    /// Reage às propriedades IsXXVisible do MainViewModel e abre/fecha
    /// PhantomBalloon Windows reais (arrastáveis, com pin e close).
    /// Hide() em vez de Close() para preservar posição/estado.
    ///
    /// Sincronização bidirecional com a VM:
    ///   VM → balloon: PropertyChanged(IsXXVisible) → Sync()
    ///   balloon → VM: OnCloseRequested / OnPinChanged → atualiza ActiveTab/IsXXPinned
    /// </summary>
    public sealed class BalloonHostManager
    {
        private readonly Window _owner;
        private readonly MainViewModel _vm;
        private readonly Dictionary<string, PhantomBalloon> _open = new();

        // Flag para evitar reentrância: quando o host muda a VM por causa de
        // um callback do balloon, o PropertyChanged não deve voltar a chamar Sync.
        private bool _suppressSync;

        // Cascata diagonal a partir do canto superior-esquerdo (após sidebar 38px)
        private const double FirstLeft = 60;
        private const double FirstTop = 80;
        private const double CascadeStep = 28;
        private int _spawnIndex;

        // Tabela: tab code -> (titulo, factory de Page)
        private static readonly Dictionary<string, (string Title, Func<UIElement> Factory)> Tabs =
            new()
            {
                ["HW"]  = ("// HARDWARE",  () => new HardwarePage()),
                ["DET"] = ("// DETECTION", () => new DetectionPage()),
                ["TRK"] = ("// TRACKING",  () => new TrackingPage()),
                ["TRG"] = ("// TRIGGER",   () => new TriggerPage()),
                ["OVL"] = ("// OVERLAY",   () => new OverlayPage()),
                ["VIS"] = ("// VISION",    () => new VisionPage()),
                ["CAP"] = ("// CAPTURE",   () => new CapturePage()),
                ["MON"] = ("// MONITOR",   () => new MonitorPage()),
                ["USR"] = ("// USER",      () => new UserPage()),
                ["TRN"] = ("// TRAINING STUDIO", () => new TrainingPage()),
            };

        public BalloonHostManager(Window owner, MainViewModel vm)
        {
            _owner = owner;
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
            // Sincroniza o estado inicial (caso algum tab já esteja visível)
            foreach (var code in Tabs.Keys) Sync(code);
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressSync) return;
            if (string.IsNullOrEmpty(e.PropertyName)) return;
            // PropertyName: IsHWVisible, IsDETVisible, ...
            if (e.PropertyName.StartsWith("Is") && e.PropertyName.EndsWith("Visible"))
            {
                string code = e.PropertyName.Substring(2, e.PropertyName.Length - 2 - "Visible".Length);
                if (Tabs.ContainsKey(code)) Sync(code);
            }
        }

        private void Sync(string code)
        {
            bool visible = GetIsVisible(code);

            if (visible)
            {
                if (!_open.TryGetValue(code, out var balloon))
                {
                    var (title, factory) = Tabs[code];
                    var content = factory();
                    balloon = new PhantomBalloon(_owner, title, content)
                    {
                        DataContext = _vm  // Pages dependem de bindings no MainViewModel
                    };

                    // Wire callbacks balloon → VM
                    string capturedCode = code;
                    balloon.OnCloseRequested = () => HandleBalloonClose(capturedCode);
                    balloon.OnPinChanged     = pinned => HandleBalloonPinChanged(capturedCode, pinned);

                    balloon.Closed += (_, __) => _open.Remove(capturedCode);
                    _open[code] = balloon;

                    // Sincroniza estado de pin inicial sem disparar callback
                    balloon.SetPinnedSilently(GetIsPinned(code));

                    // Posicionamento em cascata (relativo à MainWindow)
                    balloon.WindowStartupLocation = WindowStartupLocation.Manual;
                    balloon.Left = _owner.Left + FirstLeft + (_spawnIndex * CascadeStep);
                    balloon.Top  = _owner.Top  + FirstTop  + (_spawnIndex * CascadeStep);
                    _spawnIndex = (_spawnIndex + 1) % 8;

                    balloon.Show();
                }
                else
                {
                    if (!balloon.IsVisible) balloon.Show();
                    balloon.Activate();
                }
            }
            else
            {
                if (_open.TryGetValue(code, out var balloon) && balloon.IsVisible)
                    balloon.Hide();
            }
        }

        // ── balloon → VM: usuário clicou no X do balloon ──
        // Deve fechar completamente: limpar pin + desativar tab.
        private void HandleBalloonClose(string code)
        {
            _suppressSync = true;
            try
            {
                // Despin (se estava fixado)
                SetPinned(code, false);
                // Limpa active tab se for este
                if (_vm.ActiveTab == code) _vm.ActiveTab = "";
            }
            finally
            {
                _suppressSync = false;
            }

            // Esconder o balloon manualmente (porque suppressSync bloqueou Sync)
            if (_open.TryGetValue(code, out var balloon) && balloon.IsVisible)
                balloon.Hide();
        }

        // ── balloon → VM: usuário clicou no PIN do balloon ──
        private void HandleBalloonPinChanged(string code, bool pinned)
        {
            _suppressSync = true;
            try
            {
                SetPinned(code, pinned);
            }
            finally
            {
                _suppressSync = false;
            }
        }

        private bool GetIsVisible(string code)
        {
            return code switch
            {
                "HW"  => _vm.IsHWVisible,
                "DET" => _vm.IsDETVisible,
                "TRK" => _vm.IsTRKVisible,
                "TRG" => _vm.IsTRGVisible,
                "OVL" => _vm.IsOVLVisible,
                "VIS" => _vm.IsVISVisible,
                "CAP" => _vm.IsCAPVisible,
                "MON" => _vm.IsMONVisible,
                "USR" => _vm.IsUSRVisible,
                "TRN" => _vm.IsTRNVisible,
                _     => false
            };
        }

        private bool GetIsPinned(string code)
        {
            return code switch
            {
                "HW"  => _vm.IsHWPinned,
                "DET" => _vm.IsDETPinned,
                "TRK" => _vm.IsTRKPinned,
                "TRG" => _vm.IsTRGPinned,
                "OVL" => _vm.IsOVLPinned,
                "VIS" => _vm.IsVISPinned,
                "CAP" => _vm.IsCAPPinned,
                "MON" => _vm.IsMONPinned,
                "USR" => _vm.IsUSRPinned,
                "TRN" => _vm.IsTRNPinned,
                _     => false
            };
        }

        private void SetPinned(string code, bool value)
        {
            switch (code)
            {
                case "HW":  _vm.IsHWPinned  = value; break;
                case "DET": _vm.IsDETPinned = value; break;
                case "TRK": _vm.IsTRKPinned = value; break;
                case "TRG": _vm.IsTRGPinned = value; break;
                case "OVL": _vm.IsOVLPinned = value; break;
                case "VIS": _vm.IsVISPinned = value; break;
                case "CAP": _vm.IsCAPPinned = value; break;
                case "MON": _vm.IsMONPinned = value; break;
                case "USR": _vm.IsUSRPinned = value; break;
                case "TRN": _vm.IsTRNPinned = value; break;
            }
        }
    }
}
