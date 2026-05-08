using System.Diagnostics;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;

namespace FireStickScreenSaverEnforcer.App.Services;

/// <summary>
/// Result of an ADB command execution.
/// </summary>
public sealed class AdbResult
{
    public bool Success { get; init; }

    /// <summary>
    /// Sanitized output (control chars replaced with '?', length-limited). Safe for logging.
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Raw, unsanitized output from the device. Use this for parsing (preserves \r and \n).
    /// </summary>
    public string RawOutput { get; init; } = string.Empty;

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
/// Service for executing ADB commands against a Fire TV device.
///
/// v1.6.0: Migrated from per-command Process.Start("adb.exe", ...) to AdvancedSharpAdbClient,
/// which keeps a persistent TCP socket connection to the local ADB server. adb.exe is still
/// required (used to start the local ADB server daemon).
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

    private static readonly SemaphoreSlim ServerStartLock = new(1, 1);
    private static bool _serverStarted;

    /// <summary>
    /// Serial of the most recently connected target device (e.g. "192.168.1.50:5555").
    /// Used as the device selector for shell commands.
    /// </summary>
    private static string _targetSerial = string.Empty;

    /// <summary>
    /// Path to the bundled adb.exe. Exposed for components that need to invoke
    /// platform-tools directly (e.g. server reset).
    /// </summary>
    public static string AdbPath => AdbExePath;

    /// <summary>
    /// Validates that all required ADB files exist.
    /// </summary>
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
    /// Ensures the local ADB server is running. Idempotent — safe to call repeatedly.
    /// </summary>
    public static async Task<AdbResult> EnsureServerStartedAsync(CancellationToken ct = default)
    {
        if (_serverStarted) return Ok();

        await ServerStartLock.WaitAsync(ct);
        try
        {
            if (_serverStarted) return Ok();

            var server = new AdbServer();
            // restartServerIfNewer: false — keeps existing server if running
            await server.StartServerAsync(AdbExePath, restartServerIfNewer: false, ct);
            _serverStarted = true;
            return Ok();
        }
        catch (Exception ex)
        {
            return Fail($"Failed to start ADB server: {ex.Message}");
        }
        finally
        {
            ServerStartLock.Release();
        }
    }

