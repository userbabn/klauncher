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
        public DateTime SessionStart { get; set; } = DateTime.UtcNow;
    }

    public class LauncherService
    {
        // ── Paths ────────────────────────────────────────────────────────────────
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string Aria2Path = Path.Combine(BaseDir, "aria2c.exe");
        private static readonly string Aria2Conf = Path.Combine(BaseDir, "aria2.conf");
        private const string StateFileName = "download_state.json";

        // ── Config ───────────────────────────────────────────────────────────────
        private const string BaseUrl = "https://cdn.vmp.ir/game/1/GTA_V_VMP_Setup";
        private const string SetupExe = "setup.exe";

        // ── State ────────────────────────────────────────────────────────────────
        private LauncherState _state = LauncherState.Idle;
        private bool _isPaused = false;
        private bool _cancelRequested = false;
        private Process? _aria2Process;
        private CancellationTokenSource? _cts;

        // Progress
        private int _completedFiles = 0;
        private int _totalFiles = 0;
        private long _totalBytesDownloaded = 0;
        private double _downloadSpeed = 0;

        // ── Events ───────────────────────────────────────────────────────────────
        public event Action<LauncherState>? StateChanged;
        public event Action<double, string>? DownloadProgressChanged;    // (%, speed)
        public event Action<double>? TotalDownloadProgressChanged;
        public event Action<int, int>? PartDownloadStarted;
        public event Action<string, double>? ExtractionProgressChanged;
        public event Action<string>? StatusMessageChanged;
        public event Action<string>? ErrorOccurred;
        public event Action<TimeSpan>? EstimatedTimeRemainingChanged;

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
                _aria2Process?.CloseMainWindow(); // sends SIGINT to aria2 (graceful stop)
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

                // Check aria2 exists
                if (!File.Exists(Aria2Path))
                {
                    State = LauncherState.Error;
                    ErrorOccurred?.Invoke("aria2c.exe not found. Please reinstall KLAUNCHER.");
                    return;
                }

                // Generate aria2 input file
                string inputFile = Path.Combine(BaseDir, "download_list.txt");
                GenerateAria2InputFile(inputFile, targetFolder);

                _totalFiles = CountFilesInInputFile(inputFile);
                StatusMessageChanged?.Invoke($"Downloading {_totalFiles} files with aria2 (64 connections each)...");

                // Run aria2
                bool success = await RunAria2Async(inputFile, targetFolder);

                if (_cancelRequested) return;

                if (success)
                {
                    DeleteState();
                    StatusMessageChanged?.Invoke("Download complete! Running setup...");

                    // Run setup.exe
                    string setupPath = Path.Combine(targetFolder, SetupExe);
                    if (File.Exists(setupPath))
                    {
                        State = LauncherState.Extracting;
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = setupPath,
                            WorkingDirectory = targetFolder,
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

        // ── Generate aria2 input file ────────────────────────────────────────────
        private void GenerateAria2InputFile(string filePath, string targetFolder)
        {
            // This generates the list of files to download
            // Format: URL\n  dir=destination\n  out=filename\n\n
            // The actual URLs will be set when the user provides hosting links
            // For now, generate from a known file list

            var lines = new List<string>();

            // Scan the setup folder for all files
            string setupSource = Path.Combine(targetFolder, "!Setup", "GTA V Legacy [SE7EN RePack]");
            if (Directory.Exists(setupSource))
            {
                foreach (string file in Directory.GetFiles(setupSource, "*", SearchOption.AllDirectories))
                {
                    string relativePath = file[(setupSource.Length + 1)..];
                    string url = $"{BaseUrl}/{relativePath.Replace('\\', '/')}";
                    string destDir = Path.GetDirectoryName(file) ?? targetFolder;

                    lines.Add(url);
                    lines.Add($"  dir={destDir}");
                    lines.Add($"  out={Path.GetFileName(file)}");
                    lines.Add("");
                }
            }

            File.WriteAllLines(filePath, lines);
        }

        private int CountFilesInInputFile(string filePath)
        {
            if (!File.Exists(filePath)) return 0;
            return File.ReadAllLines(filePath).Count(l => l.StartsWith("http"));
        }

        // ── Run aria2 ───────────────────────────────────────────────────────────
        private async Task<bool> RunAria2Async(string inputFile, string targetFolder)
        {
            var args = new List<string>
            {
                $"--input-file=\"{inputFile}\"",
                $"--conf-path=\"{Aria2Conf}\"",
                "--enable-color=false",
                "--summary-interval=1",
                "--console-log-level=notice",
                "--allow-overwrite=true",
                "--auto-file-renaming=false",
                "--continue=true",
                "--daemon=false",
                "--file-allocation=none"
            };

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

            // Read output asynchronously
            var outputTask = ReadAria2OutputAsync(_aria2Process.StandardOutput);
            var errorTask = ReadAria2OutputAsync(_aria2Process.StandardError);

            // Wait for process to exit
            await _aria2Process.WaitForExitAsync();

            // Process any remaining output
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

        // ── Parse aria2 output ───────────────────────────────────────────────────
        private readonly Regex _progressRegex = new(@"\[#\w+\s+([\d.]+[GMKB]+)/([\d.]+[GMKB])\s*\(([\d]+)%\)\s+([\d.]+[GMKB]/s)\s+ETA:([\w:]+)\]");
        private readonly Regex _summaryRegex = new(@"DOWNLOAD:(\d+)/(\d+)\s+([\d.]+[GMKB])/s\s+ETA:([\w:]+)");
        private readonly Regex _speedRegex = new(@"([\d.]+)[GMKB]/s");

        private void ParseAria2Line(string line)
        {
            // Skip info lines
            if (line.Contains("ANALYZE") || line.Contains("Verifying") || line.Contains("OK")) return;

            // Parse progress line: [#abc123 1.2GiB/2.4GiB(50%) 1.5MiB/s ETA:12:34]
            var match = _progressRegex.Match(line);
            if (match.Success)
            {
                string current = match.Groups[1].Value;
                string total = match.Groups[2].Value;
                int percent = int.Parse(match.Groups[3].Value);
                string speed = match.Groups[4].Value;
                string eta = match.Groups[5].Value;

                DownloadProgressChanged?.Invoke(percent, $"{speed} - ETA: {eta}");
                return;
            }

            // Parse summary line: DOWNLOAD:5/29 1.5MiB/s ETA:12:34
            var summaryMatch = _summaryRegex.Match(line);
            if (summaryMatch.Success)
            {
                int current = int.Parse(summaryMatch.Groups[1].Value);
                int total = int.Parse(summaryMatch.Groups[2].Value);
                string speed = summaryMatch.Groups[3].Value;
                string eta = summaryMatch.Groups[4].Value;

                _completedFiles = current;
                _totalFiles = total;

                double totalProgress = ((double)current / total) * 100;
                TotalDownloadProgressChanged?.Invoke(totalProgress);
                StatusMessageChanged?.Invoke($"File {current}/{total} - {speed} - ETA: {eta}");
                PartDownloadStarted?.Invoke(current, total);
                return;
            }

            // Parse simple speed line
            var speedMatch = _speedRegex.Match(line);
            if (speedMatch.Success && State == LauncherState.Downloading)
            {
                DownloadProgressChanged?.Invoke(0, $"{speedMatch.Groups[1].Value}{GetSpeedUnit(line)}");
            }
        }

        private string GetSpeedUnit(string line)
        {
            if (line.Contains("MiB/s")) return " MiB/s";
            if (line.Contains("GiB/s")) return " GiB/s";
            if (line.Contains("KiB/s")) return " KiB/s";
            return " B/s";
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
                using var response = await HttpClient.GetAsync(dxUrl);
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
                using var response = await HttpClient.GetAsync(vcUrl);
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

        private static readonly HttpClient HttpClient = new();
    }
}
