using System.Diagnostics;

namespace FireStickScreenSaverEnforcer.App.Services;

/// <summary>
/// Result of an ADB command execution.
/// </summary>
public sealed class AdbResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}

/// <summary>
/// Result of reading Dream/screensaver secure settings from the device.
/// </summary>
public sealed class DreamSettingsResult
{
    public bool Success { get; init; }
    public string Error { get; init; } = string.Empty;
    public string ScreensaverEnabled { get; init; } = string.Empty;
    public string ActivateOnSleep { get; init; } = string.Empty;
    public string ActivateOnDock { get; init; } = string.Empty;
    public string ScreensaverComponents { get; init; } = string.Empty;
}

/// <summary>
/// Service for executing ADB commands using the local platform-tools folder.
/// </summary>
public static class AdbRunner
{
    private static readonly string PlatformToolsPath = Path.Combine(
        AppContext.BaseDirectory,
        "platform-tools"
    );

    private static readonly string AdbExePath = Path.Combine(PlatformToolsPath, "adb.exe");
    private static readonly string AdbWinApiPath = Path.Combine(PlatformToolsPath, "AdbWinApi.dll");
    private static readonly string AdbWinUsbApiPath = Path.Combine(PlatformToolsPath, "AdbWinUsbApi.dll");

    /// <summary>
    /// Validates that all required ADB files exist.
    /// </summary>
    /// <returns>Tuple of (isValid, errorMessage)</returns>
    public static (bool IsValid, string ErrorMessage) ValidateAdbFiles()
    {
        var missingFiles = new List<string>();

        if (!File.Exists(AdbExePath))
            missingFiles.Add("adb.exe");
        
        if (!File.Exists(AdbWinApiPath))
            missingFiles.Add("AdbWinApi.dll");
        
        if (!File.Exists(AdbWinUsbApiPath))
            missingFiles.Add("AdbWinUsbApi.dll");

        if (missingFiles.Count == 0)
        {
            return (true, string.Empty);
        }

        var error = $"Missing required files in platform-tools folder:\n" +
                   $"  {string.Join(", ", missingFiles)}\n\n" +
                   $"Expected location: {PlatformToolsPath}\n\n" +
                   $"Download platform-tools from:\n" +
                   $"  https://developer.android.com/studio/releases/platform-tools\n" +
                   $"Then copy adb.exe, AdbWinApi.dll, and AdbWinUsbApi.dll to the platform-tools folder.";

        return (false, error);
    }

    /// <summary>
    /// Executes an ADB command and returns the result.
    /// </summary>
    public static async Task<AdbResult> RunCommandAsync(string arguments, int timeoutMs = 30000)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = AdbExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = PlatformToolsPath
            };

            process.Start();

            // Read output asynchronously
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

            if (!completed)
            {
                try { process.Kill(); } catch { }
                return new AdbResult
                {
                    Success = false,
                    Error = $"Command timed out after {timeoutMs / 1000} seconds",
                    ExitCode = -1
                };
            }

            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            return new AdbResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new AdbResult
            {
                Success = false,
                Error = $"Failed to execute ADB: {ex.Message}",
                ExitCode = -1
            };
        }
    }

    /// <summary>
    /// Connects to a Fire TV device via ADB.
    /// </summary>
    public static async Task<AdbResult> ConnectAsync(string ipAddress)
    {
        return await RunCommandAsync($"connect {ipAddress}");
    }

    /// <summary>
    /// Gets the current screen_off_timeout value from the device.
    /// </summary>
    public static async Task<AdbResult> GetScreenOffTimeoutAsync()
    {
        return await RunCommandAsync("shell settings get system screen_off_timeout");
    }

    /// <summary>
    /// Sets the screen_off_timeout value on the device.
    /// </summary>
    public static async Task<AdbResult> SetScreenOffTimeoutAsync(int milliseconds)
    {
        return await RunCommandAsync($"shell settings put system screen_off_timeout {milliseconds}");
    }

    /// <summary>
    /// Reads the Dream/screensaver secure settings in a single ADB shell invocation.
    /// </summary>
    public static async Task<DreamSettingsResult> GetDreamSettingsAsync()
    {
        var result = await RunCommandAsync(
            "shell \"settings get secure screensaver_enabled; " +
            "settings get secure screensaver_activate_on_sleep; " +
            "settings get secure screensaver_activate_on_dock; " +
            "settings get secure screensaver_components\"");

        if (!result.Success)
        {
            return new DreamSettingsResult
            {
                Success = false,
                Error = !string.IsNullOrEmpty(result.Error) ? result.Error : "Failed to read Dream settings"
            };
        }

        var lines = result.Output.Split('\n', StringSplitOptions.None);
        return new DreamSettingsResult
        {
            Success = true,
            ScreensaverEnabled = lines.Length > 0 ? lines[0].Trim() : string.Empty,
            ActivateOnSleep = lines.Length > 1 ? lines[1].Trim() : string.Empty,
            ActivateOnDock = lines.Length > 2 ? lines[2].Trim() : string.Empty,
            ScreensaverComponents = lines.Length > 3 ? lines[3].Trim() : string.Empty
        };
    }

    /// <summary>
    /// Enables the Dream/screensaver system by setting all required secure flags in one call.
    /// </summary>
    public static async Task<AdbResult> EnableDreamSettingsAsync()
    {
        return await RunCommandAsync(
            "shell \"settings put secure screensaver_enabled 1; " +
            "settings put secure screensaver_activate_on_sleep 1; " +
            "settings put secure screensaver_activate_on_dock 1\"");
    }

    /// <summary>
    /// Sets the screensaver component to Amazon Photos (only if it was missing).
    /// </summary>
    public static async Task<AdbResult> SetScreensaverComponentAsync()
    {
        return await RunCommandAsync(
            "shell settings put secure screensaver_components com.amazon.bueller.photos/.daydream.ScreenSaverService");
    }
}
