namespace FireStickScreenSaverEnforcer.App.Services;

/// <summary>
/// Result of the HDMI-CEC prime pulse operation.
/// </summary>
public sealed class PrimeResult
{
    public bool Success { get; init; }
    public bool WasAlreadyOff { get; init; }
    public string KeyUsed { get; init; } = string.Empty;
    public string NamespaceUsed { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
}

/// <summary>
/// Discovered CEC key info returned from key probing.
/// </summary>
public sealed class CecKeyInfo
{
    public bool Found { get; init; }
    public string Namespace { get; init; } = string.Empty;
    public string KeyName { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
}

/// <summary>
/// Performs an HDMI-CEC "prime pulse" (ON→OFF) via ADB settings writes.
/// Designed to replicate the manual workaround of toggling HDMI CEC Device Control ON then OFF.
/// Final enforced state is always OFF (0).
/// </summary>
public sealed class HdmiCecPrimer
{
    private const string DefaultCecKey = "hdmi_control_enabled";
    private const string DefaultCecNamespace = "global";
    private const int PulseDelayMs = 1000;

    private readonly Action<string> _log;

    private string _cachedNamespace = string.Empty;
    private string _cachedKeyName = string.Empty;

    public HdmiCecPrimer(Action<string> log)
    {
        _log = log;
    }

    /// <summary>
    /// Loads a previously discovered CEC key from app settings cache.
    /// Validates the cached values before using them.
    /// </summary>
    public void LoadCachedKey(string cachedNamespace, string cachedKeyName)
    {
        // Validate namespace and key name before caching
        if (SecurityHelper.ValidateNamespace(cachedNamespace) && 
            SecurityHelper.ValidateSettingsKeyName(cachedKeyName))
        {
            _cachedNamespace = cachedNamespace;
            _cachedKeyName = cachedKeyName;
        }
        else
        {
            _cachedNamespace = string.Empty;
            _cachedKeyName = string.Empty;
        }
    }

    /// <summary>
    /// Returns the currently cached key info (namespace + key name).
    /// </summary>
    public (string Namespace, string KeyName) GetCachedKey() => (_cachedNamespace, _cachedKeyName);

    /// <summary>
    /// Performs the HDMI-CEC prime pulse: ON(1) → wait → OFF(0).
    /// Final state is always OFF.
    /// </summary>
    public async Task<PrimeResult> PrimeAsync(CancellationToken ct)
    {
        // Step 1: Discover or use cached CEC key
        var keyInfo = await DiscoverCecKeyAsync(ct);
        if (!keyInfo.Found)
        {
            return new PrimeResult
            {
                Success = false,
                Error = keyInfo.Error
            };
        }

        var ns = keyInfo.Namespace;
        var key = keyInfo.KeyName;
        _log($"CEC prime pulse using {ns}/{key}");

        // Step 2: Read current value
        var currentValue = await ReadSettingAsync(ns, key);
        var sanitizedCurrentValue = SecurityHelper.SanitizeForLog(currentValue);
        _log($"CEC current value: '{sanitizedCurrentValue}'");

        if (ct.IsCancellationRequested) return Cancelled();

        // Step 3: Set ON (1)
        _log("CEC pulse: setting ON (1)...");
        var setOnResult = await WriteSettingAsync(ns, key, "1");
        if (!setOnResult.Success)
        {
            var sanitizedError = SecurityHelper.SanitizeForLog(setOnResult.Error);
            _log($"CEC pulse: failed to set ON: {sanitizedError}");
            // Ensure OFF before returning
            await WriteSettingAsync(ns, key, "0");
            return new PrimeResult { Success = false, KeyUsed = key, NamespaceUsed = ns, Error = $"Failed to set ON: {sanitizedError}" };
        }

        // Verify ON
        var verifyOn = await ReadSettingAsync(ns, key);
        var sanitizedVerifyOn = SecurityHelper.SanitizeForLog(verifyOn);
        _log($"CEC pulse: verify ON = '{sanitizedVerifyOn}'");

        if (ct.IsCancellationRequested)
        {
            // Even on cancellation, ensure OFF
            await WriteSettingAsync(ns, key, "0");
            return Cancelled();
        }

        // Step 4: Wait pulse delay
        try
        {
            await Task.Delay(PulseDelayMs, ct);
        }
        catch (OperationCanceledException)
        {
            await WriteSettingAsync(ns, key, "0");
            return Cancelled();
        }

        // Step 5: Set OFF (0)
        _log("CEC pulse: setting OFF (0)...");
        var setOffResult = await WriteSettingAsync(ns, key, "0");
        if (!setOffResult.Success)
        {
            var sanitizedError = SecurityHelper.SanitizeForLog(setOffResult.Error);
            _log($"CEC pulse: failed to set OFF: {sanitizedError}");
            return new PrimeResult { Success = false, KeyUsed = key, NamespaceUsed = ns, Error = $"Failed to set OFF: {sanitizedError}" };
        }

        // Verify OFF
        var verifyOff = await ReadSettingAsync(ns, key);
        var sanitizedVerifyOff = SecurityHelper.SanitizeForLog(verifyOff);
        _log($"CEC pulse: verify OFF = '{sanitizedVerifyOff}'");

        bool wasAlreadyOff = currentValue == "0";
        _log(wasAlreadyOff
            ? "CEC prime pulse complete (was already OFF, pulsed ON→OFF)."
            : "CEC prime pulse complete (ON→OFF).");

        return new PrimeResult
        {
            Success = true,
            WasAlreadyOff = wasAlreadyOff,
            KeyUsed = key,
            NamespaceUsed = ns
        };
    }

