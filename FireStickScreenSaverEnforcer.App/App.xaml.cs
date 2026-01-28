using Microsoft.UI.Xaml;
using FireStickScreenSaverEnforcer.App.Services;

namespace FireStickScreenSaverEnforcer.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        
        // Check if Windows App SDK Runtime is installed
        if (!RuntimeBootstrapper.IsRuntimeInstalled())
        {
            // Prompt user to install runtime
            var installed = await RuntimeBootstrapper.PromptAndInstallAsync(_window);
            
            if (!installed)
            {
                // User cancelled or installation failed - exit
                Exit();
                return;
            }
        }
        
        _window.Activate();
    }
}
