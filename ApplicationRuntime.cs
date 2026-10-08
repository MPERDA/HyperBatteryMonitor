namespace HyperXBatteryTray;

/// <summary>
/// Single source of truth for process-wide execution mode. Release builds have
/// no way to activate test-only endpoints or update installation paths.
/// </summary>
internal enum ApplicationMode
{
    Normal,
    Test
}

internal static class ApplicationRuntime
{
    private const string TestArgument = "--test";
    private const string NormalUserDataDirectoryName = "Hyper Battery Monitor";
#if DEBUG
    private const string TestUserDataDirectoryName = "Hyper Battery Monitor Test";
    private static ApplicationMode _mode = ApplicationMode.Normal;
    internal static bool IsTestMode => _mode == ApplicationMode.Test;
#else
    internal static bool IsTestMode => false;
#endif

    internal static bool Configure(string[] arguments)
    {
        bool requested = arguments.Any(arg =>
            string.Equals(arg, TestArgument, StringComparison.OrdinalIgnoreCase));
#if DEBUG
        _mode = requested ? ApplicationMode.Test : ApplicationMode.Normal;
        return true;
#else
        // Never silently treat --test as a production run: the caller must stop.
        return !requested;
#endif
    }

    internal static string UserDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#if DEBUG
        IsTestMode ? TestUserDataDirectoryName : NormalUserDataDirectoryName);
#else
        NormalUserDataDirectoryName);
#endif

    internal static string MutexName => IsTestMode
        ? "HyperBatteryMonitor_Test_8D7E0A6C9D5B4F0B9C3E5F5E9C2A1B71"
        : "HyperBatteryMonitor_8D7E0A6C9D5B4F0B9C3E5F5E9C2A1B71";

    internal static string TestIndicator => IsTestMode ? "[TEST MODE] " : string.Empty;
}
