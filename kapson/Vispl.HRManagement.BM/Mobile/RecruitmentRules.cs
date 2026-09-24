namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Rules that more than one recruitment projection has to agree on. They live here rather than
/// in each service because a count that disagrees between the home screen and the recruitment
/// dashboard reads as a bug in the data, not in the screen.
/// </summary>
internal static class RecruitmentRules
{
    /// <summary>
    /// Expiry is by the calendar, so an opening past its close date is not active even if
    /// nobody has changed its status. An opening with no explicit status falls back to whether
    /// it was published.
    /// </summary>
    public static bool IsOpeningActive(string status, bool isPublished, DateOnly? expiresOn, DateOnly today)
    {
        if (expiresOn is { } expires && expires < today)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(status)
            ? isPublished
            : string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
    }
}
