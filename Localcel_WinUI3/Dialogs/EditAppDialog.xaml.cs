using Localcel_WinUI3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Localcel_WinUI3.Dialogs
{
    public sealed partial class EditAppDialog : ContentDialog
    {
        private readonly AppConfig _config;

        public int Port { get; private set; }
        public string Domain { get; private set; } = string.Empty;
        public string AppType { get; private set; } = "node";

        public EditAppDialog(AppConfig config)
        {
            this.InitializeComponent();
            _config = config;
            Title = $"Edit {config.Name}";

            TxtPort.Text = config.Port.ToString();
            TxtDomain.Text = config.Domain ?? string.Empty;
            AppType = config.AppType;

            if (config.AppType == "node")
            {
                LblPortHeader.Text = "Port Number";
                PanelStaticOptions.Visibility = Visibility.Collapsed;
            }
            else
            {
                LblPortHeader.Text = "Local Preview Port";
                PanelStaticOptions.Visibility = Visibility.Visible;
                ComboAppType.SelectedIndex = config.AppType == "static_cf" ? 1 : 0;
            }

            UpdateDomainVisibility();
        }

        private void ComboAppType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboAppType.SelectedItem is ComboBoxItem item && item.Tag is string type)
            {
                AppType = type;
                UpdateDomainVisibility();
            }
        }

        private void UpdateDomainVisibility()
        {
            if (LblDomainHeader == null || TxtDomain == null) return;

            if (AppType == "static_gh")
            {
                LblDomainHeader.Visibility = Visibility.Collapsed;
                TxtDomain.Visibility = Visibility.Collapsed;
            }
            else
            {
                LblDomainHeader.Visibility = Visibility.Visible;
                TxtDomain.Visibility = Visibility.Visible;
            }
        }

        private void EditAppDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            TxtError.Visibility = Visibility.Collapsed;
            var portStr = TxtPort.Text.Trim();

            if (!int.TryParse(portStr, out int port) || port < 1 || port > 65535)
            {
                TxtError.Text = "Please enter a valid port number (1-65535).";
                TxtError.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            Port = port;
            Domain = TxtDomain.Text.Trim();
        }
    }
}