    /// <summary>
    /// Discovers the correct CEC settings key. Uses cache if available, otherwise probes.
    /// </summary>
    private async Task<CecKeyInfo> DiscoverCecKeyAsync(CancellationToken ct)
    {
        // Use cached key if available
        if (!string.IsNullOrEmpty(_cachedNamespace) && !string.IsNullOrEmpty(_cachedKeyName))
        {
            _log($"Using cached CEC key: {_cachedNamespace}/{_cachedKeyName}");
            return new CecKeyInfo { Found = true, Namespace = _cachedNamespace, KeyName = _cachedKeyName };
        }

        // Try the common Android TV key first
        _log($"Probing default CEC key: {DefaultCecNamespace}/{DefaultCecKey}...");
        if (await ProbeKeyAsync(DefaultCecNamespace, DefaultCecKey, ct))
        {
            CacheKey(DefaultCecNamespace, DefaultCecKey);
            return new CecKeyInfo { Found = true, Namespace = DefaultCecNamespace, KeyName = DefaultCecKey };
        }

        if (ct.IsCancellationRequested)
            return new CecKeyInfo { Found = false, Error = "Cancelled during key discovery." };

        // Scan all namespaces for CEC/HDMI candidate keys
        _log("Default CEC key not usable. Scanning settings for CEC/HDMI keys...");
        string[] namespaces = ["global", "secure", "system"];

        foreach (var ns in namespaces)
        {
            if (ct.IsCancellationRequested)
                return new CecKeyInfo { Found = false, Error = "Cancelled during key discovery." };

            var candidates = await FindCandidateKeysAsync(ns, ct);
            foreach (var candidateKey in candidates)
            {
                if (ct.IsCancellationRequested)
                    return new CecKeyInfo { Found = false, Error = "Cancelled during key discovery." };

                _log($"Probing candidate: {ns}/{candidateKey}...");
                if (await ProbeKeyAsync(ns, candidateKey, ct))
                {
                    CacheKey(ns, candidateKey);
                    return new CecKeyInfo { Found = true, Namespace = ns, KeyName = candidateKey };
                }
            }
        }

        return new CecKeyInfo { Found = false, Error = "No usable HDMI-CEC settings key found on this device." };
    }

