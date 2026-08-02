using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace Localcel.Services
{
    public class ManagedProcess
    {
        public string AppName { get; }
        public bool IsTunnel { get; }
        public string? DetectedUrl { get; set; }
        public string LogPath { get; }

        private Process? _process;
        private static readonly Regex TryCloudflareRegex = new(@"https://[a-zA-Z0-9-]+\.trycloudflare\.com", RegexOptions.Compiled);

        public ManagedProcess(string appName, bool isTunnel = false)
        {
            AppName = appName;
            IsTunnel = isTunnel;
            var suffix = isTunnel ? "_tunnel" : "";
            var logsDir = Models.AppManager.LogsDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".localcel");
            Directory.CreateDirectory(logsDir);
            LogPath = Path.Combine(logsDir, $"{appName}{suffix}.log");
        }

        public bool IsRunning => _process != null && !_process.HasExited;

        public void Start(string exePath, string arguments, string workingDir, Action<string> logCallback, DispatcherQueue? dispatcherQueue = null)
        {
            if (IsRunning) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                _process = new Process { StartInfo = psi };

                _process.OutputDataReceived += (sender, args) => HandleData(args.Data, logCallback, dispatcherQueue);
                _process.ErrorDataReceived += (sender, args) => HandleData(args.Data, logCallback, dispatcherQueue);

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                var errMsg = $"[ERROR] Failed to start process: {ex.Message}";
                HandleData(errMsg, logCallback, dispatcherQueue);
            }
        }

        private void HandleData(string? line, Action<string> logCallback, DispatcherQueue? dispatcherQueue)
        {
            if (line == null) return;

            try
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch { }

            if (IsTunnel && string.IsNullOrEmpty(DetectedUrl))
            {
                var match = TryCloudflareRegex.Match(line);
                if (match.Success)
                {
                    DetectedUrl = match.Value;
                }
            }

            if (dispatcherQueue != null)
            {
                dispatcherQueue.TryEnqueue(() => logCallback(line));
            }
            else
            {
                logCallback(line);
            }
        }

        public void Stop()
        {
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
                finally
                {
                    _process.Dispose();
                    _process = null;
                    DetectedUrl = null;
                }
            }
        }

        public static bool IsPortInUse(int port)
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var listeners = properties.GetActiveTcpListeners();
                if (listeners.Any(l => l.Port == port)) return true;

                var connections = properties.GetActiveTcpConnections();
                if (connections.Any(c => c.LocalEndPoint.Port == port)) return true;
            }
            catch { }

            return false;
        }

        public static int GetFirstAvailablePort(int startPort)
        {
            int port = startPort;
            while (IsPortInUse(port))
            {
                port++;
            }
            return port;
        }
    }
}
