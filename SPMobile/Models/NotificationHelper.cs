namespace SPMobile.Models;

public static class NotificationHelper
{
    public static void SetBadge(int count)
    {
        var tab = Shell.Current.Items
            .SelectMany(i => i.Items)
            .FirstOrDefault(i => i.Title.Contains("Notification"));

        if (tab != null)
        {
            // Must run on MainThread to update UI
            MainThread.BeginInvokeOnMainThread(() =>
            {
                tab.Title = count > 0 ? $"Notifications ({count})" : "Notifications";
            });
        }
    }

    public static void ClearBadge()
    {
        var tab = Shell.Current.Items
            .SelectMany(i => i.Items)
            .FirstOrDefault(i => i.Title.Contains("Notification"));

        if (tab != null)
        {
            MainThread.BeginInvokeOnMainThread(() => tab.Title = "Notifications");
        }
    }
}