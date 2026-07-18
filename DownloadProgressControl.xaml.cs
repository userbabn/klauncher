using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace klauncher
{
    public partial class DownloadProgressControl : UserControl
    {
        private readonly LauncherService _launcherService;
        private readonly string          _installPath;

        // Animation duration: smooth but not laggy
        private static readonly Duration AnimDuration = new Duration(TimeSpan.FromMilliseconds(300));

        public delegate void InstallationFinishedEventHandler();
        public event InstallationFinishedEventHandler? InstallationFinished;

        public DownloadProgressControl(LauncherService launcherService, string installPath)
        {
            InitializeComponent();
            _launcherService = launcherService;
            _installPath     = installPath;

            Loaded   += DownloadProgressControl_Loaded;
            Unloaded += DownloadProgressControl_Unloaded;
        }

        private void DownloadProgressControl_Loaded(object sender, RoutedEventArgs e)
        {
            _launcherService.StateChanged                  += OnStateChanged;
            _launcherService.DownloadProgressChanged       += OnDownloadProgressChanged;
            _launcherService.TotalDownloadProgressChanged  += OnTotalDownloadProgressChanged;
            _launcherService.ExtractionProgressChanged     += OnExtractionProgressChanged;
            _launcherService.StatusMessageChanged          += OnStatusMessageChanged;
            _launcherService.ErrorOccurred                 += OnErrorOccurred;
            _launcherService.EstimatedTimeRemainingChanged += OnEstimatedTimeRemainingChanged;

            // Start (or resume) installation
            _ = _launcherService.StartDownloadAndInstallAsync(_installPath);
        }

        private void DownloadProgressControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _launcherService.StateChanged                  -= OnStateChanged;
            _launcherService.DownloadProgressChanged       -= OnDownloadProgressChanged;
            _launcherService.TotalDownloadProgressChanged  -= OnTotalDownloadProgressChanged;
            _launcherService.ExtractionProgressChanged     -= OnExtractionProgressChanged;
            _launcherService.StatusMessageChanged          -= OnStatusMessageChanged;
            _launcherService.ErrorOccurred                 -= OnErrorOccurred;
            _launcherService.EstimatedTimeRemainingChanged -= OnEstimatedTimeRemainingChanged;
        }

        // ── State Handler ────────────────────────────────────────────────────────
        private void OnStateChanged(LauncherState state)
        {
            Dispatcher.Invoke(() =>
            {
                switch (state)
                {
                    case LauncherState.Downloading:
                        TxtTitle.Text              = "DESCARGANDO GTA V - VMP Edition";
                        TxtPauseButton.Text        = "Pausar";
                        IconPause.Visibility       = Visibility.Visible;
                        IconPlay.Visibility        = Visibility.Collapsed;
                        BtnPause.IsEnabled         = true;
                        break;

                    case LauncherState.Paused:
                        TxtTitle.Text              = "DESCARGA PAUSADA";
                        TxtPauseButton.Text        = "Reanudar";
                        IconPause.Visibility       = Visibility.Collapsed;
                        IconPlay.Visibility        = Visibility.Visible;
                        BtnPause.IsEnabled         = true;
                        TxtSpeed.Text              = "Pausado";
                        TxtEta.Text                = "—";
                        break;

                    case LauncherState.Extracting:
                        TxtTitle.Text              = "DESCOMPRIMIENDO GTA V - VMP Edition";
                        BtnPause.IsEnabled         = false;
                        TxtSpeed.Text              = "Extrayendo…";
                        TxtEta.Text                = "";
                        break;

                    case LauncherState.Completed:
                        InstallationFinished?.Invoke();
                        break;

                    case LauncherState.Error:
                        BtnPause.IsEnabled         = false;
                        TxtSpeed.Text              = "Error";
                        TxtEta.Text                = "";
                        break;
                }
            });
        }

        // ── Download Progress ────────────────────────────────────────────────────
        private void OnDownloadProgressChanged(double percentage, string speedString)
        {
            Dispatcher.Invoke(() =>
            {
                AnimateProgressBar(FileProgressBar, percentage);
                TxtSpeed.Text = speedString;
            });
        }

        private void OnTotalDownloadProgressChanged(double totalPercentage)
        {
            Dispatcher.Invoke(() => AnimateProgressBar(TotalProgressBar, totalPercentage));
        }

        // ── ETA ──────────────────────────────────────────────────────────────────
        private void OnEstimatedTimeRemainingChanged(TimeSpan eta)
        {
            Dispatcher.Invoke(() =>
            {
                if (eta == TimeSpan.MaxValue || eta.TotalSeconds <= 0)
                {
                    TxtEta.Text = "Calculando…";
                    return;
                }

                if (eta.TotalHours >= 1)
                    TxtEta.Text = $"{(int)eta.TotalHours}h {eta.Minutes:D2}m restantes";
                else if (eta.TotalMinutes >= 1)
                    TxtEta.Text = $"{(int)eta.TotalMinutes}m {eta.Seconds:D2}s restantes";
                else
                    TxtEta.Text = $"{(int)eta.TotalSeconds}s restantes";
            });
        }

        // ── Extraction Progress ──────────────────────────────────────────────────
        private void OnExtractionProgressChanged(string currentFile, double percentage)
        {
            Dispatcher.Invoke(() =>
            {
                TxtFileName.Text = currentFile;
                AnimateProgressBar(FileProgressBar,  percentage);
                AnimateProgressBar(TotalProgressBar, percentage);
            });
        }

        // ── Status Message ───────────────────────────────────────────────────────
        private void OnStatusMessageChanged(string message)
        {
            Dispatcher.Invoke(() => TxtFileName.Text = message);
        }

        // ── Error ────────────────────────────────────────────────────────────────
        private void OnErrorOccurred(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(errorMessage, "Error de Instalador", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtFileName.Text = $"Error: {errorMessage}";
            });
        }

        // ── Pause Button ─────────────────────────────────────────────────────────
        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (_launcherService.State == LauncherState.Downloading)
                _launcherService.Pause();
            else if (_launcherService.State == LauncherState.Paused)
                _launcherService.Resume(_installPath);
        }

        // ── Animation Helper ─────────────────────────────────────────────────────
        /// <summary>
        /// Smoothly animates a ProgressBar's Value to <paramref name="targetValue"/>.
        /// </summary>
        private static void AnimateProgressBar(ProgressBar bar, double targetValue)
        {
            var anim = new DoubleAnimation
            {
                To             = Math.Clamp(targetValue, 0, 100),
                Duration       = AnimDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            bar.BeginAnimation(ProgressBar.ValueProperty, anim);
        }
    }
}
