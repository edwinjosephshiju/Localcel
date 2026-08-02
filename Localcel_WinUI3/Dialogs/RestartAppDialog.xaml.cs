using System;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Localcel_WinUI3.Dialogs
{
    public sealed partial class RestartAppDialog : ContentDialog
    {
        private DispatcherTimer? _timer;
        private int _countdown = 10;

        public RestartAppDialog(string dependencyName)
        {
            this.InitializeComponent();
            TxtMessage.Text = $"Successfully installed {dependencyName}!\n\nTo apply system PATH environment changes and initialize the runtime, Localcel needs to restart.";

            this.Loaded += (s, e) => StartCountdown();
            this.Unloaded += (s, e) => StopTimer();
        }

        private void StartCountdown()
        {
            _countdown = 10;
            TxtCountdown.Text = $"Restarting Localcel in {_countdown} seconds...";

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) =>
            {
                _countdown--;
                if (_countdown > 0)
                {
                    TxtCountdown.Text = $"Restarting Localcel in {_countdown} second{(_countdown == 1 ? "" : "s")}...";
                }
                else
                {
                    StopTimer();
                    TriggerRestart();
                }
            };
            _timer.Start();
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }

        private void RestartAppDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            StopTimer();
            TriggerRestart();
        }

        public static void TriggerRestart()
        {
            try
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
            }
            catch { }

            Application.Current.Exit();
        }
    }
}
