using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using FireStickScreenSaverEnforcer.App.Models;
using FireStickScreenSaverEnforcer.App.Services;
using WinRT.Interop;

namespace FireStickScreenSaverEnforcer.App;

/// <summary>
/// Main window for the Fire TV Screensaver Timeout Enforcer application.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int GWL_WNDPROC = -4;
    private const int WM_SIZE = 0x0005;
    private const int SIZE_MINIMIZED = 1;
    private const int SW_RESTORE = 9;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private CancellationTokenSource? _cancellationTokenSource;
    private AppSettings _settings = new();
    private bool _isRunning;
    private readonly TrayIconManager _trayIconManager = new();
    private HdmiCecPrimer? _cecPrimer;
    private bool _forceClose;
    private IntPtr _originalWndProc;
    private WndProcDelegate? _subclassProc;

    public MainWindow()
    {
        InitializeComponent();

        // Set window title to include version (show only Major.Minor.Patch)
        var version = typeof(MainWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion?.Split('+')[0] ?? "?";
        this.Title = $"Fire TV Screensaver Timeout Enforcer v{version}";

        // Set window size
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(600, 700));

        LoadSettings();

        // Initialize the HDMI-CEC primer with cached key from settings
        _cecPrimer = new HdmiCecPrimer(Log);
        _cecPrimer.LoadCachedKey(_settings.CecKeyNamespace, _settings.CecKeyName);

        TimeoutComboBox.SelectionChanged += TimeoutComboBox_SelectionChanged;

        // Initialize system tray icon
        _trayIconManager.RestoreRequested += OnTrayRestoreRequested;
        _trayIconManager.ExitRequested += OnTrayExitRequested;
        _trayIconManager.Create("Fire TV Screensaver Timeout Enforcer");

        // Intercept the close button to minimize to tray instead of exiting
        this.AppWindow.Closing += AppWindow_Closing;

        // Subclass the native window to intercept minimize (WM_SIZE)
        var hwnd = WindowNative.GetWindowHandle(this);
        _subclassProc = SubclassWndProc;
        _originalWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_subclassProc));
    }

    private void LoadSettings()
    {
        _settings = SettingsService.Load();
        
        // Split the stored IP:Port format
        var parts = _settings.FirestickIp.Split(':');
        if (parts.Length == 2)
        {
            IpAddressTextBox.Text = parts[0];
            PortTextBox.Text = parts[1];
        }
        else if (parts.Length == 1)
        {
            IpAddressTextBox.Text = parts[0];
            PortTextBox.Text = "5555";
        }
        else
        {
            IpAddressTextBox.Text = "192.168.1.50";
            PortTextBox.Text = "5555";
        }
        
        IntervalNumberBox.Value = _settings.IntervalSeconds;
        
        // Set timeout selection
        if (_settings.TimeoutMs == 30000)
            TimeoutComboBox.SelectedIndex = 0;
        else
            TimeoutComboBox.SelectedIndex = 1;

        MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
    }

    private void SaveSettings()
    {
        var ip = IpAddressTextBox.Text?.Trim() ?? "192.168.1.50";
        var port = PortTextBox.Text?.Trim() ?? "5555";
        _settings.FirestickIp = $"{ip}:{port}";
        _settings.IntervalSeconds = (int)IntervalNumberBox.Value;
        
        // Save timeout selection
        var selectedItem = TimeoutComboBox.SelectedItem as ComboBoxItem;
        if (selectedItem != null && int.TryParse(selectedItem.Tag?.ToString(), out var ms))
            _settings.TimeoutMs = ms;
        else
            _settings.TimeoutMs = 60000;

        _settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
        SettingsService.Save(_settings);
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logLine = $"[{timestamp}] {message}";
        
        DispatcherQueue.TryEnqueue(() =>
        {
            if (string.IsNullOrEmpty(LogTextBox.Text))
            {
                LogTextBox.Text = logLine;
            }
            else
            {
                LogTextBox.Text = LogTextBox.Text + Environment.NewLine + logLine;
            }
            
            // Auto-scroll to bottom
            LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
        });
    }

    private void SetStatus(string status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusTextBlock.Text = status;
        });
    }

    private void SetButtonStates(bool isRunning)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StartButton.IsEnabled = !isRunning;
            StopButton.IsEnabled = isRunning;
            IpAddressTextBox.IsEnabled = !isRunning;
            PortTextBox.IsEnabled = !isRunning;
            IntervalNumberBox.IsEnabled = !isRunning;
        });
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate and sanitize IP address
        var ipAddress = IpAddressTextBox.Text?.Trim();
        if (!SecurityHelper.ValidateIpAddress(ipAddress, out var validIp))
        {
            SetStatus("Error: Invalid IP address format.");
            Log("Error: IP address validation failed. Expected format: xxx.xxx.xxx.xxx");
            return;
        }

        // Validate and sanitize port
        var port = PortTextBox.Text?.Trim();
        if (!SecurityHelper.ValidatePort(port, out var validPort))
        {
            SetStatus("Error: Invalid port number.");
            Log("Error: Port must be a number between 1 and 65535.");
            return;
        }

        // Create sanitized full address
        if (!SecurityHelper.SanitizeIpPort(validIp, validPort.ToString(), out var fullAddress))
        {
            SetStatus("Error: Failed to validate IP:Port combination.");
            Log("Error: IP:Port validation failed.");
            return;
        }

        // Validate interval
        var interval = (int)IntervalNumberBox.Value;
        if (!SecurityHelper.ValidateIntervalSeconds(interval))
        {
            SetStatus("Error: Interval must be between 10 and 30 seconds.");
            Log("Error: Invalid interval value. Must be 10-30 seconds.");
            return;
        }

        // Validate ADB files
        var (isValid, errorMessage) = AdbRunner.ValidateAdbFiles();
        if (!isValid)
        {
            SetStatus("Error: Missing ADB files. See log for details.");
            Log($"Error: {errorMessage}");
            return;
        }

        // Save settings (validation happens in AppSettings setters)
        SaveSettings();

        // Start enforcement loop
        _cancellationTokenSource = new CancellationTokenSource();
        _isRunning = true;
        SetButtonStates(true);
        var timeoutText = _settings.TimeoutMs == 30000 ? "30 seconds" : "1 minute";
        SetStatus($"Running - Enforcing {timeoutText} timeout...");
        Log($"Started enforcement. Target: {fullAddress}, Interval: {interval}s, Timeout: {timeoutText}");

        try
        {
            await RunEnforcementLoopAsync(fullAddress, interval, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Log("Enforcement stopped by user.");
        }
        catch (Exception ex)
        {
            var sanitizedError = SecurityHelper.SanitizeForLog(ex.Message);
            Log($"Unexpected error: {sanitizedError}");
            SetStatus($"Error: {sanitizedError}");
        }
        finally
        {
            _isRunning = false;
            SetButtonStates(false);
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                SetStatus("Stopped.");
            }
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            Log("Stopping enforcement...");
            SetStatus("Stopping...");
            _cancellationTokenSource.Cancel();
        }
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        LogTextBox.Text = string.Empty;
    }

    private async Task RunEnforcementLoopAsync(string ipAddress, int intervalSeconds, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await EnforceTimeoutAsync(ipAddress, cancellationToken);
            
            // Wait for the next interval
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task EnforceTimeoutAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;

        // Step 1: Connect to the device
        Log($"Connecting to {ipAddress}...");
        var connectResult = await AdbRunner.ConnectAsync(ipAddress);
        
        if (!connectResult.Success)
        {
            var errorMsg = !string.IsNullOrEmpty(connectResult.Error) 
                ? connectResult.Error 
                : connectResult.Output;
            Log($"Connect failed: {errorMsg} - Will retry next interval.");
            SetStatus($"Connection failed - will retry in next interval");
            return;
        }

        // Check if already connected or newly connected
        var connectOutput = connectResult.Output.ToLowerInvariant();
        var sanitizedOutput = SecurityHelper.SanitizeForLog(connectResult.Output);
        if (connectOutput.Contains("connected") || connectOutput.Contains("already"))
        {
            Log($"Connected: {sanitizedOutput}");
        }
        else
        {
            Log($"Connect response: {sanitizedOutput}");
        }

        if (cancellationToken.IsCancellationRequested) return;

        // Step 2: Ensure Dream/screensaver system is enabled
        await EnsureDreamScreensaverEnabledAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested) return;

        // Step 3: HDMI-CEC prime pulse (ON→OFF) to fix screensaver-not-showing after "Never" workaround
        await RunCecPrimePulseAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested) return;

        // Step 4: Get current timeout value
        var getResult = await AdbRunner.GetScreenOffTimeoutAsync();
        
        if (!getResult.Success)
        {
            var errorMsg = !string.IsNullOrEmpty(getResult.Error) 
                ? SecurityHelper.SanitizeForLog(getResult.Error)
                : "Failed to get timeout value";
            Log($"Get timeout failed: {errorMsg}");
            SetStatus($"Could not read timeout - will retry");
            return;
        }

        // Parse the current value (trim whitespace and newlines)
        var currentValueStr = getResult.Output.Trim();

        if (!int.TryParse(currentValueStr, out var currentValue))
        {
            var sanitizedValue = SecurityHelper.SanitizeForLog(currentValueStr);
            Log($"Could not parse timeout value: '{sanitizedValue}'");
            return;
        }

        Log($"Current timeout: {currentValue}ms ({currentValue / 1000}s)");

        // Step 5: Set timeout if needed
        if (currentValue != _settings.TimeoutMs)
        {
            if (cancellationToken.IsCancellationRequested) return;
            
            Log($"Timeout is {currentValue}ms, setting to {_settings.TimeoutMs}ms ({(_settings.TimeoutMs == 30000 ? "30 seconds" : "1 minute")})...");
            
            var setResult = await AdbRunner.SetScreenOffTimeoutAsync(_settings.TimeoutMs);
            
            if (setResult.Success)
            {
                var timeoutText = _settings.TimeoutMs == 30000 ? "30 seconds" : "1 minute";
                Log($"Timeout set to {_settings.TimeoutMs}ms ({timeoutText})");
                SetStatus($"Enforced {timeoutText} timeout at {DateTime.Now:HH:mm:ss}");
            }
            else
            {
                var errorMsg = !string.IsNullOrEmpty(setResult.Error) 
                    ? setResult.Error 
                    : "Failed to set timeout";
                Log($"Set timeout failed: {errorMsg}");
            }
        }
        else
        {
            Log($"Timeout already at {_settings.TimeoutMs}ms - no change needed");
            SetStatus($"Timeout correct as of {DateTime.Now:HH:mm:ss}");
        }
    }

    private async Task RunCecPrimePulseAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || _cecPrimer is null) return;

        var result = await _cecPrimer.PrimeAsync(cancellationToken);

        if (result.Success)
        {
            // Persist the discovered key so future runs skip probing
            var (ns, key) = _cecPrimer.GetCachedKey();
            if (_settings.CecKeyNamespace != ns || _settings.CecKeyName != key)
            {
                _settings.CecKeyNamespace = ns;
                _settings.CecKeyName = key;
                SettingsService.Save(_settings);
            }
        }
        else if (!string.IsNullOrEmpty(result.Error) && result.Error != "Operation cancelled.")
        {
            var sanitizedError = SecurityHelper.SanitizeForLog(result.Error);
            Log($"CEC prime pulse failed: {sanitizedError}");
        }
    }

    private async Task EnsureDreamScreensaverEnabledAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;

        var dreamSettings = await AdbRunner.GetDreamSettingsAsync();

        if (!dreamSettings.Success)
        {
            var sanitizedError = SecurityHelper.SanitizeForLog(dreamSettings.Error);
            Log($"Dream settings read failed: {sanitizedError}");
            return;
        }

        var enabled = dreamSettings.ScreensaverEnabled;
        var onSleep = dreamSettings.ActivateOnSleep;
        var onDock = dreamSettings.ActivateOnDock;
        var components = dreamSettings.ScreensaverComponents;

        bool needsEnable = enabled != "1";
        bool needsActivate = onSleep != "1" || onDock != "1";

        if (needsEnable || needsActivate)
        {
            var sanitizedEnabled = SecurityHelper.SanitizeForLog(enabled);
            var sanitizedOnSleep = SecurityHelper.SanitizeForLog(onSleep);
            var sanitizedOnDock = SecurityHelper.SanitizeForLog(onDock);
            Log($"Dream/screensaver system not fully enabled (enabled={sanitizedEnabled}, onSleep={sanitizedOnSleep}, onDock={sanitizedOnDock}). Re-enabling...");

            if (cancellationToken.IsCancellationRequested) return;

            var setResult = await AdbRunner.EnableDreamSettingsAsync();
            if (setResult.Success)
            {
                Log("Dream/screensaver system re-enabled successfully.");
            }
            else
            {
                var errorMsg = !string.IsNullOrEmpty(setResult.Error) 
                    ? SecurityHelper.SanitizeForLog(setResult.Error)
                    : "Failed to enable Dream settings";
                Log($"Failed to re-enable Dream settings: {errorMsg}");
            }
        }

        // Optional: set screensaver component to Amazon Photos if missing/null
        bool needsComponent = string.IsNullOrEmpty(components) || components == "null";
        if (needsComponent)
        {
            Log("Screensaver component is not set. Setting to Amazon Photos...");

            if (cancellationToken.IsCancellationRequested) return;

            var compResult = await AdbRunner.SetScreensaverComponentAsync();
            if (compResult.Success)
            {
                Log("Screensaver component set to Amazon Photos.");
            }
            else
            {
                var errorMsg = !string.IsNullOrEmpty(compResult.Error) ? compResult.Error : "Failed to set screensaver component";
                Log($"Failed to set screensaver component: {errorMsg}");
            }
        }
    }

    private void TimeoutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedItem = TimeoutComboBox.SelectedItem as ComboBoxItem;
        if (selectedItem != null && int.TryParse(selectedItem.Tag?.ToString(), out var ms))
        {
            TargetTimeoutTextBlock.Text = $"Target timeout: {ms}ms ({(ms == 30000 ? "30 seconds" : "1 minute")})";
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_forceClose && MinimizeToTrayCheckBox.IsChecked == true)
        {
            // Cancel close and hide window to tray instead
            args.Cancel = true;
            this.AppWindow.Hide();
            _trayIconManager.UpdateTooltip(_isRunning
                ? "Fire TV Enforcer - Running"
                : "Fire TV Enforcer - Idle");
        }
        else
        {
            // Actually closing: clean up tray icon and cancel enforcement
            _trayIconManager.Dispose();
            _cancellationTokenSource?.Cancel();
        }
    }

    private IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_SIZE && (int)wParam == SIZE_MINIMIZED)
        {
            if (MinimizeToTrayCheckBox.IsChecked == true)
            {
                // Restore from minimized state first, then hide to tray
                ShowWindow(hWnd, SW_RESTORE);
                this.AppWindow.Hide();
                _trayIconManager.UpdateTooltip(_isRunning
                    ? "Fire TV Enforcer - Running"
                    : "Fire TV Enforcer - Idle");
                return IntPtr.Zero;
            }
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    private void OnTrayRestoreRequested()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            this.AppWindow.Show();
            this.Activate();
        });
    }

    private void OnTrayExitRequested()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SaveSettings();
            _forceClose = true;
            Close();
        });
    }
}



