using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.BM.ITF.Mobile;

public interface IRecruitmentDashboardService
{
    Task<RecruitmentDashboardDto> GetAsync(MobileCallerContext caller, CancellationToken ct);
}

public interface IRequisitionService
{
    Task<CursorPage<RequisitionListItemDto>> ListAsync(
        MobileCallerContext caller,
        RequisitionListQuery query,
        CancellationToken ct);

    Task<RequisitionDetailDto> GetAsync(MobileCallerContext caller, long requisitionId, CancellationToken ct);

    Task<MutationResultDto> SaveDraftAsync(
        MobileCallerContext caller,
        long? requisitionId,
        SaveRequisitionRequest request,
        CancellationToken ct);

    Task<MutationResultDto> SubmitAsync(MobileCallerContext caller, long requisitionId, CancellationToken ct);

    Task<MutationResultDto> CancelAsync(
        MobileCallerContext caller,
        long requisitionId,
        string reasonCode,
        string? comment,
        CancellationToken ct);

    Task<IReadOnlyList<WorkflowStepDto>> GetApprovalTrailAsync(
        MobileCallerContext caller,
        long requisitionId,
        CancellationToken ct);
}

public interface IMobileJobService
{
    Task<CursorPage<JobListItemDto>> ListAsync(
        MobileCallerContext caller,
        JobListQuery query,
        CancellationToken ct);

    Task<JobDetailDto> GetAsync(MobileCallerContext caller, long jobId, CancellationToken ct);

    Task<MutationResultDto> SaveDraftAsync(
        MobileCallerContext caller,
        long? jobId,
        SaveJobRequest request,
        CancellationToken ct);

    Task<MutationResultDto> SaveCompensationAsync(
        MobileCallerContext caller,
        long jobId,
        SaveJobCompensationRequest request,
        CancellationToken ct);

    Task<IReadOnlyList<ApiWarningDto>> ValidateAsync(
        MobileCallerContext caller,
        long jobId,
        CancellationToken ct);

    Task<MutationResultDto> PublishAsync(
        MobileCallerContext caller,
        long jobId,
        PublishJobRequest request,
        CancellationToken ct);

    Task<MutationResultDto> ChangeStatusAsync(
        MobileCallerContext caller,
        long jobId,
        ChangeJobStatusRequest request,
        CancellationToken ct);

    Task<MutationResultDto> DuplicateAsync(MobileCallerContext caller, long jobId, CancellationToken ct);

    Task<JobAnalyticsDto> GetAnalyticsAsync(
        MobileCallerContext caller,
        long jobId,
        JobAnalyticsQuery query,
        CancellationToken ct);
}

public interface IMobileApplicationService
{
    Task<CursorPage<ApplicationListItemDto>> ListAsync(
        MobileCallerContext caller,
        ApplicationListQuery query,
        CancellationToken ct);

    Task<ApplicantPipelineDto> GetPipelineAsync(
        MobileCallerContext caller,
        long jobId,
        CancellationToken ct);

    Task<ApplicationDetailDto> GetAsync(
        MobileCallerContext caller,
        string applicationId,
        CancellationToken ct);

    Task<MutationResultDto> ChangeStageAsync(
        MobileCallerContext caller,
        string applicationId,
        ChangeApplicationStageRequest request,
        CancellationToken ct);

    Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller,
        string applicationId,
        ApplicationDecisionRequest request,
        CancellationToken ct);

    Task<MutationResultDto> RateAsync(
        MobileCallerContext caller,
        string applicationId,
        RateApplicationRequest request,
        CancellationToken ct);

    Task<ScreeningScorecardDto> GetScreeningAsync(
        MobileCallerContext caller,
        string applicationId,
        CancellationToken ct);

    Task<MutationResultDto> SaveScreeningAsync(
        MobileCallerContext caller,
        string applicationId,
        SaveScreeningRequest request,
        CancellationToken ct);
}

public interface ICandidateService
{
    Task<CandidateProfileDto> GetProfileAsync(
        MobileCallerContext caller,
        string candidateId,
        CancellationToken ct);

    Task<ProtectedFileTicketDto> GetResumeTicketAsync(
        MobileCallerContext caller,
        string candidateId,
        CancellationToken ct);
}

public interface ICandidateDocumentService
{
    Task<IReadOnlyList<CandidateDocumentDto>> ListAsync(
        MobileCallerContext caller,
        string applicationId,
        CancellationToken ct);

    Task<UploadIntentDto> CreateUploadIntentAsync(
        MobileCallerContext caller,
        string applicationId,
        RequestDocumentRequest request,
        CancellationToken ct);

    Task<ProtectedFileTicketDto> GetTicketAsync(
        MobileCallerContext caller,
        string documentId,
        CancellationToken ct);

    Task<MutationResultDto> VerifyAsync(
        MobileCallerContext caller,
        string documentId,
        VerifyDocumentRequest request,
        CancellationToken ct);

    Task<MutationResultDto> RequestFromCandidateAsync(
        MobileCallerContext caller,
        string applicationId,
        RequestDocumentRequest request,
        CancellationToken ct);
}

public interface IInterviewService
{
    Task<CursorPage<InterviewListItemDto>> ListAsync(
        MobileCallerContext caller,
        InterviewListQuery query,
        CancellationToken ct);

    Task<InterviewDetailDto> GetAsync(MobileCallerContext caller, string interviewId, CancellationToken ct);

    Task<InterviewAvailabilityDto> GetAvailabilityAsync(
        MobileCallerContext caller,
        InterviewAvailabilityQuery query,
        CancellationToken ct);

    Task<MutationResultDto> ScheduleAsync(
        MobileCallerContext caller,
        ScheduleInterviewRequest request,
        CancellationToken ct);

    Task<MutationResultDto> RescheduleAsync(
        MobileCallerContext caller,
        string interviewId,
        RescheduleInterviewRequest request,
        CancellationToken ct);

    Task<MutationResultDto> CancelAsync(
        MobileCallerContext caller,
        string interviewId,
        CancelInterviewRequest request,
        CancellationToken ct);

    Task<InterviewEvaluationDto> GetEvaluationAsync(
        MobileCallerContext caller,
        string interviewId,
        CancellationToken ct);

    Task<MutationResultDto> SaveEvaluationAsync(
        MobileCallerContext caller,
        string interviewId,
        SaveInterviewEvaluationRequest request,
        CancellationToken ct);
}

public interface IOfferService
{
    Task<CursorPage<OfferListItemDto>> ListAsync(
        MobileCallerContext caller,
        OfferListQuery query,
        CancellationToken ct);

    Task<OfferDetailDto> GetAsync(MobileCallerContext caller, string offerId, CancellationToken ct);

    Task<MutationResultDto> GenerateAsync(
        MobileCallerContext caller,
        GenerateOfferRequest request,
        CancellationToken ct);

    Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller,
        string offerId,
        OfferDecisionRequest request,
        CancellationToken ct);

    Task<MutationResultDto> SendAsync(
        MobileCallerContext caller,
        string offerId,
        SendOfferRequest request,
        CancellationToken ct);

    Task<ProtectedFileTicketDto> GetDocumentTicketAsync(
        MobileCallerContext caller,
        string offerId,
        CancellationToken ct);
}
