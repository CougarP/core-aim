using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Core_Aim.Services.Movements
{
    public static class SpeedProfileManager
    {
        private static Dictionary<string, List<List<float>>> _profiles = new Dictionary<string, List<List<float>>>();
        private const string ProfileFileName = "speed_profiles.json";

        public static void Load()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, ProfileFileName);
                if (!File.Exists(path)) CreateDefaultProfiles(path);

                string json = File.ReadAllText(path);
                _profiles = JsonSerializer.Deserialize<Dictionary<string, List<List<float>>>>(json)
                            ?? new Dictionary<string, List<List<float>>>();
            }
            catch { _profiles = new Dictionary<string, List<List<float>>>(); }
        }

        public static List<string> GetProfileNames()
        {
            if (_profiles == null || _profiles.Count == 0) return new List<string> { "Flat" };
            return _profiles.Keys.ToList();
        }

        public static float GetMultiplier(string profileName, float distanceFactor)
        {
            if (!_profiles.ContainsKey(profileName) || _profiles[profileName].Count == 0) return 1.0f;
            var curve = _profiles[profileName][0];
            if (curve.Count == 0) return 1.0f;

            int index = (int)(distanceFactor * (curve.Count - 1));
            index = Math.Clamp(index, 0, curve.Count - 1);
            return curve[index];
        }

        private static void CreateDefaultProfiles(string path)
        {
            var defaults = new Dictionary<string, List<List<float>>>
            {
                { "Flat", new List<List<float>> { new List<float> { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 } } },
                { "Human", new List<List<float>> { new List<float> { 0.5f, 0.7f, 0.9f, 1.0f, 1.1f, 1.0f, 0.9f, 0.7f, 0.5f, 0.3f } } }
            };
            try { File.WriteAllText(path, JsonSerializer.Serialize(defaults)); } catch { }
        }
    }
}