using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Contracts.Recruitment;
using Vispl.HRManagement.DL.Admin;
using Vispl.HRManagement.DL.Recruitment;

namespace Vispl.HRManagement.DL.Shared;

public sealed class HrManagementDatabaseInitializer(
    HrManagementDbContext dbContext,
    ILogger<HrManagementDatabaseInitializer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task InitializeAsync(
        string seedDataPath,
        bool seedOnEmpty,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying HR Management SQL Server migrations.");
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (!seedOnEmpty)
        {
            return;
        }

        if (!await dbContext.Tenants.AnyAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            dbContext.Tenants.Add(new TenantEntity
            {
                TenantKey = "kapson", DisplayName = "Kapson", PrimaryDomain = null,
                IsActive = true, CreatedUtc = now, UpdatedUtc = now
            });
        }

        if (!await dbContext.ThemeTemplates.AnyAsync(cancellationToken))
        {
            var templates = await ReadJsonAsync<List<ThemeTemplateDto>>(
                Path.Combine(seedDataPath, "theme-templates.json"), cancellationToken);
            dbContext.ThemeTemplates.AddRange(templates.Select(item => new ThemeTemplateEntity
            {
                TemplateKey = item.TemplateKey, Name = item.Name, Description = item.Description,
                PrimaryColor = item.PrimaryColor, AccentColor = item.AccentColor, SurfaceColor = item.SurfaceColor,
                FontFamily = item.FontFamily, HeadingFontFamily = item.HeadingFontFamily,
                BorderRadius = item.BorderRadius, PreviewGradient = item.PreviewGradient,
                SortOrder = item.SortOrder, IsActive = item.IsActive
            }));
        }

        var seedOptions = await ReadJsonAsync<List<MasterOptionDto>>(
            Path.Combine(seedDataPath, "kapson-masters.json"), cancellationToken);
        var masterNow = DateTimeOffset.UtcNow;

        // Additive seed: insert any (tenantKey, category, code) not yet in the database
        var existingKeys = await dbContext.MasterOptions
            .Select(m => new { m.TenantKey, m.Category, m.Code })
            .ToListAsync(cancellationToken);
        var existingSet = existingKeys
            .Select(x => $"{x.TenantKey}:{x.Category}:{x.Code}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = seedOptions
            .Where(item => !existingSet.Contains($"{item.TenantKey}:{item.Category}:{item.Code}"))
            .Select(item => new MasterOptionEntity
            {
                TenantKey = item.TenantKey, Category = item.Category, Code = item.Code,
                Name = item.Name, SortOrder = item.SortOrder, ParentCode = item.ParentCode, Description = item.Description,
                IsActive = item.IsActive, UpdatedUtc = masterNow
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            logger.LogInformation("Seeding {Count} new master option(s).", toAdd.Count);
            dbContext.MasterOptions.AddRange(toAdd);
        }

        // Fill in, never overwrite: a value seeded before it had a parent or description gets the
        // seed's, while anything HR has since typed into those fields is left alone.
        var detailedSeeds = seedOptions
            .Where(item => item.ParentCode is not null || item.Description is not null)
            .GroupBy(item => $"{item.TenantKey}:{item.Category}:{item.Code}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (detailedSeeds.Count > 0)
        {
            var detailedCategories = detailedSeeds.Values.Select(item => item.Category).Distinct().ToList();
            var incomplete = await dbContext.MasterOptions
                .Where(m => detailedCategories.Contains(m.Category) && (m.ParentCode == null || m.Description == null))
                .ToListAsync(cancellationToken);

            foreach (var row in incomplete)
            {
                if (detailedSeeds.TryGetValue($"{row.TenantKey}:{row.Category}:{row.Code}", out var seed))
                {
                    row.ParentCode ??= seed.ParentCode;
                    row.Description ??= seed.Description;
                }
            }
        }

        // Additive, like the masters above: a policy the client has since edited or retired is
        // left alone, and only codes that do not exist yet are inserted.
        var seedPolicies = await ReadJsonAsync<List<PolicySeedDto>>(
            Path.Combine(seedDataPath, "kapson-policies.json"), cancellationToken);

        var existingPolicyKeys = (await dbContext.Policies
                .Select(policy => new { policy.TenantKey, policy.Code })
                .ToListAsync(cancellationToken))
            .Select(policy => $"{policy.TenantKey}:{policy.Code}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var policiesToAdd = seedPolicies
            .Where(item => !existingPolicyKeys.Contains($"{item.TenantKey}:{item.Code}"))
            .Select(item => new PolicyEntity
            {
                TenantKey = item.TenantKey,
                Code = item.Code,
                Title = item.Title,
                Icon = item.Icon,
                Excerpt = item.Excerpt,
                SectionsJson = JsonSerializer.Serialize(item.Sections, JsonOptions),
                Version = "1",
                IsPublished = item.IsPublished,
                UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow),
                UpdatedUtc = masterNow
            })
            .ToList();

        if (policiesToAdd.Count > 0)
        {
            logger.LogInformation("Seeding {Count} new company policy/policies.", policiesToAdd.Count);
            dbContext.Policies.AddRange(policiesToAdd);
        }

        var seedDesignations = await ReadJsonAsync<List<DesignationSeedDto>>(
            Path.Combine(seedDataPath, "kapson-designations.json"), cancellationToken);

        var existingDesignationKeys = (await dbContext.Designations
                .Select(item => new { item.TenantKey, item.Code })
                .ToListAsync(cancellationToken))
            .Select(item => $"{item.TenantKey}:{item.Code}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var designationsToAdd = seedDesignations
            .Where(item => !existingDesignationKeys.Contains($"{item.TenantKey}:{item.Code}"))
            .Select(item => new DesignationEntity
            {
                TenantKey = item.TenantKey,
                Code = item.Code,
                Name = item.Name,
                GradeCode = item.GradeCode,
                DepartmentCode = item.DepartmentCode,
                Description = item.Description,
                SortOrder = item.SortOrder,
                IsActive = item.IsActive,
                UpdatedUtc = masterNow
            })
            .ToList();

        if (designationsToAdd.Count > 0)
        {
            logger.LogInformation("Seeding {Count} new designation(s).", designationsToAdd.Count);
            dbContext.Designations.AddRange(designationsToAdd);
        }

        var gradeSalarySeed = await ReadJsonAsync<GradeSalarySeedFileDto>(
            Path.Combine(seedDataPath, "kapson-grade-salaries.json"), cancellationToken);

        var existingGradeSalaryKeys = (await dbContext.GradeSalaryStructures
                .Select(item => new { item.TenantKey, item.GradeCode })
                .ToListAsync(cancellationToken))
            .Select(item => $"{item.TenantKey}:{item.GradeCode}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var defaultComponentsJson = JsonSerializer.Serialize(gradeSalarySeed.DefaultComponents, JsonOptions);

        var gradeSalariesToAdd = gradeSalarySeed.Grades
            .Where(item => !existingGradeSalaryKeys.Contains($"{item.TenantKey}:{item.GradeCode}"))
            .Select(item => new GradeSalaryEntity
            {
                TenantKey = item.TenantKey,
                GradeCode = item.GradeCode,
                BasicMonthly = item.BasicMonthly,
                ComponentsJson = defaultComponentsJson,
                UpdatedUtc = masterNow
            })
            .ToList();

        if (gradeSalariesToAdd.Count > 0)
        {
            logger.LogInformation("Seeding {Count} grade salary structure(s).", gradeSalariesToAdd.Count);
            dbContext.GradeSalaryStructures.AddRange(gradeSalariesToAdd);
        }

        if (!await dbContext.CareerSites.AnyAsync(cancellationToken))
        {
            var site = await ReadJsonAsync<CareerSiteConfigurationDto>(
                Path.Combine(seedDataPath, "kapson-career-site.json"),
                cancellationToken);
            var now = DateTimeOffset.UtcNow;
            site.UpdatedUtc = now;
            site.PublishedUtc = now;
            site.PublishedVersion = 1;
            var json = JsonSerializer.Serialize(site, JsonOptions);
            dbContext.CareerSites.Add(new CareerSiteEntity
            {
                TenantKey = site.TenantKey,
                DraftJson = json,
                PublishedJson = json,
                PublishedVersion = 1,
                UpdatedUtc = now,
                PublishedUtc = now
            });
        }

        if (!await dbContext.JobOpenings.AnyAsync(cancellationToken))
        {
            var jobs = await ReadJsonAsync<List<JobOpeningDto>>(
                Path.Combine(seedDataPath, "kapson-jobs.json"),
                cancellationToken);
            var now = DateTimeOffset.UtcNow;
            dbContext.JobOpenings.AddRange(jobs.Select(job => new JobOpeningEntity
            {
                TenantKey = job.TenantKey,
                Slug = job.Slug,
                Title = job.Title,
                Department = job.Department,
                Location = job.Location,
                EmploymentType = job.EmploymentType,
                WorkMode = job.WorkMode,
                Experience = job.Experience,
                Summary = job.Summary,
                ResponsibilitiesJson = JsonSerializer.Serialize(job.Responsibilities, JsonOptions),
                RequirementsJson = JsonSerializer.Serialize(job.Requirements, JsonOptions),
                IsPublished = job.IsPublished,
                PublishedUtc = job.IsPublished ? now : null,
                UpdatedUtc = now
            }));
        }

        await SeedLeaveAsync(seedDataPath, cancellationToken);
        await SeedShiftsAsync(seedDataPath, cancellationToken);

        // An advance created before Advance Type existed still needs one to show on the report;
        // "Other" until HR edits it. A loan never gets one — this touches Type = Advance only.
        foreach (var untyped in await dbContext.Loans
                     .Where(item => item.Type == "Advance" && item.AdvanceTypeCode == null)
                     .ToListAsync(cancellationToken))
        {
            untyped.AdvanceTypeCode = "OTHER";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Leave policies and public holidays, seeded additively by their natural keys. A tenant
    /// that has edited a policy keeps its edit: only codes that are missing are inserted.
    /// </summary>
    private async Task SeedLeaveAsync(string seedDataPath, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var policies = await ReadJsonAsync<List<LeavePolicySeedDto>>(
            Path.Combine(seedDataPath, "kapson-leave-policies.json"), cancellationToken);

        var existingPolicies = (await dbContext.LeavePolicies
                .Select(item => new { item.TenantKey, item.Code })
                .ToListAsync(cancellationToken))
            .Select(item => $"{item.TenantKey}:{item.Code}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        dbContext.LeavePolicies.AddRange(policies
            .Where(item => !existingPolicies.Contains($"{item.TenantKey}:{item.Code}"))
            .Select(item => new Workforce.LeavePolicyEntity
            {
                TenantKey = item.TenantKey,
                Code = item.Code,
                Name = item.Name,
                Unit = item.Unit,
                Version = item.Version,
                AnnualEntitlement = item.AnnualEntitlement,
                AccrualMode = item.AccrualMode,
                CarryForwardCap = item.CarryForwardCap,
                NoticeDays = item.NoticeDays,
                DocumentRequiredAboveDays = item.DocumentRequiredAboveDays,
                AllowsHalfDay = item.AllowsHalfDay,
                AllowsNegativeBalance = item.AllowsNegativeBalance,
                CountsNonWorkingDays = item.CountsNonWorkingDays,
                EligibilityText = item.EligibilityText,
                AccrualRuleText = item.AccrualRuleText,
                CarryForwardRuleText = item.CarryForwardRuleText,
                NoticeRuleText = item.NoticeRuleText,
                DocumentsJson = JsonSerializer.Serialize(item.Documents, JsonOptions),
                ToneKey = item.ToneKey ?? "blue",
                Emoji = item.Emoji ?? "🗓️",
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            }));

        // Fill in, never overwrite: a policy created before colours existed still carries the
        // column defaults, so it takes the seed's colour and icon. One HR has changed keeps its own.
        var seededLooks = policies
            .Where(item => item.ToneKey is not null || item.Emoji is not null)
            .ToDictionary(item => $"{item.TenantKey}:{item.Code}", StringComparer.OrdinalIgnoreCase);

        foreach (var existing in await dbContext.LeavePolicies
                     .Where(item => item.ToneKey == "blue" && item.Emoji == "🗓️")
                     .ToListAsync(cancellationToken))
        {
            if (seededLooks.TryGetValue($"{existing.TenantKey}:{existing.Code}", out var seed))
            {
                existing.ToneKey = seed.ToneKey ?? existing.ToneKey;
                existing.Emoji = seed.Emoji ?? existing.Emoji;
            }
        }

        // Only the fixed-date national holidays are seeded. Festival dates move every year and
        // vary by location, so they are left for the tenant to configure rather than guessed.
        var holidays = await ReadJsonAsync<List<LeaveHolidaySeedDto>>(
            Path.Combine(seedDataPath, "kapson-holidays.json"), cancellationToken);

        var existingHolidays = (await dbContext.LeaveHolidays
                .Select(item => new { item.TenantKey, item.Date, item.LocationCode })
                .ToListAsync(cancellationToken))
            .Select(item => $"{item.TenantKey}:{item.Date:yyyy-MM-dd}:{item.LocationCode}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        dbContext.LeaveHolidays.AddRange(holidays
            .Where(item => !existingHolidays.Contains(
                $"{item.TenantKey}:{item.Date:yyyy-MM-dd}:{item.LocationCode}"))
            .Select(item => new Workforce.LeaveHolidayEntity
            {
                TenantKey = item.TenantKey,
                Date = item.Date,
                Name = item.Name,
                LocationCode = item.LocationCode
            }));
    }

    /// <summary>
    /// Shift definitions, seeded additively by code. A tenant that has edited a shift keeps its
    /// edit, and exactly one shift is left marked default.
    /// </summary>
    private async Task SeedShiftsAsync(string seedDataPath, CancellationToken cancellationToken)
    {
        var shifts = await ReadJsonAsync<List<ShiftSeedDto>>(
            Path.Combine(seedDataPath, "kapson-shifts.json"), cancellationToken);

        var existing = (await dbContext.ShiftDefinitions
                .Select(item => new { item.TenantKey, item.Code })
                .ToListAsync(cancellationToken))
            .Select(item => $"{item.TenantKey}:{item.Code}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tenantsWithDefault = (await dbContext.ShiftDefinitions
                .Where(item => item.IsDefault)
                .Select(item => item.TenantKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;

        foreach (var shift in shifts)
        {
            if (existing.Contains($"{shift.TenantKey}:{shift.Code}"))
            {
                continue;
            }

            // Never seed a second default over a tenant that already has one.
            var isDefault = shift.IsDefault && !tenantsWithDefault.Contains(shift.TenantKey);

            if (isDefault)
            {
                tenantsWithDefault.Add(shift.TenantKey);
            }

            dbContext.ShiftDefinitions.Add(new Workforce.ShiftDefinitionEntity
            {
                TenantKey = shift.TenantKey,
                Code = shift.Code,
                Name = shift.Name,
                StartTime = TimeOnly.Parse(shift.StartTime),
                EndTime = TimeOnly.Parse(shift.EndTime),
                BreakMinutes = shift.BreakMinutes,
                GraceMinutes = shift.GraceMinutes,
                WorkingDays = shift.WorkingDays,
                CrossesMidnight = shift.CrossesMidnight,
                IsDefault = isDefault,
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            });
        }
    }

    private sealed record ShiftSeedDto(
        string TenantKey,
        string Code,
        string Name,
        string StartTime,
        string EndTime,
        int BreakMinutes,
        int GraceMinutes,
        string WorkingDays,
        bool CrossesMidnight,
        bool IsDefault);

    private sealed record LeavePolicySeedDto(
        string TenantKey,
        string Code,
        string Name,
        string Unit,
        string Version,
        decimal AnnualEntitlement,
        string AccrualMode,
        decimal CarryForwardCap,
        int NoticeDays,
        decimal? DocumentRequiredAboveDays,
        bool AllowsHalfDay,
        bool AllowsNegativeBalance,
        bool CountsNonWorkingDays,
        string EligibilityText,
        string AccrualRuleText,
        string CarryForwardRuleText,
        string NoticeRuleText,
        IReadOnlyList<string> Documents,
        string? ToneKey = null,
        string? Emoji = null);

    private sealed record LeaveHolidaySeedDto(
        string TenantKey,
        DateOnly Date,
        string Name,
        string? LocationCode);

    private sealed record PolicySeedDto(
        string TenantKey,
        string Code,
        string Title,
        string Icon,
        string Excerpt,
        IReadOnlyList<PolicySectionSeedDto> Sections,
        bool IsPublished);

    private sealed record PolicySectionSeedDto(
        string Heading,
        IReadOnlyList<string> Points,
        string? Intro,
        IReadOnlyList<PolicySubsectionSeedDto>? Subsections);

    private sealed record PolicySubsectionSeedDto(string Label, IReadOnlyList<string> Points);

    private sealed record DesignationSeedDto(
        string TenantKey,
        string Code,
        string Name,
        string GradeCode,
        string DepartmentCode,
        string? Description,
        int SortOrder,
        bool IsActive);

    private sealed record GradeSalarySeedFileDto(
        IReadOnlyList<GradeSalaryComponentSeedDto> DefaultComponents,
        IReadOnlyList<GradeSalarySeedDto> Grades);

    private sealed record GradeSalaryComponentSeedDto(
        string Kind,
        string ComponentCode,
        string CalculationType,
        decimal Value,
        decimal? CapAmount,
        decimal? GrossCeiling);

    private sealed record GradeSalarySeedDto(string TenantKey, string GradeCode, decimal BasicMonthly);

    private static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Required initial database seed file was not found.", path);
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Seed file '{path}' did not contain valid data.");
    }
}
