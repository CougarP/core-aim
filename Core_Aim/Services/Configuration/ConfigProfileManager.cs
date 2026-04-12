using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Core_Aim.Services.Configuration
{
    /// <summary>
    /// Manages named config profiles.  Each profile is a separate JSON file
    /// inside  {AppDir}/config/profiles/.   A small meta file tracks which
    /// profile was last active.
    /// </summary>
    public class ConfigProfileManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly string _profilesDir;
        private readonly string _metaPath;

        public ObservableCollection<string> ProfileNames { get; } = new();

        private string _activeProfile = "";
        public string ActiveProfile
        {
            get => _activeProfile;
            private set { if (_activeProfile != value) { _activeProfile = value; Notify(nameof(ActiveProfile)); } }
        }

        /// <summary>True when there are zero profiles — UI should show "create first profile" message.</summary>
        public bool IsEmpty => ProfileNames.Count == 0;

        /// <summary>Clipboard for copy/paste.</summary>
        private string? _clipboardJson;
        public bool HasClipboard => _clipboardJson != null;

        public ConfigProfileManager()
        {
            _profilesDir = Path.Combine(AppContext.BaseDirectory, "config", "profiles");
            _metaPath     = Path.Combine(AppContext.BaseDirectory, "config", "profiles_meta.json");
            Directory.CreateDirectory(_profilesDir);
            Refresh();
        }

        // ─── Public API ────────────────────────────────────────────

        /// <summary>Refreshes the profile list from disk.</summary>
        public void Refresh()
        {
            ProfileNames.Clear();
            foreach (var f in Directory.GetFiles(_profilesDir, "*.json").OrderBy(f => f))
                ProfileNames.Add(Path.GetFileNameWithoutExtension(f));

            var last = ReadMeta();
            if (!string.IsNullOrEmpty(last) && ProfileNames.Contains(last))
                ActiveProfile = last;
            else if (ProfileNames.Count > 0)
                ActiveProfile = ProfileNames[0];
            else
                ActiveProfile = "";

            Notify(nameof(IsEmpty));
        }

        /// <summary>Creates a new profile with current (or default) settings.</summary>
        public bool Create(string name, AppConfig? baseConfig = null)
        {
            name = Sanitize(name);
            if (string.IsNullOrEmpty(name)) return false;
            if (ProfileNames.Contains(name)) return false;

            var cfg = baseConfig ?? new AppConfig();
            SaveProfile(name, cfg);
            ProfileNames.Add(name);
            Notify(nameof(IsEmpty));
            return true;
        }

        /// <summary>Duplicates an existing profile with a new name.</summary>
        public bool Duplicate(string sourceName, string newName)
        {
            newName = Sanitize(newName);
            if (string.IsNullOrEmpty(newName) || ProfileNames.Contains(newName)) return false;
            var cfg = LoadProfile(sourceName);
            if (cfg == null) return false;
            SaveProfile(newName, cfg);
            ProfileNames.Add(newName);
            Notify(nameof(IsEmpty));
            return true;
        }

        /// <summary>Deletes a profile. Returns true if deleted.</summary>
        public bool Delete(string name)
        {
            var path = ProfilePath(name);
            if (!File.Exists(path)) return false;
            try { File.Delete(path); } catch { return false; }
            ProfileNames.Remove(name);
            if (ActiveProfile == name)
                ActiveProfile = ProfileNames.Count > 0 ? ProfileNames[0] : "";
            Notify(nameof(IsEmpty));
            return true;
        }

        /// <summary>Copies the profile JSON to internal clipboard.</summary>
        public void CopyToClipboard(string name)
        {
            var path = ProfilePath(name);
            if (File.Exists(path))
            {
                _clipboardJson = File.ReadAllText(path);
                Notify(nameof(HasClipboard));
            }
        }

        /// <summary>Pastes clipboard contents as a new profile.</summary>
        public bool PasteFromClipboard(string newName)
        {
            if (_clipboardJson == null) return false;
            newName = Sanitize(newName);
            if (string.IsNullOrEmpty(newName) || ProfileNames.Contains(newName)) return false;
            try
            {
                // validate JSON
                var cfg = JsonSerializer.Deserialize<AppConfig>(_clipboardJson, _jsonOpts);
                if (cfg == null) return false;
                SaveProfile(newName, cfg);
                ProfileNames.Add(newName);
                Notify(nameof(IsEmpty));
                return true;
            }
            catch { return false; }
        }

        /// <summary>Loads a profile into the given AppSettingsService.</summary>
        public bool Activate(string name, AppSettingsService settings)
        {
            var cfg = LoadProfile(name);
            if (cfg == null) return false;

            settings.ReplaceConfig(cfg);
            ActiveProfile = name;
            WriteMeta(name);
            return true;
        }

        /// <summary>Saves current settings into the active profile.</summary>
        public void SaveCurrent(AppSettingsService settings)
        {
            if (string.IsNullOrEmpty(ActiveProfile)) return;
            SaveProfile(ActiveProfile, settings.GetConfigCopy());
        }

        /// <summary>Returns the last active profile name (or empty).</summary>
        public string GetLastActiveProfile() => ReadMeta();

        // ─── Internal helpers ──────────────────────────────────────

        private string ProfilePath(string name) => Path.Combine(_profilesDir, name + ".json");

        private AppConfig? LoadProfile(string name)
        {
            var path = ProfilePath(name);
            if (!File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppConfig>(json, _jsonOpts);
            }
            catch { return null; }
        }

        private void SaveProfile(string name, AppConfig cfg)
        {
            var json = JsonSerializer.Serialize(cfg, _jsonOpts);
            File.WriteAllText(ProfilePath(name), json);
        }

        private string ReadMeta()
        {
            try
            {
                if (!File.Exists(_metaPath)) return "";
                var json = File.ReadAllText(_metaPath);
                var meta = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return meta?.GetValueOrDefault("lastProfile", "") ?? "";
            }
            catch { return ""; }
        }

        private void WriteMeta(string profileName)
        {
            try
            {
                var meta = new Dictionary<string, string> { ["lastProfile"] = profileName };
                File.WriteAllText(_metaPath, JsonSerializer.Serialize(meta, _jsonOpts));
            }
            catch { }
        }

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        }
    }
}