    /// <summary>
    /// Resets the ADB server (kill + start). Used for recovery from "device offline" errors.
    /// Note: this does NOT clear authorization keys (those live on disk and on the device).
    /// </summary>
    public static async Task<AdbResult> ResetServerAsync(CancellationToken ct = default)
    {
        try
        {
            var server = new AdbServer();
            try
            {
                await server.StopServerAsync(ct);
            }
            catch
            {
                // StopServer may fail if server already dead — that's fine
            }

            // Force re-start
            _serverStarted = false;
            return await EnsureServerStartedAsync(ct);
        }
        catch (Exception ex)
        {
            return Fail($"ADB server reset failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes an ADB command and returns the result.
    /// Supports the same argument shapes as the previous Process.Start-based implementation:
    ///   - "connect host:port"
    ///   - "disconnect host:port"
    ///   - "shell &lt;command&gt;"  (command may be wrapped in outer double quotes)
    ///   - "kill-server"
    /// </summary>
    public static async Task<AdbResult> RunCommandAsync(string arguments, int timeoutMs = 30000)
    {
        if (!SecurityHelper.ValidateTimeoutMs(timeoutMs))
        {
            return Fail("Invalid timeout value");
        }

        if (string.IsNullOrWhiteSpace(arguments))
        {
            return Fail("ADB command arguments cannot be empty");
        }

        using var cts = new CancellationTokenSource(timeoutMs);
        var ct = cts.Token;

        var serverResult = await EnsureServerStartedAsync(ct);
        if (!serverResult.Success) return serverResult;

        var trimmed = arguments.TrimStart();

        try
        {
            if (trimmed.StartsWith("connect ", StringComparison.OrdinalIgnoreCase))
            {
                var target = trimmed.Substring("connect ".Length).Trim();
                return await ConnectInternalAsync(target, ct);
            }

            if (trimmed.StartsWith("disconnect ", StringComparison.OrdinalIgnoreCase))
            {
                var target = trimmed.Substring("disconnect ".Length).Trim();
                return await DisconnectInternalAsync(target, ct);
            }

            if (trimmed.Equals("kill-server", StringComparison.OrdinalIgnoreCase))
            {
                return await ResetServerAsync(ct);
            }

            if (trimmed.StartsWith("shell ", StringComparison.OrdinalIgnoreCase))
            {
                var command = trimmed.Substring("shell ".Length).Trim();
                // Strip outer double quotes if present
                if (command.Length >= 2 && command.StartsWith('"') && command.EndsWith('"'))
                {
                    command = command.Substring(1, command.Length - 2);
                }
                return await ShellInternalAsync(command, ct);
            }

            return Fail($"Unsupported ADB command shape: {trimmed}");
        }
        catch (OperationCanceledException)
        {
            return Fail($"Command timed out after {timeoutMs / 1000} seconds");
        }
        catch (Exception ex)
        {
            return Fail($"Failed to execute ADB: {ex.Message}");
        }
    }

    /// <summary>
    /// Connects to a Fire TV device via ADB over TCP/IP.
    /// </summary>
    public static async Task<AdbResult> ConnectAsync(string ipAddress)
    {
        return await RunCommandAsync($"connect {ipAddress}");
    }

    /// <summary>
    /// Disconnects from a Fire TV device. Used by reconnect state machine.
    /// </summary>
    public static async Task<AdbResult> DisconnectAsync(string ipAddress)
    {
        return await RunCommandAsync($"disconnect {ipAddress}");
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
        if (!SecurityHelper.ValidateTimeoutMs(milliseconds))
        {
            return Fail("Invalid timeout value");
        }

        return await RunCommandAsync($"shell settings put system screen_off_timeout {milliseconds}");
    }

    /// <summary>
    /// Gets the current secure sleep_timeout value (controls deep-sleep, separate from screensaver).
    /// </summary>
    public static async Task<AdbResult> GetSleepTimeoutAsync()
    {
        return await RunCommandAsync("shell settings get secure sleep_timeout");
    }

    /// <summary>
    /// Disables device sleep by setting secure sleep_timeout to 0.
    /// </summary>
    public static async Task<AdbResult> DisableSleepAsync()
    {
        return await RunCommandAsync("shell settings put secure sleep_timeout 0");
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

        // Parse from RawOutput, NOT Output. SanitizeForLog (used to build Output) replaces \r with '?',
        // which would corrupt multi-line parsing on Windows where shell returns \r\n line endings.
        // RawOutput preserves the original output for correct parsing.
        var lines = result.RawOutput.Split('\n', StringSplitOptions.None);
        return new DreamSettingsResult
        {
            Success = true,
            ScreensaverEnabled = lines.Length > 0 ? lines[0].Trim().Trim('\r') : string.Empty,
            ActivateOnSleep = lines.Length > 1 ? lines[1].Trim().Trim('\r') : string.Empty,
            ActivateOnDock = lines.Length > 2 ? lines[2].Trim().Trim('\r') : string.Empty,
            ScreensaverComponents = lines.Length > 3 ? lines[3].Trim().Trim('\r') : string.Empty
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

    // ---------------- internal helpers ----------------

    private static async Task<AdbResult> ConnectInternalAsync(string target, CancellationToken ct)
    {
        var (host, port, parseError) = ParseHostPort(target);
        if (parseError != null) return Fail(parseError);

        try
        {
            var response = await AdbClient.Instance.ConnectAsync(host, port, ct);
            // Library returns server response string, e.g. "connected to 192.168.1.50:5555" or
            // "already connected to 192.168.1.50:5555" or an error message.
            var raw = response ?? string.Empty;
            var lower = raw.ToLowerInvariant();
            var success = lower.Contains("connected") && !lower.Contains("failed") && !lower.Contains("cannot");

            if (success)
            {
                _targetSerial = $"{host}:{port}";
            }

            return new AdbResult
            {
                Success = success,
                RawOutput = raw,
                Output = SecurityHelper.SanitizeForLog(raw),
                Error = success ? string.Empty : SecurityHelper.SanitizeForLog(raw),
                ExitCode = success ? 0 : 1
            };
        }
        catch (Exception ex)
        {
            return Fail($"Connect failed: {ex.Message}");
        }
    }

    private static async Task<AdbResult> DisconnectInternalAsync(string target, CancellationToken ct)
    {
        var (host, port, parseError) = ParseHostPort(target);
        if (parseError != null) return Fail(parseError);

        try
        {
            var response = await AdbClient.Instance.DisconnectAsync(host, port, ct);
            var raw = response ?? string.Empty;

            return new AdbResult
            {
                Success = true,
                RawOutput = raw,
                Output = SecurityHelper.SanitizeForLog(raw),
                ExitCode = 0
            };
        }
        catch (Exception ex)
        {
            // Disconnect is best-effort — treat as soft success
            return new AdbResult
            {
                Success = false,
                Error = SecurityHelper.SanitizeForLog($"Disconnect: {ex.Message}"),
                ExitCode = 1
            };
        }
    }

    private static async Task<AdbResult> ShellInternalAsync(string command, CancellationToken ct)
    {
        DeviceData? device;
        try
        {
            var devices = await AdbClient.Instance.GetDevicesAsync(ct);

            if (!string.IsNullOrEmpty(_targetSerial))
            {
                device = devices.FirstOrDefault(d =>
                    string.Equals(d.Serial, _targetSerial, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                device = devices.FirstOrDefault(d => d.State == DeviceState.Online);
            }

            if (device is null || device.IsEmpty)
            {
                return Fail("error: no devices/emulators found");
            }

            if (device.State != DeviceState.Online)
            {
                var stateText = device.State.ToString().ToLowerInvariant();
                return Fail($"error: device {stateText}");
            }
        }
        catch (Exception ex)
        {
            return Fail($"Failed to enumerate devices: {ex.Message}");
        }

        try
        {
            var receiver = new ConsoleOutputReceiver();
            await AdbClient.Instance.ExecuteRemoteCommandAsync(
                command, device, receiver, System.Text.Encoding.UTF8, ct);

            var raw = receiver.ToString() ?? string.Empty;

            // Heuristic success detection: common shell error markers
            var lower = raw.ToLowerInvariant();
            var hasErrorMarker =
                lower.Contains("permission denied") ||
                lower.Contains("not found") ||
                lower.StartsWith("error:") ||
                lower.Contains("\nerror:");

            return new AdbResult
            {
                Success = !hasErrorMarker,
                RawOutput = raw,
                Output = SecurityHelper.SanitizeForLog(raw),
                Error = hasErrorMarker ? SecurityHelper.SanitizeForLog(raw) : string.Empty,
                ExitCode = hasErrorMarker ? 1 : 0
            };
        }
        catch (Exception ex)
        {
            return Fail($"Shell command failed: {ex.Message}");
        }
    }

    private static (string Host, int Port, string? Error) ParseHostPort(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return (string.Empty, 0, "Empty target");
        }

        var idx = target.LastIndexOf(':');
        if (idx <= 0 || idx == target.Length - 1)
        {
            // No port specified — use ADB default
            return (target.Trim(), 5555, null);
        }

        var host = target.Substring(0, idx).Trim();
        var portStr = target.Substring(idx + 1).Trim();

        if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
        {
            return (string.Empty, 0, $"Invalid port: '{portStr}'");
        }

        return (host, port, null);
    }

    private static AdbResult Ok() => new()
    {
        Success = true,
        ExitCode = 0
    };

    private static AdbResult Fail(string error) => new()
    {
        Success = false,
        Error = SecurityHelper.SanitizeForLog(error),
        ExitCode = -1
    };
}
