namespace Vispl.HRManagement.Mobile.Shared.Motion;

/// <summary>
/// The one live read of "should anything animate right now". A static rather than DI because the
/// controls that need it — KpiTile, JobCard, ApplicantCard, InterviewCard — are constructed by
/// the XAML parser, not the container, so they have no way to ask for a service.
/// </summary>
public static class MotionSettings
{
    /// <summary>Set once from <c>MauiProgram</c>/<c>App</c> at startup.</summary>
    public static IReducedMotionService? Service { get; set; }

    /// <summary>Read fresh every time, not cached — the OS setting can change while the app is open.</summary>
    public static bool ReducedMotionEnabled => Service?.IsReducedMotionEnabled ?? false;
}
