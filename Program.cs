using System.Diagnostics;
using HyperXBatteryTray.Settings;

namespace HyperXBatteryTray;

internal static class Program
{
    private const string ResetUserDataArgument = "--reset-user-data";
    private const string MigrateUserDataArgument = "--migrate-user-data";

    [STAThread]
    static int Main(string[] args)
    {
        if (!ApplicationRuntime.Configure(args))
        {
            Debug.WriteLine("--test is supported only in DEBUG builds.");
            return 2;
        }

        if (ApplicationRuntime.IsTestMode &&
            (HasArgument(args, ResetUserDataArgument) ||
             HasArgument(args, MigrateUserDataArgument)))
        {
            // Installer maintenance commands must never operate in test mode.
            return 2;
        }

        SettingsManager settingsManager = new();

        if (HasArgument(args, ResetUserDataArgument))
        {
            try
            {
                settingsManager.DeleteUserData();
                new StartupManager().MigrateLegacyRegistration();
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Could not delete Hyper Battery Monitor user data: {ex.Message}");
                return 1;
            }
        }

        if (HasArgument(args, MigrateUserDataArgument))
        {
            try
            {
                settingsManager.MigrateLegacyUserData();
                new StartupManager().MigrateLegacyRegistration();
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Could not migrate Hyper Battery Monitor user data: {ex.Message}");
                return 1;
            }
        }

        try
        {
            settingsManager.MigrateLegacyUserData();
            new StartupManager().MigrateLegacyRegistration();
        }
        catch (Exception ex)
        {
            // The installer invokes the explicit migration command and stops cleanup on
            // failure. A direct/MSIX launch keeps the legacy data untouched and allows
            // the application to continue rather than deleting the only existing copy.
            Debug.WriteLine(
                $"Could not migrate legacy Hyper Battery Monitor state: {ex.Message}");
        }

        using Mutex applicationMutex = new(
            initiallyOwned: false,
            ApplicationRuntime.MutexName);

        ApplicationConfiguration.Initialize();
        Application.Run(
            new HyperXBatteryMonitorApplicationContext());

        return 0;
    }

    private static bool HasArgument(
        IEnumerable<string> args,
        string expectedArgument)
    {
        return args.Any(arg =>
            string.Equals(
                arg,
                expectedArgument,
                StringComparison.OrdinalIgnoreCase));
    }
}
