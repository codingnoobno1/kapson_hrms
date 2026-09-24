using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// One query across the records the caller is allowed to see.
///
/// Each scope is gated by the permission that guards the screen it links to, so a result can
/// never be the first hint that something exists. A caller without the grant gets no group and
/// no facet for that scope rather than an empty one, because an empty group still discloses
/// that the scope was searched and found nothing.
/// </summary>
public sealed class MobileSearchService(
    IEmployeeRepository employees,
    IJobOpeningRepository openings,
    IApplicationRepository applications) : IMobileSearchService
{
    private const int SuggestionLimit = 8;

    public async Task<IReadOnlyList<string>> SuggestAsync(
        MobileCallerContext caller, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var result = await SearchAsync(
            caller, new GlobalSearchQuery { Query = query, PageSize = SuggestionLimit }, ct);

        return result.Groups.Values
            .SelectMany(items => items)
            .Select(item => item.Title)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(SuggestionLimit)
            .ToList();
    }

    public async Task<GlobalSearchResultDto> SearchAsync(
        MobileCallerContext caller, GlobalSearchQuery query, CancellationToken ct)
    {
        var term = query.Query?.Trim();
        var groups = new Dictionary<string, IReadOnlyList<SearchResultItemDto>>(StringComparer.OrdinalIgnoreCase);
        var facets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(term))
        {
            return new GlobalSearchResultDto(groups, facets);
        }

        var limit = Math.Clamp(query.PageSize, 1, 100);

        if (Wanted(query, "employees") && caller.Has("EMPLOYEES.VIEW"))
        {
            var matches = await employees.QueryAsync(
                caller.TenantKey, new EmployeeQueryFilter(term, null, null, null, null), ct);

            Add("employees", matches.Take(limit).Select(person => new SearchResultItemDto(
                "employee",
                person.Id.ToString(),
                DisplayName(person),
                person.JobTitle,
                $"/people/{person.Id}",
                "user",
                null)).ToList(), matches.Count);
        }

        if (Wanted(query, "jobs") && caller.Has("RECRUITMENT.VIEW"))
        {
            var all = await openings.GetAllAsync(caller.TenantKey, ct);
            var matches = all
                .Where(job =>
                    job.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    job.Department.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Add("jobs", matches.Take(limit).Select(job => new SearchResultItemDto(
                "job",
                job.Id.ToString(),
                job.Title,
                $"{job.Department} · {job.Location}",
                $"/jobs/{job.Slug}",
                "clipboard",
                null)).ToList(), matches.Count);
        }

        if (Wanted(query, "applicants") && caller.Has("RECRUITMENT.APPLICATIONS"))
        {
            // The repository already filters by name and title, so the term is not re-applied.
            var matches = await applications.GetSummariesAsync(caller.TenantKey, null, term, ct);

            Add("applicants", matches.Take(limit).Select(row => new SearchResultItemDto(
                "applicant",
                row.Application.Id.ToString(),
                row.Candidate.FullName,
                row.JobTitle,
                $"/applicants/{row.Application.Id}",
                "users",
                null)).ToList(), matches.Count);
        }

        return new GlobalSearchResultDto(groups, facets);

        void Add(string scope, IReadOnlyList<SearchResultItemDto> items, int total)
        {
            if (items.Count == 0)
            {
                return;
            }

            groups[scope] = items;

            // The total before paging, so the UI can offer "see all 42" rather than counting
            // the handful it was given.
            facets[scope] = total;
        }
    }

    /// <summary>An empty scope list means every scope the caller can reach.</summary>
    private static bool Wanted(GlobalSearchQuery query, string scope) =>
        query.Scopes.Count == 0 ||
        query.Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);

    private static string DisplayName(EmployeeRecord person) =>
        string.IsNullOrWhiteSpace(person.PreferredName)
            ? $"{person.LegalFirstName} {person.LegalLastName}".Trim()
            : person.PreferredName;
}
