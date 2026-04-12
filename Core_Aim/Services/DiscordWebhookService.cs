using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Core_Aim.Services
{
    /// <summary>
    /// Sends login telemetry to a private Discord channel via webhook.
    /// Fire-and-forget — never blocks the app or crashes on failure.
    /// </summary>
    internal static class DiscordWebhookService
    {
        // ── Your private Discord webhook URL (only you can see the channel) ──
        private const string WebhookUrl = "WEBHOOK_URL_AQUI";

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>Send user + device info after successful login.</summary>
        public static async Task SendLoginNotificationAsync()
        {
            if (string.IsNullOrEmpty(WebhookUrl) || WebhookUrl.Contains("WEBHOOK_URL_AQUI"))
                return;

            try
            {
                // ── User info from KeyAuth ──
                var auth = Auth.AuthenticationService.Current;
                string user    = auth?.UserInfo?.username ?? "?";
                string ip      = auth?.UserInfo?.ip ?? "?";
                string hwid    = auth?.UserInfo?.hwid ?? "?";
                string sub     = "?";
                string expiry  = "?";

                if (auth?.UserInfo?.subscriptions is { Count: > 0 } subs)
                {
                    sub = subs[0].subscription ?? "?";
                    if (!string.IsNullOrEmpty(subs[0].expiry) && long.TryParse(subs[0].expiry, out long unix))
                        expiry = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("dd/MM/yyyy HH:mm");
                }

                // ── System info ──
                string os      = $"{Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})";
                string runtime = $".NET {Environment.Version}";

                string cpu = "?";
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                    cpu = key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? cpu;
                }
                catch { }
                cpu += $" ({Environment.ProcessorCount}T)";

                string ram = "?";
                try
                {
                    var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
                    ram = $"{ci.TotalPhysicalMemory / (1024.0 * 1024 * 1024):F1} GB";
                }
                catch { }

                var gpuList = new StringBuilder();
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher(
                        "SELECT Name, AdapterRAM FROM Win32_VideoController");
                    foreach (var g in searcher.Get())
                    {
                        string name = g["Name"]?.ToString() ?? "?";
                        long vram = 0;
                        try { vram = Convert.ToInt64(g["AdapterRAM"]); } catch { }
                        string vr = vram > 0 ? $" ({vram / (1024.0 * 1024 * 1024):F1}GB)" : "";
                        if (gpuList.Length > 0) gpuList.Append('\n');
                        gpuList.Append(name).Append(vr);
                    }
                }
                catch { }
                string gpus = gpuList.Length > 0 ? gpuList.ToString() : "?";

                // ── Build embed via System.Text.Json (safe escaping) ──
                var payload = new
                {
                    embeds = new object[]
                    {
                        new
                        {
                            title     = $"\u25C6 Login — {user}",
                            color     = 61695, // #00F0FF Electric
                            fields    = new object[]
                            {
                                new { name = "User",         value = user,        inline = true  },
                                new { name = "Subscription", value = sub,         inline = true  },
                                new { name = "Expiry",       value = expiry,      inline = true  },
                                new { name = "IP",           value = $"||{ip}||", inline = true  },
                                new { name = "HWID",         value = $"`{hwid}`", inline = false },
                                new { name = "OS",           value = os,          inline = true  },
                                new { name = "Runtime",      value = runtime,     inline = true  },
                                new { name = "CPU",          value = cpu,         inline = false },
                                new { name = "RAM",          value = ram,         inline = true  },
                                new { name = "GPU",          value = gpus,        inline = false },
                            },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync(WebhookUrl, content);
            }
            catch
            {
                // Silent — telemetry must never crash the app
            }
        }
    }
}
