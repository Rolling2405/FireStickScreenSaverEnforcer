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
}
