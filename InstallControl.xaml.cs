using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;

namespace klauncher
{
    public partial class InstallControl : UserControl
    {
        public delegate void StartInstallationEventHandler(string installPath);
        public event StartInstallationEventHandler? StartInstallation;

        private readonly LauncherService _launcherService;

        public InstallControl(LauncherService launcherService)
        {
            InitializeComponent();
            _launcherService = launcherService;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select GTA V installation folder",
                    InitialDirectory = TxtInstallPath.Text
                };

                if (dialog.ShowDialog() == true)
                {
                    TxtInstallPath.Text = dialog.FolderName;
                    CheckDiskSpace(dialog.FolderName);
                }
            }
            catch
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Select installation folder",
                    Filter = "Folder|*.directory",
                    FileName = "Select this folder"
                };

                if (dialog.ShowDialog() == true)
                {
                    string path = Path.GetDirectoryName(dialog.FileName) ?? TxtInstallPath.Text;
                    TxtInstallPath.Text = path;
                    CheckDiskSpace(path);
                }
            }
        }

        private void CheckDiskSpace(string path)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
                var drive = new System.IO.DriveInfo(root);
                double freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);

                if (freeGB < 80)
                {
                    TxtDiskSpace.Text = $"Low disk space: {freeGB:F1} GB free (need ~80 GB)";
                    TxtDiskSpace.Foreground = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    TxtDiskSpace.Text = $"Disk space: {freeGB:F1} GB free";
                    TxtDiskSpace.Foreground = new System.Windows.Media.BrushConverter().ConvertFromString("#2ecc71") as System.Windows.Media.Brush;
                }
                TxtDiskSpace.Visibility = Visibility.Visible;
            }
            catch
            {
                TxtDiskSpace.Visibility = Visibility.Collapsed;
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            string path = TxtInstallPath.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show("Please enter a valid installation path.\n\nلطفاً مسیر نصب معتبر وارد کنید.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!LauncherService.HasEnoughDiskSpace(path))
            {
                double freeGB = 0;
                try
                {
                    string root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
                    freeGB = new System.IO.DriveInfo(root).AvailableFreeSpace / (1024.0 * 1024 * 1024);
                }
                catch { }

                MessageBox.Show(
                    $"Not enough disk space!\n\nFree: {freeGB:F1} GB\nRequired: ~80 GB\n\n---\n\nفضای دیسک کافی نیست!\n\nآزاد: {freeGB:F1} گیگابایت\nلازم: ~۸۰ گیگابایت",
                    "Insufficient Disk Space", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Window parentWindow = Window.GetWindow(this);
            ConfirmDialog confirm = new ConfirmDialog { Owner = parentWindow };

            if (confirm.ShowDialog() == true && confirm.Result)
            {
                StartInstallation?.Invoke(path);
            }
        }

        private void Requirements_Click(object sender, RoutedEventArgs e)
        {
            PanelRequirements.Visibility = PanelRequirements.Visibility == Visibility.Collapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void DxInstall_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Starting DirectX download and installation.", "Install DirectX", MessageBoxButton.OK, MessageBoxImage.Information);
            await _launcherService.InstallDirectXAsync();
        }

        private async void VcInstall_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Starting Visual C++ Redistributable installation.", "Install Visual C++", MessageBoxButton.OK, MessageBoxImage.Information);
            await _launcherService.InstallVcRedistAsync();
            MessageBox.Show("VC++ Redistributable installed successfully.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
