using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Localcel.Dialogs;
using Localcel.Models;
using Localcel.Services;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Localcel
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, ManagedProcess> _engineServers = new();
        private readonly Dictionary<string, ManagedProcess> _engineTunnels = new();
        private readonly ObservableCollection<AppItemViewModel> _appViewModels = new();

        private AppConfig? _selectedApp;
        private DispatcherTimer? _statusTimer;
        private string? _ghUserCache;
        private bool _isFetchingGhUser;
        private bool _isExiting;

        public MainWindow()
        {
            this.InitializeComponent();

            // Set native Mica / DesktopAcrylic backdrop
            if (MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
            }
            else if (DesktopAcrylicController.IsSupported())
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }

            // Set window title & icon
            Title = "Localcel - Vercel for Localhost";
            SetWindowIcon();

            // Extend Mica backdrop into window titlebar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            AppListView.ItemsSource = _appViewModels;

            // Handle window close event for minimize-to-tray
            this.Closed += MainWindow_Closed;

            this.Activated += MainWindow_Activated;
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated;
            await EnsureWorkspaceAndLoadAsync();
            StartTimer();
        }

        private void SetWindowIcon()
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "localcel_logo.ico");
                if (File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
            }
            catch { }
        }

        private async Task EnsureWorkspaceAndLoadAsync()
        {
            var ws = AppManager.GetWorkspacePath();
            if (string.IsNullOrEmpty(ws) || !Directory.Exists(ws))
            {
                var setupDialog = new ContentDialog
                {
                    Title = "Workspace Setup",
                    Content = "Welcome to Localcel!\n\nPlease select a directory to act as your Server Workspace.\nAll your applications, configurations, and logs will be securely stored there.",
                    PrimaryButtonText = "Select Workspace Directory",
                    CloseButtonText = "Exit",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };

                var res = await setupDialog.ShowAsync();
                if (res != ContentDialogResult.Primary)
                {
                    Application.Current.Exit();
                    return;
                }

                var picker = new FolderPicker();
                picker.SuggestedStartLocation = PickerLocationId.Desktop;
                picker.FileTypeFilter.Add("*");

                var hwnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hwnd);

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    AppManager.SetWorkspacePath(folder.Path);
                }
                else
                {
                    Application.Current.Exit();
                    return;
                }
            }

            RefreshAppList();
            await CheckPostRestartCfLoginAsync();
        }

        private async Task CheckPostRestartCfLoginAsync()
        {
            if (AppManager.GetPromptCfLoginAfterRestart())
            {
                var root = (this.Content as FrameworkElement)?.XamlRoot;
                if (root == null) return;

                AppManager.SetPromptCfLoginAfterRestart(false);

                var cfDialog = new ContentDialog
                {
                    Title = "Cloudflare Tunnel Setup",
                    Content = "Cloudflare Tunnel (cloudflared) was recently installed on this machine.\n\nWould you like to complete Cloudflare Login now to authenticate your tunnels?",
                    PrimaryButtonText = "Login to Cloudflare",
                    CloseButtonText = "Later",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = root
                };

                if (await cfDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    BtnCfLogin_Click(this, new RoutedEventArgs());
                }
            }
        }

        private void StartTimer()
        {
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statusTimer.Tick += (s, e) => CheckLoop();
            _statusTimer.Start();
        }

        private void RefreshAppList()
        {
            _appViewModels.Clear();
            var apps = AppManager.GetApps();

            foreach (var app in apps)
            {
                var vm = new AppItemViewModel(app)
                {
                    IsRunning = _engineServers.TryGetValue(app.Name, out var srv) && srv.IsRunning
                };
                _appViewModels.Add(vm);
            }

            if (_selectedApp != null)
            {
                var selectedVm = _appViewModels.FirstOrDefault(a => a.Name == _selectedApp.Name);
                if (selectedVm != null)
                {
                    AppListView.SelectedItem = selectedVm;
                }
            }
        }

        private void CheckLoop()
        {
            var runningApps = new List<string>();

            foreach (var vm in _appViewModels)
            {
                var isRun = _engineServers.TryGetValue(vm.Name, out var srv) && srv.IsRunning;
                vm.IsRunning = isRun;
                if (isRun) runningApps.Add(vm.Name);
            }

            if (runningApps.Count > 0)
            {
                TrayIcon.ToolTipText = $"Localcel - Running:\n{string.Join("\n", runningApps)}";
            }
            else
            {
                TrayIcon.ToolTipText = "Localcel - No running servers";
            }

            UpdateUiState();
        }

        private void AppListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppListView.SelectedItem is AppItemViewModel vm)
            {
                _selectedApp = vm.Config;
                TxtAppTitle.Text = vm.DisplayTitle;

                UpdateUiState();
                TxtServerLogs.Text = string.Empty;
                TxtTunnelLogs.Text = string.Empty;

                LoadLogFile(vm.Name, "", TxtServerLogs);
                LoadLogFile(vm.Name, "_tunnel", TxtTunnelLogs);
            }
        }

        private void LoadLogFile(string appName, string suffix, TextBox targetTextBox)
        {
            if (AppManager.LogsDir == null) return;
            var logPath = Path.Combine(AppManager.LogsDir, $"{appName}{suffix}.log");
            if (File.Exists(logPath))
            {
                try
                {
                    var lines = File.ReadAllLines(logPath);
                    var recentLines = lines.TakeLast(100);
                    targetTextBox.Text = string.Join(Environment.NewLine, recentLines);
                }
                catch { }
            }
        }

        private void UpdateUiState()
        {
            if (_selectedApp == null) return;
            var name = _selectedApp.Name;
            var isRunning = _engineServers.TryGetValue(name, out var srv) && srv.IsRunning;

            BtnStart.IsEnabled = !isRunning;
            BtnStop.IsEnabled = isRunning;

            if (_selectedApp.AppType == "static_gh")
            {
                BtnDeploy.Visibility = Visibility.Visible;
                BtnDeploy.Content = _selectedApp.GhPagesDeployed ? "Undeploy" : "Deploy";
            }
            else
            {
                BtnDeploy.Visibility = Visibility.Collapsed;
            }

            var localUrl = $"http://localhost:{_selectedApp.Port}";

            if (isRunning)
            {
                BtnLocalUrl.Content = $"Local: {localUrl}";
                BtnLocalUrl.NavigateUri = new Uri(localUrl);
                BtnLocalUrl.Visibility = Visibility.Visible;

                var tun = _engineTunnels.GetValueOrDefault(name);

                if (_selectedApp.AppType == "static_gh")
                {
                    if (_selectedApp.GhPagesDeployed)
                    {
                        EnsureGhUserFetch();
                        if (!string.IsNullOrEmpty(_ghUserCache))
                        {
                            var ghUrl = $"https://{_ghUserCache}.github.io/{name}/";
                            BtnPublicUrl.Content = $"Live: {ghUrl}";
                            BtnPublicUrl.NavigateUri = new Uri(ghUrl);
                            BtnPublicUrl.Visibility = Visibility.Visible;
                            TxtSeparator.Visibility = Visibility.Visible;
                            TxtAppStatus.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            BtnPublicUrl.Visibility = Visibility.Collapsed;
                            TxtSeparator.Visibility = Visibility.Visible;
                            TxtAppStatus.Text = "Live: Fetching URL...";
                            TxtAppStatus.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        BtnPublicUrl.Visibility = Visibility.Collapsed;
                        TxtSeparator.Visibility = Visibility.Collapsed;
                        TxtAppStatus.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    string? publicUrl = null;

                    if (tun != null && !string.IsNullOrEmpty(tun.DetectedUrl))
                    {
                        publicUrl = tun.DetectedUrl;
                    }
                    else if (!string.IsNullOrEmpty(_selectedApp.Domain))
                    {
                        publicUrl = $"https://{_selectedApp.Domain}";
                    }

                    if (!string.IsNullOrEmpty(publicUrl))
                    {
                        BtnPublicUrl.Content = $"Tunnel: {publicUrl}";
                        BtnPublicUrl.NavigateUri = new Uri(publicUrl);
                        BtnPublicUrl.Visibility = Visibility.Visible;
                        TxtSeparator.Visibility = Visibility.Visible;
                        TxtAppStatus.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        BtnPublicUrl.Visibility = Visibility.Collapsed;
                        TxtSeparator.Visibility = Visibility.Visible;
                        TxtAppStatus.Text = "(Tunnelling...)";
                        TxtAppStatus.Visibility = Visibility.Visible;
                    }
                }
            }
            else
            {
                BtnLocalUrl.Visibility = Visibility.Collapsed;

                if (_selectedApp.AppType == "static_gh" && _selectedApp.GhPagesDeployed)
                {
                    EnsureGhUserFetch();
                    if (!string.IsNullOrEmpty(_ghUserCache))
                    {
                        var ghUrl = $"https://{_ghUserCache}.github.io/{name}/";
                        BtnPublicUrl.Content = $"Live: {ghUrl}";
                        BtnPublicUrl.NavigateUri = new Uri(ghUrl);
                        BtnPublicUrl.Visibility = Visibility.Visible;
                        TxtSeparator.Visibility = Visibility.Visible;
                        TxtAppStatus.Text = "Stopped (Local)";
                        TxtAppStatus.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        BtnPublicUrl.Visibility = Visibility.Collapsed;
                        TxtSeparator.Visibility = Visibility.Collapsed;
                        TxtAppStatus.Text = "Stopped (Local) | Fetching GH URL...";
                        TxtAppStatus.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    BtnPublicUrl.Visibility = Visibility.Collapsed;
                    TxtSeparator.Visibility = Visibility.Collapsed;
                    TxtAppStatus.Text = "Not running";
                    TxtAppStatus.Visibility = Visibility.Visible;
                }
            }
        }

        private void EnsureGhUserFetch()
        {
            if (string.IsNullOrEmpty(_ghUserCache) && !_isFetchingGhUser)
            {
                _isFetchingGhUser = true;
                Task.Run(async () =>
                {
                    _ghUserCache = await GitHubService.GetLoggedInUserAsync();
                    _isFetchingGhUser = false;
                    this.DispatcherQueue.TryEnqueue(UpdateUiState);
                });
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null) return;
            var app = _selectedApp;

            // --- PRE-FLIGHT DEPENDENCY CHECK ---
            var missingDeps = new List<(string Name, string PackageId, bool IsCloudflare)>();

            if (app.AppType == "node" && string.IsNullOrEmpty(GitHubService.GetExecutablePath("node")))
            {
                missingDeps.Add(("Node.js Runtime", "OpenJS.NodeJS.LTS", false));
            }

            if (app.AppType != "static_gh" && string.IsNullOrEmpty(CloudflareService.GetExecutablePath()))
            {
                missingDeps.Add(("Cloudflare Tunnel (cloudflared)", "Cloudflare.cloudflared", true));
            }

            if (missingDeps.Count > 0)
            {
                foreach (var (depName, pkgId, isCf) in missingDeps)
                {
                    var promptDialog = new ContentDialog
                    {
                        Title = "Missing System Dependency",
                        Content = $"Localcel requires '{depName}' to run '{app.Name}'.\n\nWould you like to auto-install it via Winget?",
                        PrimaryButtonText = "Auto Install",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.Content.XamlRoot
                    };

                    if (await promptDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        var installer = new DependencyInstallerDialog(depName, pkgId)
                        {
                            XamlRoot = this.Content.XamlRoot
                        };

                        await installer.ShowAsync();

                        if (installer.IsSuccess)
                        {
                            if (isCf)
                            {
                                AppManager.SetPromptCfLoginAfterRestart(true);
                            }

                            var restartDialog = new RestartAppDialog(depName)
                            {
                                XamlRoot = this.Content.XamlRoot
                            };
                            await restartDialog.ShowAsync();
                            RestartAppDialog.TriggerRestart();
                            return;
                        }
                        else
                        {
                            return; // Installation failed or was closed
                        }
                    }
                    else
                    {
                        return; // User cancelled install prompt
                    }
                }
            }

            // Check if port is in use
            while (ManagedProcess.IsPortInUse(app.Port))
            {
                var nextFreePort = ManagedProcess.GetFirstAvailablePort(app.Port + 1);
                var portDialog = new ContentDialog
                {
                    Title = "Port in Use",
                    Content = $"Port {app.Port} is currently in use by another program.\n\nWould you like to switch to free port {nextFreePort}?",
                    PrimaryButtonText = $"Use Port {nextFreePort}",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };

                if (await portDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    AppManager.UpdateApp(app.Name, nextFreePort, app.Domain ?? "", app.AppType, app.GithubRepo ?? "", app.GhPagesDeployed);
                    app.Port = nextFreePort;
                    RefreshAppList();
                }
                else
                {
                    return; // User cancelled
                }
            }

            var srv = new ManagedProcess(app.Name);
            var tun = new ManagedProcess(app.Name, isTunnel: true);
            _engineServers[app.Name] = srv;
            _engineTunnels[app.Name] = tun;

            var appDir = Path.Combine(AppManager.AppsDir!, app.Name);

            // Launch HTTP/Node process
            if (app.AppType is "static_cf" or "static_gh")
            {
                srv.Start("python", $"-u -m http.server {app.Port}", appDir, line => AppendLog(app.Name, "server", line), this.DispatcherQueue);
            }
            else
            {
                var nodeBin = GitHubService.GetExecutablePath("node") ?? "node";
                srv.Start(nodeBin, app.Entry, appDir, line => AppendLog(app.Name, "server", line), this.DispatcherQueue);
            }

            // Launch Cloudflare Tunnel if available and not static_gh
            var cfBin = CloudflareService.GetExecutablePath();
            if (app.AppType != "static_gh" && !string.IsNullOrEmpty(cfBin))
            {
                if (!string.IsNullOrEmpty(app.Domain))
                {
                    try
                    {
                        var cfg = await CloudflareService.SetupNamedTunnelAsync(app.Name, app.Port, app.Domain);
                        tun.DetectedUrl = $"https://{app.Domain}";
                        tun.Start(cfBin, $"tunnel --config \"{cfg}\" run", AppManager.BaseDir!, line => AppendLog(app.Name, "tunnel", line), this.DispatcherQueue);
                    }
                    catch (Exception ex)
                    {
                        AppendLog(app.Name, "tunnel", $"❌ Cloudflare Tunnel Setup Error: {ex.Message}");
                    }
                }
                else
                {
                    tun.Start(cfBin, $"tunnel --url http://localhost:{app.Port}", AppManager.BaseDir!, line => AppendLog(app.Name, "tunnel", line), this.DispatcherQueue);
                }
            }
            else if (app.AppType == "static_gh")
            {
                AppendLog(app.Name, "tunnel", "GitHub Pages mode active. Local preview running. Use 'Deploy' to push to GitHub.");
            }
            else
            {
                AppendLog(app.Name, "tunnel", "cloudflared not installed. Skipping public tunneling.");
            }

            UpdateUiState();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null) return;
            StopApp(_selectedApp.Name);
            UpdateUiState();
        }

        private void StopApp(string name)
        {
            if (_engineServers.TryGetValue(name, out var srv)) srv.Stop();
            if (_engineTunnels.TryGetValue(name, out var tun)) tun.Stop();
        }

        private void StopAllServers()
        {
            foreach (var srv in _engineServers.Values) srv.Stop();
            foreach (var tun in _engineTunnels.Values) tun.Stop();
        }

        private async void BtnDeploy_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null || _selectedApp.AppType != "static_gh") return;
            var app = _selectedApp;

            var gitBin = GitHubService.GetExecutablePath("git");
            var ghBin = GitHubService.GetExecutablePath("gh");

            if (string.IsNullOrEmpty(gitBin) || string.IsNullOrEmpty(ghBin))
            {
                var dialog = new ContentDialog
                {
                    Title = "Git Tools Missing",
                    Content = "Git or GitHub CLI (gh) was not found. Please install Git and GitHub CLI.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            var user = await GitHubService.GetLoggedInUserAsync();
            if (string.IsNullOrEmpty(user))
            {
                var dialog = new ContentDialog
                {
                    Title = "Login Required",
                    Content = "Please log in to GitHub first using the Git Login button on the sidebar.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
                GitHubService.GitLogin();
                return;
            }

            BtnDeploy.IsEnabled = false;
            var isDeployed = app.GhPagesDeployed;
            var appDir = Path.Combine(AppManager.AppsDir!, app.Name);

            if (isDeployed)
            {
                BtnDeploy.Content = "Undeploying...";
                var success = await Task.Run(() => GitHubService.UndeployGitHubPagesAsync(app.Name, appDir, line => AppendLog(app.Name, "tunnel", line)));
                AppManager.UpdateApp(app.Name, app.Port, app.Domain ?? "", app.AppType, app.GithubRepo ?? "", ghPagesDeployed: false);
                app.GhPagesDeployed = false;
            }
            else
            {
                BtnDeploy.Content = "Deploying...";
                var success = await Task.Run(() => GitHubService.DeployToGitHubPagesAsync(app.Name, appDir, line => AppendLog(app.Name, "tunnel", line)));
                if (success)
                {
                    AppManager.UpdateApp(app.Name, app.Port, app.Domain ?? "", app.AppType, app.GithubRepo ?? "", ghPagesDeployed: true);
                    app.GhPagesDeployed = true;
                }
            }

            BtnDeploy.IsEnabled = true;
            RefreshAppList();
            UpdateUiState();
        }

        private async void BtnNewApp_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewAppDialog
            {
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                AppManager.CreateApp(dialog.AppName, dialog.Port, dialog.Domain, "server.js", dialog.AppType, "");
                RefreshAppList();
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null) return;
            var app = _selectedApp;

            var dialog = new EditAppDialog(app)
            {
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var wasRunning = _engineServers.TryGetValue(app.Name, out var srv) && srv.IsRunning;
                if (wasRunning) StopApp(app.Name);

                AppManager.UpdateApp(app.Name, dialog.Port, dialog.Domain, dialog.AppType, app.GithubRepo ?? "");
                RefreshAppList();

                if (wasRunning) BtnStart_Click(sender, e);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null) return;
            var app = _selectedApp;

            if (app.AppType == "static_gh")
            {
                var deleteDialog = new ContentDialog
                {
                    Title = "Delete Application",
                    Content = $"Do you want to delete the remote GitHub repository '{app.Name}' as well?\n\n- Delete Remote & Local: Removes GitHub repository and local workspace files.\n- Keep Remote: Removes local workspace files only.",
                    PrimaryButtonText = "Delete Remote & Local",
                    SecondaryButtonText = "Keep Remote (Local Only)",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var res = await deleteDialog.ShowAsync();
                if (res == ContentDialogResult.None) return;

                if (res == ContentDialogResult.Primary)
                {
                    var appDir = Path.Combine(AppManager.AppsDir!, app.Name);
                    AppendLog(app.Name, "tunnel", $"[GIT] Deleting remote repository {app.Name}...");
                    var (success, errorMsg) = await GitHubService.DeleteRemoteRepoAsync(app.Name, appDir);
                    if (!success)
                    {
                        var errDialog = new ContentDialog
                        {
                            Title = "Remote Deletion Warning",
                            Content = $"Could not delete remote repository:\n\n{errorMsg}\n\nLocal files will still be deleted.",
                            CloseButtonText = "OK",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errDialog.ShowAsync();
                    }
                }
            }
            else
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "Confirm App Deletion",
                    Content = $"Are you sure you want to delete '{app.Name}' and all its files?",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary) return;
            }

            StopApp(app.Name);
            AppManager.DeleteApp(app.Name);
            _selectedApp = null;
            RefreshAppList();

            TxtAppTitle.Text = "Select an application";
            TxtAppStatus.Text = "Not running";
            BtnLocalUrl.Visibility = Visibility.Collapsed;
            BtnPublicUrl.Visibility = Visibility.Collapsed;
            TxtSeparator.Visibility = Visibility.Collapsed;
            TxtServerLogs.Text = string.Empty;
            TxtTunnelLogs.Text = string.Empty;
            BtnDeploy.Visibility = Visibility.Collapsed;
        }

        private async void BtnCfLogin_Click(object sender, RoutedEventArgs e)
        {
            var cfBin = CloudflareService.GetExecutablePath();
            if (string.IsNullOrEmpty(cfBin))
            {
                var dialog = new ContentDialog
                {
                    Title = "Install Cloudflare CLI",
                    Content = "cloudflared was not found. Would you like to install it via winget?",
                    PrimaryButtonText = "Install",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.Content.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await CloudflareService.InstallCloudflaredAsync();
                }
                return;
            }

            CloudflareService.LoginCloudflare();
        }

        private async void BtnGitLogin_Click(object sender, RoutedEventArgs e)
        {
            await GitHubService.EnsureGitAsync();
            await GitHubService.EnsureGhAsync();

            var user = await GitHubService.GetLoggedInUserAsync();
            if (!string.IsNullOrEmpty(user))
            {
                var dialog = new ContentDialog
                {
                    Title = "GitHub Account",
                    Content = $"You are currently logged in as: {user}\n\nWould you like to log out?",
                    PrimaryButtonText = "Log Out",
                    CloseButtonText = "Keep Logged In",
                    XamlRoot = this.Content.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await GitHubService.LogoutAsync();
                }
                return;
            }

            var loginInfo = new ContentDialog
            {
                Title = "Git Authentication",
                Content = "Localcel uses GitHub CLI for secure authentication. A browser window will open to authorize this device.",
                PrimaryButtonText = "Open GitHub Login",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            if (await loginInfo.ShowAsync() == ContentDialogResult.Primary)
            {
                GitHubService.GitLogin();
            }
        }

        private async void BtnTunnelManager_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TunnelManagerDialog
            {
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void AppendLog(string appName, string logType, string line)
        {
            if (_selectedApp != null && _selectedApp.Name == appName)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    var targetTextBox = logType == "server" ? TxtServerLogs : TxtTunnelLogs;
                    targetTextBox.Text += line + Environment.NewLine;
                    targetTextBox.Select(targetTextBox.Text.Length, 0);
                });
            }
        }

        private void TrayShow_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            this.Activate();
        }

        private void TrayExit_Click(object sender, RoutedEventArgs e)
        {
            _isExiting = true;
            StopAllServers();
            Application.Current.Exit();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_isExiting) return;

            var runningApps = _engineServers.Where(kv => kv.Value.IsRunning).Select(kv => kv.Key).ToList();
            if (runningApps.Count > 0)
            {
                args.Handled = true; // Prevent app exit
                this.AppWindow.Hide();

                try
                {
                    TrayIcon.ShowNotification(
                        "Localcel Background Mode",
                        "Localcel has been minimized to the system tray to keep your servers active."
                    );
                }
                catch { }
            }
            else
            {
                StopAllServers();
                Application.Current.Exit();
            }
        }
    }
}
