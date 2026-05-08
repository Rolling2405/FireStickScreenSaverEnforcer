using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;

namespace FireStickScreenSaverEnforcer.App.Services;

/// <summary>
/// Connection states surfaced to the UI.
/// </summary>
public enum ConnectionState
{
    /// <summary>Not started — no monitoring active.</summary>
    Idle,
    /// <summary>Device is online and authorized.</summary>
    Online,
    /// <summary>Device is offline or not present.</summary>
    Offline,
    /// <summary>Active reconnect attempts in progress.</summary>
    Reconnecting,
    /// <summary>Device is connected but unauthorized.</summary>
    Unauthorized
}

/// <summary>
/// Reasons that explain a transition into Offline / Reconnecting state.
/// Drives distinct log messages (U3) and UI hints.
/// </summary>
public enum AdbErrorKind
{
    None,
    DeviceOffline,
    Unauthorized,
    NoDevices,
    AdbServerError,
    CommandFailed
}

/// <summary>
/// Monitors the ADB connection to a single Fire TV target and exposes a simple state
/// model for the UI. Internally wraps AdvancedSharpAdbClient's DeviceMonitor (real-time
/// device state events) and lets the enforcement loop push state transitions during
/// reconnect attempts and command failures.
/// </summary>
public sealed class AdbConnectionMonitor : IAsyncDisposable
{
    private readonly object _stateLock = new();
    private DeviceMonitor? _monitor;
    private string _targetSerial = string.Empty;
    private ConnectionState _state = ConnectionState.Idle;
    private AdbErrorKind _lastError = AdbErrorKind.None;

    /// <summary>
    /// Raised whenever the high-level connection state changes. Fired on a background thread —
    /// the UI must dispatch to the UI thread before mutating controls.
    /// </summary>
    public event EventHandler<ConnectionState>? StateChanged;

    public ConnectionState State
    {
        get { lock (_stateLock) return _state; }
    }

    public AdbErrorKind LastError
    {
        get { lock (_stateLock) return _lastError; }
    }

    /// <summary>
    /// Begins monitoring the given target. Idempotent — calling again with the same target
    /// is a no-op. Calling with a different target restarts the monitor.
    /// </summary>
    public async Task StartAsync(string targetSerial, CancellationToken ct)
    {
        if (string.Equals(_targetSerial, targetSerial, StringComparison.OrdinalIgnoreCase) && _monitor != null)
        {
            return;
        }

        await StopAsync();
        _targetSerial = targetSerial;

        _monitor = new DeviceMonitor(new AdbSocket(AdbClient.Instance.EndPoint));
        _monitor.DeviceConnected += OnDeviceConnected;
        _monitor.DeviceDisconnected += OnDeviceDisconnected;
        _monitor.DeviceChanged += OnDeviceChanged;

        await _monitor.StartAsync(ct);

        // Seed initial state from current device list
        try
        {
            var devices = await AdbClient.Instance.GetDevicesAsync(ct);
            var dev = devices.FirstOrDefault(d =>
                string.Equals(d.Serial, _targetSerial, StringComparison.OrdinalIgnoreCase));

            if (dev is not null && !dev.IsEmpty)
            {
                ApplyDeviceState(dev);
            }
            else
            {
                SetState(ConnectionState.Offline, AdbErrorKind.NoDevices);
            }
        }
        catch
        {
            SetState(ConnectionState.Offline, AdbErrorKind.AdbServerError);
        }
    }

    public async Task StopAsync()
    {
        if (_monitor is null) return;

        _monitor.DeviceConnected -= OnDeviceConnected;
        _monitor.DeviceDisconnected -= OnDeviceDisconnected;
        _monitor.DeviceChanged -= OnDeviceChanged;

        try { await _monitor.DisposeAsync(); } catch { /* swallow shutdown errors */ }
        _monitor = null;

        SetState(ConnectionState.Idle, AdbErrorKind.None);
    }

    /// <summary>
    /// Called by the enforcement loop when it begins active reconnect attempts.
    /// </summary>
    public void NotifyReconnecting(AdbErrorKind reason)
    {
        SetState(ConnectionState.Reconnecting, reason);
    }

    /// <summary>
    /// Called by the enforcement loop after a confirmed-successful enforcement tick.
    /// </summary>
    public void NotifyOnline()
    {
        SetState(ConnectionState.Online, AdbErrorKind.None);
    }

    /// <summary>
    /// Called by the enforcement loop when a command surfaces an offline/unauthorized error.
    /// </summary>
    public void NotifyOffline(AdbErrorKind reason)
    {
        var newState = reason == AdbErrorKind.Unauthorized
            ? ConnectionState.Unauthorized
            : ConnectionState.Offline;
        SetState(newState, reason);
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    // ---------------- internals ----------------

    private bool IsTarget(DeviceData? d)
    {
        if (d is null || d.IsEmpty) return false;
        if (string.IsNullOrEmpty(_targetSerial)) return false;
        return string.Equals(d.Serial, _targetSerial, StringComparison.OrdinalIgnoreCase);
    }

    private void OnDeviceConnected(object? sender, DeviceDataConnectEventArgs e)
    {
        if (!IsTarget(e.Device)) return;
        // Connected event fires when the device first appears — actual state may still be
        // "offline" or "unauthorized" until ADB completes its handshake. Wait for DeviceChanged.
    }

    private void OnDeviceDisconnected(object? sender, DeviceDataConnectEventArgs e)
    {
        if (!IsTarget(e.Device)) return;
        SetState(ConnectionState.Offline, AdbErrorKind.DeviceOffline);
    }

    private void OnDeviceChanged(object? sender, DeviceDataChangeEventArgs e)
    {
        if (!IsTarget(e.Device)) return;
        ApplyDeviceState(e.Device);
    }

    private void ApplyDeviceState(DeviceData device)
    {
        switch (device.State)
        {
            case DeviceState.Online:
                SetState(ConnectionState.Online, AdbErrorKind.None);
                break;
            case DeviceState.Unauthorized:
                SetState(ConnectionState.Unauthorized, AdbErrorKind.Unauthorized);
                break;
            case DeviceState.Offline:
                SetState(ConnectionState.Offline, AdbErrorKind.DeviceOffline);
                break;
            default:
                SetState(ConnectionState.Offline, AdbErrorKind.DeviceOffline);
                break;
        }
    }

    private void SetState(ConnectionState newState, AdbErrorKind reason)
    {
        bool changed;
        lock (_stateLock)
        {
            changed = _state != newState;
            _state = newState;
            _lastError = reason;
        }

        if (changed)
        {
            try { StateChanged?.Invoke(this, newState); } catch { /* never let listener exceptions bubble */ }
        }
    }
}
