using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;

namespace FireStickScreenSaverEnforcer.App.Services;

/// <summary>
/// Checks for and installs Windows App SDK Runtime if missing.
/// </summary>
public static class RuntimeBootstrapper
{
    private const string RuntimeInstallerName = "windowsappruntimeinstall-x64.exe";
    
    /// <summary>
    /// Checks if Windows App SDK Runtime is installed.
    /// </summary>
    public static bool IsRuntimeInstalled()
    {
        try
        {
            // Check if the runtime package is installed
            var packageManager = new Windows.Management.Deployment.PackageManager();
            var packages = packageManager.FindPackagesForUser(string.Empty);
            return packages.Any(p => p.Id.Name.Contains("WindowsAppRuntime") && 
                                   p.Id.Version.Major >= 1 && 
                                   p.Id.Version.Minor >= 8);
        }
        catch
        {
            // If we can't check, assume it's not installed
            return false;
        }
    }
    
    /// <summary>
    /// Installs the Windows App SDK Runtime from bundled installer.
    /// </summary>
    public static async Task<bool> InstallRuntimeAsync()
    {
        try
        {
            var installerPath = Path.Combine(AppContext.BaseDirectory, RuntimeInstallerName);
            
            if (!File.Exists(installerPath))
            {
                throw new FileNotFoundException(
                    $"Runtime installer not found at: {installerPath}\n\n" +
                    "Please download it manually from:\n" +
                    "https://aka.ms/windowsappsdk/1.8/latest/windowsappruntimeinstall-x64.exe");
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Verb = "runas" // Request admin elevation
            };
            
            var process = Process.Start(startInfo);
            if (process != null)
            {
                await Task.Run(() => process.WaitForExit());
                return process.ExitCode == 0;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Runtime install failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Shows a dialog prompting user to install the runtime.
    /// </summary>
    public static async Task<bool> PromptAndInstallAsync(Window mainWindow)
    {
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Windows App SDK Runtime Required",
            Content = "This app requires the Windows App SDK Runtime to run.\n\n" +
                     "Click 'Install' to install it now (requires administrator permission).\n\n" +
                     "The app will restart after installation.",
            PrimaryButtonText = "Install",
            CloseButtonText = "Exit",
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
            XamlRoot = mainWindow.Content.XamlRoot
        };
        
        var result = await dialog.ShowAsync();
        
        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            // Show progress dialog
            var progressDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Installing Runtime",
                Content = "Please wait while the Windows App SDK Runtime is installed...\n\n" +
                         "This may take a few minutes.",
                XamlRoot = mainWindow.Content.XamlRoot
            };
            
            // Start installation in background
            var installTask = InstallRuntimeAsync();
            
            // Show progress dialog (non-blocking)
            _ = progressDialog.ShowAsync();
            
            var success = await installTask;
            
            progressDialog.Hide();
            
            if (success)
            {
                // Show restart prompt
                var restartDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Installation Complete",
                    Content = "The Windows App SDK Runtime has been installed.\n\n" +
                             "Please restart the application.",
                    CloseButtonText = "OK",
                    XamlRoot = mainWindow.Content.XamlRoot
                };
                
                await restartDialog.ShowAsync();
                
                // Restart the app
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(exePath);
                }
                
                Application.Current.Exit();
                return true;
            }
            else
            {
                var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Installation Failed",
                    Content = "Failed to install the Windows App SDK Runtime.\n\n" +
                             "Please download and install it manually from:\n" +
                             "https://aka.ms/windowsappsdk/1.8/latest/windowsappruntimeinstall-x64.exe",
                    CloseButtonText = "OK",
                    XamlRoot = mainWindow.Content.XamlRoot
                };
                
                await errorDialog.ShowAsync();
            }
        }
        
        return false;
    }
}
