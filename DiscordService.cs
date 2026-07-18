using System;
using DiscordRPC;
using DiscordRPC.Logging;

namespace klauncher
{
    /// <summary>
    /// Servicio de Discord Rich Presence para KLAUNCHER.
    /// Muestra en Discord el estado actual del launcher en tiempo real.
    /// 
    /// CONFIGURACIÓN:
    /// Para usar tu propio Application ID:
    ///   1. Ve a https://discord.com/developers/applications
    ///   2. Crea una nueva aplicación
    ///   3. Copia el "Application ID" y reemplaza el valor de CLIENT_ID
    ///   4. En la sección "Rich Presence > Art Assets", sube imágenes con los keys:
    ///      - "klauncher_logo" → logo del launcher (koala)
    ///      - "gta_logo"       → logo de GTA V
    /// </summary>
    public class DiscordService : IDisposable
    {
        // ⚠️ Reemplaza este ID con el de tu propia aplicación en Discord Developer Portal
        private const string CLIENT_ID = "1528029499491356715";

        private DiscordRpcClient? _client;
        private bool _disposed = false;
        private DateTime _startTime;

        public bool IsConnected => _client?.IsInitialized ?? false;

        public void Initialize()
        {
            try
            {
                _startTime = DateTime.UtcNow;

                _client = new DiscordRpcClient(CLIENT_ID)
                {
                    Logger = new NullLogger()
                };

                _client.OnReady += (sender, e) =>
                {
                    // Rich Presence conectado
                };

                _client.OnError += (sender, e) =>
                {
                    // Silenciar errores de conexión (Discord puede no estar abierto)
                };

                _client.Initialize();

                // Estado inicial: en el menú principal
                SetMenuState();
            }
            catch
            {
                // Si Discord no está instalado o hay error, ignorar silenciosamente
            }
        }

        /// <summary>Estado: En el menú principal del launcher</summary>
        public void SetMenuState()
        {
            SetPresence(
                details: "GTA V - VMP Edition",
                state: "En el menú principal",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "GTA V VMP Edition"
            );
        }

        /// <summary>Estado: Descargando archivos</summary>
        public void SetDownloadingState(int currentPart, int totalParts, string speed)
        {
            SetPresence(
                details: "Descargando GTA V VMP Edition",
                state: $"Parte {currentPart}/{totalParts} • {speed}",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Descargando..."
            );
        }

        /// <summary>Estado: Descargando con ETA</summary>
        public void SetDownloadingState(int currentPart, int totalParts, string speed, TimeSpan eta)
        {
            string etaStr = (eta == TimeSpan.MaxValue || eta.TotalSeconds <= 0)
                ? ""
                : (eta.TotalHours >= 1
                    ? $" • ~{(int)eta.TotalHours}h {eta.Minutes:D2}m"
                    : $" • ~{(int)eta.TotalMinutes}m {eta.Seconds:D2}s");

            SetPresence(
                details: "Descargando GTA V VMP Edition",
                state: $"Parte {currentPart}/{totalParts} • {speed}{etaStr}",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Descargando..."
            );
        }

        /// <summary>Estado: Descarga pausada</summary>
        public void SetPausedState()
        {
            SetPresence(
                details: "GTA V - VMP Edition",
                state: "⏸ Descarga pausada",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Pausado"
            );
        }

        /// <summary>Estado: Extrayendo archivos</summary>
        public void SetExtractingState(double percentage)
        {
            SetPresence(
                details: "Descomprimiendo GTA V VMP Edition",
                state: $"Extrayendo archivos... {percentage:F0}%",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Extrayendo RAR..."
            );
        }

        /// <summary>Estado: Jugando GTA V</summary>
        public void SetPlayingState()
        {
            SetPresence(
                details: "Jugando GTA V - VMP Edition",
                state: "En el servidor 🎮",
                largeImageKey: "gta_logo",
                largeImageText: "GTA V VMP Edition",
                smallImageKey: "klauncher_logo",
                smallImageText: "KLAUNCHER"
            );
        }

        /// <summary>Estado: Instalación completada</summary>
        public void SetCompletedState()
        {
            SetPresence(
                details: "GTA V - VMP Edition",
                state: "✅ Instalación completada",
                largeImageKey: "klauncher_logo",
                largeImageText: "KLAUNCHER by Koala Gamer",
                smallImageKey: "gta_logo",
                smallImageText: "Listo para jugar"
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
                        new Button { Label = "🎮 Discord VMP", Url = "https://discord.gg/RRAE3uYNC" }
                    }
                };

                _client.SetPresence(presence);
            }
            catch
            {
                // Ignorar errores silenciosamente
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
