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
                        TxtTitle.Text              = "DOWNLOADING GTA V - VMP Edition";
                        TxtPauseButton.Text        = "Pause";
                        IconPause.Visibility       = Visibility.Visible;
                        IconPlay.Visibility        = Visibility.Collapsed;
                        BtnPause.IsEnabled         = true;
                        ActiveDotGrid.Visibility   = Visibility.Visible;
                        PausedDotGrid.Visibility   = Visibility.Collapsed;
                        PulsingDot.Fill            = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#00bcd4");
                        break;

                    case LauncherState.Paused:
                        TxtTitle.Text              = "DOWNLOAD PAUSED";
                        TxtPauseButton.Text        = "Resume";
                        IconPause.Visibility       = Visibility.Collapsed;
                        IconPlay.Visibility        = Visibility.Visible;
                        BtnPause.IsEnabled         = true;
                        TxtSpeed.Text              = "Paused";
                        TxtEta.Text                = "\u2014";
                        ActiveDotGrid.Visibility   = Visibility.Collapsed;
                        PausedDotGrid.Visibility   = Visibility.Visible;
                        break;

                    case LauncherState.Extracting:
                        TxtTitle.Text              = "RUNNING SETUP";
                        BtnPause.IsEnabled         = false;
                        TxtSpeed.Text              = "Launching installer...";
                        TxtEta.Text                = "";
                        ActiveDotGrid.Visibility   = Visibility.Visible;
                        PausedDotGrid.Visibility   = Visibility.Collapsed;
                        PulsingDot.Fill            = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2ecc71");
                        break;

                    case LauncherState.Completed:
                        ActiveDotGrid.Visibility   = Visibility.Collapsed;
                        PausedDotGrid.Visibility   = Visibility.Collapsed;
                        InstallationFinished?.Invoke();
                        break;

                    case LauncherState.Error:
                        BtnPause.IsEnabled         = false;
                        TxtSpeed.Text              = "Error";
                        TxtEta.Text                = "";
                        ActiveDotGrid.Visibility   = Visibility.Collapsed;
                        PausedDotGrid.Visibility   = Visibility.Collapsed;
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
                    TxtEta.Text = "Calculating...";
                    return;
                }

                if (eta.TotalHours >= 1)
                    TxtEta.Text = $"{(int)eta.TotalHours}h {eta.Minutes:D2}m remaining";
                else if (eta.TotalMinutes >= 1)
                    TxtEta.Text = $"{(int)eta.TotalMinutes}m {eta.Seconds:D2}s remaining";
                else
                    TxtEta.Text = $"{(int)eta.TotalSeconds}s remaining";
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
                MessageBox.Show(errorMessage, "Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
