using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Recruitment;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.BM.Recruitment;

public sealed class OnboardingService(
    IApplicationRepository applications,
    IJobOpeningRepository openings,
    IMediaStorage storage,
    ITF.Employee.IEmployeeProvisioning employeeProvisioning) : IOnboardingService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---- Reads ------------------------------------------------------------

    public async Task<OnboardingDto?> GetAsync(
        string tenantKey, long onboardingId, CancellationToken cancellationToken = default)
    {
        var onboarding = await applications.GetOnboardingAsync(tenantKey, onboardingId, cancellationToken);
        return onboarding is null ? null : await ProjectAsync(tenantKey, onboarding, cancellationToken);
    }

    public async Task<OnboardingDto?> GetByApplicationAsync(
        string tenantKey, long applicationId, CancellationToken cancellationToken = default)
    {
        var onboarding = await applications.GetOnboardingByApplicationAsync(tenantKey, applicationId, cancellationToken);
        return onboarding is null ? null : await ProjectAsync(tenantKey, onboarding, cancellationToken);
    }

    public async Task<CandidateProgressDto> GetCandidateProgressAsync(
        string tenantKey, long candidateId, CancellationToken cancellationToken = default)
    {
        var mine = await applications.GetForCandidateAsync(tenantKey, candidateId, cancellationToken);

        var rows = mine
            .OrderByDescending(item => item.Application.AppliedUtc)
            .Select(item => new CandidateApplicationDto(
                item.Application.Id,

                // The same shape the apply response gave them. It is the only handle a
                // candidate has on their own application, so it must not read differently here.
                $"APP-{item.Application.Id:D6}",
                item.JobTitle,
                item.Department,
                item.Application.Stage,
                ApplicationStages.Tone(item.Application.Stage),
                Explain(item.Application.Stage),
                item.Application.AppliedUtc,
                string.Equals(item.Application.Stage, ApplicationStages.Offered, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // At most one checklist matters: the application that got as far as an offer being
        // accepted. If somehow there were two, the newest is the one they are working on.
        OnboardingDto? onboarding = null;

        foreach (var application in rows)
        {
            var found = await GetByApplicationAsync(tenantKey, application.ApplicationId, cancellationToken);
            if (found is not null)
            {
                onboarding = found;
                break;
            }
        }

        var candidate = await applications.GetCandidateByIdAsync(tenantKey, candidateId, cancellationToken);

        return new CandidateProgressDto(
            candidate?.FullName ?? "Candidate",
            candidate?.Email ?? string.Empty,
            rows,
            onboarding);
    }

    /// <summary>
    /// The stage in words the person it is about would use. "Interviewing" is our word for a
    /// column on a board; "we are arranging interviews" is what it means to them.
    /// </summary>
    private static string Explain(string stage) => stage switch
    {
        ApplicationStages.Applied => "We have your application and are reviewing it.",
        ApplicationStages.Screening => "Your application is being screened.",
        ApplicationStages.Shortlisted => "You have been shortlisted.",
        ApplicationStages.Interviewing => "We are arranging interviews.",
        ApplicationStages.Offered => "You have an offer waiting for your response.",
        ApplicationStages.OfferAccepted => "You accepted the offer. Your joining checklist is below.",
        ApplicationStages.OfferDeclined => "You declined this offer.",
        ApplicationStages.Onboarding => "Joining formalities are in progress.",
        ApplicationStages.Hired => "All done — welcome aboard.",
        ApplicationStages.Rejected => "We are not taking this application further.",
        ApplicationStages.Withdrawn => "You withdrew this application.",
        _ => stage
    };

    // ---- Offer response ---------------------------------------------------

    public async Task<long> AcceptOfferAsync(
        string tenantKey, long applicationId, long candidateId, DateOnly? joiningOn, CancellationToken cancellationToken = default)
    {
        var application = await LoadOwnedApplicationAsync(tenantKey, applicationId, candidateId, cancellationToken);

        // A mobile client can legitimately replay the request after losing the first response.
        // Return the checklist that was already created instead of rejecting the replay because
        // the first request has advanced the application to Onboarding.
        var existing = await applications.GetOnboardingByApplicationAsync(tenantKey, applicationId, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        if (!string.Equals(application.Stage, ApplicationStages.Offered, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException(
                "NO_OFFER_PENDING", "There is no offer awaiting a response on this application.");
        }

        var now = DateTimeOffset.UtcNow;

        await MoveAsync(application, ApplicationStages.OfferAccepted, null, candidateId.ToString(), "Candidate", now, cancellationToken);

        var onboardingId = await applications.UpsertOnboardingAsync(
            new OnboardingRecord(0, tenantKey, applicationId, candidateId, "InProgress", joiningOn, now, null, now),
            cancellationToken);

        var order = 0;
        await applications.AddOnboardingTasksAsync(
            OnboardingTemplate.Default
                .Select(template => new OnboardingTaskRecord(
                    0, tenantKey, onboardingId, template.Code, template.Title, template.Instructions,
                    template.TaskType, template.OwnerKind, template.IsRequired, order++,
                    OnboardingTaskStatuses.Pending, null, null, null, null, null, now))
                .ToList(),
            cancellationToken);

        // The application follows the checklist into Onboarding.
        var accepted = application with { Stage = ApplicationStages.OfferAccepted };
        await MoveAsync(accepted, ApplicationStages.Onboarding, null, null, "System", now, cancellationToken);

        return onboardingId;
    }

    public async Task DeclineOfferAsync(
        string tenantKey, long applicationId, long candidateId, string? reason, CancellationToken cancellationToken = default)
    {
        var application = await LoadOwnedApplicationAsync(tenantKey, applicationId, candidateId, cancellationToken);

        if (!string.Equals(application.Stage, ApplicationStages.Offered, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException(
                "NO_OFFER_PENDING", "There is no offer awaiting a response on this application.");
        }

        await MoveAsync(
            application, ApplicationStages.OfferDeclined, reason,
            candidateId.ToString(), "Candidate", DateTimeOffset.UtcNow, cancellationToken);
    }

    // ---- Task submission --------------------------------------------------

    public async Task SubmitDocumentTaskAsync(
        string tenantKey, long onboardingId, long taskId, long candidateId, UploadedFile file, CancellationToken cancellationToken = default)
    {
        var (onboarding, task) = await LoadOwnedTaskAsync(tenantKey, onboardingId, taskId, candidateId, cancellationToken);

        if (!string.Equals(task.TaskType, OnboardingTemplate.Document, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException("WRONG_TASK_TYPE", "This task does not take a file.");
        }

        try
        {
            await ApplicationFileValidator.ValidateAsync(file, cancellationToken);
        }
        catch (ApplicationRejected rejected)
        {
            throw new ApplicationRejectedException(rejected.Code, rejected.Message);
        }

        var now = DateTimeOffset.UtcNow;

        // Replacing an earlier upload removes the previous blob rather than orphaning it.
        var replaced = await applications.RemoveDocumentsAsync(
            tenantKey, onboarding.ApplicationId, [task.Code], cancellationToken);
        foreach (var path in replaced)
        {
            await storage.DeleteAsync(path, cancellationToken);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedName = $"onboarding/{onboardingId}/{Guid.NewGuid():N}{extension}";
        var storagePath = await storage.SaveAsync(tenantKey, storedName, file.Content, cancellationToken);

        var documentId = await applications.AddDocumentAsync(
            new ApplicationDocumentRecord(
                0, tenantKey, onboarding.ApplicationId, task.Code, Path.GetFileName(file.FileName),
                file.ContentType, file.Length, storagePath, "PendingScan", now),
            cancellationToken);

        await applications.UpdateOnboardingTaskAsync(
            task with
            {
                Status = OnboardingTaskStatuses.Submitted,
                DocumentId = documentId,
                DecisionReason = null,
                UpdatedUtc = now
            },
            cancellationToken);
    }

    public async Task SubmitFormTaskAsync(
        string tenantKey, long onboardingId, long taskId, long candidateId,
        IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default)
    {
        var (_, task) = await LoadOwnedTaskAsync(tenantKey, onboardingId, taskId, candidateId, cancellationToken);

        if (!string.Equals(task.TaskType, OnboardingTemplate.Form, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException("WRONG_TASK_TYPE", "This task expects a file, not form values.");
        }

        if (values.Count == 0)
        {
            throw new ApplicationRejectedException("VALUES_REQUIRED", "Fill the form before submitting it.");
        }

        await applications.UpdateOnboardingTaskAsync(
            task with
            {
                Status = OnboardingTaskStatuses.Submitted,
                FormValuesJson = JsonSerializer.Serialize(values, Json),
                DecisionReason = null,
                UpdatedUtc = DateTimeOffset.UtcNow
            },
            cancellationToken);
    }

    // ---- HR-owned tasks ---------------------------------------------------

    public async Task CompleteHrTaskAsync(
        string tenantKey, long onboardingId, long taskId,
        IReadOnlyDictionary<string, string> values, string? actorId, CancellationToken cancellationToken = default)
    {
        var onboarding = await applications.GetOnboardingAsync(tenantKey, onboardingId, cancellationToken)
            ?? throw new ApplicationRejectedException("ONBOARDING_NOT_FOUND", "That onboarding no longer exists.");

        if (!string.Equals(onboarding.Stage, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException("ONBOARDING_CLOSED", "This onboarding is no longer open for changes.");
        }

        var task = await applications.GetOnboardingTaskAsync(tenantKey, taskId, cancellationToken);
        if (task is null || task.OnboardingId != onboardingId)
        {
            throw new ApplicationRejectedException("TASK_NOT_FOUND", "That onboarding task no longer exists.");
        }

        if (!string.Equals(task.OwnerKind, OnboardingTemplate.Hr, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException(
                "NOT_AN_HR_TASK", "This task belongs to the candidate and must be submitted by them.");
        }

        var now = DateTimeOffset.UtcNow;

        await applications.UpdateOnboardingTaskAsync(
            task with
            {
                Status = OnboardingTaskStatuses.Approved,
                FormValuesJson = JsonSerializer.Serialize(values, Json),
                DecidedBy = actorId,
                DecidedUtc = now,
                UpdatedUtc = now
            },
            cancellationToken);

        await CompleteIfFinishedAsync(tenantKey, onboardingId, now, cancellationToken);
    }

    // ---- HR decision ------------------------------------------------------

    public async Task DecideTaskAsync(
        string tenantKey, long taskId, OnboardingTaskDecisionRequest request, string? actorId, CancellationToken cancellationToken = default)
    {
        var task = await applications.GetOnboardingTaskAsync(tenantKey, taskId, cancellationToken)
            ?? throw new ApplicationRejectedException("TASK_NOT_FOUND", "That onboarding task no longer exists.");

        if (!string.Equals(task.Status, OnboardingTaskStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException(
                "NOT_SUBMITTED", "Only a submitted task can be approved or rejected.");
        }

        var approve = string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase);

        if (!approve && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ApplicationRejectedException("REASON_REQUIRED", "A reason is required to reject a task.");
        }

        var now = DateTimeOffset.UtcNow;

        await applications.UpdateOnboardingTaskAsync(
            task with
            {
                Status = approve ? OnboardingTaskStatuses.Approved : OnboardingTaskStatuses.Rejected,
                DecisionReason = request.Reason,
                DecidedBy = actorId,
                DecidedUtc = now,
                UpdatedUtc = now
            },
            cancellationToken);

        if (approve)
        {
            await CompleteIfFinishedAsync(tenantKey, task.OnboardingId, now, cancellationToken);
        }
    }

    /// <summary>Onboarding finishes only when every required task is approved.</summary>
    private async Task CompleteIfFinishedAsync(
        string tenantKey, long onboardingId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var onboarding = await applications.GetOnboardingAsync(tenantKey, onboardingId, cancellationToken);
        if (onboarding is null || !string.Equals(onboarding.Stage, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var tasks = await applications.GetOnboardingTasksAsync(tenantKey, onboardingId, cancellationToken);
        var outstanding = tasks.Count(task =>
            task.IsRequired && !string.Equals(task.Status, OnboardingTaskStatuses.Approved, StringComparison.OrdinalIgnoreCase));

        if (outstanding > 0)
        {
            return;
        }

        await applications.UpsertOnboardingAsync(
            onboarding with { Stage = "Completed", CompletedUtc = now, UpdatedUtc = now },
            cancellationToken);

        var application = await applications.GetAsync(tenantKey, onboarding.ApplicationId, cancellationToken);
        if (application is null)
        {
            return;
        }

        await MoveAsync(application, ApplicationStages.Hired, null, null, "System", now, cancellationToken);

        // Recruitment marks the application Hired and hands off. The employee record is created
        // by the employee module through its own contract, so no employee table is written here.
        var candidate = await applications.GetCandidateByIdAsync(tenantKey, onboarding.CandidateId, cancellationToken);
        var opening = await openings.GetByIdAsync(application.JobOpeningId, cancellationToken);

        if (candidate is not null && opening is not null)
        {
            await employeeProvisioning.HireAsync(
                new ITF.Employee.HireRequest(
                    tenantKey,
                    candidate.Id,
                    candidate.FullName,
                    candidate.Email,
                    candidate.Phone,
                    onboarding.JoiningOn ?? DateOnly.FromDateTime(now.UtcDateTime),
                    opening.Department,
                    opening.Location,
                    opening.Title,
                    opening.EmploymentType),
                cancellationToken);
        }
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<ApplicationRecord> LoadOwnedApplicationAsync(
        string tenantKey, long applicationId, long candidateId, CancellationToken cancellationToken)
    {
        var application = await applications.GetAsync(tenantKey, applicationId, cancellationToken)
            ?? throw new ApplicationRejectedException("APPLICATION_NOT_FOUND", "That application no longer exists.");

        // Ownership is checked against the token's candidate id, never a body field.
        if (application.CandidateId != candidateId)
        {
            throw new ApplicationRejectedException("NOT_YOUR_APPLICATION", "That application does not belong to you.");
        }

        return application;
    }

    private async Task<(OnboardingRecord Onboarding, OnboardingTaskRecord Task)> LoadOwnedTaskAsync(
        string tenantKey, long onboardingId, long taskId, long candidateId, CancellationToken cancellationToken)
    {
        var onboarding = await applications.GetOnboardingAsync(tenantKey, onboardingId, cancellationToken)
            ?? throw new ApplicationRejectedException("ONBOARDING_NOT_FOUND", "That onboarding no longer exists.");

        if (onboarding.CandidateId != candidateId)
        {
            throw new ApplicationRejectedException("NOT_YOUR_ONBOARDING", "That onboarding does not belong to you.");
        }

        if (!string.Equals(onboarding.Stage, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException("ONBOARDING_CLOSED", "This onboarding is no longer open for changes.");
        }

        var task = await applications.GetOnboardingTaskAsync(tenantKey, taskId, cancellationToken);
        if (task is null || task.OnboardingId != onboardingId)
        {
            throw new ApplicationRejectedException("TASK_NOT_FOUND", "That onboarding task no longer exists.");
        }

        if (!string.Equals(task.OwnerKind, OnboardingTemplate.Candidate, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException("NOT_YOUR_TASK", "This task is completed by HR, not by you.");
        }

        return (onboarding, task);
    }

    private async Task MoveAsync(
        ApplicationRecord application, string toStage, string? reason,
        string? actorId, string actorKind, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await applications.UpsertApplicationAsync(
            application with { Stage = toStage, StageChangedUtc = now, StageChangedBy = actorId, UpdatedUtc = now },
            cancellationToken);

        await applications.AddEventAsync(
            new ApplicationEventRecord(
                0, application.TenantKey, application.Id, application.Stage, toStage, reason, actorId, actorKind, now),
            cancellationToken);
    }

    private async Task<OnboardingDto> ProjectAsync(
        string tenantKey, OnboardingRecord onboarding, CancellationToken cancellationToken)
    {
        var tasks = await applications.GetOnboardingTasksAsync(tenantKey, onboarding.Id, cancellationToken);
        var candidate = await applications.GetCandidateByIdAsync(tenantKey, onboarding.CandidateId, cancellationToken);
        var application = await applications.GetAsync(tenantKey, onboarding.ApplicationId, cancellationToken);
        var opening = application is null ? null : await openings.GetByIdAsync(application.JobOpeningId, cancellationToken);

        var required = tasks.Where(task => task.IsRequired).ToList();
        var approved = required.Count(task =>
            string.Equals(task.Status, OnboardingTaskStatuses.Approved, StringComparison.OrdinalIgnoreCase));

        var percent = required.Count == 0 ? 100m : Math.Round(approved * 100m / required.Count, 0);

        return new OnboardingDto(
            onboarding.Id,
            onboarding.ApplicationId,
            onboarding.CandidateId,
            candidate?.FullName ?? "Unknown",
            opening?.Title ?? "Unknown",
            onboarding.Stage,
            onboarding.JoiningOn,
            percent,
            required.Count - approved,
            tasks.Select(task => new OnboardingTaskDto(
                task.Id, task.Code, task.Title, task.Instructions, task.TaskType, task.OwnerKind,
                task.IsRequired, task.Status, task.DocumentId, task.DecisionReason, null)).ToList(),
            onboarding.StartedUtc,
            onboarding.CompletedUtc);
    }
}
