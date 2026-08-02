using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Localcel.Models;

namespace Localcel.Services
{
    public class CloudflareTunnelInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        public string ShortId => Id.Length >= 8 ? Id[..8] : Id;
    }

    public static class CloudflareService
    {
        public static string? GetExecutablePath()
        {
            return GitHubService.GetExecutablePath("cloudflared");
        }

        public static async Task<bool> InstallCloudflaredAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "install Cloudflare.cloudflared --accept-package-agreements --accept-source-agreements",
                    UseShellExecute = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    return proc.ExitCode == 0;
                }
            }
            catch { }
            return false;
        }

        public static async Task<List<CloudflareTunnelInfo>> ListTunnelsAsync()
        {
            var cfBin = GetExecutablePath();
            if (string.IsNullOrEmpty(cfBin)) return new List<CloudflareTunnelInfo>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cfBin,
                    Arguments = "tunnel list --output json",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc == null) return new List<CloudflareTunnelInfo>();

                var stdout = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
                {
                    var list = JsonSerializer.Deserialize<List<CloudflareTunnelInfo>>(stdout, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return list ?? new List<CloudflareTunnelInfo>();
                }
            }
            catch { }

            return new List<CloudflareTunnelInfo>();
        }

        public static async Task DeleteTunnelAsync(string tunnelIdOrName)
        {
            var cfBin = GetExecutablePath();
            if (string.IsNullOrEmpty(cfBin)) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cfBin,
                    Arguments = $"tunnel delete -f {tunnelIdOrName}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                }
            }
            catch { }
        }

        public static async Task<string> SetupNamedTunnelAsync(string appName, int port, string domain)
        {
            var cfBin = GetExecutablePath();
            if (string.IsNullOrEmpty(cfBin))
                throw new InvalidOperationException("cloudflared binary not found.");

            var tunnelName = $"localcel_{appName}";

            // 1. Create tunnel
            var psiCreate = new ProcessStartInfo
            {
                FileName = cfBin,
                Arguments = $"tunnel create {tunnelName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var procCreate = Process.Start(psiCreate);
            if (procCreate != null) await procCreate.WaitForExitAsync();

            // 2. Find tunnel ID
            var tunnels = await ListTunnelsAsync();
            var tunnel = tunnels.Find(t => t.Name.Equals(tunnelName, StringComparison.OrdinalIgnoreCase));
            if (tunnel == null || string.IsNullOrEmpty(tunnel.Id))
                throw new InvalidOperationException($"Tunnel ID not found for {tunnelName}.");

            // 3. Route DNS
            var psiRoute = new ProcessStartInfo
            {
                FileName = cfBin,
                Arguments = $"tunnel route dns -f {tunnelName} {domain}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var procRoute = Process.Start(psiRoute);
            if (procRoute != null) await procRoute.WaitForExitAsync();

            // 4. Create tunnel.yml inside app directory
            if (AppManager.AppsDir == null) throw new InvalidOperationException("Workspace apps directory not initialized.");
            var configPath = Path.Combine(AppManager.AppsDir, appName, "tunnel.yml");
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var credFile = Path.Combine(userProfile, ".cloudflared", $"{tunnel.Id}.json").Replace('\\', '/');

            var yaml = $"tunnel: {tunnel.Id}\n" +
                       $"credentials-file: {credFile}\n" +
                       $"ingress:\n" +
                       $"  - hostname: {domain}\n" +
                       $"    service: http://localhost:{port}\n" +
                       $"  - service: http_status:404\n";

            await File.WriteAllTextAsync(configPath, yaml);
            return configPath;
        }

        public static void LoginCloudflare()
        {
            var cfBin = GetExecutablePath();
            if (string.IsNullOrEmpty(cfBin)) return;

            var certPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cloudflared", "cert.pem");
            if (File.Exists(certPath))
            {
                try { File.Delete(certPath); } catch { }
            }

            var psi = new ProcessStartInfo
            {
                FileName = cfBin,
                Arguments = "tunnel login",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }
}
