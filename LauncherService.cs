using System;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace klauncher
{
    public enum LauncherState
    {
        Idle,
        Downloading,
        Paused,
        Extracting,
        Completed,
        Error
    }

    public class DownloadState
    {
        public int CompletedFiles { get; set; }
        public int TotalFiles { get; set; }
        public string TargetFolder { get; set; } = string.Empty;
        public DateTime SessionStart { get; set; } = DateTime.UtcNow;
    }

    public class LauncherService
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string Aria2Path = Path.Combine(BaseDir, "aria2c.exe");
        private static readonly string Aria2Conf = Path.Combine(BaseDir, "aria2.conf");
        private static readonly string TorrentDir = Path.Combine(BaseDir, "torrents");
        private const string StateFileName = "download_state.json";
        private const long RequiredBytes = 80L * 1024 * 1024 * 1024;

        private bool _isPaused;
        private bool _cancelRequested;
        private Process? _aria2Process;
        private int _completedFiles;
        private int _totalFiles;
        private long _totalSize;

        public event Action<LauncherState>? StateChanged;
        public event Action<double, string>? DownloadProgressChanged;
        public event Action<double>? TotalDownloadProgressChanged;
        public event Action<int, int>? PartDownloadStarted;
        public event Action<string>? StatusMessageChanged;
        public event Action<string>? ErrorOccurred;

        public LauncherState State
        {
            get => _state;
            private set
            {
                if (_state != value) { _state = value; StateChanged?.Invoke(_state); }
            }
        }
        private LauncherState _state = LauncherState.Idle;

        public static bool HasEnoughDiskSpace(string path)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
                var drive = new DriveInfo(root);
                return drive.AvailableFreeSpace >= RequiredBytes;
            }
            catch { return false; }
        }

        public static string GetRequiredSpaceString() => "~80 GB";

        private static string GetStateFilePath() => Path.Combine(BaseDir, StateFileName);

        public DownloadState? GetSavedState()
        {
            try
            {
                string path = GetStateFilePath();
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<DownloadState>(File.ReadAllText(path));
            }
            catch { return null; }
        }

        private void SaveState(string targetFolder)
        {
            try
            {
                var st = new DownloadState
                {
                    CompletedFiles = _completedFiles,
                    TotalFiles = _totalFiles,
                    TargetFolder = targetFolder,
                    SessionStart = DateTime.UtcNow
                };
                File.WriteAllText(GetStateFilePath(), JsonSerializer.Serialize(st));
            }
            catch { }
        }

        private void DeleteState()
        {
            try
            {
                string path = GetStateFilePath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        public void Pause()
        {
            if (State == LauncherState.Downloading)
            {
                _isPaused = true;
                _aria2Process?.CloseMainWindow();
                State = LauncherState.Paused;
                StatusMessageChanged?.Invoke("Download paused.");
            }
        }

        public void Resume(string targetFolder)
        {
            if (State == LauncherState.Paused)
            {
                _isPaused = false;
                _cancelRequested = false;
                StatusMessageChanged?.Invoke("Resuming download...");
                Task.Run(() => RunDownloadWorkflowAsync(targetFolder));
            }
        }

        public void Cancel()
        {
            _cancelRequested = true;
            try { _aria2Process?.Kill(); } catch { }
            State = LauncherState.Idle;
            StatusMessageChanged?.Invoke("Operation cancelled.");
        }

        public async Task StartDownloadAndInstallAsync(string targetFolder)
        {
            if (State == LauncherState.Downloading || State == LauncherState.Extracting)
                return;

            _isPaused = false;
            _cancelRequested = false;

            var saved = GetSavedState();
            if (saved != null && !string.IsNullOrEmpty(saved.TargetFolder) && saved.CompletedFiles < saved.TotalFiles)
            {
                _completedFiles = saved.CompletedFiles;
                _totalFiles = saved.TotalFiles;
                StatusMessageChanged?.Invoke($"Resuming download ({_completedFiles}/{_totalFiles} files)...");
            }
            else
            {
                _completedFiles = 0;
                _totalFiles = 0;
            }

            await RunDownloadWorkflowAsync(targetFolder);
        }

        private async Task RunDownloadWorkflowAsync(string targetFolder)
        {
            State = LauncherState.Downloading;

            try
            {
                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                if (!Directory.Exists(TorrentDir))
                    Directory.CreateDirectory(TorrentDir);

                if (!File.Exists(Aria2Path))
                {
                    State = LauncherState.Error;
                    ErrorOccurred?.Invoke("aria2c.exe not found. Please reinstall KLAUNCHER.");
                    return;
                }

                if (!HasEnoughDiskSpace(targetFolder))
                {
                    State = LauncherState.Error;
                    ErrorOccurred?.Invoke($"Not enough disk space. Need at least {GetRequiredSpaceString()} free.\n\nفضای دیسک کافی نیست. حداقل {GetRequiredSpaceString()} فضا لازم است.");
                    return;
                }

                string torrentPath = await EnsureTorrentFileAsync();
                if (string.IsNullOrEmpty(torrentPath))
                {
                    State = LauncherState.Error;
                    ErrorOccurred?.Invoke("Torrent file not found. Please reinstall KLAUNCHER.");
                    return;
                }

                if (_cancelRequested) return;

                StatusMessageChanged?.Invoke("Starting torrent download...");
                bool success = await RunAria2TorrentAsync(torrentPath, targetFolder);

                if (_cancelRequested) return;

                // Verify actual files were downloaded
                bool hasFiles = false;
                try
                {
                    var files = Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories);
                    hasFiles = files.Length > 5;
                }
                catch { }

                if (success && hasFiles)
                {
                    DeleteState();
                    int fileCount = 0;
                    try { fileCount = Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories).Length; } catch { }
                    State = LauncherState.Completed;
                    StatusMessageChanged?.Invoke($"Download complete! {fileCount} files saved to: {targetFolder}");
                    ErrorOccurred?.Invoke($"DONE: Download complete — {fileCount} files in:\n{targetFolder}\n\nNow install VMP manually from the setup files.\n\n---\n\nدانلود کامل شد — {fileCount} فایل در:\n{targetFolder}\n\nاکنون VMP را به صورت دستی نصب کنید.");
                }
                else if (_isPaused)
                {
                    SaveState(targetFolder);
                    State = LauncherState.Paused;
                }
                else
                {
                    State = LauncherState.Error;
                    if (!hasFiles)
                        ErrorOccurred?.Invoke("Download failed — no files were received. Check your internet connection and try again.\n\nدانلود ناموفق بود — هیچ فایلی دریافت نشد. اتصال اینترنت خود را بررسی کنید.");
                    else
                        ErrorOccurred?.Invoke("Download failed. Check your connection and try again.");
                }
            }
            catch (Exception ex)
            {
                State = LauncherState.Error;
                ErrorOccurred?.Invoke($"Error: {ex.Message}");
            }
        }

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

        private async Task<string> EnsureTorrentFileAsync()
        {
            string bundledPath = Path.Combine(BaseDir, "gta-v_legacy.torrent");
            if (File.Exists(bundledPath) && new FileInfo(bundledPath).Length > 1000)
                return bundledPath;

            string localPath = Path.Combine(TorrentDir, "gta-v_legacy.torrent");
            if (File.Exists(localPath) && new FileInfo(localPath).Length > 1000)
                return localPath;

            StatusMessageChanged?.Invoke("Downloading .torrent file...");
            try
            {
                using var response = await _http.GetAsync("https://se7en.ws/torrents/gta-v_legacy.torrent");
                response.EnsureSuccessStatusCode();
                if (!Directory.Exists(TorrentDir)) Directory.CreateDirectory(TorrentDir);
                using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);
                return localPath;
            }
            catch
            {
                return File.Exists(localPath) ? localPath : string.Empty;
            }
        }

        private async Task<bool> RunAria2TorrentAsync(string torrentPath, string targetFolder)
        {
            var args = new List<string>
            {
                $"--dir=\"{targetFolder}\"",
                "--follow-torrent=true",
                "--enable-color=false",
                "--summary-interval=1",
                "--console-log-level=notice",
                "--allow-overwrite=true",
                "--auto-file-renaming=false",
                "--continue=true",
                "--daemon=false",
                "--file-allocation=none",
                "--bt-stop-timeout=600",
                "--seed-time=0",
                "--bt-remove-unselected-file=true",
                "--bt-enable-lpd=true",
                "--bt-max-peers=512",
                "--enable-dht=true",
                "--dht-listen-port=6881",
                "--bt-listen-port=6881-6889",
                $"\"{torrentPath}\""
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = Aria2Path,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = targetFolder
            };

            _aria2Process = new Process { StartInfo = startInfo };
            _aria2Process.Start();

            var outputTask = ReadAria2OutputAsync(_aria2Process.StandardOutput);
            var errorTask = ReadAria2OutputAsync(_aria2Process.StandardError);

            await _aria2Process.WaitForExitAsync();
            await outputTask;
            await errorTask;

            return _aria2Process.ExitCode == 0;
        }

        private async Task ReadAria2OutputAsync(StreamReader reader)
        {
            while (true)
            {
                string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null) break;
                if (line.Length > 0) ParseAria2Line(line);
            }
        }

        private static readonly Regex ProgressRegex = new(@"\[#[a-f0-9]+\s+([\d.]+[GMK]iB)/([\d.]+[GMK]iB)\((\d+)%\)\s+CN:\d+\s+SD:\d+\s+DL:([\d.]+[GMK]iB)\s+ETA:([\w:]+)\]");
        private static readonly Regex FilesRegex = new(@"FILE:[^\(]+\((\d+)more\)");
        private static readonly Regex SizeRegex = new(@"Length:(\d+)");
        private static readonly Regex SpeedLineRegex = new(@"([\d.]+)\s*([MGK]iB)/s");

        private void ParseAria2Line(string line)
        {
            if (line.Contains("ANALYZE") || line.Contains("Verifying") || line.Contains("Loading"))
                return;

            var sizeMatch = SizeRegex.Match(line);
            if (sizeMatch.Success)
            {
                _totalSize = long.Parse(sizeMatch.Groups[1].Value);
                return;
            }

            // FILE: path/to/file.bin (139more)
            var filesMatch = FilesRegex.Match(line);
            if (filesMatch.Success)
            {
                int remaining = int.Parse(filesMatch.Groups[1].Value);
                _totalFiles = remaining + 1;
                PartDownloadStarted?.Invoke(_completedFiles, _totalFiles);
                return;
            }

            var progressMatch = ProgressRegex.Match(line);
            if (progressMatch.Success)
            {
                int percent = int.Parse(progressMatch.Groups[3].Value);
                string speed = progressMatch.Groups[4].Value;
                string eta = progressMatch.Groups[5].Value;

                DownloadProgressChanged?.Invoke(percent, $"{speed}/s - ETA: {eta}");

                long currentBytes = ParseBytes(progressMatch.Groups[1].Value);
                long totalBytes = ParseBytes(progressMatch.Groups[2].Value);

                if (totalBytes > 0)
                {
                    _totalSize = totalBytes;
                    TotalDownloadProgressChanged?.Invoke((double)currentBytes / totalBytes * 100);
                }
                return;
            }

            if (line.Contains("Download complete"))
            {
                _completedFiles++;
                if (_totalFiles > 0)
                {
                    TotalDownloadProgressChanged?.Invoke((double)_completedFiles / _totalFiles * 100);
                    PartDownloadStarted?.Invoke(_completedFiles, _totalFiles);
                }
                return;
            }

            var speedMatch = SpeedLineRegex.Match(line);
            if (speedMatch.Success)
            {
                DownloadProgressChanged?.Invoke(0, $"{speedMatch.Groups[1].Value} {speedMatch.Groups[2].Value}/s");
            }
        }

        private static long ParseBytes(string value)
        {
            string num = Regex.Match(value, @"[\d.]+").Value;
            double d = double.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
            if (value.Contains("GiB")) return (long)(d * 1024 * 1024 * 1024);
            if (value.Contains("MiB")) return (long)(d * 1024 * 1024);
            if (value.Contains("KiB")) return (long)(d * 1024);
            return (long)d;
        }

        private static string FindSetupExe(string targetFolder)
        {
            string[] searchPaths = new[]
            {
                Path.Combine(targetFolder, "!Setup", "GTA V Legacy [SE7EN RePack]", "setup.exe"),
                Path.Combine(targetFolder, "GTA V Legacy [SE7EN RePack]", "setup.exe"),
                Path.Combine(targetFolder, "setup.exe"),
                Path.Combine(targetFolder, "Setup.exe"),
                Path.Combine(targetFolder, "Setup", "setup.exe"),
                Path.Combine(targetFolder, "!Setup", "setup.exe"),
            };
            foreach (string p in searchPaths)
                if (File.Exists(p)) return p;

            // Deep search
            try
            {
                var found = Directory.GetFiles(targetFolder, "setup.exe", SearchOption.AllDirectories);
                if (found.Length > 0) return found[0];
            }
            catch { }

            return Path.Combine(targetFolder, "setup.exe");
        }

        public void Dispose()
        {
            try { _aria2Process?.Kill(); } catch { }
            _aria2Process?.Dispose();
        }

        public async Task<bool> InstallDirectXAsync()
        {
            StatusMessageChanged?.Invoke("Downloading DirectX Runtime...");
            string tempDir = Path.Combine(Path.GetTempPath(), "KLauncher_Setup");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string dxSetupPath = Path.Combine(tempDir, "dxwebsetup.exe");
            string dxUrl = "https://download.microsoft.com/download/1/7/1/1718CCC3-7CDD-41F4-890B-4090575B8356/dxwebsetup.exe";

            try
            {
                using var response = await _http.GetAsync(dxUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(dxSetupPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);

                StatusMessageChanged?.Invoke("Running DirectX installer...");
                var process = Process.Start(new ProcessStartInfo { FileName = dxSetupPath, Arguments = "/silent", UseShellExecute = true });
                if (process != null) { await process.WaitForExitAsync(); StatusMessageChanged?.Invoke("DirectX installed."); return true; }
            }
            catch (Exception ex) { ErrorOccurred?.Invoke($"DirectX install error: {ex.Message}"); }
            return false;
        }

        public async Task<bool> InstallVcRedistAsync()
        {
            StatusMessageChanged?.Invoke("Downloading Visual C++ Redistributable...");
            string tempDir = Path.Combine(Path.GetTempPath(), "KLauncher_Setup");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string vcPath = Path.Combine(tempDir, "vc_redist.x64.exe");
            string vcUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

            try
            {
                using var response = await _http.GetAsync(vcUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(vcPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);

                StatusMessageChanged?.Invoke("Running Visual C++ installer...");
                var process = Process.Start(new ProcessStartInfo { FileName = vcPath, Arguments = "/passive /norestart", UseShellExecute = true });
                if (process != null) { await process.WaitForExitAsync(); StatusMessageChanged?.Invoke("Visual C++ Redistributable installed."); return true; }
            }
            catch (Exception ex) { ErrorOccurred?.Invoke($"Visual C++ install error: {ex.Message}"); }
            return false;
        }
    }
}
