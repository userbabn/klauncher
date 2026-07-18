using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace klauncher
{
    public partial class MainWindow : Window
    {
        private readonly LauncherService _launcherService;
        private readonly DiscordService  _discordService;
        private int    _currentNewsSlide = 1;
        private string _installedPath    = @"C:\Games\GTAV_KLAUNCHER";

        // Latest ETA from the service – used for Discord updates
        private TimeSpan _lastEta         = TimeSpan.MaxValue;
        private int      _lastDiscordPart = 0;

        public MainWindow()
        {
            InitializeComponent();
            _launcherService = new LauncherService();
            _discordService  = new DiscordService();

            _discordService.Initialize();

            // Subscribe to launcher events
            _launcherService.StateChanged                  += OnLauncherStateChanged;
            _launcherService.DownloadProgressChanged       += OnDownloadProgressForDiscord;
            _launcherService.PartDownloadStarted           += OnPartDownloadStarted;
            _launcherService.ExtractionProgressChanged     += OnExtractionProgressForDiscord;
            _launcherService.EstimatedTimeRemainingChanged += OnEtaChanged;

            // Close-confirmation and cleanup
            Closing += MainWindow_Closing;
            Closed  += MainWindow_Closed;
            Loaded  += MainWindow_Loaded;
        }

        // ── Close Handling ────────────────────────────────────────────────────────

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var saved = _launcherService.GetSavedState();
            if (saved != null && !string.IsNullOrEmpty(saved.TargetFolder) && saved.CompletedParts < 29)
            {
                // Auto-resume download
                ShowInstallScreen(); // This sets panel visibilities
                InstallCtrl_StartInstallation(saved.TargetFolder);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // If a download is active, ask the user before closing
            if (_launcherService.State == LauncherState.Downloading ||
                _launcherService.State == LauncherState.Extracting)
            {
                var result = MessageBox.Show(
                    "Hay una descarga en curso.\n\nEl progreso se guardará y podrás reanudar más tarde.\n¿Deseas salir?",
                    "Cerrar KLAUNCHER",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;    // keep window open
                    return;
                }

                // User confirmed – cancel gracefully (state is already persisted per-part)
                _launcherService.Cancel();
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _launcherService.StateChanged                  -= OnLauncherStateChanged;
            _launcherService.DownloadProgressChanged       -= OnDownloadProgressForDiscord;
            _launcherService.PartDownloadStarted           -= OnPartDownloadStarted;
            _launcherService.ExtractionProgressChanged     -= OnExtractionProgressForDiscord;
            _launcherService.EstimatedTimeRemainingChanged -= OnEtaChanged;
            _discordService.Dispose();
        }

        // ── Discord Rich Presence handlers ────────────────────────────────────────

        private void OnLauncherStateChanged(LauncherState state)
        {
            switch (state)
            {
                case LauncherState.Paused:
                    _discordService.SetPausedState();
                    break;
                case LauncherState.Extracting:
                    _discordService.SetExtractingState(0);
                    break;
                case LauncherState.Completed:
                    _discordService.SetCompletedState();
                    break;
                case LauncherState.Idle:
                case LauncherState.Error:
                    _discordService.SetMenuState();
                    break;
            }
        }

        private void OnPartDownloadStarted(int currentPart, int totalParts)
        {
            _lastDiscordPart = currentPart;
        }

        private void OnDownloadProgressForDiscord(double percentage, string speed)
        {
            // Update Discord with speed + latest ETA
            _discordService.SetDownloadingState(_lastDiscordPart, 29, speed, _lastEta);
        }

        private void OnEtaChanged(TimeSpan eta)
        {
            _lastEta = eta;
        }

        private void OnExtractionProgressForDiscord(string currentFile, double percentage)
        {
            _discordService.SetExtractingState(percentage);
        }

        // ── Window Controls ────────────────────────────────────────────────────────

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();   // will trigger MainWindow_Closing which handles download confirmation
        }

        // ── Sidebar Buttons ────────────────────────────────────────────────────────

        private void Instalar_Click(object sender, RoutedEventArgs e)
        {
            ShowInstallScreen();
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "https://discord.gg/RRAE3uYNC",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el enlace: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Jugar_Click(object sender, RoutedEventArgs e)
        {
            string exePath = Path.Combine(_installedPath, "VMP.exe");
            if (File.Exists(exePath))
            {
                try
                {
                    _discordService.SetPlayingState();
                    Process.Start(new ProcessStartInfo
                    {
                        FileName         = exePath,
                        WorkingDirectory = _installedPath,
                        UseShellExecute  = true
                    });
                    WindowState = WindowState.Minimized;
                }
                catch (Exception ex)
                {
                    _discordService.SetMenuState();
                    MessageBox.Show($"Error al iniciar el juego: {ex.Message}",
                        "Error de ejecución", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(
                    "El juego no está instalado o no se encuentra el archivo 'VMP.exe'.\n\nSerás redirigido a la pantalla de instalación.",
                    "Juego no encontrado", MessageBoxButton.OK, MessageBoxImage.Information);
                ShowInstallScreen();
            }
        }

        // ── Navigation ──────────────────────────────────────────────────────────────

        private void ShowInstallScreen()
        {
            PanelNews.Visibility    = Visibility.Collapsed;
            ContentArea.Visibility  = Visibility.Visible;

            var installCtrl = new InstallControl(_launcherService);
            installCtrl.StartInstallation += InstallCtrl_StartInstallation;
            ContentArea.Content = installCtrl;
        }

        private void InstallCtrl_StartInstallation(string installPath)
        {
            _installedPath   = installPath;
            _lastDiscordPart = 1;

            var progressCtrl = new DownloadProgressControl(_launcherService, installPath);
            progressCtrl.InstallationFinished += ProgressCtrl_InstallationFinished;
            ContentArea.Content = progressCtrl;
        }

        private void ProgressCtrl_InstallationFinished()
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    "¡GTA V VMP Edition se ha descargado y extraído correctamente!\n\nYa puedes hacer clic en JUGAR.",
                    "Instalación Finalizada", MessageBoxButton.OK, MessageBoxImage.Information);

                ContentArea.Content    = null;
                ContentArea.Visibility = Visibility.Collapsed;
                PanelNews.Visibility   = Visibility.Visible;
            });
        }

        // ── News Slider ──────────────────────────────────────────────────────────────

        private void PrevNews_Click(object sender, RoutedEventArgs e)
        {
            _currentNewsSlide = _currentNewsSlide == 1 ? 3 : _currentNewsSlide - 1;
            UpdateNewsSlides();
        }

        private void NextNews_Click(object sender, RoutedEventArgs e)
        {
            _currentNewsSlide = _currentNewsSlide == 3 ? 1 : _currentNewsSlide + 1;
            UpdateNewsSlides();
        }

        private void UpdateNewsSlides()
        {
            NewsSlide1.Visibility = _currentNewsSlide == 1 ? Visibility.Visible : Visibility.Collapsed;
            NewsSlide2.Visibility = _currentNewsSlide == 2 ? Visibility.Visible : Visibility.Collapsed;
            NewsSlide3.Visibility = _currentNewsSlide == 3 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}