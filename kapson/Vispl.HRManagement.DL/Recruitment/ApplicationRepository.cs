using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.DL.Recruitment;

public sealed class ApplicationRepository(HrManagementDbContext dbContext) : IApplicationRepository
{

    public async Task<IReadOnlyList<(long JobOpeningId, string Stage, int Count, DateTimeOffset LatestUtc)>>
        CountByOpeningAndStageAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Applications
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .GroupBy(item => new { item.JobOpeningId, item.Stage })
            .Select(group => new
            {
                group.Key.JobOpeningId,
                group.Key.Stage,
                Count = group.Count(),
                LatestUtc = group.Max(item => item.AppliedUtc)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => (item.JobOpeningId, item.Stage, item.Count, item.LatestUtc))
            .ToList();
    }
    // ---- Candidates -------------------------------------------------------

    public async Task<CandidateRecord?> GetCandidateByEmailAsync(
        string tenantKey, string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var entity = await dbContext.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Email == normalized, cancellationToken);
        return entity is null ? null : MapCandidate(entity);
    }

    public async Task<CandidateRecord?> GetCandidateByIdAsync(
        string tenantKey, long candidateId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == candidateId, cancellationToken);
        return entity is null ? null : MapCandidate(entity);
    }

    public async Task<long> UpsertCandidateAsync(CandidateRecord candidate, CancellationToken cancellationToken = default)
    {
        var normalized = candidate.Email.Trim().ToLowerInvariant();
        var entity = candidate.Id > 0
            ? await dbContext.Candidates.FirstOrDefaultAsync(
                item => item.TenantKey == candidate.TenantKey && item.Id == candidate.Id, cancellationToken)
            : await dbContext.Candidates.FirstOrDefaultAsync(
                item => item.TenantKey == candidate.TenantKey && item.Email == normalized, cancellationToken);

        // Someone reapplying from a second address is the same person, not a new one. Matching
        // on the subscriber number as well as the address stops a fresh email minting a fresh
        // candidate each time; the stored address is left as first recorded, so the unique
        // email index is never disturbed.
        if (entity is null && candidate.Id <= 0)
        {
            var subscriber = SubscriberNumber(candidate.Phone);
            if (subscriber is not null)
            {
                entity = await dbContext.Candidates.FirstOrDefaultAsync(
                    item => item.TenantKey == candidate.TenantKey &&
                            item.Phone != null &&
                            item.Phone.EndsWith(subscriber),
                    cancellationToken);
            }
        }

        if (entity is null)
        {
            entity = new CandidateEntity
            {
                TenantKey = candidate.TenantKey,
                Email = normalized,
                CreatedUtc = candidate.CreatedUtc
            };
            dbContext.Candidates.Add(entity);
        }

        entity.FullName = candidate.FullName;
        entity.Phone = candidate.Phone;
        entity.CurrentLocation = candidate.CurrentLocation;
        entity.TotalExperienceYears = candidate.TotalExperienceYears;
        entity.AccountId = candidate.AccountId ?? entity.AccountId;
        entity.UpdatedUtc = candidate.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <summary>
    /// The last ten digits of a phone number, which is the part that identifies the subscriber
    /// once country and trunk prefixes are stripped. Returns null for anything shorter, because
    /// a partial number would match too many people to be worth matching on at all.
    /// </summary>
    private static string? SubscriberNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length < 10 ? null : digits[^10..];
    }

    // ---- Applications -----------------------------------------------------

    public async Task<ApplicationRecord?> GetAsync(
        string tenantKey, long applicationId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == applicationId, cancellationToken);
        return entity is null ? null : MapApplication(entity);
    }

    public async Task<ApplicationRecord?> GetByOpeningAndCandidateAsync(
        string tenantKey, long jobOpeningId, long candidateId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey &&
                        item.JobOpeningId == jobOpeningId &&
                        item.CandidateId == candidateId,
                cancellationToken);
        return entity is null ? null : MapApplication(entity);
    }

    public async Task<IReadOnlyList<ApplicationSummaryRecord>> GetSummariesAsync(
        string tenantKey, string? stage, string? query, CancellationToken cancellationToken = default)
    {
        // Joined in the query rather than with navigation properties, keeping the
        // module boundary free of ORM relationships.
        var applications = dbContext.Applications.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (!string.IsNullOrWhiteSpace(stage))
        {
            applications = applications.Where(item => item.Stage == stage);
        }

        var joined =
            from application in applications
            join candidate in dbContext.Candidates.AsNoTracking() on application.CandidateId equals candidate.Id
            join opening in dbContext.JobOpenings.AsNoTracking() on application.JobOpeningId equals opening.Id
            where candidate.TenantKey == tenantKey && opening.TenantKey == tenantKey
            select new { application, candidate, opening.Title, opening.Department };

        if (!string.IsNullOrWhiteSpace(query))
        {
            joined = joined.Where(row =>
                EF.Functions.Like(row.candidate.FullName, $"%{query}%") ||
                EF.Functions.Like(row.Title, $"%{query}%"));
        }

        var rows = await joined
            .OrderByDescending(row => row.application.AppliedUtc)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ApplicationSummaryRecord(
                MapApplication(row.application), MapCandidate(row.candidate), row.Title, row.Department))
            .ToList();
    }

    public async Task<IReadOnlyList<ApplicationSummaryRecord>> GetForCandidateAsync(
        string tenantKey, long candidateId, CancellationToken cancellationToken = default)
    {
        var joined =
            from application in dbContext.Applications.AsNoTracking()
            join candidate in dbContext.Candidates.AsNoTracking() on application.CandidateId equals candidate.Id
            join opening in dbContext.JobOpenings.AsNoTracking() on application.JobOpeningId equals opening.Id
            where application.TenantKey == tenantKey && application.CandidateId == candidateId
            select new { application, candidate, opening.Title, opening.Department };

        var rows = await joined
            .OrderByDescending(row => row.application.AppliedUtc)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ApplicationSummaryRecord(
                MapApplication(row.application), MapCandidate(row.candidate), row.Title, row.Department))
            .ToList();
    }

    public async Task<long> UpsertApplicationAsync(
        ApplicationRecord application, CancellationToken cancellationToken = default)
    {
        var entity = application.Id > 0
            ? await dbContext.Applications.FirstOrDefaultAsync(
                item => item.TenantKey == application.TenantKey && item.Id == application.Id, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new ApplicationEntity
            {
                TenantKey = application.TenantKey,
                JobOpeningId = application.JobOpeningId,
                CandidateId = application.CandidateId,
                AppliedUtc = application.AppliedUtc
            };
            dbContext.Applications.Add(entity);
        }

        entity.Stage = application.Stage;
        entity.Source = application.Source;
        entity.NoticePeriodDays = application.NoticePeriodDays;
        entity.ExpectedCtc = application.ExpectedCtc;
        entity.AnswersJson = application.AnswersJson;
        entity.Rating = application.Rating;
        entity.RejectionReason = application.RejectionReason;
        entity.ConsentGranted = application.ConsentGranted;
        entity.ConsentPolicyVersion = application.ConsentPolicyVersion;
        entity.ConsentUtc = application.ConsentUtc;
        entity.StageChangedUtc = application.StageChangedUtc;
        entity.StageChangedBy = application.StageChangedBy;
        entity.UpdatedUtc = application.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    // ---- Documents --------------------------------------------------------

    public async Task<IReadOnlyList<ApplicationDocumentRecord>> GetDocumentsAsync(
        string tenantKey, long applicationId, CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.ApplicationDocuments
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.ApplicationId == applicationId)
            .OrderBy(item => item.CategoryCode)
            .ToListAsync(cancellationToken);
        return entities.Select(MapDocument).ToList();
    }

    public async Task<ApplicationDocumentRecord?> GetDocumentAsync(
        string tenantKey, long documentId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ApplicationDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == documentId, cancellationToken);
        return entity is null ? null : MapDocument(entity);
    }

    public async Task<long> AddDocumentAsync(
        ApplicationDocumentRecord document, CancellationToken cancellationToken = default)
    {
        var entity = new ApplicationDocumentEntity
        {
            TenantKey = document.TenantKey,
            ApplicationId = document.ApplicationId,
            CategoryCode = document.CategoryCode,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            SizeBytes = document.SizeBytes,
            StoragePath = document.StoragePath,
            ScanState = document.ScanState,
            UploadedUtc = document.UploadedUtc
        };

        dbContext.ApplicationDocuments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<string>> RemoveDocumentsAsync(
        string tenantKey, long applicationId, IReadOnlyList<string> categoryCodes, CancellationToken cancellationToken = default)
    {
        if (categoryCodes.Count == 0)
        {
            return [];
        }

        var entities = await dbContext.ApplicationDocuments
            .Where(item => item.TenantKey == tenantKey &&
                           item.ApplicationId == applicationId &&
                           categoryCodes.Contains(item.CategoryCode))
            .ToListAsync(cancellationToken);

        var paths = entities.Select(item => item.StoragePath).ToList();
        dbContext.ApplicationDocuments.RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
        return paths;
    }

    // ---- Timeline ---------------------------------------------------------

    public async Task<IReadOnlyList<ApplicationEventRecord>> GetEventsAsync(
        string tenantKey, long applicationId, CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.ApplicationEvents
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.ApplicationId == applicationId)
            .OrderBy(item => item.OccurredUtc)
            .ToListAsync(cancellationToken);

        return entities
            .Select(item => new ApplicationEventRecord(
                item.Id, item.TenantKey, item.ApplicationId, item.FromStage, item.ToStage,
                item.Reason, item.ActorId, item.ActorKind, item.OccurredUtc))
            .ToList();
    }

    public async Task AddEventAsync(ApplicationEventRecord entry, CancellationToken cancellationToken = default)
    {
        dbContext.ApplicationEvents.Add(new ApplicationEventEntity
        {
            TenantKey = entry.TenantKey,
            ApplicationId = entry.ApplicationId,
            FromStage = entry.FromStage,
            ToStage = entry.ToStage,
            Reason = entry.Reason,
            ActorId = entry.ActorId,
            ActorKind = entry.ActorKind,
            OccurredUtc = entry.OccurredUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Onboarding -------------------------------------------------------

    public async Task<OnboardingRecord?> GetOnboardingAsync(
        string tenantKey, long onboardingId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Onboardings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == onboardingId, cancellationToken);
        return entity is null ? null : MapOnboarding(entity);
    }

    public async Task<OnboardingRecord?> GetOnboardingByApplicationAsync(
        string tenantKey, long applicationId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Onboardings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : MapOnboarding(entity);
    }

    public async Task<long> UpsertOnboardingAsync(
        OnboardingRecord onboarding, CancellationToken cancellationToken = default)
    {
        var entity = onboarding.Id > 0
            ? await dbContext.Onboardings.FirstOrDefaultAsync(
                item => item.TenantKey == onboarding.TenantKey && item.Id == onboarding.Id, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new OnboardingEntity
            {
                TenantKey = onboarding.TenantKey,
                ApplicationId = onboarding.ApplicationId,
                CandidateId = onboarding.CandidateId,
                StartedUtc = onboarding.StartedUtc
            };
            dbContext.Onboardings.Add(entity);
        }

        entity.Stage = onboarding.Stage;
        entity.JoiningOn = onboarding.JoiningOn;
        entity.CompletedUtc = onboarding.CompletedUtc;
        entity.UpdatedUtc = onboarding.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<OnboardingTaskRecord>> GetOnboardingTasksAsync(
        string tenantKey, long onboardingId, CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.OnboardingTasks
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.OnboardingId == onboardingId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        return entities.Select(MapTask).ToList();
    }

    public async Task<OnboardingTaskRecord?> GetOnboardingTaskAsync(
        string tenantKey, long taskId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OnboardingTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == taskId, cancellationToken);
        return entity is null ? null : MapTask(entity);
    }

    public async Task AddOnboardingTasksAsync(
        IReadOnlyList<OnboardingTaskRecord> tasks, CancellationToken cancellationToken = default)
    {
        dbContext.OnboardingTasks.AddRange(tasks.Select(task => new OnboardingTaskEntity
        {
            TenantKey = task.TenantKey,
            OnboardingId = task.OnboardingId,
            Code = task.Code,
            Title = task.Title,
            Instructions = task.Instructions,
            TaskType = task.TaskType,
            OwnerKind = task.OwnerKind,
            IsRequired = task.IsRequired,
            SortOrder = task.SortOrder,
            Status = task.Status,
            UpdatedUtc = task.UpdatedUtc
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOnboardingTaskAsync(
        OnboardingTaskRecord task, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OnboardingTasks
            .FirstOrDefaultAsync(item => item.TenantKey == task.TenantKey && item.Id == task.Id, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.Status = task.Status;
        entity.DocumentId = task.DocumentId;
        entity.FormValuesJson = task.FormValuesJson;
        entity.DecisionReason = task.DecisionReason;
        entity.DecidedBy = task.DecidedBy;
        entity.DecidedUtc = task.DecidedUtc;
        entity.UpdatedUtc = task.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Mapping ----------------------------------------------------------

    private static CandidateRecord MapCandidate(CandidateEntity item) => new(
        item.Id, item.TenantKey, item.Email, item.FullName, item.Phone,
        item.CurrentLocation, item.TotalExperienceYears, item.AccountId,
        item.CreatedUtc, item.UpdatedUtc);

    private static ApplicationRecord MapApplication(ApplicationEntity item) => new(
        item.Id, item.TenantKey, item.JobOpeningId, item.CandidateId, item.Stage, item.Source,
        item.NoticePeriodDays, item.ExpectedCtc, item.AnswersJson, item.Rating, item.RejectionReason,
        item.ConsentGranted, item.ConsentPolicyVersion, item.ConsentUtc,
        item.AppliedUtc, item.StageChangedUtc, item.StageChangedBy, item.UpdatedUtc);

    private static ApplicationDocumentRecord MapDocument(ApplicationDocumentEntity item) => new(
        item.Id, item.TenantKey, item.ApplicationId, item.CategoryCode, item.OriginalFileName,
        item.ContentType, item.SizeBytes, item.StoragePath, item.ScanState, item.UploadedUtc);

    private static OnboardingRecord MapOnboarding(OnboardingEntity item) => new(
        item.Id, item.TenantKey, item.ApplicationId, item.CandidateId, item.Stage,
        item.JoiningOn, item.StartedUtc, item.CompletedUtc, item.UpdatedUtc);

    private static OnboardingTaskRecord MapTask(OnboardingTaskEntity item) => new(
        item.Id, item.TenantKey, item.OnboardingId, item.Code, item.Title, item.Instructions,
        item.TaskType, item.OwnerKind, item.IsRequired, item.SortOrder, item.Status,
        item.DocumentId, item.FormValuesJson, item.DecisionReason, item.DecidedBy,
        item.DecidedUtc, item.UpdatedUtc);
}
