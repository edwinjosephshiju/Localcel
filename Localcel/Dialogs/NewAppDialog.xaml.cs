using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Localcel.Dialogs
{
    public sealed partial class NewAppDialog : ContentDialog
    {
        public string AppName { get; private set; } = string.Empty;
        public int Port { get; private set; } = 3000;
        public string Domain { get; private set; } = string.Empty;
        public string AppType { get; private set; } = "node";

        public NewAppDialog()
        {
            this.InitializeComponent();
        }

        private void ComboAppType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtDomain == null || LblDomain == null || TxtPort == null) return;

            if (ComboAppType.SelectedItem is ComboBoxItem item && item.Tag is string type)
            {
                AppType = type;
                if (type == "static_gh")
                {
                    TxtDomain.Visibility = Visibility.Collapsed;
                    LblDomain.Visibility = Visibility.Collapsed;
                    TxtPort.PlaceholderText = "Local Preview Port (e.g. 8080)";
                    if (TxtPort.Text == "3000") TxtPort.Text = "8080";
                }
                else
                {
                    TxtDomain.Visibility = Visibility.Visible;
                    LblDomain.Visibility = Visibility.Visible;
                    TxtPort.PlaceholderText = type == "static_cf" ? "Local Preview Port (e.g. 8080)" : "Port (e.g. 3000)";
                }
            }
        }

        private void NewAppDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            TxtError.Visibility = Visibility.Collapsed;
            var name = TxtAppName.Text.Trim();
            var portStr = TxtPort.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                TxtError.Text = "Please enter an app name.";
                TxtError.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (!int.TryParse(portStr, out int port) || port < 1 || port > 65535)
            {
                TxtError.Text = "Please enter a valid port number (1-65535).";
                TxtError.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            AppName = name;
            Port = port;
            Domain = TxtDomain.Text.Trim();
        }
    }
}
