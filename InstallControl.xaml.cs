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
                    Title = "Seleccionar carpeta de instalación de GTA V",
                    InitialDirectory = TxtInstallPath.Text
                };

                if (dialog.ShowDialog() == true)
                {
                    TxtInstallPath.Text = dialog.FolderName;
                }
            }
            catch
            {
                // Fallback for older environments (just in case, though .NET 10 supports OpenFolderDialog)
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Seleccionar carpeta de instalación",
                    Filter = "Folder Selection|*.directory",
                    FileName = "Seleccione esta carpeta"
                };

                if (dialog.ShowDialog() == true)
                {
                    string path = Path.GetDirectoryName(dialog.FileName) ?? TxtInstallPath.Text;
                    TxtInstallPath.Text = path;
                }
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            string path = TxtInstallPath.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show("Por favor ingresa una ruta de instalación válida.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Launch Confirm Dialog
            Window parentWindow = Window.GetWindow(this);
            ConfirmDialog confirm = new ConfirmDialog
            {
                Owner = parentWindow
            };

            if (confirm.ShowDialog() == true && confirm.Result)
            {
                StartInstallation?.Invoke(path);
            }
        }

        private void Requirements_Click(object sender, RoutedEventArgs e)
        {
            // Toggle panel
            if (PanelRequirements.Visibility == Visibility.Collapsed)
            {
                PanelRequirements.Visibility = Visibility.Visible;
            }
            else
            {
                PanelRequirements.Visibility = Visibility.Collapsed;
            }
        }

        private async void DxInstall_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Iniciando la descarga e instalación de DirectX. Siga las instrucciones del instalador.", "Instalar DirectX", MessageBoxButton.OK, MessageBoxImage.Information);
            await _launcherService.InstallDirectXAsync();
        }

        private async void VcInstall_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Iniciando la descarga e instalación de Visual C++ Redistributable en segundo plano.", "Instalar Visual C++", MessageBoxButton.OK, MessageBoxImage.Information);
            await _launcherService.InstallVcRedistAsync();
            MessageBox.Show("VC++ Redistributable instalado con éxito.", "Finalizado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
