using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using System.Collections.Generic;
using System.Text.Json;

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

    /// <summary>
    /// Persisted state written to disk so a crash/accidental close can be resumed.
    /// </summary>
    internal class DownloadState
    {
        public int CompletedParts { get; set; } = 0;
        public long TotalBytesDownloaded { get; set; } = 0;
        public DateTime SessionStart { get; set; } = DateTime.UtcNow;
        public string TargetFolder { get; set; } = string.Empty;
    }

    public class LauncherService
    {
        // ── HTTP Client ──────────────────────────────────────────────────────────
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            MaxConnectionsPerServer = 4   // allow parallel streams per host
        })
        {
            Timeout = TimeSpan.FromHours(2),
            DefaultRequestVersion = new Version(2, 0),                 // prefer HTTP/2
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        static LauncherService()
        {
            _httpClient.DefaultRequestHeaders.ConnectionClose = false;  // Keep-Alive
        }

        // ── Constants ────────────────────────────────────────────────────────────
        private const int    TotalParts      = 29;
        private const string UrlTemplate     = "https://cdn.vmp.ir/game/1/Grand.Theft.Auto.V.VMP.Edition.part{0:D2}.rar";
        private const int    BufferSize      = 131072;  // 128 KB – faster than 8 KB
        private const string StateFileName   = "download_state.json";

        // ── State ────────────────────────────────────────────────────────────────
        private LauncherState _state    = LauncherState.Idle;
        private bool _isPaused          = false;
        private bool _cancelRequested   = false;
        private CancellationTokenSource? _downloadCts;

        // Progress tracking
        private long   _totalBytesDownloaded = 0;
        private double _downloadSpeed        = 0;  // bytes/s (exponentially smoothed)
        private int    _completedParts       = 0;

        // ETA tracking across parts (session-wide moving average of speed)
        private double _sessionSpeedEMA = 0;
        private const double SpeedAlpha = 0.08;  // smoothing factor (lower = smoother)
        private const long   EstimatedTotalBytes = 70L * 1024 * 1024 * 1024; // ~70 GB

        // ── Events ───────────────────────────────────────────────────────────────
        public event Action<LauncherState>?        StateChanged;
        public event Action<double, string>?       DownloadProgressChanged;    // (%, speed)
        public event Action<double>?               TotalDownloadProgressChanged;
        public event Action<int, int>?             PartDownloadStarted;
        public event Action<string, double>?       ExtractionProgressChanged;
        public event Action<string>?               StatusMessageChanged;
        public event Action<string>?               ErrorOccurred;
        public event Action<TimeSpan>?             EstimatedTimeRemainingChanged; // NEW

        // ── Public Properties ────────────────────────────────────────────────────
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

        // ── State File Helpers ───────────────────────────────────────────────────
        private static string GetStateFilePath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateFileName);

        public DownloadState? GetSavedState()
        {
            return LoadState();
        }

        private void SaveState(string targetFolder)
        {
            try
            {
                var st = new DownloadState
                {
                    CompletedParts       = _completedParts,
                    TotalBytesDownloaded = _totalBytesDownloaded,
                    SessionStart         = DateTime.UtcNow,
                    TargetFolder         = targetFolder
                };
                File.WriteAllText(GetStateFilePath(), JsonSerializer.Serialize(st));
            }
            catch { /* ignore write errors */ }
        }

        private DownloadState? LoadState()
        {
            try
            {
                string path = GetStateFilePath();
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<DownloadState>(json);
            }
            catch { return null; }
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
                _downloadCts?.Cancel();
                State = LauncherState.Paused;
                StatusMessageChanged?.Invoke("Descarga pausada.");
            }
        }

        public void Resume(string targetFolder)
        {
            if (State == LauncherState.Paused)
            {
                _isPaused = false;
                _cancelRequested = false;
                StatusMessageChanged?.Invoke("Reanudando descarga...");
                Task.Run(() => RunDownloadAndInstallWorkflowAsync(targetFolder));
            }
        }

        public void Cancel()
        {
            _cancelRequested = true;
            _downloadCts?.Cancel();
            State = LauncherState.Idle;
            StatusMessageChanged?.Invoke("Operación cancelada.");
        }

        /// <summary>
        /// Starts a new download or resumes from a persisted state if one exists.
        /// </summary>
        public async Task StartDownloadAndInstallAsync(string targetFolder)
        {
            if (State == LauncherState.Downloading || State == LauncherState.Extracting)
                return;

            _isPaused        = false;
            _cancelRequested = false;

            // Try to resume from persisted state
            var saved = LoadState();
            if (saved != null && saved.CompletedParts > 0)
            {
                _completedParts       = saved.CompletedParts;
                _totalBytesDownloaded = saved.TotalBytesDownloaded;
                StatusMessageChanged?.Invoke($"Reanudando desde la parte {_completedParts + 1}/{TotalParts}...");
            }
            else
            {
                _completedParts       = 0;
                _totalBytesDownloaded = 0;
            }

            await RunDownloadAndInstallWorkflowAsync(targetFolder);
        }

        // ── Core Workflow ────────────────────────────────────────────────────────
        private async Task RunDownloadAndInstallWorkflowAsync(string targetFolder)
        {
            State = LauncherState.Downloading;

            try
            {
                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                bool downloadSuccess = await DownloadAllPartsAsync(targetFolder);
                if (!downloadSuccess)
                {
                    if (_isPaused)        State = LauncherState.Paused;
                    else if (_cancelRequested) State = LauncherState.Idle;
                    return;
                }

                if (_cancelRequested) return;

                // Extract
                State = LauncherState.Extracting;
                StatusMessageChanged?.Invoke("Descomprimiendo archivos… Esto puede tardar varios minutos.");

                bool extractSuccess = await ExtractAllPartsAsync(targetFolder);
                if (extractSuccess)
                {
                    DeleteState();  // clean up persisted state
                    State = LauncherState.Completed;
                    StatusMessageChanged?.Invoke("¡Instalación completada con éxito!");
                }
                else
                {
                    State = LauncherState.Error;
                    ErrorOccurred?.Invoke("Error al extraer los archivos de instalación.");
                }
            }
            catch (Exception ex)
            {
                State = LauncherState.Error;
                ErrorOccurred?.Invoke($"Error general: {ex.Message}");
            }
        }

        private async Task<bool> DownloadAllPartsAsync(string targetFolder)
        {
            for (int partNum = 1; partNum <= TotalParts; partNum++)
            {
                if (_isPaused || _cancelRequested) return false;

                // Skip already-completed parts (from state or this session)
                if (partNum <= _completedParts)
                {
                    // Already counted in restored state, just fire progress
                    double skipProg = ((double)_completedParts / TotalParts) * 100;
                    TotalDownloadProgressChanged?.Invoke(skipProg);
                    continue;
                }

                string fileName  = string.Format("Grand.Theft.Auto.V.VMP.Edition.part{0:D2}.rar", partNum);
                string url       = string.Format(UrlTemplate, partNum);
                string finalPath = Path.Combine(targetFolder, fileName);
                string tempPath  = finalPath + ".tmp";

                StatusMessageChanged?.Invoke($"Descargando parte {partNum}/{TotalParts}…");
                PartDownloadStarted?.Invoke(partNum, TotalParts);

                // If final RAR already exists skip it
                if (File.Exists(finalPath))
                {
                    _totalBytesDownloaded += new FileInfo(finalPath).Length;
                    _completedParts++;
                    double skipProg = ((double)_completedParts / TotalParts) * 100;
                    TotalDownloadProgressChanged?.Invoke(skipProg);
                    SaveState(targetFolder);
                    continue;
                }

                _downloadCts = new CancellationTokenSource();
                bool partSuccess = await DownloadFileWithResumeAsync(url, tempPath, _downloadCts.Token, partNum);

                if (!partSuccess) return false;

                // Rename .tmp → final
                if (File.Exists(tempPath))
                {
                    File.Move(tempPath, finalPath, overwrite: true);
                    _completedParts++;
                    _totalBytesDownloaded += new FileInfo(finalPath).Length;
                    double totalProgress = ((double)_completedParts / TotalParts) * 100;
                    TotalDownloadProgressChanged?.Invoke(totalProgress);
                    SaveState(targetFolder);  // persist after every finished part
                }
            }

            return true;
        }

        private async Task<bool> DownloadFileWithResumeAsync(string url, string tempPath, CancellationToken token, int partNum = 0)
        {
            long existingLength = 0;
            if (File.Exists(tempPath))
                existingLength = new FileInfo(tempPath).Length;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (existingLength > 0)
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);

                HttpResponseMessage response = await _httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, token);

                bool appendMode = existingLength > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent;
                if (!appendMode && existingLength > 0)
                {
                    existingLength = 0;
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }

                long? contentLength = response.Content.Headers.ContentLength;
                long totalBytes     = (contentLength ?? 0) + existingLength;

                using var responseStream = await response.Content.ReadAsStreamAsync(token);
                using var fileStream     = new FileStream(
                    tempPath,
                    appendMode ? FileMode.Append : FileMode.Create,
                    FileAccess.Write, FileShare.None, BufferSize, true);

                if (!appendMode && contentLength.HasValue)
                {
                    fileStream.SetLength(contentLength.Value); // Pre-allocate to reduce fragmentation
                }

                byte[] buffer             = new byte[BufferSize];
                int    bytesRead;
                long   currentBytesDownloaded = existingLength;

                var    stopwatch      = Stopwatch.StartNew();
                long   lastBytesRead  = 0;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    if (token.IsCancellationRequested || _isPaused || _cancelRequested)
                        return false;

                    await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                    currentBytesDownloaded += bytesRead;

                    // Update speed & ETA every ~500 ms
                    if (stopwatch.ElapsedMilliseconds >= 500)
                    {
                        double seconds          = stopwatch.Elapsed.TotalSeconds;
                        long   bytesDelta       = (currentBytesDownloaded - existingLength) - lastBytesRead;
                        double instantSpeed     = bytesDelta / seconds;

                        // Exponential moving average
                        _sessionSpeedEMA = (_sessionSpeedEMA == 0)
                            ? instantSpeed
                            : _sessionSpeedEMA * (1 - SpeedAlpha) + instantSpeed * SpeedAlpha;

                        _downloadSpeed = _sessionSpeedEMA;

                        lastBytesRead = currentBytesDownloaded - existingLength;
                        stopwatch.Restart();

                        // --- ETA ---
                        long remainingBytes = EstimatedTotalBytes - _totalBytesDownloaded - (currentBytesDownloaded - existingLength);
                        if (remainingBytes < 0) remainingBytes = 0;
                        TimeSpan eta = (_downloadSpeed > 0)
                            ? TimeSpan.FromSeconds(remainingBytes / _downloadSpeed)
                            : TimeSpan.MaxValue;
                        EstimatedTimeRemainingChanged?.Invoke(eta);

                        // Progress for this file
                        double percentage  = (totalBytes > 0) ? ((double)currentBytesDownloaded / totalBytes) * 100.0 : 0;
                        string speedString = FormatSpeed(_downloadSpeed);
                        DownloadProgressChanged?.Invoke(percentage, speedString);
                    }
                }

                // 100% for this part
                DownloadProgressChanged?.Invoke(100.0, "0.0 KB/s");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error de red: {ex.Message}");
                return false;
            }
        }

        // ── Extraction ───────────────────────────────────────────────────────────
        private async Task<bool> ExtractAllPartsAsync(string targetFolder)
        {
            string firstPartPath = Path.Combine(targetFolder, "Grand.Theft.Auto.V.VMP.Edition.part01.rar");

            if (!File.Exists(firstPartPath))
            {
                ErrorOccurred?.Invoke("No se encontró el archivo de la parte 1 para iniciar la descompresión.");
                return false;
            }

            try
            {
                return await Task.Run(() =>
                {
                    using (var archive = ArchiveFactory.OpenArchive(firstPartPath))
                    {
                        long totalSizeToExtract = 0;
                        long totalBytesExtracted = 0;
                        foreach (var entry in archive.Entries)
                            if (!entry.IsDirectory) totalSizeToExtract += entry.Size;

                        if (totalSizeToExtract == 0) totalSizeToExtract = 1;

                        foreach (var entry in archive.Entries)
                        {
                            if (_cancelRequested) return false;
                            if (entry.IsDirectory) continue;

                            string entryKey = entry.Key ?? "archivo";
                            ExtractionProgressChanged?.Invoke(entryKey, ((double)totalBytesExtracted / totalSizeToExtract) * 100);

                            string  destPath  = Path.Combine(targetFolder, entryKey);
                            string? directory = Path.GetDirectoryName(destPath);
                            if (directory != null && !Directory.Exists(directory))
                                Directory.CreateDirectory(directory);

                            using var entryStream = entry.OpenEntryStream();
                            using var fileStream  = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 262144, true);

                            if (entry.Size > 0)
                            {
                                fileStream.SetLength(entry.Size); // Pre-allocate to reduce fragmentation
                            }

                            byte[]    buffer              = new byte[262144]; // Increased to 256KB for faster copy
                            int       read;
                            long      entryBytesWritten   = 0;
                            var       progressStopwatch   = Stopwatch.StartNew();

                            while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                if (_cancelRequested) return false;
                                fileStream.Write(buffer, 0, read);
                                totalBytesExtracted += read;
                                entryBytesWritten   += read;

                                if (progressStopwatch.ElapsedMilliseconds > 100)
                                {
                                    double pct = ((double)totalBytesExtracted / totalSizeToExtract) * 100;
                                    ExtractionProgressChanged?.Invoke(
                                        $"{entryKey} ({FormatBytes(entryBytesWritten)} / {FormatBytes(entry.Size)})", pct);
                                    progressStopwatch.Restart();
                                }
                            }
                        }
                    }

                    // Clean up RAR files
                    try
                    {
                        for (int i = 1; i <= TotalParts; i++)
                        {
                            string rarPath = Path.Combine(targetFolder,
                                string.Format("Grand.Theft.Auto.V.VMP.Edition.part{0:D2}.rar", i));
                            if (File.Exists(rarPath)) File.Delete(rarPath);
                        }
                    }
                    catch { }

                    return true;
                });
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error de descompresión: {ex.Message}");
                return false;
            }
        }

        // ── Dependency Installers ────────────────────────────────────────────────
        public async Task<bool> InstallDirectXAsync()
        {
            StatusMessageChanged?.Invoke("Descargando DirectX Runtime…");
            string tempDir = Path.Combine(Path.GetTempPath(), "KLauncher_Setup");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string dxSetupPath = Path.Combine(tempDir, "dxwebsetup.exe");
            string dxUrl       = "https://download.microsoft.com/download/1/7/1/1718CCC3-7CDD-41F4-890B-4090575B8356/dxwebsetup.exe";

            try
            {
                using (var resp = await _httpClient.GetAsync(dxUrl))
                using (var fs   = new FileStream(dxSetupPath, FileMode.Create, FileAccess.Write))
                    await resp.Content.CopyToAsync(fs);

                StatusMessageChanged?.Invoke("Ejecutando instalador de DirectX…");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName       = dxSetupPath,
                    Arguments      = "/silent",
                    UseShellExecute = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    StatusMessageChanged?.Invoke("DirectX instalado o actualizado.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error al instalar DirectX: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> InstallVcRedistAsync()
        {
            StatusMessageChanged?.Invoke("Descargando Visual C++ Redistributable…");
            string tempDir = Path.Combine(Path.GetTempPath(), "KLauncher_Setup");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string vcRedistPath = Path.Combine(tempDir, "vc_redist.x64.exe");
            string vcUrl        = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

            try
            {
                using (var resp = await _httpClient.GetAsync(vcUrl))
                using (var fs   = new FileStream(vcRedistPath, FileMode.Create, FileAccess.Write))
                    await resp.Content.CopyToAsync(fs);

                StatusMessageChanged?.Invoke("Ejecutando instalador de Visual C++…");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName        = vcRedistPath,
                    Arguments       = "/passive /norestart",
                    UseShellExecute = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    StatusMessageChanged?.Invoke("Visual C++ Redistributable 2015-2022 instalado.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error al instalar Visual C++: {ex.Message}");
            }
            return false;
        }

        // ── Formatters ───────────────────────────────────────────────────────────
        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
                return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
            if (bytesPerSecond >= 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond:F0} B/s";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            if (bytes >= 1024L * 1024)        return $"{bytes / (1024.0 * 1024):F2} MB";
            if (bytes >= 1024L)               return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} B";
        }
    }
}
