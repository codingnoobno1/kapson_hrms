using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Operations;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.DL.ITF.Employee;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// The caller's own record, read from the employee row their token resolves to.
///
/// Editing is reported as unavailable rather than merely hidden: the app decides what to offer
/// from <see cref="FieldAccessDto"/>, so a field that silently rejected a write would look like
/// a bug in the app. Whether self-service edits are permitted at all is HR's switch, which is
/// why it is read here rather than assumed.
/// </summary>
public sealed class MobileProfileService(
    IEmployeeRepository employees,
    ITenantService tenants,
    IEmployeeAppSettingsService appSettings) : IMobileProfileService
{
    public async Task<OwnProfileDto> GetAsync(MobileCallerContext caller, CancellationToken ct)
    {
        // The token carries the employee *number*, not the row id: MobileTokenService emits it
        // as the "employee_number" claim. An HR portal account has none, so there is no "own
        // record" to show and saying so beats returning a hollow profile.
        if (string.IsNullOrWhiteSpace(caller.EmployeeId))
        {
            throw new MobileNotFoundException("This account is not linked to an employee record.");
        }

        var person = await employees.GetByNumberAsync(caller.TenantKey, caller.EmployeeId, ct)
            ?? throw new MobileNotFoundException("Your employee record no longer exists.");

        var tenant = await tenants.GetByKeyAsync(caller.TenantKey, ct);
        var settings = await appSettings.GetAsync(caller.TenantKey, ct);

        var canRequestChanges = settings.AllowProfileChangeRequests;
        var offReason = canRequestChanges ? null : "Your HR team has turned off profile changes.";

        return new OwnProfileDto(
            new PersonReferenceDto(
                person.Id.ToString(),
                DisplayName(person),
                person.AvatarUrl,
                person.EmployeeNumber),
            person.Email,
            person.Phone,
            Locale: "en-IN",
            TimeZoneId: tenant?.TimeZoneId ?? "UTC",
            ThemeMode: "light",
            FieldAccess:
            [
                // Identity and employment terms are HR's to change; contact details are the
                // only things an employee may ask to correct about themselves.
                new FieldAccessDto("displayName", CanView: true, CanEdit: false, IsMasked: false,
                    "Names are changed by HR."),
                new FieldAccessDto("employeeNumber", CanView: true, CanEdit: false, IsMasked: false,
                    "Issued by HR."),
                new FieldAccessDto("jobTitle", CanView: true, CanEdit: false, IsMasked: false,
                    "Set by HR."),
                new FieldAccessDto("email", CanView: true, CanEdit: canRequestChanges, IsMasked: false, offReason),
                new FieldAccessDto("phone", CanView: true, CanEdit: canRequestChanges, IsMasked: false, offReason)
            ],
            RowVersion: null);
    }

    public Task<MutationResultDto> UpdateAsync(
        MobileCallerContext caller, UpdateOwnProfileRequest request, CancellationToken ct) =>
        // A change to one's own record is a request HR approves, not a direct write. The
        // change-request pipeline exists in the portal but has no mobile-side store yet, and
        // writing straight to the employee row would bypass that approval entirely.
        throw new MobileFeatureNotReadyException(
            "Profile Updates", "employee change-request store", "approval routing");

    public Task<AppPreferencesDto> GetPreferencesAsync(MobileCallerContext caller, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "App Preferences", "per-user preference store");

    public Task<AppPreferencesDto> UpdatePreferencesAsync(
        MobileCallerContext caller, UpdateAppPreferencesRequest request, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "App Preferences", "per-user preference store");

    public Task<NotificationPreferencesDto> GetNotificationPreferencesAsync(
        MobileCallerContext caller, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Notification Preferences", "per-user delivery preference store");

    public Task<NotificationPreferencesDto> UpdateNotificationPreferencesAsync(
        MobileCallerContext caller, UpdateNotificationPreferencesRequest request, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Notification Preferences", "per-user delivery preference store");

    public Task<UploadIntentDto> CreateAvatarIntentAsync(
        MobileCallerContext caller, AvatarUploadIntentRequest request, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Avatar Upload", "upload ticket issuer", "object storage adapter");

    private static string DisplayName(VO.Employee.EmployeeRecord person) =>
        string.IsNullOrWhiteSpace(person.PreferredName)
            ? $"{person.LegalFirstName} {person.LegalLastName}".Trim()
            : person.PreferredName;
}
