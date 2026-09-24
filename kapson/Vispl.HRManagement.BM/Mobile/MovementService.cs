using System.Globalization;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Workforce;
using Vispl.HRManagement.BM.Shared;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Who is off site during working hours.
///
/// A declaration is made when someone leaves, not reconstructed afterwards — the difference
/// between personal and official time out has a payroll consequence, and letting it be decided
/// later means deciding it once the consequence is already known.
/// </summary>
public sealed class MovementService(
    IMovementRepository movements,
    IEmployeeRepository employees,
    ITF.Shared.INotificationPublisher notifications) : IMovementService
{
    private const string TeamPermission = "MOVEMENT.TEAM";

    /// <summary>Nobody is out for a fortnight on a lunchtime errand.</summary>
    private static readonly TimeSpan MaximumExpectedAbsence = TimeSpan.FromHours(12);

    public async Task<MovementBoardDto> GetBoardAsync(
        MobileCallerContext caller, MovementQuery query, CancellationToken ct)
    {
        Require(caller, TeamPermission);

        var date = query.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var staff = await LoadStaffAsync(caller, ct);

        var rows = await movements.QueryMovementsAsync(
            caller.TenantKey,
            new MovementFilter(date, date, staff.Keys.ToList(), query.Kind, null, query.OverdueOnly),
            ct);

        var now = DateTimeOffset.UtcNow;
        var mapped = rows
            .Where(row => staff.ContainsKey(row.EmployeeId))
            .Select(row => Map(row, staff[row.EmployeeId], now))
            .ToList();

        return new MovementBoardDto(
            mapped.Where(row => row.Kind == "Personal").ToList(),
            mapped.Where(row => row.Kind == "Official").ToList(),
            mapped.Count(row => row.IsOverdue),
            now);
    }

    public async Task<IReadOnlyList<MovementRowDto>> GetMineAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await movements.QueryMovementsAsync(
            caller.TenantKey,
            new MovementFilter(today.AddDays(-30), today, [employee.Id], null, null, false),
            ct);

        var now = DateTimeOffset.UtcNow;
        return rows.Select(row => Map(row, employee, now)).ToList();
    }

    public async Task<MutationResultDto> DeclareAsync(
        MobileCallerContext caller, DeclareMovementRequest request, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var now = DateTimeOffset.UtcNow;
        var goingOut = request.GoingOutUtc ?? now;

        // A declaration is about leaving now or having just left. Booking a future absence is
        // what a gate pass is for.
        if (goingOut > now.AddMinutes(5))
        {
            throw new MobileValidationException(
                "FUTURE_DEPARTURE",
                "Going out cannot be declared in advance. Request a gate pass instead.");
        }

        if (request.ExpectedReturnUtc <= goingOut)
        {
            throw new MobileValidationException(
                "RETURN_BEFORE_DEPARTURE", "The expected return is not after the departure.");
        }

        if (request.ExpectedReturnUtc - goingOut > MaximumExpectedAbsence)
        {
            throw new MobileValidationException(
                "ABSENCE_TOO_LONG",
                $"An expected absence over {MaximumExpectedAbsence.TotalHours:0} hours needs a gate pass or leave.");
        }

        // One open movement at a time. Two would both claim to be current, and neither could be
        // closed without guessing which.
        var open = await movements.GetOpenMovementAsync(caller.TenantKey, employee.Id, ct);
        if (open is not null)
        {
            throw new MobileValidationException(
                "ALREADY_OUT",
                $"You are already recorded as out since {open.GoingOutUtc:HH\\:mm}. Record a return first.");
        }

        var id = await movements.AddMovementAsync(
            new MovementRecord(
                0, caller.TenantKey, employee.Id, request.Kind,
                DateOnly.FromDateTime(goingOut.UtcDateTime), goingOut, request.ExpectedReturnUtc,
                null, request.Detail.Trim(), request.DestinationLabel?.Trim(), "Open",
                caller.UserId, now, now),
            ct);

        return new MutationResultDto(
            id.ToString(CultureInfo.InvariantCulture), "Open", null, now,
            $"Expected back by {request.ExpectedReturnUtc:HH\\:mm}.");
    }

    public async Task<MutationResultDto> RecordReturnAsync(
        MobileCallerContext caller, string movementId, ReturnMovementRequest request, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);

        if (!long.TryParse(movementId, out var id))
        {
            throw new MobileNotFoundException($"Movement {movementId} was not found.");
        }

        var movement = await movements.GetMovementAsync(caller.TenantKey, id, ct)
                       ?? throw new MobileNotFoundException($"Movement {movementId} was not found.");

        // Someone else's movement is closed by them, or by an HR user with the manage right.
        if (movement.EmployeeId != employee.Id && !caller.Has("MOVEMENT.MANAGE"))
        {
            throw new MobileValidationException(
                "NOT_YOURS", "Only the person who went out can record their return.");
        }

        if (movement.ActualReturnUtc is not null || movement.State != "Open")
        {
            throw new MobileValidationException(
                "ALREADY_CLOSED", "This movement has already been closed.");
        }

        var now = DateTimeOffset.UtcNow;
        var returned = request.ActualReturnUtc ?? now;

        if (returned < movement.GoingOutUtc)
        {
            throw new MobileValidationException(
                "RETURN_BEFORE_DEPARTURE", "The return is before the departure.");
        }

        if (returned > now.AddMinutes(5))
        {
            throw new MobileValidationException(
                "FUTURE_RETURN", "A return cannot be recorded in the future.");
        }

        await movements.UpdateMovementAsync(
            movement with { ActualReturnUtc = returned, State = "Returned", UpdatedUtc = now }, ct);

        var minutes = (int)(returned - movement.GoingOutUtc).TotalMinutes;
        var late = returned > movement.ExpectedReturnUtc;

        // Only a late return is notified. Going out and coming back on time is the ordinary
        // shape of a working day, and a notification for every one of them would train people
        // to ignore the whole category — including the overdue notices that matter.
        if (late)
        {
            var lateBy = (int)(returned - movement.ExpectedReturnUtc).TotalMinutes;
            var person = await employees.DescribeAsync(caller.TenantKey, movement.EmployeeId, ct);

            await notifications.PublishToPermissionAsync(
                new ITF.Shared.NotificationRequest(
                    caller.TenantKey,
                    MovementNotifications.Category,
                    "Returned late",
                    $"{person.DisplayName} returned {lateBy} minutes after the expected " +
                    $"{movement.ExpectedReturnUtc:HH\\:mm}. {movement.Detail}",
                    "/movement/me",
                    "Normal",
                    MovementNotifications.Subject,
                    $"{movement.Id}:late"),
                TeamPermission,
                ct);
        }

        return new MutationResultDto(
            movementId, "Returned", null, now,
            late
                ? $"Back after {minutes} minutes, {(int)(returned - movement.ExpectedReturnUtc).TotalMinutes} later than expected."
                : $"Back after {minutes} minutes.");
    }

    // ---- Helpers ----------------------------------------------------------

    private static MovementRowDto Map(MovementRecord row, EmployeeRecord person, DateTimeOffset now) => new(
        row.Id.ToString(CultureInfo.InvariantCulture),
        row.EmployeeId.ToString(CultureInfo.InvariantCulture),
        DisplayName(person),
        person.DepartmentCode,
        person.JobTitle,
        row.Kind,
        row.WorkDate,
        row.GoingOutUtc,
        row.ExpectedReturnUtc,
        row.ActualReturnUtc,
        row.Detail,
        row.DestinationLabel,
        row.State,
        row.IsOverdue(now),
        row.MinutesOut(now));

    private static string DisplayName(EmployeeRecord person) =>
        string.IsNullOrWhiteSpace(person.PreferredName)
            ? $"{person.LegalFirstName} {person.LegalLastName}".Trim()
            : person.PreferredName;

    private async Task<Dictionary<long, EmployeeRecord>> LoadStaffAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var staff = await employees.QueryAsync(
            caller.TenantKey,
            new EmployeeQueryFilter(null, null, null, null, null, ActiveOnly: true),
            ct);

        return staff.ToDictionary(person => person.Id);
    }

    private async Task<EmployeeRecord> RequireOwnEmployeeAsync(
        MobileCallerContext caller, CancellationToken ct) =>
        await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct)
        ?? throw new MobileValidationException(
            "NO_EMPLOYEE_RECORD",
            "This account is not linked to an employee record, so it cannot declare a movement.");

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }
}
