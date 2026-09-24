using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

public sealed class CareerSiteConfigurationDto
{
    [Required, RegularExpression("^[a-z0-9-]+$")]
    public string TenantKey { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string CompanyName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string PortalName { get; set; } = "Careers";

    [Required, StringLength(80)]
    public string TemplateKey { get; set; } = "executive";

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [Required, StringLength(4)]
    public string BrandInitial { get; set; } = string.Empty;

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")]
    public string PrimaryColor { get; set; } = "#0b1530";

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")]
    public string AccentColor { get; set; } = "#e3b13d";

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")]
    public string SurfaceColor { get; set; } = "#f7f3e9";

    [Required, StringLength(180)] public string SeoTitle { get; set; } = string.Empty;
    [Required, StringLength(320)] public string SeoDescription { get; set; } = string.Empty;
    [Required, StringLength(80)] public string HeroEyebrow { get; set; } = string.Empty;
    [Required, StringLength(240)] public string HeroTitle { get; set; } = string.Empty;
    [Required, StringLength(500)] public string HeroSummary { get; set; } = string.Empty;
    [Required, StringLength(160)] public string HeroCardTitle { get; set; } = string.Empty;
    [Required, StringLength(240)] public string HeroCardSubtitle { get; set; } = string.Empty;
    [Required, StringLength(80)] public string IntroEyebrow { get; set; } = string.Empty;
    [Required, StringLength(240)] public string IntroTitle { get; set; } = string.Empty;
    [Required, StringLength(1200)] public string IntroBody { get; set; } = string.Empty;
    [Required, StringLength(240)] public string ValuesTitle { get; set; } = string.Empty;
    [Required, StringLength(240)] public string HiringTitle { get; set; } = string.Empty;
    [Required, StringLength(600)] public string HiringSummary { get; set; } = string.Empty;
    [Required, StringLength(240)] public string ClosingTitle { get; set; } = string.Empty;
    [Required, StringLength(600)] public string ClosingSummary { get; set; } = string.Empty;
    [Required, StringLength(240)] public string AboutHeroTitle { get; set; } = string.Empty;
    [Required, StringLength(600)] public string AboutHeroSummary { get; set; } = string.Empty;
    [Required, StringLength(240)] public string AboutStoryTitle { get; set; } = string.Empty;
    [Required, StringLength(2400)] public string AboutStoryBody { get; set; } = string.Empty;
    [Required, StringLength(240)] public string CareersTitle { get; set; } = string.Empty;
    [Required, StringLength(600)] public string CareersSummary { get; set; } = string.Empty;
    [Required, StringLength(500)] public string FooterTagline { get; set; } = string.Empty;
    [Required, StringLength(240)] public string ContactAddress { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(240)] public string ContactEmail { get; set; } = string.Empty;

    public List<NavigationItemDto> NavigationItems { get; set; } = [];
    public List<ContentCardDto> HeroStatistics { get; set; } = [];
    public List<ContentCardDto> Values { get; set; } = [];
    public List<ContentCardDto> HiringSteps { get; set; } = [];
    public List<ContentCardDto> Principles { get; set; } = [];
    public List<PageSectionDto> HomeSections { get; set; } = [];

    public int PublishedVersion { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

public sealed class NavigationItemDto
{
    [Required, StringLength(80)] public string Label { get; set; } = string.Empty;
    [Required, StringLength(300)] public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class ContentCardDto
{
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(180)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(800)] public string Body { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class PageSectionDto
{
    [Required, StringLength(80)] public string Key { get; set; } = string.Empty;
    [Required, StringLength(120)] public string DisplayName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}
