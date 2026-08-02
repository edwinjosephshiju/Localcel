using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Localcel_WinUI3.Models;

public static class AppManager
{
    private static readonly string GlobalAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".localcel");
    private static readonly string ConfigFile = Path.Combine(GlobalAppDir, "config.json");

    public static string? BaseDir { get; private set; }
    public static string? AppsDir { get; private set; }
    public static string? LogsDir { get; private set; }
    public static string? PidsDir { get; private set; }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    static AppManager()
    {
        Directory.CreateDirectory(GlobalAppDir);
        var ws = GetWorkspacePath();
        if (!string.IsNullOrEmpty(ws) && Directory.Exists(ws))
        {
            InitializeWorkspace(ws);
        }
    }

    public static bool GetPromptCfLoginAfterRestart()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("prompt_cf_login_after_restart", out var prop))
                {
                    return prop.GetBoolean();
                }
            }
        }
        catch { }
        return false;
    }

    public static void SetPromptCfLoginAfterRestart(bool value)
    {
        try
        {
            var wsPath = GetWorkspacePath() ?? "";
            var json = JsonSerializer.Serialize(new
            {
                workspace = wsPath,
                prompt_cf_login_after_restart = value
            }, JsonOpts);
            File.WriteAllText(ConfigFile, json);
        }
        catch { }
    }

    public static string? GetWorkspacePath()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("workspace", out var wsProp))
                {
                    var ws = wsProp.GetString();
                    if (!string.IsNullOrEmpty(ws) && Directory.Exists(ws))
                    {
                        return ws;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    public static void SetWorkspacePath(string path)
    {
        var finalPath = Path.GetFileName(path).Equals("localcel_workspace", StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.Combine(path, "localcel_workspace");

        Directory.CreateDirectory(finalPath);
        var json = JsonSerializer.Serialize(new { workspace = finalPath }, JsonOpts);
        File.WriteAllText(ConfigFile, json);
        InitializeWorkspace(finalPath);
    }

    public static void InitializeWorkspace(string path)
    {
        BaseDir = path;
        AppsDir = Path.Combine(BaseDir, "apps");
        LogsDir = Path.Combine(BaseDir, "logs");
        PidsDir = Path.Combine(BaseDir, "pids");

        EnsureDirectories();
    }

    public static void EnsureDirectories()
    {
        if (BaseDir != null)
        {
            if (AppsDir != null) Directory.CreateDirectory(AppsDir);
            if (LogsDir != null) Directory.CreateDirectory(LogsDir);
            if (PidsDir != null) Directory.CreateDirectory(PidsDir);
        }
    }

    public static List<AppConfig> GetApps()
    {
        EnsureDirectories();
        var apps = new List<AppConfig>();

        if (AppsDir == null || !Directory.Exists(AppsDir))
            return apps;

        foreach (var dir in Directory.GetDirectories(AppsDir))
        {
            var configPath = Path.Combine(dir, "config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                    if (config != null)
                    {
                        if (string.IsNullOrEmpty(config.AppType)) config.AppType = "node";
                        if (config.AppType == "static") config.AppType = "static_gh";
                        apps.Add(config);
                    }
                }
                catch { }
            }
        }

        return apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void CreateApp(string name, int port, string domain = "", string entry = "server.js", string appType = "node", string githubRepo = "", bool ghPagesDeployed = false)
    {
        EnsureDirectories();
        if (AppsDir == null) return;

        var appDir = Path.Combine(AppsDir, name);
        Directory.CreateDirectory(appDir);

        var config = new AppConfig
        {
            Name = name,
            Port = port,
            Entry = entry,
            Domain = string.IsNullOrWhiteSpace(domain) ? null : domain,
            AppType = appType,
            GithubRepo = string.IsNullOrWhiteSpace(githubRepo) ? null : githubRepo,
            GhPagesDeployed = ghPagesDeployed
        };

        var json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(Path.Combine(appDir, "config.json"), json);

        if (appType == "node")
        {
            var serverJs = $@"const http = require('http');
const port = process.env.PORT || {port};
const server = http.createServer((req, res) => {{
  console.log(`[${{new Date().toISOString()}}] ${{req.method}} ${{req.url}}`);
  res.end('Localcel: {name} is running!');
}});
server.listen(port, () => {{
  console.log(`Server started on port ${{port}}`);
}});";
            File.WriteAllText(Path.Combine(appDir, entry), serverJs);
        }
        else if (appType is "static_cf" or "static_gh")
        {
            var indexHtml = $@"<!DOCTYPE html>
<html>
<head>
    <title>{name}</title>
    <style>body {{ font-family: sans-serif; text-align: center; padding: 50px; }} h1 {{ color: #2D3748; }}</style>
</head>
<body>
    <h1>Localcel: {name} is running!</h1>
    <p>This is a static site managed by Localcel.</p>
</body>
</html>";
            File.WriteAllText(Path.Combine(appDir, "index.html"), indexHtml);

            // Initialize Git repository if git is available
            var gitBin = Services.GitHubService.GetExecutablePath("git");
            if (!string.IsNullOrEmpty(gitBin))
            {
                RunQuietCommand(gitBin, "init", appDir);
                RunQuietCommand(gitBin, "config user.name \"Localcel Deployer\"", appDir);
                RunQuietCommand(gitBin, "config user.email \"deploy@localcel.app\"", appDir);
                RunQuietCommand(gitBin, "add .", appDir);
                RunQuietCommand(gitBin, "commit -m \"Initial commit from Localcel\"", appDir);
                RunQuietCommand(gitBin, "branch -M main", appDir);
            }
        }
    }

    public static void UpdateApp(string name, int port, string domain = "", string appType = "node", string githubRepo = "", bool? ghPagesDeployed = null)
    {
        if (AppsDir == null) return;
        var configPath = Path.Combine(AppsDir, name, "config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig { Name = name };

                config.Port = port;
                config.AppType = appType;
                config.GithubRepo = string.IsNullOrWhiteSpace(githubRepo) ? null : githubRepo;
                config.Domain = string.IsNullOrWhiteSpace(domain) ? null : domain;

                if (ghPagesDeployed.HasValue)
                {
                    config.GhPagesDeployed = ghPagesDeployed.Value;
                }

                var updatedJson = JsonSerializer.Serialize(config, JsonOpts);
                File.WriteAllText(configPath, updatedJson);
            }
            catch { }
        }
    }

    public static void DeleteApp(string name)
    {
        if (AppsDir == null) return;
        var appDir = Path.Combine(AppsDir, name);
        if (Directory.Exists(appDir))
        {
            DeleteDirectoryRecursive(appDir);
        }
    }

    private static void DeleteDirectoryRecursive(string path)
    {
        foreach (var file in Directory.GetFiles(path))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var dir in Directory.GetDirectories(path))
        {
            DeleteDirectoryRecursive(dir);
        }

        Directory.Delete(path, false);
    }

    private static void RunQuietCommand(string exe, string args, string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch { }
    }
}
