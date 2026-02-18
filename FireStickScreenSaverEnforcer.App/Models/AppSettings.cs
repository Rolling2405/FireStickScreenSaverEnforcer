using FireStickScreenSaverEnforcer.App.Services;

namespace FireStickScreenSaverEnforcer.App.Models;

/// <summary>
/// Application settings stored in settings.json next to the executable.
/// </summary>
public sealed class AppSettings
{
    private string _firestickIp = "192.168.1.50:5555";
    private int _intervalSeconds = 30;
    private int _timeoutMs = 60000;
    private string _cecKeyNamespace = string.Empty;
    private string _cecKeyName = string.Empty;

    /// <summary>
    /// Fire TV Stick IP address including port (e.g., "192.168.1.50:5555").
    /// </summary>
    public string FirestickIp
    {
        get => _firestickIp;
        set
        {
            // Validate and sanitize IP:Port
            if (!string.IsNullOrWhiteSpace(value))
            {
                var parts = value.Split(':');
                if (parts.Length == 2 && 
                    SecurityHelper.ValidateIpAddress(parts[0], out var validIp) &&
                    SecurityHelper.ValidatePort(parts[1], out var validPort))
                {
                    _firestickIp = $"{validIp}:{validPort}";
                }
                else
                {
                    // Invalid format, keep default
                    _firestickIp = "192.168.1.50:5555";
                }
            }
        }
    }

    /// <summary>
    /// Interval in seconds between enforcement checks (10-600, default 30).
    /// </summary>
    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set => _intervalSeconds = SecurityHelper.ValidateIntervalSeconds(value) ? value : 30;
    }

    /// <summary>
    /// Screensaver timeout in milliseconds (default: 60000).
    /// </summary>
    public int TimeoutMs
    {
        get => _timeoutMs;
        set => _timeoutMs = SecurityHelper.ValidateTimeoutMs(value) ? value : 60000;
    }

    /// <summary>
    /// When true, closing the window minimizes to the system tray instead of exiting.
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Cached HDMI-CEC settings namespace discovered by key probing (e.g., "global", "secure", "system").
    /// Empty means not yet discovered.
    /// </summary>
    public string CecKeyNamespace
    {
        get => _cecKeyNamespace;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || SecurityHelper.ValidateNamespace(value))
            {
                _cecKeyNamespace = value?.Trim() ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Cached HDMI-CEC settings key name discovered by key probing (e.g., "hdmi_control_enabled").
    /// Empty means not yet discovered.
    /// </summary>
    public string CecKeyName
    {
        get => _cecKeyName;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || SecurityHelper.ValidateSettingsKeyName(value))
            {
                _cecKeyName = value?.Trim() ?? string.Empty;
            }
        }
    }
}
