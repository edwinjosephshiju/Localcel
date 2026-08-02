using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Localcel_WinUI3.Models;

namespace Localcel_WinUI3.Services
{
    public static class GitHubService
    {
        public static string? GetExecutablePath(string name)
        {
            var exeName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.exe";

            // Check system PATH via where.exe
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = name,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadLine();
                    proc.WaitForExit();
                    if (!string.IsNullOrWhiteSpace(output) && File.Exists(output.Trim()))
                    {
                        return output.Trim();
                    }
                }
            }
            catch { }

            // Check bundled offline binaries directory if present in ~/.localcel/bin
            var userBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".localcel", "bin");
            if (Directory.Exists(userBin))
            {
                if (name.Equals("git", StringComparison.OrdinalIgnoreCase))
                {
                    var gitPath = Path.Combine(userBin, "git", "cmd", "git.exe");
                    if (File.Exists(gitPath)) return gitPath;
                }
                else
                {
                    var targetPath = Path.Combine(userBin, exeName);
                    if (File.Exists(targetPath)) return targetPath;
                }
            }

            return null;
        }

        public static async Task<bool> EnsureGitAsync()
        {
            if (!string.IsNullOrEmpty(GetExecutablePath("git"))) return true;
            return await InstallWingetPackageAsync("Git.Git");
        }

        public static async Task<bool> EnsureGhAsync()
        {
            if (!string.IsNullOrEmpty(GetExecutablePath("gh"))) return true;
            return await InstallWingetPackageAsync("GitHub.cli");
        }

        private static async Task<bool> InstallWingetPackageAsync(string packageId)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install {packageId} --accept-source-agreements --accept-package-agreements",
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

        public static async Task<string?> GetLoggedInUserAsync()
        {
            var gh = GetExecutablePath("gh");
            if (string.IsNullOrEmpty(gh)) return null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = gh,
                    Arguments = "auth status",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc == null) return null;

                var stdout = await proc.StandardOutput.ReadToEndAsync();
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                var output = stderr + stdout;
                var match = Regex.Match(output, @"Logged in to github\.com account ([a-zA-Z0-9-]+)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch { }

            return null;
        }

        public static void GitLogin()
        {
            var gh = GetExecutablePath("gh");
            if (string.IsNullOrEmpty(gh)) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = gh,
                    Arguments = "auth login --web --scopes repo,delete_repo",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/login",
                    UseShellExecute = true
                });
            }
        }

        public static async Task LogoutAsync()
        {
            var gh = GetExecutablePath("gh");
            if (string.IsNullOrEmpty(gh)) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = gh,
                    Arguments = "auth logout --hostname github.com",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync();
            }
            catch { }
        }

        public static async Task<bool> DeployToGitHubPagesAsync(string appName, string appPath, Action<string> logCallback)
        {
            var gitBin = GetExecutablePath("git");
            var ghBin = GetExecutablePath("gh");

            if (string.IsNullOrEmpty(gitBin) || string.IsNullOrEmpty(ghBin))
            {
                logCallback("❌ Git or GitHub CLI (gh) not found on system.");
                return false;
            }

            logCallback("--- Starting GitHub Deploy Sequence ---");

            await RunCmdAsync(gitBin, "config user.name \"Localcel Deployer\"", appPath, logCallback);
            await RunCmdAsync(gitBin, "config user.email \"deploy@localcel.app\"", appPath, logCallback);

            await RunCmdAsync(gitBin, "add .", appPath, logCallback);
            await RunCmdAsync(gitBin, "commit -m \"Auto-deploy from Localcel\"", appPath, logCallback);

            // Check if remote origin exists
            var remoteOutput = await GetCmdOutputAsync(gitBin, "remote -v", appPath);
            if (!remoteOutput.Contains("origin"))
            {
                logCallback($"Creating new public GitHub repository '{appName}' via GitHub CLI...");
                var createCode = await RunCmdAsync(ghBin, $"repo create {appName} --public --source=. --push", appPath, logCallback);
                if (createCode != 0)
                {
                    logCallback("❌ Failed to create remote repository.");
                    return false;
                }
            }
            else
            {
                logCallback("Pushing to GitHub...");
                await RunCmdAsync(gitBin, "push -u origin main", appPath, logCallback);
            }

            // Enable GitHub Pages
            var repoName = (await GetCmdOutputAsync(ghBin, "repo view --json nameWithOwner -q .nameWithOwner", appPath)).Trim();
            if (!string.IsNullOrEmpty(repoName))
            {
                logCallback("Enabling GitHub Pages...");
                await RunCmdAsync(ghBin, $"api -X POST /repos/{repoName}/pages -f source[branch]=main -f source[path]=/", appPath, logCallback);
            }

            logCallback("✅ Deploy successful! Your site is live on GitHub Pages.");
            return true;
        }

        public static async Task<bool> UndeployGitHubPagesAsync(string appName, string appPath, Action<string> logCallback)
        {
            var ghBin = GetExecutablePath("gh");
            if (string.IsNullOrEmpty(ghBin)) return false;

            logCallback("--- Disabling GitHub Pages ---");
            var repoName = (await GetCmdOutputAsync(ghBin, "repo view --json nameWithOwner -q .nameWithOwner", appPath)).Trim();

            if (!string.IsNullOrEmpty(repoName))
            {
                await RunCmdAsync(ghBin, $"api -X DELETE /repos/{repoName}/pages", appPath, logCallback);
                logCallback("✅ GitHub Pages has been disabled. (Repository remains intact)");
                return true;
            }
            else
            {
                logCallback("❌ Could not determine remote repository name.");
                return false;
            }
        }

        public static async Task<(bool Success, string ErrorMessage)> DeleteRemoteRepoAsync(string appName, string appPath)
        {
            var ghBin = GetExecutablePath("gh");
            if (string.IsNullOrEmpty(ghBin))
                return (false, "GitHub CLI (gh) not found.");

            var repoName = (await GetCmdOutputAsync(ghBin, "repo view --json nameWithOwner -q .nameWithOwner", appPath)).Trim();
            if (string.IsNullOrEmpty(repoName))
                repoName = appName;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ghBin,
                    Arguments = $"repo delete {repoName} --yes",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc == null) return (false, "Failed to start gh process.");

                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (proc.ExitCode == 0)
                {
                    return (true, string.Empty);
                }
                return (false, stderr.Trim());
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static async Task<int> RunCmdAsync(string exe, string args, string cwd, Action<string> logCallback)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = cwd,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var proc = new Process { StartInfo = psi };
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) logCallback(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) logCallback(e.Data); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync();
                return proc.ExitCode;
            }
            catch (Exception ex)
            {
                logCallback($"[ERROR] Executing {Path.GetFileName(exe)} {args}: {ex.Message}");
                return -1;
            }
        }

        private static async Task<string> GetCmdOutputAsync(string exe, string args, string cwd)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = cwd,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc == null) return string.Empty;

                var stdout = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                return stdout;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
