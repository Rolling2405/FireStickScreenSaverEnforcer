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
}
