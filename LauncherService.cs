using System;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Threading;
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
        public int CompletedFiles { get; set; } = 0;
        public int TotalFiles { get; set; } = 0;
        public string TargetFolder { get; set; } = string.Empty;
        public string TorrentName { get; set; } = string.Empty;
        public DateTime SessionStart { get; set; } = DateTime.UtcNow;
    }

    public class LauncherService
    {
        // ── Paths ────────────────────────────────────────────────────────────────
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string Aria2Path = Path.Combine(BaseDir, "aria2c.exe");
        private static readonly string Aria2Conf = Path.Combine(BaseDir, "aria2.conf");
        private static readonly string TorrentDir = Path.Combine(BaseDir, "torrents");
        private const string StateFileName = "download_state.json";

        // ── Config ───────────────────────────────────────────────────────────────
        private const string TorrentUrl = "https://se7en.ws/torrents/gta-v_legacy.torrent";
        private const string SetupExe = "setup.exe";

        // ── State ────────────────────────────────────────────────────────────────
        private bool _isPaused = false;
        private bool _cancelRequested = false;
        private Process? _aria2Process;

        // Progress
        private int _completedFiles = 0;
        private int _totalFiles = 0;
        private long _totalDownloaded = 0;
        private long _totalSize = 0;

        // ── Events ───────────────────────────────────────────────────────────────
        public event Action<LauncherState>? StateChanged;
        public event Action<double, string>? DownloadProgressChanged;
        public event Action<double>? TotalDownloadProgressChanged;
        public event Action<int, int>? PartDownloadStarted;
        public event Action<string>? StatusMessageChanged;
        public event Action<string>? ErrorOccurred;
        public event Action<TimeSpan>? EstimatedTimeRemainingChanged;
        public event Action<string, double>? ExtractionProgressChanged;

        public LauncherState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(_state);
                }
            }
        }
        private LauncherState _state = LauncherState.Idle;

        // ── State persistence ────────────────────────────────────────────────────
        private static string GetStateFilePath() =>
            Path.Combine(BaseDir, StateFileName);

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
                    TorrentName = "GTA V Legacy [SE7EN RePack]",
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

        // ── Public Control Methods ───────────────────────────────────────────────
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

        // ── Core Workflow ────────────────────────────────────────────────────────
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

                string torrentPath = await EnsureTorrentFileAsync();
                if (string.IsNullOrEmpty(torrentPath))
                {
                    State = LauncherState.Error;
                    ErrorOccurred?.Invoke("Failed to download .torrent file. Check your internet connection.");
                    return;
                }

                if (_cancelRequested) return;

                StatusMessageChanged?.Invoke("Starting torrent download...");
                bool success = await RunAria2TorrentAsync(torrentPath, targetFolder);

                if (_cancelRequested) return;

                if (success)
                {
                    DeleteState();
                    StatusMessageChanged?.Invoke("Download complete! Running setup...");

                    string setupPath = Path.Combine(targetFolder, "!Setup", "GTA V Legacy [SE7EN RePack]", SetupExe);
                    if (!File.Exists(setupPath))
                        setupPath = Path.Combine(targetFolder, SetupExe);

                    if (File.Exists(setupPath))
                    {
                        State = LauncherState.Extracting;
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = setupPath,
                            WorkingDirectory = Path.GetDirectoryName(setupPath) ?? targetFolder,
                            UseShellExecute = true
                        });
                        State = LauncherState.Completed;
                        StatusMessageChanged?.Invoke("Setup launched successfully!");
                    }
                    else
                    {
                        State = LauncherState.Completed;
                        StatusMessageChanged?.Invoke("Download completed. Run setup.exe manually.");
                    }
                }
                else if (_isPaused)
                {
                    SaveState(targetFolder);
                    State = LauncherState.Paused;
                }
                else
                {
                    State = LauncherState.Error;
                    ErrorOccurred?.Invoke("Download failed. Check your connection and try again.");
                }
            }
            catch (Exception ex)
            {
                State = LauncherState.Error;
                ErrorOccurred?.Invoke($"Error: {ex.Message}");
            }
        }

        // ── Download .torrent file ──────────────────────────────────────────────
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

        private async Task<string> EnsureTorrentFileAsync()
        {
            string localPath = Path.Combine(TorrentDir, "gta-v_legacy.torrent");

            if (File.Exists(localPath) && new FileInfo(localPath).Length > 1000)
                return localPath;

            StatusMessageChanged?.Invoke("Downloading .torrent file...");
            try
            {
                using var response = await _http.GetAsync(TorrentUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);
                return localPath;
            }
            catch
            {
                return File.Exists(localPath) ? localPath : string.Empty;
            }
        }

        // ── Run aria2 in torrent mode ───────────────────────────────────────────
        private async Task<bool> RunAria2TorrentAsync(string torrentPath, string targetFolder)
        {
            var args = new List<string>
            {
                $"--follow-torrent=true",
                $"--dir=\"{targetFolder}\"",
                $"--input-file=\"{torrentPath}\"",
                "--enable-color=false",
                "--summary-interval=1",
                "--console-log-level=notice",
                "--allow-overwrite=true",
                "--auto-file-renaming=false",
                "--continue=true",
                "--daemon=false",
                "--file-allocation=none",
                "--bt-stop-timeout=0",
                "--seed-time=0",
                "--bt-remove-unselected-file=true",
                "--max-overall-upload-limit=0",
                "--bt-tracker=tracker.7n.re/announce,tracker.se7en.ws/announce,tracker.7launcher.com/announce,tracker.7launcher.ru/announce",
                "--bt-enable-lpd=true",
                "--bt-max-peers=512",
                "--bt-request-peer-speed-limit=10M"
            };

            if (File.Exists(Aria2Conf))
                args.Add($"--conf-path=\"{Aria2Conf}\"");

            var startInfo = new ProcessStartInfo
            {
                FileName = Aria2Path,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
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
            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;
                ParseAria2Line(line);
            }
        }

        // ── Parse aria2 torrent output ──────────────────────────────────────────
        private readonly Regex _progressRegex = new(@"\[#\w+\s+([\d.]+[GMKB]+)/([\d.]+[GMKB])\s*\((\d+)%\)\s+([\d.]+[GMKB]+)/s\s+ETA:([\w:]+)");
        private readonly Regex _summaryRegex = new(@"Summary:");
        private readonly Regex _filesRegex = new(@"FILE:\d+/(\d+)");
        private readonly Regex _seedPeerRegex = new(@"Seeds:(\d+)\s+Peers:(\d+)");
        private readonly Regex _downloadLenRegex = new(@"Length:(\d+)");
        private readonly Regex _bitfieldRegex = new(@"BitTorrent");
        private readonly Regex _nameRegex = new(@"Name:([^\n]+)");
        private readonly Regex _progressBarRegex = new(@"\[#{0,1}[A-Fa-f0-9]+\s+([\d.]+[GMKB]+)/([\d.]+[GMKB])\s*\((\d+)%\)\s+([\d.]+[GMKB]+)/s\s+ETA:([\w:]+)\]");

        private void ParseAria2Line(string line)
        {
            if (line.Contains("ANALYZE") || line.Contains("Verifying") || line.Contains("OK") || line.Contains("Loading"))
                return;

            var nameMatch = _nameRegex.Match(line);
            if (nameMatch.Success)
            {
                StatusMessageChanged?.Invoke($"Downloading: {nameMatch.Groups[1].Value.Trim()}");
                return;
            }

            var dlLenMatch = _downloadLenRegex.Match(line);
            if (dlLenMatch.Success)
            {
                _totalSize = long.Parse(dlLenMatch.Groups[1].Value);
                return;
            }

            var filesMatch = _filesRegex.Match(line);
            if (filesMatch.Success)
            {
                _totalFiles = int.Parse(filesMatch.Groups[1].Value);
                PartDownloadStarted?.Invoke(_completedFiles, _totalFiles);
                return;
            }

            var progressMatch = _progressBarRegex.Match(line);
            if (!progressMatch.Success)
                progressMatch = _progressRegex.Match(line);

            if (progressMatch.Success)
            {
                string currentStr = progressMatch.Groups[1].Value;
                string totalStr = progressMatch.Groups[2].Value;
                int percent = int.Parse(progressMatch.Groups[3].Value);
                string speed = progressMatch.Groups[4].Value;
                string eta = progressMatch.Groups[5].Value;

                DownloadProgressChanged?.Invoke(percent, $"{speed} - ETA: {eta}");

                long currentBytes = ParseBytes(currentStr);
                long totalBytes = ParseBytes(totalStr);

                if (totalBytes > 0)
                {
                    double totalProgress = ((double)currentBytes / totalBytes) * 100;
                    TotalDownloadProgressChanged?.Invoke(totalProgress);

                    _totalDownloaded = currentBytes;
                    _totalSize = totalBytes;
                }
                else if (_totalSize > 0)
                {
                    double totalProgress = ((double)currentBytes / _totalSize) * 100;
                    TotalDownloadProgressChanged?.Invoke(totalProgress);
                }

                return;
            }

            var seedPeerMatch = _seedPeerRegex.Match(line);
            if (seedPeerMatch.Success)
            {
                int seeds = int.Parse(seedPeerMatch.Groups[1].Value);
                int peers = int.Parse(seedPeerMatch.Groups[2].Value);
                StatusMessageChanged?.Invoke($"Seeds: {seeds} | Peers: {peers}");
                return;
            }

            if (line.Contains("Download complete") || line.Contains("Download Complete"))
            {
                _completedFiles++;
                if (_totalFiles > 0)
                {
                    double totalProgress = ((double)_completedFiles / _totalFiles) * 100;
                    TotalDownloadProgressChanged?.Invoke(totalProgress);
                    PartDownloadStarted?.Invoke(_completedFiles, _totalFiles);
                }
                return;
            }

            if (line.Contains("used") && line.Contains("MiB") || line.Contains("GiB"))
            {
                var speedMatch = Regex.Match(line, @"([\d.]+)\s*([MGK]iB)/s");
                if (speedMatch.Success)
                {
                    string spd = speedMatch.Groups[1].Value;
                    string unit = speedMatch.Groups[2].Value;
                    DownloadProgressChanged?.Invoke(0, $"{spd} {unit}/s");
                }
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

        public void Dispose()
        {
            try { _aria2Process?.Kill(); } catch { }
            _aria2Process?.Dispose();
        }

        // ── Dependency Installers ────────────────────────────────────────────────
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
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = dxSetupPath,
                    Arguments = "/silent",
                    UseShellExecute = true
                });
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    StatusMessageChanged?.Invoke("DirectX installed or updated.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"DirectX install error: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> InstallVcRedistAsync()
        {
            StatusMessageChanged?.Invoke("Downloading Visual C++ Redistributable...");
            string tempDir = Path.Combine(Path.GetTempPath(), "KLauncher_Setup");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string vcRedistPath = Path.Combine(tempDir, "vc_redist.x64.exe");
            string vcUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

            try
            {
                using var response = await _http.GetAsync(vcUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(vcRedistPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);

                StatusMessageChanged?.Invoke("Running Visual C++ installer...");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = vcRedistPath,
                    Arguments = "/passive /norestart",
                    UseShellExecute = true
                });
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    StatusMessageChanged?.Invoke("Visual C++ Redistributable 2015-2022 installed.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Visual C++ install error: {ex.Message}");
            }
            return false;
        }
    }
}
