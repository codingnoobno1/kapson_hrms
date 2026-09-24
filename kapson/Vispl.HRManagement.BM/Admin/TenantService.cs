using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class TenantService(ITenantRepository tenantRepository, ICareerSiteRepository careerSiteRepository) : ITenantService
{
    /// <summary>Words that already name a page under /hr, /career or /admin.</summary>
    private static readonly HashSet<string> ReservedTenantKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "hr", "admin", "career", "identity", "api", "dashboard", "change-password",
        "access-denied", "login", "logout", "careers", "about"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<TenantDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await tenantRepository.GetAllAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<TenantDto?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        var record = await tenantRepository.GetByKeyAsync(Normalize(tenantKey), cancellationToken);
        return record is null ? null : ToDto(record);
    }

    public async Task<long> SaveAsync(TenantDto tenant, CancellationToken cancellationToken = default)
    {
        tenant.TenantKey = Normalize(tenant.TenantKey);

        // Client keys become a path segment under /hr, so one that collides with a page would
        // make that page unreachable for everybody.
        if (ReservedTenantKeys.Contains(tenant.TenantKey))
        {
            throw new InvalidOperationException(
                $"'{tenant.TenantKey}' cannot be used as a client key: it is a reserved portal address.");
        }

        var existing = await tenantRepository.GetByKeyAsync(tenant.TenantKey, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var id = await tenantRepository.UpsertAsync(new TenantRecord
        {
            Id = tenant.Id,
            TenantKey = tenant.TenantKey,
            DisplayName = tenant.DisplayName.Trim(),
            PrimaryDomain = string.IsNullOrWhiteSpace(tenant.PrimaryDomain) ? null : tenant.PrimaryDomain.Trim().ToLowerInvariant(),
            TimeZoneId = string.IsNullOrWhiteSpace(tenant.TimeZoneId) ? "UTC" : tenant.TimeZoneId.Trim(),
            RetirementAgeYears = tenant.RetirementAgeYears is >= 40 and <= 75 ? tenant.RetirementAgeYears : 58,
            HrPortalEnabled = tenant.HrPortalEnabled,
            IsActive = tenant.IsActive,
            CreatedUtc = existing?.CreatedUtc ?? now,
            UpdatedUtc = now
        }, cancellationToken);

        if (await careerSiteRepository.GetAsync(tenant.TenantKey, cancellationToken) is null)
        {
            var configuration = CreateDefaultConfiguration(tenant.TenantKey, tenant.DisplayName.Trim(), now);
            await careerSiteRepository.UpsertAsync(new CareerSiteRecord
            {
                Id = 0, TenantKey = tenant.TenantKey,
                DraftJson = JsonSerializer.Serialize(configuration, JsonOptions), PublishedJson = null,
                PublishedVersion = 0, PublishedUtc = null, UpdatedUtc = now
            }, cancellationToken);
        }

        return id;
    }

    private static CareerSiteConfigurationDto CreateDefaultConfiguration(string key, string name, DateTimeOffset now) => new()
    {
        TenantKey = key, CompanyName = name, PortalName = "Careers", TemplateKey = "executive",
        BrandInitial = name[..1].ToUpperInvariant(), SeoTitle = $"Careers at {name}", SeoDescription = $"Explore opportunities at {name}.",
        HeroEyebrow = $"CAREERS AT {name.ToUpperInvariant()}", HeroTitle = "Build your next chapter with us.",
        HeroSummary = "Discover meaningful work, ambitious teams and room to grow.", HeroCardTitle = "Your work matters here.", HeroCardSubtitle = $"Make your mark with {name}.",
        IntroEyebrow = "WHO WE ARE", IntroTitle = "A place for ambitious people to do meaningful work.", IntroBody = "Tell candidates what makes your organisation a great place to build a career.",
        ValuesTitle = "Bring your strengths. Do your best work.", HiringTitle = "A clear, people-first hiring process.", HiringSummary = "Explain what candidates can expect from application to offer.",
        ClosingTitle = "Ready for your next opportunity?", ClosingSummary = "Explore our open positions and find where you can make an impact.",
        AboutHeroTitle = $"Meet {name}.", AboutHeroSummary = "Share your organisation’s purpose, history and ambition.", AboutStoryTitle = "Our story", AboutStoryBody = "Add your organisation’s story from the Admin portal.",
        CareersTitle = "Find work that moves you forward.", CareersSummary = "Explore current opportunities across our teams.",
        FooterTagline = "Building the future through remarkable people.", ContactAddress = "Add company address", ContactEmail = $"careers@{key}.com",
        NavigationItems = [
            new() { Label = "Home", Url = $"/career/{key}", SortOrder = 1 },
            new() { Label = "About us", Url = $"/career/{key}/about", SortOrder = 2 },
            new() { Label = "Open positions", Url = $"/career/{key}/careers", SortOrder = 3 }
        ],
        HeroStatistics = [new() { Code = "01", Title = "People", Body = "Growing together", SortOrder = 1 }],
        Values = [new() { Code = "01", Title = "Grow together", Body = "Describe the value candidates can expect.", SortOrder = 1 }],
        HiringSteps = [new() { Code = "01", Title = "Apply", Body = "Choose a role and submit your application.", SortOrder = 1 }],
        Principles = [new() { Code = "01", Title = "People first", Body = "Describe a principle that guides your organisation.", SortOrder = 1 }],
        HomeSections = CareerSiteService.CreateDefaultSections(), UpdatedUtc = now
    };

    private static TenantDto ToDto(TenantRecord record) => new()
    {
        Id = record.Id, TenantKey = record.TenantKey, DisplayName = record.DisplayName, PrimaryDomain = record.PrimaryDomain,
        TimeZoneId = record.TimeZoneId, RetirementAgeYears = record.RetirementAgeYears, HrPortalEnabled = record.HrPortalEnabled, IsActive = record.IsActive, CreatedUtc = record.CreatedUtc, UpdatedUtc = record.UpdatedUtc
    };

    private static string Normalize(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim().ToLowerInvariant(); }
}
