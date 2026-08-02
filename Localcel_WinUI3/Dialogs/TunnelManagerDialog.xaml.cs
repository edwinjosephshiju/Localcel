using System;
using System.Threading.Tasks;
using Localcel_WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Localcel_WinUI3.Dialogs
{
    public sealed partial class TunnelManagerDialog : ContentDialog
    {
        public TunnelManagerDialog()
        {
            this.InitializeComponent();
            this.Loaded += async (s, e) => await RefreshTunnelsAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshTunnelsAsync();
        }

        public async Task RefreshTunnelsAsync()
        {
            PanelTunnels.Children.Clear();
            BtnRefresh.IsEnabled = false;

            var tunnels = await CloudflareService.ListTunnelsAsync();
            if (tunnels.Count == 0)
            {
                PanelTunnels.Children.Add(new TextBlock
                {
                    Text = "No active tunnels found or not logged in.",
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    Margin = new Thickness(0, 12, 0, 0)
                });
            }
            else
            {
                foreach (var t in tunnels)
                {
                    var card = new Grid
                    {
                        Padding = new Thickness(12, 8, 12, 8),
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                            new ColumnDefinition { Width = GridLength.Auto }
                        }
                    };

                    var nameTxt = new TextBlock
                    {
                        Text = $"{t.Name}  •  {t.ShortId}",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var delBtn = new Button
                    {
                        Content = "Delete",
                        Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(180, 196, 43, 28)),
                        VerticalAlignment = VerticalAlignment.Center,
                        CornerRadius = new CornerRadius(4)
                    };

                    var tunnelId = t.Id;
                    var tunnelShortId = t.ShortId;

                    var flyout = new Flyout();
                    var flyoutPanel = new StackPanel { Spacing = 8, Padding = new Thickness(4), Width = 200 };
                    flyoutPanel.Children.Add(new TextBlock
                    {
                        Text = $"Delete tunnel '{tunnelShortId}'?",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    });

                    var confirmBtn = new Button
                    {
                        Content = "Confirm Delete",
                        Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28)),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        CornerRadius = new CornerRadius(4)
                    };

                    confirmBtn.Click += async (s, e) =>
                    {
                        flyout.Hide();
                        await CloudflareService.DeleteTunnelAsync(tunnelId);
                        await RefreshTunnelsAsync();
                    };

                    flyoutPanel.Children.Add(confirmBtn);
                    flyout.Content = flyoutPanel;
                    delBtn.Flyout = flyout;

                    Grid.SetColumn(nameTxt, 0);
                    Grid.SetColumn(delBtn, 1);

                    card.Children.Add(nameTxt);
                    card.Children.Add(delBtn);

                    PanelTunnels.Children.Add(card);
                }
            }

            BtnRefresh.IsEnabled = true;
        }
    }
}
