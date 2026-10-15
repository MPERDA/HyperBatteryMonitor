using System.Diagnostics;
using HyperXBatteryTray.Hid;

namespace HyperXBatteryTray.Devices;

/// <summary>
/// Selects one verified receiver implementation for a user-facing headset model.
/// Existing headset drivers and their HID protocol implementations remain unchanged.
/// Only a single physically present candidate may be opened; shared TIO receivers
/// additionally validate their child slot through their own battery-query parser.
/// </summary>
internal sealed class ReceiverAwareDevice : IHyperXDevice
{
    private readonly string _modelName;
    private readonly IReadOnlyList<HyperXDeviceRegistration> _candidates;
    private const ushort SharedTioReceiverPid = 0x01BF;
    private readonly object _lifetimeLock = new();
    private IHyperXDevice? _active;
    private bool _disposed;

    public ReceiverAwareDevice(string modelName, IReadOnlyList<HyperXDeviceRegistration> candidates)
    {
        if (candidates.Count == 0)
            throw new ArgumentException("At least one receiver registration is required.", nameof(candidates));
        _modelName = modelName;
        _candidates = candidates;
    }

    public string Name => _modelName;
    public ushort VendorId => Volatile.Read(ref _active)?.VendorId ?? _candidates[0].Definition.VendorId;
    public ushort ProductId => Volatile.Read(ref _active)?.ProductId ?? _candidates[0].Definition.ProductId;
    public bool IsConnected => Volatile.Read(ref _active)?.IsConnected == true;
    public int Battery => Volatile.Read(ref _active)?.Battery ?? -1;
    public bool IsCharging => Volatile.Read(ref _active)?.IsCharging == true;
    public bool IsMicrophoneMuted => Volatile.Read(ref _active)?.IsMicrophoneMuted == true;
    public bool IsMicrophoneMuteStateKnown => Volatile.Read(ref _active)?.IsMicrophoneMuteStateKnown == true;
    public bool SupportsMicrophoneMuteMonitoring =>
        Volatile.Read(ref _active)?.SupportsMicrophoneMuteMonitoring ??
        _candidates.Any(item => item.Definition.SupportsMicrophoneMuteMonitoring);

    public event EventHandler<int>? BatteryChanged;
    public event EventHandler<bool>? ChargingChanged;
    public event EventHandler<bool>? MicrophoneMuteChanged;

    public bool Connect()
    {
        lock (_lifetimeLock)
        {
            if (_disposed) return false;
            if (_active?.IsConnected == true) return true;
            ReleaseActive();

            // Non-invasive enumeration: do not send a query to any endpoint that
            // cannot be identified as belonging to the selected headset family.
            List<HyperXDeviceRegistration> present = _candidates.Where(candidate =>
                !string.IsNullOrWhiteSpace(HidConnection.FindDevice(candidate.Definition))).ToList();

            // A shared TIO dongle may be used by an unrelated peripheral.
            // A physically present dedicated receiver takes precedence;
            // never let an unrelated TIO dongle suppress an existing driver.
            List<HyperXDeviceRegistration> dedicated = present.Where(candidate =>
                candidate.Definition.ProductId != SharedTioReceiverPid).ToList();
            List<HyperXDeviceRegistration> eligible = dedicated.Count > 0 ? dedicated : present;
            if (eligible.Count != 1)
            {
                if (eligible.Count > 1)
                    Debug.WriteLine($"Receiver selection for '{_modelName}' is ambiguous ({eligible.Count} receivers present). No receiver was opened.");
                return false;
            }

            IHyperXDevice? device = null;
            try
            {
                device = eligible[0].Factory();
                if (!device.Connect())
                {
                    device.Dispose();
                    return false;
                }

                device.BatteryChanged += Active_BatteryChanged;
                device.ChargingChanged += Active_ChargingChanged;
                device.MicrophoneMuteChanged += Active_MicrophoneMuteChanged;
                Volatile.Write(ref _active, device);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Receiver connection failed for '{_modelName}': {ex.Message}");
                device?.Dispose();
                return false;
            }
        }
    }

    public void Disconnect()
    {
        lock (_lifetimeLock)
            ReleaseActive();
    }

    public Task<int?> QueryBatteryAsync(CancellationToken cancellationToken = default) =>
        Volatile.Read(ref _active)?.QueryBatteryAsync(cancellationToken) ?? Task.FromResult<int?>(null);

    public Task<bool?> QueryChargeStatusAsync(CancellationToken cancellationToken = default) =>
        Volatile.Read(ref _active)?.QueryChargeStatusAsync(cancellationToken) ?? Task.FromResult<bool?>(null);

    public Task<bool?> QueryMicrophoneMuteStatusAsync(CancellationToken cancellationToken = default) =>
        Volatile.Read(ref _active)?.QueryMicrophoneMuteStatusAsync(cancellationToken) ?? Task.FromResult<bool?>(null);

    private void Active_BatteryChanged(object? sender, int battery)
    {
        if (ReferenceEquals(sender, Volatile.Read(ref _active)))
            BatteryChanged?.Invoke(this, battery);
    }

    private void Active_ChargingChanged(object? sender, bool charging)
    {
        if (ReferenceEquals(sender, Volatile.Read(ref _active)))
            ChargingChanged?.Invoke(this, charging);
    }

    private void Active_MicrophoneMuteChanged(object? sender, bool muted)
    {
        if (ReferenceEquals(sender, Volatile.Read(ref _active)))
            MicrophoneMuteChanged?.Invoke(this, muted);
    }

    private void ReleaseActive()
    {
        IHyperXDevice? old = Interlocked.Exchange(ref _active, null);
        if (old == null) return;
        old.BatteryChanged -= Active_BatteryChanged;
        old.ChargingChanged -= Active_ChargingChanged;
        old.MicrophoneMuteChanged -= Active_MicrophoneMuteChanged;
        old.Dispose();
    }

    public void Dispose()
    {
        lock (_lifetimeLock)
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseActive();
        }
    }
}
