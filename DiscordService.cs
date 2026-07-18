using System;
using System.Threading;
using DiscordRPC;
using DiscordRPC.Logging;

namespace klauncher
{
    /// <summary>
    /// Discord Rich Presence service for KLAUNCHER.
    /// Shows real-time launcher status in Discord.
    /// </summary>
    public class DiscordService : IDisposable
    {
        private const string CLIENT_ID = "1528029499491356715";

        private DiscordRpcClient? _client;
        private bool _disposed = false;
        private DateTime _startTime;
        private bool _initialized = false;

        public bool IsConnected => _client?.IsInitialized ?? false;

        public void Initialize()
        {
            try
            {
                _startTime = DateTime.UtcNow;

                _client = new DiscordRpcClient(CLIENT_ID, pipe: -1)
                {
                    Logger = new ConsoleLogger() { Level = LogLevel.Warning }
                };

                _client.OnReady += (sender, e) =>
                {
                    _initialized = true;
                };

                _client.OnError += (sender, e) =>
                {
                    // Log but don't crash
                    System.Diagnostics.Debug.WriteLine($"[Discord] Error: {e.Type} - {e.Message}");
                };

                _client.OnConnectionFailed += (sender, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Discord] Connection failed: {e.FailedPipe}");
                };

                _client.Initialize();

                // Give Discord a moment to connect, then set initial state
                Thread.Sleep(200);
                SetMenuState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Discord] Init failed: {ex.Message}");
                // Don't crash - Discord is optional
            }
        }

        /// <summary>Retry connection if it failed (call after a delay)</summary>
        public void RetryConnect()
        {
            if (_initialized || _client?.IsInitialized == true) return;

            try
            {
                _client?.Dispose();
                _client = null;
                Initialize();
            }
            catch { }
        }

        /// <summary>Main menu state</summary>
        public void SetMenuState()
        {
            SetPresence(
                details: "GTA V - VMP Edition",
                state: "In the main menu",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "GTA V VMP Edition"
            );
        }

        /// <summary>Downloading state</summary>
        public void SetDownloadingState(int currentPart, int totalParts, string speed)
        {
            SetPresence(
                details: "Downloading GTA V VMP Edition",
                state: $"Part {currentPart}/{totalParts} \u2022 {speed}",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Downloading..."
            );
        }

        /// <summary>Downloading state with ETA</summary>
        public void SetDownloadingState(int currentPart, int totalParts, string speed, TimeSpan eta)
        {
            string etaStr = (eta == TimeSpan.MaxValue || eta.TotalSeconds <= 0)
                ? ""
                : (eta.TotalHours >= 1
                    ? $" \u2022 ~{(int)eta.TotalHours}h {eta.Minutes:D2}m"
                    : $" \u2022 ~{(int)eta.TotalMinutes}m {eta.Seconds:D2}s");

            SetPresence(
                details: "Downloading GTA V VMP Edition",
                state: $"Part {currentPart}/{totalParts} \u2022 {speed}{etaStr}",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Downloading..."
            );
        }

        /// <summary>Download paused</summary>
        public void SetPausedState()
        {
            SetPresence(
                details: "GTA V - VMP Edition",
                state: "Download paused",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Paused"
            );
        }

        /// <summary>Extracting files</summary>
        public void SetExtractingState(double percentage)
        {
            SetPresence(
                details: "Extracting GTA V VMP Edition",
                state: $"Extracting files... {percentage:F0}%",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Extracting RAR..."
            );
        }

        /// <summary>Playing GTA V</summary>
        public void SetPlayingState()
        {
            SetPresence(
                details: "Playing GTA V - VMP Edition",
                state: "On the server",
                largeImageKey: "gta_logo",
                largeImageText: "GTA V VMP Edition",
                smallImageKey: "klauncher_logo",
                smallImageText: "KLAUNCHER"
            );
        }

        /// <summary>Installation completed</summary>
        public void SetCompletedState()
        {
            SetPresence(
                details: "GTA V - VMP Edition",
                state: "Installation completed",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Ready to play"
            );
        }

        private void SetPresence(
            string details,
            string state,
            string largeImageKey,
            string largeImageText,
            string? smallImageKey = null,
            string? smallImageText = null)
        {
            if (_client == null || !_client.IsInitialized) return;

            try
            {
                var presence = new RichPresence
                {
                    Details = details,
                    State = state,
                    Timestamps = new Timestamps(_startTime),
                    Assets = new Assets
                    {
                        LargeImageKey = largeImageKey,
                        LargeImageText = largeImageText,
                        SmallImageKey = smallImageKey,
                        SmallImageText = smallImageText
                    },
                    Buttons = new Button[]
                    {
                        new Button { Label = "Discord VMP", Url = "https://discord.gg/RRAE3uYNC" }
                    }
                };

                _client.SetPresence(presence);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Discord] SetPresence failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    _client?.ClearPresence();
                    _client?.Dispose();
                }
                catch { }
            }
        }
    }
}
