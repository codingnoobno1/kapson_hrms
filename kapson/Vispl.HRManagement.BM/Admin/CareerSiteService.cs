using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class CareerSiteService(ICareerSiteRepository repository) : ICareerSiteService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CareerSiteConfigurationDto?> GetDraftAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var record = await repository.GetAsync(NormalizeTenantKey(tenantKey), cancellationToken);
        return record is null ? null : NormalizeConfiguration(Deserialize(record.DraftJson, record));
    }

    public async Task<CareerSiteConfigurationDto?> GetPublishedAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var record = await repository.GetAsync(NormalizeTenantKey(tenantKey), cancellationToken);
        return string.IsNullOrWhiteSpace(record?.PublishedJson)
            ? null
            : NormalizeConfiguration(Deserialize(record.PublishedJson, record));
    }

    public async Task SaveDraftAsync(
        CareerSiteConfigurationDto configuration,
        CancellationToken cancellationToken = default)
    {
        configuration.TenantKey = NormalizeTenantKey(configuration.TenantKey);
        var existing = await repository.GetAsync(configuration.TenantKey, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        configuration.UpdatedUtc = now;
        configuration.PublishedVersion = existing?.PublishedVersion ?? 0;
        configuration.PublishedUtc = existing?.PublishedUtc;

        await repository.UpsertAsync(new CareerSiteRecord
        {
            Id = existing?.Id ?? 0,
            TenantKey = configuration.TenantKey,
            DraftJson = JsonSerializer.Serialize(configuration, JsonOptions),
            PublishedJson = existing?.PublishedJson,
            PublishedVersion = existing?.PublishedVersion ?? 0,
            PublishedUtc = existing?.PublishedUtc,
            UpdatedUtc = now
        }, cancellationToken);
    }

    public async Task PublishAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeTenantKey(tenantKey);
        var existing = await repository.GetAsync(normalizedKey, cancellationToken)
            ?? throw new InvalidOperationException($"No career-site draft exists for tenant '{normalizedKey}'.");
        var now = DateTimeOffset.UtcNow;
        var publishedVersion = existing.PublishedVersion + 1;
        var draft = JsonSerializer.Deserialize<CareerSiteConfigurationDto>(existing.DraftJson, JsonOptions)
            ?? throw new InvalidDataException("The stored career-site draft is invalid.");
        draft.PublishedVersion = publishedVersion;
        draft.PublishedUtc = now;
        draft.UpdatedUtc = now;
        var publishedJson = JsonSerializer.Serialize(draft, JsonOptions);

        existing.DraftJson = publishedJson;
        existing.PublishedJson = publishedJson;
        existing.PublishedVersion = publishedVersion;
        existing.PublishedUtc = now;
        existing.UpdatedUtc = now;
        await repository.UpsertAsync(existing, cancellationToken);
    }

    private static CareerSiteConfigurationDto Deserialize(string json, CareerSiteRecord record)
    {
        var configuration = JsonSerializer.Deserialize<CareerSiteConfigurationDto>(json, JsonOptions)
            ?? throw new InvalidDataException("The stored career-site configuration is invalid.");
        configuration.PublishedVersion = record.PublishedVersion;
        configuration.PublishedUtc = record.PublishedUtc;
        configuration.UpdatedUtc = record.UpdatedUtc;
        return configuration;
    }

    internal static List<PageSectionDto> CreateDefaultSections() =>
    [
        new() { Key = "hero", DisplayName = "Hero", SortOrder = 1, IsVisible = true },
        new() { Key = "intro", DisplayName = "Introduction", SortOrder = 2, IsVisible = true },
        new() { Key = "openings", DisplayName = "Open positions", SortOrder = 3, IsVisible = true },
        new() { Key = "values", DisplayName = "Values", SortOrder = 4, IsVisible = true },
        new() { Key = "hiring", DisplayName = "Hiring process", SortOrder = 5, IsVisible = true },
        new() { Key = "closing", DisplayName = "Closing call to action", SortOrder = 6, IsVisible = true }
    ];

    private static CareerSiteConfigurationDto NormalizeConfiguration(CareerSiteConfigurationDto configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.TemplateKey)) configuration.TemplateKey = "executive";
        if (configuration.HomeSections.Count == 0) configuration.HomeSections = CreateDefaultSections();
        return configuration;
    }

    private static string NormalizeTenantKey(string tenantKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        return tenantKey.Trim().ToLowerInvariant();
    }
}
