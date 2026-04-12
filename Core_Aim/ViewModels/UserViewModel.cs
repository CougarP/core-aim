using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Core_Aim.Commands;
using Core_Aim.Services.Auth;
using Core_Aim.Services.Configuration;

namespace Core_Aim.ViewModels
{
    public class UserViewModel : ViewModelBase
    {
        private readonly AppSettingsService _settings;
        private readonly ConfigProfileManager _profiles;

        // ── KeyAuth info ──────────────────────────────────────────────
        public string Username   => AuthenticationService.Current?.UserInfo?.username ?? "—";
        public string UserIp     => AuthenticationService.Current?.UserInfo?.ip ?? "—";
        public string UserHwid   => AuthenticationService.Current?.UserInfo?.hwid ?? "—";
        public string CreateDate => FormatUnix(AuthenticationService.Current?.UserInfo?.createdate);
        public string LastLogin  => FormatUnix(AuthenticationService.Current?.UserInfo?.lastlogin);
        public string Subscription => GetSubscriptionName();
        public string Expiry       => GetSubscriptionExpiry();

        // ── Config profiles ───────────────────────────────────────────
        public ObservableCollection<string> ProfileNames => _profiles.ProfileNames;
        public bool IsEmpty      => _profiles.IsEmpty;
        public bool HasClipboard => _profiles.HasClipboard;

        private string _activeProfile = "";
        public string ActiveProfile
        {
            get => _activeProfile;
            set { if (_activeProfile != value) { _activeProfile = value; OnPropertyChanged(); } }
        }

        private string _selectedProfile = "";
        public string SelectedProfile
        {
            get => _selectedProfile;
            set { if (_selectedProfile != value) { _selectedProfile = value; OnPropertyChanged(); } }
        }

        private string _newProfileName = "";
        public string NewProfileName
        {
            get => _newProfileName;
            set { if (_newProfileName != value) { _newProfileName = value; OnPropertyChanged(); } }
        }

        private string _profileStatus = "";
        public string ProfileStatus
        {
            get => _profileStatus;
            set { if (_profileStatus != value) { _profileStatus = value; OnPropertyChanged(); } }
        }

        // ── Commands ──────────────────────────────────────────────────
        public ICommand CreateProfileCommand { get; }
        public ICommand LoadProfileCommand   { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand CopyProfileCommand   { get; }
        public ICommand PasteProfileCommand  { get; }
        public ICommand SaveCurrentCommand   { get; }
        public ICommand OpenDiscordCommand   { get; }

        public UserViewModel(AppSettingsService settings)
        {
            _settings = settings;
            _profiles = new ConfigProfileManager();

            _activeProfile  = _profiles.ActiveProfile;
            _selectedProfile = _activeProfile;

            _profiles.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ConfigProfileManager.ActiveProfile))
                {
                    ActiveProfile = _profiles.ActiveProfile;
                }
                if (e.PropertyName == nameof(ConfigProfileManager.IsEmpty))
                    OnPropertyChanged(nameof(IsEmpty));
                if (e.PropertyName == nameof(ConfigProfileManager.HasClipboard))
                    OnPropertyChanged(nameof(HasClipboard));
            };

            CreateProfileCommand = new RelayCommand(_ => CreateProfile());
            LoadProfileCommand   = new RelayCommand(_ => LoadProfile());
            DeleteProfileCommand = new RelayCommand(_ => DeleteProfile());
            CopyProfileCommand   = new RelayCommand(_ => CopyProfile());
            PasteProfileCommand  = new RelayCommand(_ => PasteProfile());
            SaveCurrentCommand   = new RelayCommand(_ => SaveCurrent());
            OpenDiscordCommand   = new RelayCommand(_ => OpenDiscord());

            // Auto-load last profile on startup
            AutoLoadLastProfile();
        }

        private void AutoLoadLastProfile()
        {
            var last = _profiles.GetLastActiveProfile();
            if (!string.IsNullOrEmpty(last) && _profiles.ProfileNames.Contains(last))
            {
                _profiles.Activate(last, _settings);
                ActiveProfile = last;
                SelectedProfile = last;
                ProfileStatus = $"✓ {last}";
            }
            else if (_profiles.IsEmpty)
            {
                ProfileStatus = "No profiles — create one";
            }
        }

        private void CreateProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName)) return;
            var cfg = _settings.GetConfigCopy();
            if (_profiles.Create(NewProfileName, cfg))
            {
                _profiles.Activate(NewProfileName, _settings);
                SelectedProfile = NewProfileName;
                ProfileStatus = $"✓ Created: {NewProfileName}";
                NewProfileName = "";
            }
            else
            {
                ProfileStatus = "Name already exists or invalid";
            }
        }

        private void LoadProfile()
        {
            if (string.IsNullOrEmpty(SelectedProfile)) return;
            if (_profiles.Activate(SelectedProfile, _settings))
            {
                ProfileStatus = $"✓ Loaded: {SelectedProfile}";
            }
        }

        private void DeleteProfile()
        {
            if (string.IsNullOrEmpty(SelectedProfile)) return;
            var name = SelectedProfile;
            if (_profiles.Delete(name))
            {
                SelectedProfile = _profiles.ActiveProfile;
                ProfileStatus = $"Deleted: {name}";
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private void CopyProfile()
        {
            if (string.IsNullOrEmpty(SelectedProfile)) return;
            _profiles.CopyToClipboard(SelectedProfile);
            ProfileStatus = $"Copied: {SelectedProfile}";
        }

        private void PasteProfile()
        {
            if (!HasClipboard || string.IsNullOrWhiteSpace(NewProfileName)) return;
            if (_profiles.PasteFromClipboard(NewProfileName))
            {
                SelectedProfile = NewProfileName;
                ProfileStatus = $"✓ Pasted: {NewProfileName}";
                NewProfileName = "";
            }
            else
            {
                ProfileStatus = "Paste failed — name exists or invalid";
            }
        }

        private void SaveCurrent()
        {
            _profiles.SaveCurrent(_settings);
            ProfileStatus = $"✓ Saved: {ActiveProfile}";
        }

        // ── Discord ───────────────────────────────────────────────────
        private const string DiscordInvite = "https://discord.gg/5rcTzjFF";

        private static void OpenDiscord()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = DiscordInvite,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // ── Helpers ───────────────────────────────────────────────────
        private static string FormatUnix(string? unix)
        {
            if (string.IsNullOrEmpty(unix)) return "—";
            try
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(unix)).LocalDateTime;
                return dt.ToString("dd/MM/yyyy HH:mm");
            }
            catch { return unix; }
        }

        private string GetSubscriptionName()
        {
            var subs = AuthenticationService.Current?.UserInfo?.subscriptions;
            if (subs == null || subs.Count == 0) return "—";
            return subs[0].subscription ?? "—";
        }

        private string GetSubscriptionExpiry()
        {
            var subs = AuthenticationService.Current?.UserInfo?.subscriptions;
            if (subs == null || subs.Count == 0) return "—";
            return FormatUnix(subs[0].expiry);
        }
    }
}
