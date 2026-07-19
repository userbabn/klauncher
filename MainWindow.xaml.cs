using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
        private const string VmpPathFile = "vmp_path.txt";

        // Latest ETA from the service – used for Discord updates
        private TimeSpan _lastEta         = TimeSpan.MaxValue;
        private int      _lastDiscordPart = 0;
        private int      _lastTotalFiles  = 140;

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

            // Close-confirmation and cleanup
            Closing += MainWindow_Closing;
            Closed  += MainWindow_Closed;
            Loaded  += MainWindow_Loaded;
        }

        // ── Close Handling ────────────────────────────────────────────────────────

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Retry Discord connection after 2 seconds if it didn't connect initially
            await Task.Delay(2000);
            if (!_discordService.IsConnected)
            {
                _discordService.RetryConnect();
            }

            var saved = _launcherService.GetSavedState();
            if (saved != null && !string.IsNullOrEmpty(saved.TargetFolder) && saved.CompletedFiles < saved.TotalFiles)
            {
                DownloadStatusBar.Visibility = Visibility.Visible;
                StatusText.Text = $"Resuming download ({saved.CompletedFiles + 1}/{saved.TotalFiles})...";
                StatusDot.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#00bcd4")!;

                ShowInstallScreen();
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
                    "A download is in progress.\n\nProgress will be saved and you can resume later.\nDo you want to exit?\n\n---\n\nیک دانلود در حال انجام است.\nپیشرفت ذخیره خواهد شد و می‌توانید بعداً ادامه دهید.\nآیا می‌خواهید خارج شوید؟",
                    "Close KLAUNCHER / بستن KLAUNCHER",
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
            _lastTotalFiles = totalParts;
        }

        private void OnDownloadProgressForDiscord(double percentage, string speed)
        {
            _discordService.SetDownloadingState(_lastDiscordPart, _lastTotalFiles, speed, _lastEta);
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
                    FileName        = "https://discord.gg/ekhkHWQbr2",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open link: {ex.Message}\n\nلینک باز نشد: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Jugar_Click(object sender, RoutedEventArgs e)
        {
            string exePath = GetSavedVmpPath();

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                LaunchVmp(exePath);
                return;
            }

            // Ask user to pick VMP.exe
            MessageBoxResult choice = MessageBox.Show(
                "Select your VMP.exe file to play.\n\n---\n\nفایل VMP.exe خود را برای بازی انتخاب کنید.",
                "Select VMP.exe", MessageBoxButton.OKCancel, MessageBoxImage.Question);

            if (choice != MessageBoxResult.OK) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select VMP.exe",
                Filter = "VMP.exe|VMP.exe|All files|*.*",
                FileName = "VMP.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                SaveVmpPath(dialog.FileName);
                LaunchVmp(dialog.FileName);
            }
        }

        private void LaunchVmp(string exePath)
        {
            try
            {
                _discordService.SetPlayingState();
                Process.Start(new ProcessStartInfo
                {
                    FileName         = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? Path.GetDirectoryName(exePath) ?? "",
                    UseShellExecute  = true
                });
                WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                _discordService.SetMenuState();
                MessageBox.Show($"Error launching game: {ex.Message}\n\nخطا در اجرای بازی: {ex.Message}",
                    "Execution Error / خطای اجرا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetSavedVmpPath()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VmpPathFile);
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(saved) && File.Exists(saved))
                        return saved;
                }
            }
            catch { }
            return string.Empty;
        }

        private static void SaveVmpPath(string exePath)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VmpPathFile),
                    exePath);
            }
            catch { }
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
                // Hide download status indicator
                DownloadStatusBar.Visibility = Visibility.Collapsed;

                MessageBox.Show(
                    "GTA V VMP Edition has been downloaded and extracted successfully!\n\nYou can now click PLAY.\n\n---\n\nبازی GTA V VMP Edition با موفقیت دانلود و استخراج شد!\nاکنون می‌توانید روی PLAY کلیک کنید.",
                    "Installation Complete / نصب کامل شد", MessageBoxButton.OK, MessageBoxImage.Information);

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