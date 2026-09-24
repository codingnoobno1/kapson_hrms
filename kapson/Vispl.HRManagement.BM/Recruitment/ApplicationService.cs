using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Recruitment;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.BM.Recruitment;

public sealed class ApplicationService(
    IApplicationRepository applications,
    IJobOpeningRepository openings,
    IMasterOptionRepository masters,
    IMediaStorage storage,
    ITF.Shared.INotificationPublisher notifications) : IApplicationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---- Public opening ---------------------------------------------------

    public async Task<PublicOpeningDto?> GetPublicOpeningAsync(
        string tenantKey, string slug, CancellationToken cancellationToken = default)
    {
        var opening = await openings.GetBySlugAsync(tenantKey, slug, publishedOnly: true, cancellationToken);
        if (opening is null || !IsAcceptingApplications(opening))
        {
            return null;
        }

        var requiredCodes = Deserialize<List<string>>(opening.RequiredDocumentCodesJson) ?? [];
        var categories = await masters.GetActiveAsync(tenantKey, "DocumentCategory", cancellationToken);
        var names = categories.ToDictionary(item => item.Code, item => item.Name, StringComparer.OrdinalIgnoreCase);

        return new PublicOpeningDto(
            opening.Id,
            opening.Slug,
            opening.Title,
            opening.Department,
            opening.Location,
            opening.EmploymentType,
            opening.WorkMode,
            opening.Experience,
            opening.Summary,
            Deserialize<List<string>>(opening.ResponsibilitiesJson) ?? [],
            Deserialize<List<string>>(opening.RequirementsJson) ?? [],
            FormatSalary(opening),
            opening.ExpiresOn,
            requiredCodes
                .Select(code => new RequiredDocumentDto(code, names.GetValueOrDefault(code, code)))
                .ToList(),
            Deserialize<List<ScreeningQuestionDto>>(opening.ScreeningQuestionsJson) ?? []);
    }

    // ---- Submit -----------------------------------------------------------

    public async Task<SubmitApplicationResultDto> SubmitAsync(
        string tenantKey,
        string slug,
        SubmitApplicationRequest request,
        IReadOnlyList<UploadedFile> files,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await SubmitCoreAsync(tenantKey, slug, request, files, cancellationToken);
        }
        catch (ApplicationRejected rejected)
        {
            throw new ApplicationRejectedException(rejected.Code, rejected.Message);
        }
    }

    /// <summary>
    /// The fields without which an application is not one.
    ///
    /// The email is the candidate's identity within a client and the only way to reach them:
    /// an application without it can never be activated, so accepting one loses the person
    /// rather than merely storing an incomplete record.
    /// </summary>
    private static void ValidateIdentity(SubmitApplicationRequest request)
    {
        var name = request.FullName?.Trim();
        var email = request.Email?.Trim();
        var phone = request.Phone?.Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
        {
            throw new ApplicationRejected("NAME_REQUIRED", "Enter your full name.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ApplicationRejected("EMAIL_REQUIRED", "Enter the email address we should reply to.");
        }

        // Deliberately shallow: an address is proved by sending to it, not by a pattern. This
        // rejects what is obviously not an address and leaves the rest to the activation mail.
        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1 || email.IndexOf('.', at) < 0 || email.Any(char.IsWhiteSpace))
        {
            throw new ApplicationRejected("EMAIL_INVALID", "That does not look like an email address.");
        }

        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7)
        {
            throw new ApplicationRejected("PHONE_REQUIRED", "Enter a phone number we can reach you on.");
        }
    }

    private async Task<SubmitApplicationResultDto> SubmitCoreAsync(
        string tenantKey,
        string slug,
        SubmitApplicationRequest request,
        IReadOnlyList<UploadedFile> files,
        CancellationToken cancellationToken)
    {
        // Who this is. Enforced here rather than left to the caller's model binding, because
        // there are two callers and only one of them gets it: the public API endpoint is an
        // [ApiController] and validates the request object automatically, while the career
        // site's apply page calls this service directly and does not. That gap let a
        // submission through with no name, no email and no phone — a candidate record nobody
        // could ever contact or activate.
        ValidateIdentity(request);

        // Consent is a precondition, not a field to be defaulted.
        if (!request.ConsentGranted)
        {
            throw new ApplicationRejected("CONSENT_REQUIRED", "The application cannot be submitted without consent.");
        }

        var opening = await openings.GetBySlugAsync(tenantKey, slug, publishedOnly: true, cancellationToken)
            ?? throw new ApplicationRejected("OPENING_NOT_FOUND", "This opening is no longer available.");

        if (!IsAcceptingApplications(opening))
        {
            throw new ApplicationRejected("OPENING_CLOSED", "This opening has closed and is not accepting applications.");
        }

        // Every required slot must be filled, and no unexpected categories accepted.
        var requiredCodes = Deserialize<List<string>>(opening.RequiredDocumentCodesJson) ?? [];
        var expected = new HashSet<string>(requiredCodes, StringComparer.OrdinalIgnoreCase)
        {
            ApplicationDocumentRules.ResumeCategory
        };

        var supplied = files
            .GroupBy(file => file.CategoryCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (!supplied.ContainsKey(ApplicationDocumentRules.ResumeCategory))
        {
            throw new ApplicationRejected("RESUME_REQUIRED", "A résumé is required.");
        }

        var missing = expected.Where(code => !supplied.ContainsKey(code)).ToList();
        if (missing.Count > 0)
        {
            throw new ApplicationRejected(
                "DOCUMENTS_MISSING",
                $"These documents are required: {string.Join(", ", missing)}.");
        }

        var unexpected = supplied.Keys.Where(code => !expected.Contains(code)).ToList();
        if (unexpected.Count > 0)
        {
            throw new ApplicationRejected(
                "DOCUMENT_NOT_REQUESTED",
                $"This opening does not accept: {string.Join(", ", unexpected)}.");
        }

        var totalBytes = supplied.Values.Sum(file => file.Length);
        if (totalBytes > ApplicationDocumentRules.MaximumSubmissionBytes)
        {
            throw new ApplicationRejected(
                "SUBMISSION_TOO_LARGE",
                $"The total upload must be {ApplicationDocumentRules.MaximumSubmissionBytes / (1024 * 1024)} MB or smaller.");
        }

        // Validate everything before persisting or storing anything.
        foreach (var file in supplied.Values)
        {
            await ApplicationFileValidator.ValidateAsync(file, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var email = request.Email.Trim().ToLowerInvariant();

        var existingCandidate = await applications.GetCandidateByEmailAsync(tenantKey, email, cancellationToken);
        var candidateId = await applications.UpsertCandidateAsync(
            new CandidateRecord(
                existingCandidate?.Id ?? 0,
                tenantKey,
                email,
                request.FullName.Trim(),
                request.Phone.Trim(),
                string.IsNullOrWhiteSpace(request.CurrentLocation) ? null : request.CurrentLocation.Trim(),
                request.TotalExperienceYears,
                existingCandidate?.AccountId,
                existingCandidate?.CreatedUtc ?? now,
                now),
            cancellationToken);

        var existing = await applications.GetByOpeningAndCandidateAsync(
            tenantKey, opening.Id, candidateId, cancellationToken);

        // A repeat submission updates the first one, and only while it is untouched.
        if (existing is not null && !string.Equals(existing.Stage, ApplicationStages.Applied, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejected(
                "ALREADY_APPLIED",
                "You have already applied for this opening and it is being reviewed.");
        }

        var applicationId = await applications.UpsertApplicationAsync(
            new ApplicationRecord(
                existing?.Id ?? 0,
                tenantKey,
                opening.Id,
                candidateId,
                ApplicationStages.Applied,
                "CareerSite",
                request.NoticePeriodDays,
                request.ExpectedCtc,
                JsonSerializer.Serialize(request.Answers, Json),
                existing?.Rating,
                null,
                true,
                request.ConsentPolicyVersion,
                now,
                existing?.AppliedUtc ?? now,
                now,
                null,
                now),
            cancellationToken);

        // Replace prior files for the same categories so a resubmission does not accumulate.
        if (existing is not null)
        {
            var replaced = await applications.RemoveDocumentsAsync(
                tenantKey, applicationId, supplied.Keys.ToList(), cancellationToken);

            // Rows first, then blobs. A failure here leaves an orphan rather than a
            // row pointing at a file that is no longer there.
            foreach (var path in replaced)
            {
                await storage.DeleteAsync(path, cancellationToken);
            }
        }

        foreach (var file in supplied.Values)
        {
            // The client filename is never used to build a path.
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var storedName = $"applications/{applicationId}/{Guid.NewGuid():N}{extension}";
            var storagePath = await storage.SaveAsync(tenantKey, storedName, file.Content, cancellationToken);

            await applications.AddDocumentAsync(
                new ApplicationDocumentRecord(
                    0, tenantKey, applicationId, file.CategoryCode,
                    Path.GetFileName(file.FileName), file.ContentType, file.Length,
                    storagePath, "PendingScan", now),
                cancellationToken);
        }

        if (existing is null)
        {
            await applications.AddEventAsync(
                new ApplicationEventRecord(0, tenantKey, applicationId, string.Empty,
                    ApplicationStages.Applied, null, null, "Candidate", now),
                cancellationToken);
        }

        // Raised after the application is safely stored, so a delivery failure cannot lose it.
        if (existing is null)
        {
            await notifications.PublishToPermissionAsync(
                new ITF.Shared.NotificationRequest(
                    tenantKey,
                    "Recruitment",
                    "New application received",
                    $"{request.FullName.Trim()} applied for {opening.Title}. The application is awaiting review.",
                    $"/applicants",
                    "Normal",
                    "Application",
                    applicationId.ToString()),
                "RECRUITMENT.APPLICATIONS",
                cancellationToken);
        }

        return new SubmitApplicationResultDto(
            applicationId,
            $"APP-{applicationId:D6}",
            ApplicationStages.Applied,
            now,
            existing is not null);
    }

    // ---- HR review --------------------------------------------------------

    public async Task<IReadOnlyList<ApplicationListItemDto>> GetSummariesAsync(
        string tenantKey, string? stage, string? query, CancellationToken cancellationToken = default)
    {
        var summaries = await applications.GetSummariesAsync(tenantKey, stage, query, cancellationToken);
        return summaries.Select(MapListItem).ToList();
    }

    public async Task<ApplicationDetailDto?> GetDetailAsync(
        string tenantKey, long applicationId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetAsync(tenantKey, applicationId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var candidate = await applications.GetCandidateByIdAsync(tenantKey, application.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var opening = await openings.GetByIdAsync(application.JobOpeningId, cancellationToken);
        var documents = await applications.GetDocumentsAsync(tenantKey, applicationId, cancellationToken);
        var events = await applications.GetEventsAsync(tenantKey, applicationId, cancellationToken);

        var summary = MapListItem(new ApplicationSummaryRecord(
            application, candidate, opening?.Title ?? "Unknown", opening?.Department ?? string.Empty));

        return new ApplicationDetailDto(
            summary,
            candidate.CurrentLocation,
            candidate.TotalExperienceYears,
            application.NoticePeriodDays,
            application.ExpectedCtc,
            Deserialize<Dictionary<string, string>>(application.AnswersJson) ?? [],
            documents.Select(MapDocument).ToList(),
            events
                .Select(item => new ApplicationTimelineEntryDto(
                    item.FromStage, item.ToStage, item.Reason, item.ActorKind, item.OccurredUtc))
                .ToList(),
            ApplicationStages.All
                .Where(target => ApplicationStages.IsHrTransitionAllowed(application.Stage, target))
                .ToList(),
            null);
    }

    public async Task ChangeStageAsync(
        string tenantKey,
        long applicationId,
        ChangeStageRequest request,
        string? actorId,
        bool canManage,
        CancellationToken cancellationToken = default)
    {
        var application = await applications.GetAsync(tenantKey, applicationId, cancellationToken)
            ?? throw new ApplicationRejectedException("APPLICATION_NOT_FOUND", "The application no longer exists.");

        var target = request.ToStage.Trim();

        if (!ApplicationStages.IsHrTransitionAllowed(application.Stage, target))
        {
            throw new ApplicationRejectedException(
                "TRANSITION_NOT_ALLOWED",
                $"An application cannot move from {application.Stage} to {target}.");
        }

        if (ApplicationStages.RequiresManagePermission(target) && !canManage)
        {
            throw new ApplicationRejectedException(
                "PERMISSION_REQUIRED",
                "Making an offer requires the recruitment manage permission.");
        }

        if (ApplicationStages.IsRejection(target) && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ApplicationRejectedException("REASON_REQUIRED", "A reason is required to reject an application.");
        }

        var now = DateTimeOffset.UtcNow;

        await applications.UpsertApplicationAsync(
            application with
            {
                Stage = target,
                RejectionReason = ApplicationStages.IsRejection(target) ? request.Reason : application.RejectionReason,
                StageChangedUtc = now,
                StageChangedBy = actorId,
                UpdatedUtc = now
            },
            cancellationToken);

        await applications.AddEventAsync(
            new ApplicationEventRecord(
                0, tenantKey, applicationId, application.Stage, target, request.Reason, actorId, "Hr", now),
            cancellationToken);
    }

    public async Task<DocumentStream?> OpenDocumentAsync(
        string tenantKey, long documentId, CancellationToken cancellationToken = default)
    {
        var document = await applications.GetDocumentAsync(tenantKey, documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        // A file that has not been cleared by a scanner is not released.
        if (!string.Equals(document.ScanState, "Clean", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationRejectedException(
                "FILE_NOT_RELEASED",
                "This file has not completed malware scanning and cannot be downloaded yet.");
        }

        var content = await storage.OpenReadAsync(document.StoragePath, cancellationToken);
        return new DocumentStream(content, document.ContentType, document.OriginalFileName);
    }

    // ---- Helpers ----------------------------------------------------------

    private static bool IsAcceptingApplications(JobOpeningRecord opening)
    {
        if (!opening.IsPublished)
        {
            return false;
        }

        return !opening.ExpiresOn.HasValue ||
               opening.ExpiresOn.Value >= DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static string? FormatSalary(JobOpeningRecord opening)
    {
        if (opening.SalaryMinimum is null || opening.SalaryMaximum is null)
        {
            return null;
        }

        var culture = new System.Globalization.CultureInfo("en-IN");
        var symbol = opening.SalaryCurrencyCode == "INR" || opening.SalaryCurrencyCode is null ? "₹" : $"{opening.SalaryCurrencyCode} ";
        return $"{symbol}{opening.SalaryMinimum.Value.ToString("#,##0", culture)} - " +
               $"{symbol}{opening.SalaryMaximum.Value.ToString("#,##0", culture)} p.a.";
    }

    private static ApplicationListItemDto MapListItem(ApplicationSummaryRecord row) => new(
        row.Application.Id,
        row.Application.JobOpeningId,
        row.JobTitle,
        row.Department,
        row.Candidate.Id,
        row.Candidate.FullName,
        row.Candidate.Email,
        row.Candidate.Phone,
        row.Application.Rating,
        row.Application.Stage,
        ApplicationStages.Tone(row.Application.Stage),
        row.Application.AppliedUtc);

    private static ApplicationDocumentDto MapDocument(ApplicationDocumentRecord document) => new(
        document.Id,
        document.CategoryCode,
        document.OriginalFileName,
        document.ContentType,
        document.SizeBytes,
        document.ScanState,
        string.Equals(document.ScanState, "Clean", StringComparison.OrdinalIgnoreCase),
        document.UploadedUtc);

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
