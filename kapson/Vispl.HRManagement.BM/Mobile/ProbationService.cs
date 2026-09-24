using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.DL.ITF.Employee;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Where an employee stands in their probation period, derived from the dates already on their
/// record.
///
/// Reviews and the confirm/extend decision are reported as unavailable rather than accepted,
/// because there is nowhere to record them yet: an accepted decision that left no trace would
/// be worse than one that was refused.
/// </summary>
public sealed class ProbationService(IEmployeeRepository employees) : IProbationService
{
    private const string ViewPermission = "EMPLOYEES.VIEW";

    public async Task<ProbationDetailDto> GetAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        if (!long.TryParse(employeeId, out var id))
        {
            throw new MobileNotFoundException("That employee no longer exists.");
        }

        var person = await employees.GetAsync(caller.TenantKey, id, ct)
            ?? throw new MobileNotFoundException("That employee no longer exists.");

        // The route addresses an employee by row id while the token carries an employee number,
        // so "is this me" is settled on the loaded record rather than by comparing the two
        // identifiers, which are never equal.
        var isSelf = !string.IsNullOrWhiteSpace(caller.EmployeeId) &&
                     string.Equals(person.EmployeeNumber, caller.EmployeeId, StringComparison.OrdinalIgnoreCase);

        if (!isSelf && !caller.Has(ViewPermission))
        {
            throw new MobileAccessDeniedException(ViewPermission);
        }

        var endsOn = person.ProbationEndsOn
            ?? throw new MobileNotFoundException("This employee is not on probation.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isOver = endsOn < today;

        return new ProbationDetailDto(
            person.Id.ToString(),
            person.JoinedOn,
            endsOn,
            // Goals belong to a review record that does not exist yet. An invented list would
            // read as something HR had actually set.
            Goals: [],
            Recommendation: null,
            Actions:
            [
                new ActionAvailabilityDto("review", IsAllowed: false,
                    "Probation reviews are not available in the app yet."),
                new ActionAvailabilityDto("decide", IsAllowed: false,
                    isOver
                        ? "Probation decisions are not available in the app yet."
                        : "Probation has not finished yet.")
            ],
            RowVersion: null);
    }

    public Task<MutationResultDto> SaveReviewAsync(
        MobileCallerContext caller,
        string employeeId,
        SaveProbationReviewRequest request,
        CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Probation Reviews", "probation review store", "goal and rating schema");

    public Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller,
        string employeeId,
        ProbationDecisionRequest request,
        CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Probation Decisions", "probation review store", "confirmation workflow");
}
