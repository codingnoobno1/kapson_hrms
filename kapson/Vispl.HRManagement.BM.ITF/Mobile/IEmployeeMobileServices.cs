using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.BM.ITF.Mobile;

public interface IMobileEmployeeService
{
    Task<CursorPage<EmployeeListItemDto>> ListAsync(
        MobileCallerContext caller,
        EmployeeListQuery query,
        CancellationToken ct);

    Task<EmployeeOverviewDto> GetOverviewAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<EmployeePersonalDto> GetPersonalAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<MutationResultDto> UpdatePersonalAsync(
        MobileCallerContext caller,
        string employeeId,
        UpdateEmployeePersonalRequest request,
        CancellationToken ct);

    Task<EmployeeEmploymentDto> GetEmploymentAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<MutationResultDto> UpdateEmploymentAsync(
        MobileCallerContext caller,
        string employeeId,
        UpdateEmployeeEmploymentRequest request,
        CancellationToken ct);

    Task<EmployeeOrganisationDto> GetOrganisationAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<MutationResultDto> UpdateOrganisationAsync(
        MobileCallerContext caller,
        string employeeId,
        UpdateEmployeeOrganisationRequest request,
        CancellationToken ct);

    Task<CompensationSnapshotDto> GetCompensationAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<IReadOnlyList<EmployeeContactDto>> ListContactsAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<MutationResultDto> SaveContactAsync(
        MobileCallerContext caller,
        string employeeId,
        SaveEmployeeContactRequest request,
        CancellationToken ct);

    Task RemoveContactAsync(
        MobileCallerContext caller,
        string employeeId,
        string contactId,
        CancellationToken ct);

    Task<IReadOnlyList<EmployeeDependantDto>> ListDependantsAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<CursorPage<EmployeeTimelineEntryDto>> GetTimelineAsync(
        MobileCallerContext caller,
        string employeeId,
        EmployeeTimelineQuery query,
        CancellationToken ct);

    Task<CreateEmployeeResultDto> CreateAsync(
        MobileCallerContext caller,
        CreateEmployeeRequest request,
        CancellationToken ct);

    Task<IReadOnlyList<EmployeeListItemDto>> CheckDuplicateAsync(
        MobileCallerContext caller,
        CreateEmployeeRequest request,
        CancellationToken ct);

    Task<MutationResultDto> CreateChangeRequestAsync(
        MobileCallerContext caller,
        string employeeId,
        CreateEmployeeChangeRequest request,
        CancellationToken ct);

    Task<EmployeeChangeRequestDto> GetChangeRequestAsync(
        MobileCallerContext caller,
        string changeRequestId,
        CancellationToken ct);

    /// <summary>The HR decision queue — every request, newest first, or one status.</summary>
    Task<IReadOnlyList<EmployeeChangeRequestListItemDto>> ListChangeRequestsAsync(
        MobileCallerContext caller,
        EmployeeChangeRequestQuery query,
        CancellationToken ct);

    /// <summary>
    /// Approve applies the requested fields through the same write path HR uses directly
    /// (UpdatePersonalAsync, or the bank account service for a BankAccount request); reject just
    /// records why. Either way the employee is notified.
    /// </summary>
    Task<MutationResultDto> DecideChangeRequestAsync(
        MobileCallerContext caller,
        string changeRequestId,
        DecideEmployeeChangeRequest request,
        CancellationToken ct);
}

public interface IEmployeeDocumentService
{
    Task<IReadOnlyList<EmployeeDocumentDto>> ListAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<UploadIntentDto> CreateUploadIntentAsync(
        MobileCallerContext caller,
        string employeeId,
        DocumentUploadIntentRequest request,
        CancellationToken ct);

    Task<MutationResultDto> ConfirmUploadAsync(
        MobileCallerContext caller,
        string employeeId,
        ConfirmDocumentUploadRequest request,
        CancellationToken ct);

    Task<ProtectedFileTicketDto> GetTicketAsync(
        MobileCallerContext caller,
        string employeeId,
        string documentId,
        CancellationToken ct);

    Task<MutationResultDto> VerifyAsync(
        MobileCallerContext caller,
        string employeeId,
        string documentId,
        VerifyEmployeeDocumentRequest request,
        CancellationToken ct);

    Task RemoveAsync(
        MobileCallerContext caller,
        string employeeId,
        string documentId,
        CancellationToken ct);
}

public interface IMobileOnboardingService
{
    Task<OnboardingDashboardDto> GetDashboardAsync(
        MobileCallerContext caller,
        OnboardingQuery query,
        CancellationToken ct);

    Task<OnboardingSummaryDto> GetChecklistAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<OnboardingTaskDetailDto> GetTaskAsync(
        MobileCallerContext caller,
        string taskId,
        CancellationToken ct);

    Task<MutationResultDto> CompleteTaskAsync(
        MobileCallerContext caller,
        string taskId,
        CompleteOnboardingTaskRequest request,
        CancellationToken ct);

    Task<MutationResultDto> ReassignTaskAsync(
        MobileCallerContext caller,
        string taskId,
        ReassignOnboardingTaskRequest request,
        CancellationToken ct);

    Task<MutationResultDto> CommentTaskAsync(
        MobileCallerContext caller,
        string taskId,
        AddTaskCommentRequest request,
        CancellationToken ct);

    Task<IReadOnlyList<EmployeeDocumentDto>> GetVerificationQueueAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);
}

public interface IProbationService
{
    Task<ProbationDetailDto> GetAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<MutationResultDto> SaveReviewAsync(
        MobileCallerContext caller,
        string employeeId,
        SaveProbationReviewRequest request,
        CancellationToken ct);

    Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller,
        string employeeId,
        ProbationDecisionRequest request,
        CancellationToken ct);
}

public interface IOffboardingService
{
    Task<OffboardingDetailDto> GetAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<MutationResultDto> InitiateAsync(
        MobileCallerContext caller,
        string employeeId,
        InitiateOffboardingRequest request,
        CancellationToken ct);

    Task<MutationResultDto> UpdateAsync(
        MobileCallerContext caller,
        string employeeId,
        UpdateOffboardingRequest request,
        CancellationToken ct);

    Task<IReadOnlyList<ClearanceTaskDto>> GetClearanceAsync(
        MobileCallerContext caller,
        string employeeId,
        CancellationToken ct);

    Task<MutationResultDto> UpdateClearanceTaskAsync(
        MobileCallerContext caller,
        string employeeId,
        string taskId,
        UpdateClearanceTaskRequest request,
        CancellationToken ct);

    Task<MutationResultDto> CloseAsync(
        MobileCallerContext caller,
        string employeeId,
        CloseOffboardingRequest request,
        CancellationToken ct);
}
