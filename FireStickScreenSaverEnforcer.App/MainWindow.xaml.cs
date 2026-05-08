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
    private readonly AdbConnectionMonitor _connectionMonitor = new();
    private bool _forceClose;
    private IntPtr _originalWndProc;
    private WndProcDelegate? _subclassProc;
    private bool _cecPrimeCompleted;

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

        // Wire connection monitor → UI badge
        _connectionMonitor.StateChanged += OnConnectionStateChanged;

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
        _cecPrimeCompleted = false;
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
        // Begin device monitoring (DeviceMonitor wraps the persistent socket to ADB server)
        try
        {
            await _connectionMonitor.StartAsync(ipAddress, cancellationToken);
        }
        catch (Exception ex)
        {
            var sanitizedError = SecurityHelper.SanitizeForLog(ex.Message);
            Log($"Device monitor failed to start: {sanitizedError}");
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Phase 1: ensure connected (loops every 30s, resets server after 3 failures)
                var connected = await EnsureConnectedAsync(ipAddress, cancellationToken);
                if (!connected) break; // cancellation

                // Phase 2: run a single enforcement tick
                var success = await EnforceTimeoutAsync(ipAddress, cancellationToken);

                if (success)
                {
                    _connectionMonitor.NotifyOnline();
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                // If !success, EnsureConnectedAsync next iteration handles reconnect/wait.
            }
        }
        finally
        {
            await _connectionMonitor.StopAsync();
        }
    }

    /// <summary>
    /// Loops every 30 seconds attempting to (re)connect. After 3 consecutive failures runs an
    /// ADB server reset (kill+start) and continues retrying. Returns true once connected,
    /// false only on cancellation.
    /// </summary>
    private async Task<bool> EnsureConnectedAsync(string ipAddress, CancellationToken cancellationToken)
    {
        const int RetryDelaySeconds = 30;
        const int FailuresBeforeServerReset = 3;
        int consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Log($"Connecting to {ipAddress}...");
            var connectResult = await AdbRunner.ConnectAsync(ipAddress);

            if (connectResult.Success)
            {
                if (consecutiveFailures > 0)
                {
                    Log($"Reconnected to {ipAddress} after {consecutiveFailures} failed attempt(s).");
                }
                else
                {
                    var sanitizedOutput = SecurityHelper.SanitizeForLog(connectResult.Output);
                    Log($"Connected: {sanitizedOutput}");
                }
                _connectionMonitor.NotifyOnline();
                return true;
            }

            consecutiveFailures++;
            var kind = ClassifyError(connectResult);
            _connectionMonitor.NotifyReconnecting(kind);
            LogClassifiedError(kind, connectResult, $"Reconnect attempt {consecutiveFailures}");

            if (consecutiveFailures >= FailuresBeforeServerReset)
            {
                Log($"{consecutiveFailures} consecutive failures - resetting ADB server (this does NOT clear authorization)...");
                var resetResult = await AdbRunner.ResetServerAsync(cancellationToken);
                if (resetResult.Success)
                {
                    Log("ADB server reset complete.");
                }
                else
                {
                    var sanitizedError = SecurityHelper.SanitizeForLog(resetResult.Error);
                    Log($"ADB server reset failed: {sanitizedError}");
                }
                consecutiveFailures = 0;
            }

            SetStatus($"Reconnecting in {RetryDelaySeconds}s... ({kind})");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Runs a single enforcement tick. Returns true on success; false if a step detected the
    /// device is offline/unauthorized (caller will then trigger reconnect).
    /// </summary>
    private async Task<bool> EnforceTimeoutAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return false;

        // Step 1: Ensure Dream/screensaver system is enabled
        var dreamOk = await EnsureDreamScreensaverEnabledAsync(cancellationToken);
        if (!dreamOk) return false;

        if (cancellationToken.IsCancellationRequested) return false;

        // Step 2: HDMI-CEC prime pulse (ON→OFF) - only on the first enforcement tick
        if (!_cecPrimeCompleted)
        {
            Log("First enforcement tick - running HDMI-CEC prime pulse...");
            await RunCecPrimePulseAsync(cancellationToken);
            _cecPrimeCompleted = true;
        }

        if (cancellationToken.IsCancellationRequested) return false;

        // Step 3: Ensure deep-sleep is disabled (every tick)
        var sleepOk = await EnsureSleepDisabledAsync(cancellationToken);
        if (!sleepOk) return false;

        if (cancellationToken.IsCancellationRequested) return false;

        // Step 4: Get current screensaver timeout
        var getResult = await AdbRunner.GetScreenOffTimeoutAsync();

        if (!getResult.Success)
        {
            var kind = ClassifyError(getResult);
            LogClassifiedError(kind, getResult, "Get timeout failed");
            SetStatus("Could not read timeout - will retry");
            if (IsConnectivityError(kind))
            {
                _connectionMonitor.NotifyOffline(kind);
                return false;
            }
            return true;
        }

        // Parse from RawOutput (preserved \r\n) — Output is sanitized for logging only
        var currentValueStr = getResult.RawOutput.Trim();
        if (string.IsNullOrEmpty(currentValueStr))
        {
            currentValueStr = getResult.Output.Trim();
        }

        if (!int.TryParse(currentValueStr, out var currentValue))
        {
            var sanitizedValue = SecurityHelper.SanitizeForLog(currentValueStr);
            Log($"Could not parse timeout value: '{sanitizedValue}'");
            return true;
        }

        Log($"Current timeout: {currentValue}ms ({currentValue / 1000}s)");

        // Step 5: Set timeout if needed
        if (currentValue != _settings.TimeoutMs)
        {
            if (cancellationToken.IsCancellationRequested) return false;

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
                var kind = ClassifyError(setResult);
                LogClassifiedError(kind, setResult, "Set timeout failed");
                if (IsConnectivityError(kind))
                {
                    _connectionMonitor.NotifyOffline(kind);
                    return false;
                }
            }
        }
        else
        {
            Log($"Timeout already at {_settings.TimeoutMs}ms - no change needed");
            SetStatus($"Timeout correct as of {DateTime.Now:HH:mm:ss}");
        }

        return true;
    }

    private async Task<bool> EnsureDreamScreensaverEnabledAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return false;

        var dreamSettings = await AdbRunner.GetDreamSettingsAsync();

        if (!dreamSettings.Success)
        {
            // Build a faux AdbResult so ClassifyError works on the same string content
            var faux = new AdbResult { Output = dreamSettings.Error, Error = dreamSettings.Error };
            var kind = ClassifyError(faux);
            LogClassifiedError(kind, faux, "Dream settings read failed");
            if (IsConnectivityError(kind))
            {
                _connectionMonitor.NotifyOffline(kind);
                return false;
            }
            return true; // transient — keep the loop going
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

            if (cancellationToken.IsCancellationRequested) return false;

            var setResult = await AdbRunner.EnableDreamSettingsAsync();
            if (setResult.Success)
            {
                Log("Dream/screensaver system re-enabled successfully.");
            }
            else
            {
                var kind = ClassifyError(setResult);
                LogClassifiedError(kind, setResult, "Failed to re-enable Dream settings");
                if (IsConnectivityError(kind))
                {
                    _connectionMonitor.NotifyOffline(kind);
                    return false;
                }
            }
        }

        // Set screensaver component to Amazon Photos if missing/null
        bool needsComponent = string.IsNullOrEmpty(components) || components == "null";
        if (needsComponent)
        {
            Log("Screensaver component is not set. Setting to Amazon Photos...");

            if (cancellationToken.IsCancellationRequested) return false;

            var compResult = await AdbRunner.SetScreensaverComponentAsync();
            if (compResult.Success)
            {
                Log("Screensaver component set to Amazon Photos.");
            }
            else
            {
                var kind = ClassifyError(compResult);
                LogClassifiedError(kind, compResult, "Failed to set screensaver component");
                if (IsConnectivityError(kind))
                {
                    _connectionMonitor.NotifyOffline(kind);
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Ensures the device's secure sleep_timeout is disabled (set to 0). Runs every enforcement
    /// tick (same pattern as EnsureDreamScreensaverEnabledAsync) so OTA-resets are caught.
    /// Acceptable "already disabled" values: 0 and -1 (both mean never sleep).
    /// </summary>
    private async Task<bool> EnsureSleepDisabledAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return false;

        var getResult = await AdbRunner.GetSleepTimeoutAsync();

        if (!getResult.Success)
        {
            var kind = ClassifyError(getResult);
            LogClassifiedError(kind, getResult, "Sleep timeout read failed");
            if (IsConnectivityError(kind))
            {
                _connectionMonitor.NotifyOffline(kind);
                return false;
            }
            return true;
        }

        // Parse from RawOutput so \r\n line endings on Windows don't corrupt the value
        var currentRaw = getResult.RawOutput.Trim();
        if (string.IsNullOrEmpty(currentRaw))
        {
            currentRaw = getResult.Output.Trim();
        }

        // Accept either 0 or -1 as "already disabled"
        bool alreadyDisabled = currentRaw == "0" || currentRaw == "-1";
        if (alreadyDisabled)
        {
            Log($"Sleep already disabled (sleep_timeout={currentRaw}) - no change needed.");
            return true;
        }

        var sanitizedCurrent = SecurityHelper.SanitizeForLog(currentRaw);
        Log($"Sleep timeout is '{sanitizedCurrent}', disabling sleep (setting sleep_timeout=0)...");

        if (cancellationToken.IsCancellationRequested) return false;

        var setResult = await AdbRunner.DisableSleepAsync();
        if (setResult.Success)
        {
            Log("Sleep disabled successfully.");
        }
        else
        {
            var kind = ClassifyError(setResult);
            LogClassifiedError(kind, setResult, "Failed to disable sleep");
            if (IsConnectivityError(kind))
            {
                _connectionMonitor.NotifyOffline(kind);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Inspects an AdbResult and classifies the error type for U3 distinct logging.
    /// </summary>
    private static AdbErrorKind ClassifyError(AdbResult r)
    {
        // Combine output + error and lowercase for pattern matching. Use RawOutput when available
        // so we don't miss tokens that SanitizeForLog may have mangled (rare but possible).
        var combined = ((r.RawOutput ?? string.Empty) + "\n" +
                        (r.Output ?? string.Empty) + "\n" +
                        (r.Error ?? string.Empty)).ToLowerInvariant();

        if (combined.Contains("unauthorized")) return AdbErrorKind.Unauthorized;

        if (combined.Contains("device offline") ||
            combined.Contains("device 'offline'") ||
            combined.Contains("device is offline") ||
            combined.Contains("error: device offline"))
        {
            return AdbErrorKind.DeviceOffline;
        }

        if (combined.Contains("no devices") ||
            combined.Contains("no devices/emulators") ||
            combined.Contains("device not found") ||
            combined.Contains("device '") && combined.Contains("' not found"))
        {
            return AdbErrorKind.NoDevices;
        }

        if (combined.Contains("cannot connect to daemon") ||
            combined.Contains("daemon not running") ||
            combined.Contains("failed to start daemon") ||
            combined.Contains("server failed") ||
            combined.Contains("adb server"))
        {
            return AdbErrorKind.AdbServerError;
        }

        return AdbErrorKind.CommandFailed;
    }

    private static bool IsConnectivityError(AdbErrorKind kind) =>
        kind == AdbErrorKind.DeviceOffline ||
        kind == AdbErrorKind.Unauthorized ||
        kind == AdbErrorKind.NoDevices ||
        kind == AdbErrorKind.AdbServerError;

    /// <summary>
    /// Writes a distinct, human-readable log line for each AdbErrorKind (U3).
    /// </summary>
    private void LogClassifiedError(AdbErrorKind kind, AdbResult r, string contextLabel)
    {
        var detail = !string.IsNullOrEmpty(r.Error) ? r.Error : r.Output;
        if (string.IsNullOrEmpty(detail)) detail = "(no detail)";
        var sanitized = SecurityHelper.SanitizeForLog(detail);

        switch (kind)
        {
            case AdbErrorKind.DeviceOffline:
                Log($"[Device Offline] {contextLabel}: {sanitized}");
                break;
            case AdbErrorKind.Unauthorized:
                Log($"[Unauthorized] {contextLabel}: device authorization missing. Accept the ADB prompt on your Fire TV. Detail: {sanitized}");
                break;
            case AdbErrorKind.NoDevices:
                Log($"[No Devices] {contextLabel}: {sanitized}");
                break;
            case AdbErrorKind.AdbServerError:
                Log($"[ADB Server Error] {contextLabel}: {sanitized}");
                break;
            default:
                Log($"[Command Failed] {contextLabel}: {sanitized}");
                break;
        }
    }

    /// <summary>
    /// Updates the connection status badge (U1). Called from the connection monitor on
    /// background threads — must dispatch to UI thread.
    /// </summary>
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        DispatcherQueue.TryEnqueue(() => UpdateConnectionBadge(state));
    }

    private void UpdateConnectionBadge(ConnectionState state)
    {
        string text;
        Microsoft.UI.Xaml.Media.Brush bg;
        switch (state)
        {
            case ConnectionState.Online:
                text = "🟢 Online";
                bg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.SeaGreen);
                break;
            case ConnectionState.Offline:
                text = "🔴 Offline";
                bg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed);
                break;
            case ConnectionState.Reconnecting:
                text = "🟡 Reconnecting";
                bg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkGoldenrod);
                break;
            case ConnectionState.Unauthorized:
                text = "🔒 Unauthorized";
                bg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkOrange);
                break;
            default:
                text = "Idle";
                bg = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                break;
        }
        ConnectionBadgeText.Text = text;
        ConnectionBadge.Background = bg;
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



