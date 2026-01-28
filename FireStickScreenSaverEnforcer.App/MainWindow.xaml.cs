using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FireStickScreenSaverEnforcer.App.Models;
using FireStickScreenSaverEnforcer.App.Services;

namespace FireStickScreenSaverEnforcer.App;

/// <summary>
/// Main window for the Fire TV Screensaver Timeout Enforcer application.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int TargetTimeoutMs = 60000; // 1 minute
    private CancellationTokenSource? _cancellationTokenSource;
    private AppSettings _settings = new();
    private bool _isRunning;

    public MainWindow()
    {
        InitializeComponent();
        
        // Set window size
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(600, 700));
        
        LoadSettings();
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
    }

    private void SaveSettings()
    {
        var ip = IpAddressTextBox.Text?.Trim() ?? "192.168.1.50";
        var port = PortTextBox.Text?.Trim() ?? "5555";
        _settings.FirestickIp = $"{ip}:{port}";
        _settings.IntervalSeconds = (int)IntervalNumberBox.Value;
        SettingsService.Save(_settings);
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logLine = $"[{timestamp}] {message}";
        
        DispatcherQueue.TryEnqueue(() =>
        {
            LogTextBox.Text += (string.IsNullOrEmpty(LogTextBox.Text) ? "" : "\n") + logLine;
            
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
        // Validate IP address
        var ipAddress = IpAddressTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(ipAddress))
        {
            SetStatus("? Error: Please enter a Fire TV IP address.");
            Log("Error: IP address is empty.");
            return;
        }

        // Validate port
        var port = PortTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(port) || !int.TryParse(port, out var portNumber) || portNumber < 1 || portNumber > 65535)
        {
            SetStatus("? Error: Please enter a valid port (1-65535).");
            Log("Error: Invalid port number.");
            return;
        }

        // Combine IP and port for ADB
        var fullAddress = $"{ipAddress}:{port}";

        // Validate interval
        var interval = (int)IntervalNumberBox.Value;
        if (interval < 10 || interval > 600)
        {
            SetStatus("? Error: Interval must be between 10 and 600 seconds.");
            Log("Error: Invalid interval value.");
            return;
        }

        // Validate ADB files
        var (isValid, errorMessage) = AdbRunner.ValidateAdbFiles();
        if (!isValid)
        {
            SetStatus("? Error: Missing ADB files. See log for details.");
            Log($"Error: {errorMessage}");
            return;
        }

        // Save settings
        SaveSettings();

        // Start enforcement loop
        _cancellationTokenSource = new CancellationTokenSource();
        _isRunning = true;
        SetButtonStates(true);
        SetStatus("?? Running - Enforcing 1-minute timeout...");
        Log($"Started enforcement. Target: {fullAddress}, Interval: {interval}s");

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
            Log($"Unexpected error: {ex.Message}");
            SetStatus($"? Error: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
            SetButtonStates(false);
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                SetStatus("? Stopped.");
            }
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            Log("Stopping enforcement...");
            SetStatus("? Stopping...");
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
            Log($"? Connect failed: {errorMsg} - Will retry next interval.");
            SetStatus($"? Connection failed - will retry in next interval");
            return;
        }

        // Check if already connected or newly connected
        var connectOutput = connectResult.Output.ToLowerInvariant();
        if (connectOutput.Contains("connected") || connectOutput.Contains("already"))
        {
            Log($"? Connected: {connectResult.Output}");
        }
        else
        {
            Log($"Connect response: {connectResult.Output}");
        }

        if (cancellationToken.IsCancellationRequested) return;

        // Step 2: Get current timeout value
        var getResult = await AdbRunner.GetScreenOffTimeoutAsync();
        
        if (!getResult.Success)
        {
            var errorMsg = !string.IsNullOrEmpty(getResult.Error) 
                ? getResult.Error 
                : "Failed to get timeout value";
            Log($"? Get timeout failed: {errorMsg}");
            SetStatus($"? Could not read timeout - will retry");
            return;
        }

        // Parse the current value (trim whitespace and newlines)
        var currentValueStr = getResult.Output.Trim();
        
        if (!int.TryParse(currentValueStr, out var currentValue))
        {
            Log($"? Could not parse timeout value: '{currentValueStr}'");
            return;
        }

        Log($"Current timeout: {currentValue}ms ({currentValue / 1000}s)");

        // Step 3: Set timeout if needed
        if (currentValue != TargetTimeoutMs)
        {
            if (cancellationToken.IsCancellationRequested) return;
            
            Log($"? Timeout is {currentValue}ms, setting to {TargetTimeoutMs}ms (1 minute)...");
            
            var setResult = await AdbRunner.SetScreenOffTimeoutAsync(TargetTimeoutMs);
            
            if (setResult.Success)
            {
                Log($"? Timeout set to {TargetTimeoutMs}ms (1 minute)");
                SetStatus($"? Enforced 1-minute timeout at {DateTime.Now:HH:mm:ss}");
            }
            else
            {
                var errorMsg = !string.IsNullOrEmpty(setResult.Error) 
                    ? setResult.Error 
                    : "Failed to set timeout";
                Log($"? Set timeout failed: {errorMsg}");
            }
        }
        else
        {
            Log($"? Timeout already at {TargetTimeoutMs}ms - no change needed");
            SetStatus($"? Timeout correct as of {DateTime.Now:HH:mm:ss}");
        }
    }
}

