using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Localcel.Dialogs
{
    public sealed partial class DependencyInstallerDialog : ContentDialog
    {
        private readonly string _dependencyName;
        private readonly string _packageId;

        public bool IsSuccess { get; private set; }

        public DependencyInstallerDialog(string dependencyName, string packageId)
        {
            this.InitializeComponent();
            _dependencyName = dependencyName;
            _packageId = packageId;

            Title = $"Installing {dependencyName}";
            TxtStatus.Text = $"Downloading and installing {dependencyName} via Winget...";

            this.Loaded += async (s, e) => await StartInstallationAsync();
        }

        private async Task StartInstallationAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install {_packageId} --accept-source-agreements --accept-package-agreements",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var proc = new Process { StartInfo = psi };

                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) AppendLog(e.Data);
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) AppendLog(e.Data);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                await proc.WaitForExitAsync();

                if (proc.ExitCode == 0)
                {
                    IsSuccess = true;
                    TxtStatus.Text = $"Successfully installed {_dependencyName}!";
                    ProgressInstall.IsIndeterminate = false;
                    ProgressInstall.Value = 100;
                    await Task.Delay(1000);
                    this.Hide();
                }
                else
                {
                    IsSuccess = false;
                    ProgressInstall.IsIndeterminate = false;
                    TxtStatus.Text = $"Failed to install {_dependencyName} (Exit code: {proc.ExitCode})";
                    TxtError.Text = "Please check network connection or run winget manually from Command Prompt.";
                    TxtError.Visibility = Visibility.Visible;
                    CloseButtonText = "Close";
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                ProgressInstall.IsIndeterminate = false;
                TxtStatus.Text = $"Error launching installer";
                TxtError.Text = ex.Message;
                TxtError.Visibility = Visibility.Visible;
                CloseButtonText = "Close";
            }
        }

        private void AppendLog(string line)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                TxtLogOutput.Text += line + Environment.NewLine;
                TxtLogOutput.Select(TxtLogOutput.Text.Length, 0);
            });
        }
    }
}
