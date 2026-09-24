using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.DL.Recruitment;

public sealed class InterviewRepository(HrManagementDbContext dbContext) : IInterviewRepository
{
    public async Task<IReadOnlyList<InterviewSummaryRecord>> QueryAsync(
        string tenantKey, InterviewFilter filter, CancellationToken cancellationToken = default)
    {
        var interviews = dbContext.Interviews.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            interviews = interviews.Where(item => item.Status == filter.Status);
        }

        if (filter.ApplicationId is { } applicationId)
        {
            interviews = interviews.Where(item => item.ApplicationId == applicationId);
        }

        if (filter.From is { } from)
        {
            var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            interviews = interviews.Where(item => item.StartUtc >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            interviews = interviews.Where(item => item.StartUtc <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.PanelMemberId))
        {
            // Restricting to a panel member is a semi-join rather than a join, so an
            // interview with three panellists still returns once.
            var member = filter.PanelMemberId;
            interviews = interviews.Where(item =>
                dbContext.InterviewPanelists.Any(panelist =>
                    panelist.InterviewId == item.Id && panelist.PanelMemberId == member));
        }

        var joined =
            from interview in interviews
            join candidate in dbContext.Candidates.AsNoTracking() on interview.CandidateId equals candidate.Id
            join opening in dbContext.JobOpenings.AsNoTracking() on interview.JobOpeningId equals opening.Id
            select new { interview, candidate, opening.Title, opening.Department };

        var rows = await joined
            .OrderBy(row => row.interview.StartUtc)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new InterviewSummaryRecord(
                Map(row.interview), row.candidate.FullName, row.candidate.Email, row.Title, row.Department))
            .ToList();
    }

    public async Task<InterviewSummaryRecord?> GetAsync(
        string tenantKey, long interviewId, CancellationToken cancellationToken = default)
    {
        var joined =
            from interview in dbContext.Interviews.AsNoTracking()
            join candidate in dbContext.Candidates.AsNoTracking() on interview.CandidateId equals candidate.Id
            join opening in dbContext.JobOpenings.AsNoTracking() on interview.JobOpeningId equals opening.Id
            where interview.TenantKey == tenantKey && interview.Id == interviewId
            select new { interview, candidate, opening.Title, opening.Department };

        var row = await joined.FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new InterviewSummaryRecord(
                Map(row.interview), row.candidate.FullName, row.candidate.Email, row.Title, row.Department);
    }

    public async Task<long> UpsertAsync(
        InterviewRecord interview, CancellationToken cancellationToken = default)
    {
        var entity = interview.Id > 0
            ? await dbContext.Interviews.FirstOrDefaultAsync(
                item => item.TenantKey == interview.TenantKey && item.Id == interview.Id, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new InterviewEntity
            {
                TenantKey = interview.TenantKey,
                ApplicationId = interview.ApplicationId,
                CandidateId = interview.CandidateId,
                JobOpeningId = interview.JobOpeningId,
                CreatedUtc = interview.CreatedUtc
            };
            dbContext.Interviews.Add(entity);
        }

        entity.RoundCode = interview.RoundCode;
        entity.RoundLabel = interview.RoundLabel;
        entity.StartUtc = interview.StartUtc;
        entity.DurationMinutes = interview.DurationMinutes;
        entity.TimeZoneId = interview.TimeZoneId;
        entity.Mode = interview.Mode;
        entity.LocationOrLink = interview.LocationOrLink;
        entity.Instructions = interview.Instructions;
        entity.Status = interview.Status;
        entity.CancellationReasonCode = interview.CancellationReasonCode;
        entity.CancellationComment = interview.CancellationComment;
        entity.ScheduledBy = interview.ScheduledBy;
        entity.UpdatedUtc = interview.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<InterviewRecord>> GetPanelBookingsAsync(
        string tenantKey,
        IReadOnlyList<string> panelMemberIds,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        if (panelMemberIds.Count == 0)
        {
            return [];
        }

        // Cancelled interviews do not block a slot.
        var rows = await dbContext.Interviews
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.Status != "Cancelled" &&
                           item.StartUtc < toUtc &&
                           item.StartUtc >= fromUtc.AddHours(-24) &&
                           dbContext.InterviewPanelists.Any(panelist =>
                               panelist.InterviewId == item.Id &&
                               panelMemberIds.Contains(panelist.PanelMemberId)))
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<InterviewPanelistRecord>> GetPanelAsync(
        string tenantKey, long interviewId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.InterviewPanelists
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.InterviewId == interviewId)
            .OrderByDescending(item => item.IsLead)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => new InterviewPanelistRecord(
                item.Id, item.TenantKey, item.InterviewId, item.PanelMemberId,
                item.DisplayName, item.IsLead, item.AddedUtc))
            .ToList();
    }

    public async Task ReplacePanelAsync(
        string tenantKey, long interviewId, IReadOnlyList<InterviewPanelistRecord> panel,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.InterviewPanelists
            .Where(item => item.TenantKey == tenantKey && item.InterviewId == interviewId)
            .ToListAsync(cancellationToken);

        dbContext.InterviewPanelists.RemoveRange(existing);

        dbContext.InterviewPanelists.AddRange(panel.Select(member => new InterviewPanelistEntity
        {
            TenantKey = tenantKey,
            InterviewId = interviewId,
            PanelMemberId = member.PanelMemberId,
            DisplayName = member.DisplayName,
            IsLead = member.IsLead,
            AddedUtc = member.AddedUtc
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InterviewEvaluationRecord?> GetEvaluationAsync(
        string tenantKey, long interviewId, string panelMemberId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.InterviewEvaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey &&
                        item.InterviewId == interviewId &&
                        item.PanelMemberId == panelMemberId,
                cancellationToken);

        return entity is null ? null : MapEvaluation(entity);
    }

    public async Task<long> UpsertEvaluationAsync(
        InterviewEvaluationRecord evaluation, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.InterviewEvaluations.FirstOrDefaultAsync(
            item => item.TenantKey == evaluation.TenantKey &&
                    item.InterviewId == evaluation.InterviewId &&
                    item.PanelMemberId == evaluation.PanelMemberId,
            cancellationToken);

        if (entity is null)
        {
            entity = new InterviewEvaluationEntity
            {
                TenantKey = evaluation.TenantKey,
                InterviewId = evaluation.InterviewId,
                PanelMemberId = evaluation.PanelMemberId
            };
            dbContext.InterviewEvaluations.Add(entity);
        }

        entity.ScoresJson = evaluation.ScoresJson;
        entity.Recommendation = evaluation.Recommendation;
        entity.Notes = evaluation.Notes;
        entity.State = evaluation.State;
        entity.SubmittedUtc = evaluation.SubmittedUtc;
        entity.UpdatedUtc = evaluation.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static InterviewRecord Map(InterviewEntity item) => new(
        item.Id, item.TenantKey, item.ApplicationId, item.CandidateId, item.JobOpeningId,
        item.RoundCode, item.RoundLabel, item.StartUtc, item.DurationMinutes, item.TimeZoneId,
        item.Mode, item.LocationOrLink, item.Instructions, item.Status,
        item.CancellationReasonCode, item.CancellationComment, item.ScheduledBy,
        item.CreatedUtc, item.UpdatedUtc);

    private static InterviewEvaluationRecord MapEvaluation(InterviewEvaluationEntity item) => new(
        item.Id, item.TenantKey, item.InterviewId, item.PanelMemberId, item.ScoresJson,
        item.Recommendation, item.Notes, item.State, item.SubmittedUtc, item.UpdatedUtc);
}
