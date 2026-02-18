using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FireStickScreenSaverEnforcer.App.Services;

/// <summary>
/// Provides input validation and output sanitization methods for security.
/// </summary>
public static partial class SecurityHelper
{
    // Regex patterns for validation (compiled for performance)
    [GeneratedRegex(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$")]
    private static partial Regex IpAddressPattern();

    [GeneratedRegex(@"^[0-9]{1,5}$")]
    private static partial Regex PortPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9_\-\.]+$")]
    private static partial Regex SafeIdentifierPattern();

    // Characters that could be used for command injection or log injection
    private static readonly char[] DangerousChars = ['&', '|', ';', '$', '`', '\n', '\r', '<', '>', '(', ')', '{', '}'];

    /// <summary>
    /// Validates an IP address string.
    /// </summary>
    /// <param name="ipAddress">The IP address to validate.</param>
    /// <param name="validatedIp">The validated IP address (trimmed).</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateIpAddress(string? ipAddress, out string validatedIp)
    {
        validatedIp = string.Empty;

        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        var trimmed = ipAddress.Trim();

        // Check pattern match
        if (!IpAddressPattern().IsMatch(trimmed))
            return false;

        // Double-check using IPAddress.TryParse for additional safety
        if (!IPAddress.TryParse(trimmed, out var parsedIp))
            return false;

        // Ensure it's IPv4
        if (parsedIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        validatedIp = trimmed;
        return true;
    }

    /// <summary>
    /// Validates a port number.
    /// </summary>
    /// <param name="port">The port number to validate.</param>
    /// <param name="validatedPort">The validated port number.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidatePort(string? port, out int validatedPort)
    {
        validatedPort = 0;

        if (string.IsNullOrWhiteSpace(port))
            return false;

        var trimmed = port.Trim();

        // Check pattern
        if (!PortPattern().IsMatch(trimmed))
            return false;

        // Parse and validate range
        if (!int.TryParse(trimmed, out var portNum))
            return false;

        if (portNum < 1 || portNum > 65535)
            return false;

        validatedPort = portNum;
        return true;
    }

    /// <summary>
    /// Validates an interval in seconds (10-30).
    /// </summary>
    /// <param name="intervalSeconds">The interval to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateIntervalSeconds(int intervalSeconds)
    {
        return intervalSeconds >= 10 && intervalSeconds <= 30;
    }

    /// <summary>
    /// Validates a timeout in milliseconds (must be positive).
    /// </summary>
    /// <param name="timeoutMs">The timeout to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateTimeoutMs(int timeoutMs)
    {
        return timeoutMs > 0 && timeoutMs <= int.MaxValue;
    }

    /// <summary>
    /// Sanitizes a string for safe use in ADB commands by removing dangerous characters.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <returns>Sanitized string safe for use in commands.</returns>
    public static string SanitizeForCommand(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var trimmed = input.Trim();

        // Check for dangerous characters
        if (trimmed.IndexOfAny(DangerousChars) >= 0)
        {
            // Remove all dangerous characters
            var sb = new StringBuilder(trimmed.Length);
            foreach (var c in trimmed)
            {
                if (Array.IndexOf(DangerousChars, c) < 0)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        return trimmed;
    }

    /// <summary>
    /// Sanitizes output for logging by removing control characters and limiting length.
    /// </summary>
    /// <param name="output">The output to sanitize.</param>
    /// <param name="maxLength">Maximum length (default 1000 characters).</param>
    /// <returns>Sanitized output safe for logging.</returns>
    public static string SanitizeForLog(string? output, int maxLength = 1000)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        var sanitized = output.Trim();

        // Remove or replace control characters except newline and tab
        var sb = new StringBuilder(sanitized.Length);
        foreach (var c in sanitized)
        {
            if (c == '\n' || c == '\t' || (c >= 32 && c < 127) || c >= 160)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('?'); // Replace control chars with '?'
            }
        }

        sanitized = sb.ToString();

        // Limit length
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength) + "... (truncated)";
        }

        return sanitized;
    }

    /// <summary>
    /// Validates that a settings key name is safe (alphanumeric, underscore, hyphen, dot only).
    /// </summary>
    /// <param name="keyName">The key name to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateSettingsKeyName(string? keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return false;

        var trimmed = keyName.Trim();

        // Must be alphanumeric with underscore, hyphen, or dot only
        return SafeIdentifierPattern().IsMatch(trimmed);
    }

    /// <summary>
    /// Validates that a namespace is one of the allowed Android settings namespaces.
    /// </summary>
    /// <param name="ns">The namespace to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateNamespace(string? ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
            return false;

        var trimmed = ns.Trim().ToLowerInvariant();
        return trimmed is "global" or "secure" or "system";
    }

    /// <summary>
    /// Sanitizes an IP:Port combination for safe storage and use.
    /// </summary>
    /// <param name="ipAddress">IP address.</param>
    /// <param name="port">Port number.</param>
    /// <param name="sanitized">Sanitized IP:Port string.</param>
    /// <returns>True if both are valid, false otherwise.</returns>
    public static bool SanitizeIpPort(string? ipAddress, string? port, out string sanitized)
    {
        sanitized = string.Empty;

        if (!ValidateIpAddress(ipAddress, out var validIp))
            return false;

        if (!ValidatePort(port, out var validPort))
            return false;

        sanitized = $"{validIp}:{validPort}";
        return true;
    }
}
