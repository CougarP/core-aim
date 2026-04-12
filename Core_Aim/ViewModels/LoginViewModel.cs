using Core_Aim.Commands;
using Core_Aim.Services.Auth;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Core_Aim.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly AuthenticationService _authService;

        public Action<bool>? OnLoginResult;
        public Action? OnShowRequested;   // disparado quando auto-login falha → exibe janela

        private string _username = "";
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _licenseKey = "";
        public string LicenseKey { get => _licenseKey; set { _licenseKey = value; OnPropertyChanged(); } }

        private string _statusMessage = "Initializing...";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        private bool _autoLogin = false;
        public bool AutoLogin { get => _autoLogin; set { _autoLogin = value; OnPropertyChanged(); } }

        private bool _isRegisterMode = false;
        public bool IsRegisterMode
        {
            get => _isRegisterMode;
            set
            {
                _isRegisterMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(SwitchModeText));
                OnPropertyChanged(nameof(LicenseVisibility));
                StatusMessage = _isRegisterMode ? "Enter details to register" : "Enter credentials to login";
            }
        }

        private bool _isBusy = true;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        public string ActionButtonText => IsRegisterMode ? "REGISTER" : "LOGIN";
        public string SwitchModeText => IsRegisterMode ? "Back to Login" : "Register New Account";
        public Visibility LicenseVisibility => IsRegisterMode ? Visibility.Visible : Visibility.Collapsed;

        public ICommand ExecuteAuthCommand { get; }
        public ICommand ToggleModeCommand { get; }
        public ICommand CloseCommand { get; }

        public LoginViewModel()
        {
            _authService = new AuthenticationService();

            ExecuteAuthCommand = new RelayCommand(async (param) => await ExecuteAuth(param));
            ToggleModeCommand = new RelayCommand((_) => IsRegisterMode = !IsRegisterMode);
            CloseCommand = new RelayCommand((_) => System.Windows.Application.Current.Shutdown());

            InitializeKeyAuth();
        }

        private async void InitializeKeyAuth()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Connecting to server...";

                await Task.Delay(300);

                var result = await _authService.InitializeAsync();

                if (!result.success)
                {
                    // Falhou (sem internet, server down, etc.) — NÃO trava a UI.
                    // Permite ao utilizador tentar de novo manualmente.
                    StatusMessage = result.message;
                    AutoLogin = false;
                    IsBusy = false;
                    OnShowRequested?.Invoke();
                    return;
                }

                var creds = _authService.LoadCredentials();
                if (creds != null)
                {
                    // auth.bin existe → auto-login
                    AutoLogin = true;
                    Username = creds.Value.user;
                    StatusMessage = "Auto-login...";

                    var (ok, msg) = await _authService.LoginAsync(creds.Value.user, creds.Value.pass, saveCredentials: true);
                    if (ok)
                    {
                        OnLoginResult?.Invoke(true);
                        return;
                    }

                    // Credenciais inválidas / timeout — apaga auth.bin e mostra janela para login manual
                    _authService.DeleteCredentials();
                    AutoLogin = false;
                    StatusMessage = $"Auto-login falhou: {msg}";
                    OnShowRequested?.Invoke();
                }
                else
                {
                    AutoLogin = false;
                    StatusMessage = "Ready. Please login.";
                }

                IsBusy = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"ERROR: {ex.Message}";
                IsBusy = false;
                OnShowRequested?.Invoke();
            }
        }

        private async Task ExecuteAuth(object parameter)
        {
            if (IsBusy) return;

            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password ?? "";

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                StatusMessage = "Username and Password required.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Processing...";

            bool success = false;
            string message = "";

            if (IsRegisterMode)
            {
                if (string.IsNullOrWhiteSpace(LicenseKey))
                {
                    StatusMessage = "License Key required for registration.";
                    IsBusy = false;
                    return;
                }
                (success, message) = await _authService.RegisterAsync(Username, password, LicenseKey, AutoLogin);
            }
            else
            {
                (success, message) = await _authService.LoginAsync(Username, password, AutoLogin);
            }

            StatusMessage = message;
            IsBusy = false;

            if (success)
            {
                OnLoginResult?.Invoke(true);
            }
        }
    }
}
