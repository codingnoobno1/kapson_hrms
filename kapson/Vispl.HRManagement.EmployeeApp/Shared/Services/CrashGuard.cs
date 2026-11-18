namespace Vispl.HRManagement.EmployeeApp.Shared.Services;

/// <summary>
/// The last line of defence: an error that escapes a tap or page handler shows a short message
/// instead of closing the app. Screens still handle their own expected failures; this only
/// catches what they missed. The most recent error is kept (type and message, no data) so it
/// can be read back from Menu when reporting a problem.
/// </summary>
public static class CrashGuard
{
    private const string LastErrorKey = "kapsons.employee.last_error";
    private static DateTime lastShownUtc;

    public static string? LastError
    {
        get
        {
            try { return Preferences.Default.Get<string?>(LastErrorKey, null); }
            catch { return null; }
        }
    }

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Remember(e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Remember(e.Exception);
            e.SetObserved();
        };

#if ANDROID
        // Exceptions from async event handlers surface here on Android. Handled = true keeps
        // the process alive; without it every unexpected error is a crash to the home screen.
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            e.Handled = true;
            Report(e.Exception);
        };
#endif
    }

    /// <summary>Records the error and tells the person, at most once every few seconds.</summary>
    public static void Report(Exception? exception)
    {
        Remember(exception);

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
                // Showing the message must never be the thing that fails.
            }
        });
    }

    private static void Remember(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine(exception);

        try
        {
            var where = exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
            Preferences.Default.Set(LastErrorKey,
                $"{DateTime.Now:dd MMM HH:mm} {exception.GetType().Name}: {exception.Message} {where}");
        }
        catch
        {
        }
    }
}
