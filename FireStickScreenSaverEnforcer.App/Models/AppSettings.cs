namespace FireStickScreenSaverEnforcer.App.Models;

/// <summary>
/// Application settings stored in settings.json next to the executable.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Fire TV Stick IP address including port (e.g., "192.168.1.50:5555").
    /// </summary>
    public string FirestickIp { get; set; } = "192.168.1.50:5555";

    /// <summary>
    /// Interval in seconds between enforcement checks (10-600, default 30).
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Screensaver timeout in milliseconds (default: 60000).
    /// </summary>
    public int TimeoutMs { get; set; } = 60000;

    /// <summary>
    /// When true, closing the window minimizes to the system tray instead of exiting.
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Cached HDMI-CEC settings namespace discovered by key probing (e.g., "global", "secure", "system").
    /// Empty means not yet discovered.
    /// </summary>
    public string CecKeyNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Cached HDMI-CEC settings key name discovered by key probing (e.g., "hdmi_control_enabled").
    /// Empty means not yet discovered.
    /// </summary>
    public string CecKeyName { get; set; } = string.Empty;
}
