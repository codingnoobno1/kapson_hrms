namespace Vispl.HRManagement.Mobile.Shared.Services;

/// <summary>
/// The last line of defence: an error that escapes a tap or page handler shows a short message
/// instead of closing the app. Screens still handle their own expected failures.
/// </summary>
public static class CrashGuard
{
    private static DateTime lastShownUtc;

    public static void Install()
    {
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine(e.Exception);
            e.SetObserved();
        };

#if ANDROID
        // Exceptions from async event handlers surface here on Android; Handled = true keeps
        // the process alive instead of crashing to the home screen.
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            e.Handled = true;
            Report(e.Exception);
        };
#endif
    }

    public static void Report(Exception? exception)
    {
        System.Diagnostics.Debug.WriteLine(exception);

        if (DateTime.UtcNow - lastShownUtc < TimeSpan.FromSeconds(4))
        {
            return;
        }

        lastShownUtc = DateTime.UtcNow;

        var message = exception is Mobile.Client.Http.MobileApiException api
            ? api.Message
            : "Something went wrong on this screen. Please try again.";

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page is Shell shell && shell.CurrentPage is { } current)
                {
                    page = current;
                }

                if (page is not null)
                {
                    await page.DisplayAlert("Sorry", message, "OK");
                }
            }
            catch
            {
            }
        });
    }
}
