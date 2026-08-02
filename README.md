# ⚡ Localcel — Native WinUI 3 Localhost & Cloudflare Tunnel Manager

[![Target Framework](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![UI Architecture](https://img.shields.io/badge/WinUI-3-0078D4.svg)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
[![Design System](https://img.shields.io/badge/Windows%2011-Mica%20Backdrop-00A4EF.svg)](https://learn.microsoft.com/en-us/windows/apps/design/style/mica)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Localcel** is a native Windows 11 desktop application designed to effortlessly manage local development web servers, Cloudflare Tunnels, and GitHub Pages deployments — all from a single translucent, high-performance C# WinUI 3 interface.

---

## ✨ Features

- **🎨 Windows 11 Mica & Acrylic Visuals**: Immersive Windows 11 translucent Mica backdrop, rounded Fluent controls, and full dark mode aesthetic.
- **🚀 Node.js & Static Site Management**: Easily spin up local Node.js servers or static directory previews with automatic port conflict detection and resolution.
- **🌐 Instant Cloudflare Tunnels**: Publicly expose any local port (`http://localhost:3000`) with one-click `cloudflared` tunnels or custom domains.
- **🐙 GitHub Pages One-Click Deploy**: Deploy static sites directly to GitHub Pages using integrated GitHub CLI (`gh`).
- **🛠️ Integrated Dependency Checker & Winget Installer**: Auto-detects missing `Node.js` or `cloudflared` runtimes before server launch, installs them via `winget` with live progress logs, and handles post-installation application restart.
- **🔔 System Tray Integration**: Minimises to system tray (`H.NotifyIcon.WinUI`) with dynamic right-click context menus and quick restore.
- **💻 Full-Height Terminal Viewer**: Real-time monospace log output for server processes and tunnel operations with auto-scroll and selectable text.

---

## 🚀 Quick Start — Installation

Download the latest release executable from the **[Releases Page](https://github.com/edwinjosephshiju/Localcel/releases)**:

1. Download **`Localcel.exe`** (or `Localcel_v1.1_win-x64.zip`).
2. Run `Localcel.exe`.
3. Select your local server workspace folder on first launch.

---

## 🛠️ How to Build From Source

### Prerequisites
- **.NET 8.0 SDK** or **.NET 9.0 SDK**
- **Windows 10/11** (Build 19041 or higher)

### Build Steps

1. Clone the repository:
   ```powershell
   git clone https://github.com/edwinjosephshiju/Localcel.git
   cd Localcel
   ```

2. Build & publish the standalone executable:
   ```powershell
   dotnet publish Localcel_WinUI3/Localcel_WinUI3.csproj -c Release -r win-x64 -o dist -p:WindowsPackageType=None
   ```

3. Run the application from the output directory:
   ```powershell
   .\dist\Localcel_WinUI3.exe
   ```

---

## 📂 Project Architecture

```
Localcel/
├── .github/
│   └── workflows/
│       └── build-release.yml    # Automated GitHub Actions build workflow
├── Localcel_WinUI3/
│   ├── Dialogs/
│   │   ├── NewAppDialog.xaml              # New app creation dialog
│   │   ├── EditAppDialog.xaml             # Edit configuration dialog
│   │   ├── TunnelManagerDialog.xaml       # Cloudflare tunnel manager dialog
│   │   ├── DependencyInstallerDialog.xaml # Winget installer progress modal
│   │   └── RestartAppDialog.xaml          # 10s countdown restart dialog
│   ├── Models/
│   │   ├── AppConfig.cs                   # App configuration schema
│   │   └── AppItemViewModel.cs            # Data binding viewmodel
│   ├── Services/
│   │   ├── AppManager.cs                  # Workspace & JSON storage manager
│   │   ├── CloudflareService.cs           # cloudflared CLI integration
│   │   ├── GitHubService.cs               # git & gh CLI integration
│   │   └── ProcessRunner.cs               # System.Diagnostics process runner
│   ├── MainWindow.xaml                    # Main dashboard layout
│   ├── MainWindow.xaml.cs                 # Main window logic & Mica backdrop
│   └── Localcel_WinUI3.csproj             # WinUI 3 C# project file
├── README.md
├── .gitignore
├── run.bat                                # Quick launch batch script
└── run_localcel.bat                       # Standalone launch script
```

---

## 📄 License

Distributed under the **MIT License**. See `LICENSE` for details.
