using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace Core_Aim.Services.Auth
{
    public class AuthenticationService
    {
        // ====================================================
        // DADOS REAIS (SEM SECRET - API 1.3)
        // ====================================================
        private const string AppName = "V2";
        private const string OwnerId = "hqEHprwo5s";
        private const string AppVersion = "1.0";

        private readonly KeyAuthApp _api;

        /// <summary>Singleton — allows MainViewModel to read user_data after login.</summary>
        public static AuthenticationService? Current { get; private set; }

        public bool IsLoggedIn { get; private set; } = false;
        public string CurrentUsername { get; private set; } = "";

        /// <summary>Exposes KeyAuth user data (username, ip, hwid, subscriptions, etc.).</summary>
        public KeyAuthApp.UserData UserInfo => _api.user_data;

        public AuthenticationService()
        {
            // Construtor atualizado: Apenas Nome, OwnerID e Versão
            _api = new KeyAuthApp(AppName, OwnerId, AppVersion);
            Current = this;
        }

        public async Task<(bool success, string message)> InitializeAsync()
        {
            // TLS 1.2 continua obrigatório
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // KeyAuthApp.init() usa WebClient sync — sem internet bloqueia 30s+
            // antes do default timeout. Wrap com Task.WhenAny para garantir
            // que a UI nunca trava mais que 8s, e devolvemos erro "sem rede".
            var initTask = Task.Run(() =>
            {
                try
                {
                    _api.init();
                    return (_api.response.success, _api.response.message);
                }
                catch (Exception ex)
                {
                    return (false, "Exception: " + ex.Message);
                }
            });

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(8));
            var winner = await Task.WhenAny(initTask, timeoutTask);
            if (winner == timeoutTask)
            {
                return (false, "Sem conexão (timeout). Verifica a internet e tenta de novo.");
            }
            return await initTask;
        }

        public async Task<(bool success, string message)> LoginAsync(string username, string password, bool saveCredentials = true)
        {
            var loginTask = Task.Run(() =>
            {
                try
                {
                    _api.login(username, password);
                    if (_api.response.success)
                    {
                        IsLoggedIn = true;
                        CurrentUsername = _api.user_data.username;
                        if (saveCredentials)
                            SaveCredentials(username, password);
                        else
                            DeleteCredentials();
                    }
                    return (_api.response.success, _api.response.message);
                }
                catch (Exception ex)
                {
                    return (false, "Exception: " + ex.Message);
                }
            });

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var winner = await Task.WhenAny(loginTask, timeoutTask);
            if (winner == timeoutTask)
            {
                return (false, "Sem conexão (timeout no login). Verifica a internet.");
            }
            return await loginTask;
        }

        public async Task<(bool success, string message)> RegisterAsync(string username, string password, string key, bool saveCredentials = true)
        {
            return await Task.Run(() =>
            {
                _api.register(username, password, key);
                if (_api.response.success)
                {
                    IsLoggedIn = true;
                    CurrentUsername = _api.user_data.username;
                    if (saveCredentials)
                        SaveCredentials(username, password);
                    else
                        DeleteCredentials();
                }
                return (_api.response.success, _api.response.message);
            });
        }

        public void DeleteCredentials()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "auth.bin");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        public async Task<(bool success, string message)> LoginWithLicenseAsync(string key)
        {
            return await Task.Run(() =>
            {
                _api.license(key);
                if (_api.response.success)
                {
                    IsLoggedIn = true;
                    CurrentUsername = "LicenseUser";
                }
                return (_api.response.success, _api.response.message);
            });
        }

        private void SaveCredentials(string user, string pass)
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "auth.bin");
                string data = $"{user}|{pass}";
                string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data));
                File.WriteAllText(path, encoded);
            }
            catch { }
        }

        public (string user, string pass)? LoadCredentials()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "auth.bin");
                if (!File.Exists(path)) return null;

                string encoded = File.ReadAllText(path);
                string data = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                string[] parts = data.Split('|');
                if (parts.Length == 2) return (parts[0], parts[1]);
            }
            catch { }
            return null;
        }
    }
}