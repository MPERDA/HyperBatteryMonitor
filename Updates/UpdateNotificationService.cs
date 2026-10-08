using HyperXBatteryTray.Settings;
using Microsoft.Toolkit.Uwp.Notifications;

namespace HyperXBatteryTray.Updates;

/// <summary>Informational update toasts; installation requires explicit About confirmation.</summary>
internal static class UpdateNotificationService
{
    internal static void Available(string version, AppLanguage language) => Show(
        "UpdatesNoticeAvailableTitle", "UpdatesNoticeAvailableBody", version, language);

    internal static void Downloaded(string version, AppLanguage language) => Show(
        "UpdatesNoticeDownloadedTitle", "UpdatesNoticeDownloadedBody", version, language);

    internal static void Reminder(string version, AppLanguage language) => Show(
        "UpdatesNoticeReminderTitle", "UpdatesNoticeReminderBody", version, language);

    internal static void InstallationFailed(string version, AppLanguage language) => Show(
        "UpdatesNoticeInstallFailedTitle", "UpdatesNoticeInstallFailedBody", version, language);

    private static void Show(string titleKey, string bodyKey, string version, AppLanguage language)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(ApplicationRuntime.TestIndicator + Localization.Get(titleKey, language))
                .AddText(string.Format(Localization.Get(bodyKey, language), version))
                .Show();
        }
        catch
        {
            // Notifications are best-effort: About always retains the current status.
        }
    }
}