    /// <summary>
    /// Probes a key to see if it supports reliable 1/0 toggling.
    /// Always ends with the key set to 0 (OFF).
    /// </summary>
    private async Task<bool> ProbeKeyAsync(string ns, string key, CancellationToken ct)
    {
        // Read original value
        var original = await ReadSettingAsync(ns, key);
        if (original == "null" || string.IsNullOrEmpty(original))
        {
            _log($"  Probe {ns}/{key}: returned '{original}' (key missing or unset).");
            return false;
        }

        // Write 1
        var writeOn = await WriteSettingAsync(ns, key, "1");
        if (!writeOn.Success)
        {
            _log($"  Probe {ns}/{key}: write 1 failed.");
            return false;
        }

        var readOn = await ReadSettingAsync(ns, key);
        if (readOn != "1")
        {
            _log($"  Probe {ns}/{key}: read-back after write 1 = '{readOn}' (expected '1').");
            // Try to restore
            await WriteSettingAsync(ns, key, "0");
            return false;
        }

        if (ct.IsCancellationRequested)
        {
            await WriteSettingAsync(ns, key, "0");
            return false;
        }

        // Write 0
        var writeOff = await WriteSettingAsync(ns, key, "0");
        if (!writeOff.Success)
        {
            _log($"  Probe {ns}/{key}: write 0 failed.");
            return false;
        }

        var readOff = await ReadSettingAsync(ns, key);
        if (readOff != "0")
        {
            _log($"  Probe {ns}/{key}: read-back after write 0 = '{readOff}' (expected '0').");
            return false;
        }

        _log($"  Probe {ns}/{key}: OK (supports 1/0 toggle).");
        return true;
    }

    /// <summary>
    /// Finds candidate settings keys containing "cec" or "hdmi" in the given namespace.
    /// </summary>
    private async Task<List<string>> FindCandidateKeysAsync(string ns, CancellationToken ct)
    {
        var candidates = new List<string>();
        var result = await AdbRunner.RunCommandAsync($"shell settings list {ns}");

        if (!result.Success || string.IsNullOrEmpty(result.Output))
            return candidates;

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (ct.IsCancellationRequested) break;

            // Lines are typically "key=value"
            var eqIndex = line.IndexOf('=');
            var keyName = eqIndex > 0 ? line[..eqIndex].Trim() : line.Trim();

            if (keyName.Contains("cec", StringComparison.OrdinalIgnoreCase) ||
                keyName.Contains("hdmi", StringComparison.OrdinalIgnoreCase))
            {
                // Skip the default key (already tried)
                if (ns == DefaultCecNamespace && keyName == DefaultCecKey)
                    continue;

                candidates.Add(keyName);
            }
        }

        if (candidates.Count > 0)
            _log($"  Found {candidates.Count} CEC/HDMI candidate(s) in {ns}: {string.Join(", ", candidates)}");

        return candidates;
    }

    private void CacheKey(string ns, string key)
    {
        // Validate before caching
        if (SecurityHelper.ValidateNamespace(ns) && SecurityHelper.ValidateSettingsKeyName(key))
        {
            _cachedNamespace = ns;
            _cachedKeyName = key;
            _log($"Cached CEC key: {ns}/{key}");
        }
        else
        {
            _log($"Invalid CEC key format, not cached: {ns}/{key}");
        }
    }

    private static async Task<string> ReadSettingAsync(string ns, string key)
    {
        // Validate namespace and key before building command
        if (!SecurityHelper.ValidateNamespace(ns) || !SecurityHelper.ValidateSettingsKeyName(key))
        {
            return "null";
        }

        var result = await AdbRunner.RunCommandAsync($"shell settings get {ns} {key}");
        return result.Success ? result.Output.Trim() : string.Empty;
    }

    private static async Task<AdbResult> WriteSettingAsync(string ns, string key, string value)
    {
        // Validate namespace and key before building command
        if (!SecurityHelper.ValidateNamespace(ns) || !SecurityHelper.ValidateSettingsKeyName(key))
        {
            return new AdbResult
            {
                Success = false,
                Error = "Invalid namespace or key name",
                ExitCode = -1
            };
        }

        // Validate value (should be 0 or 1 for CEC settings)
        var sanitizedValue = SecurityHelper.SanitizeForCommand(value);
        if (sanitizedValue != "0" && sanitizedValue != "1")
        {
            return new AdbResult
            {
                Success = false,
                Error = "Invalid setting value (must be 0 or 1)",
                ExitCode = -1
            };
        }

        return await AdbRunner.RunCommandAsync($"shell settings put {ns} {key} {sanitizedValue}");
    }

    private static PrimeResult Cancelled() => new()
    {
        Success = false,
        Error = "Operation cancelled."
    };
}
