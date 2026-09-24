using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Recruitment;

public sealed class JobOpeningDto
{
    public long Id { get; set; }

    [Required, RegularExpression("^[a-z0-9-]+$")]
    public string TenantKey { get; set; } = string.Empty;

    [Required, RegularExpression("^[a-z0-9-]+$")]
    public string Slug { get; set; } = string.Empty;

    [Required, StringLength(180)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(120)] public string Department { get; set; } = string.Empty;
    [Required, StringLength(180)] public string Location { get; set; } = string.Empty;
    [Required, StringLength(80)] public string EmploymentType { get; set; } = string.Empty;
    [Required, StringLength(80)] public string WorkMode { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Experience { get; set; } = string.Empty;
    [Required, StringLength(1200)] public string Summary { get; set; } = string.Empty;
    public List<string> Responsibilities { get; set; } = [];
    public List<string> Requirements { get; set; } = [];
    /// <summary>Draft, Active, Inactive or Expired. Authoritative; IsPublished mirrors it.</summary>
    [StringLength(40)] public string Status { get; set; } = "Draft";

    public bool IsPublished { get; set; }

    /// <summary>Applications close after this date.</summary>
    public DateOnly? ExpiresOn { get; set; }

    [Range(0, 999999999)] public decimal? SalaryMinimum { get; set; }
    [Range(0, 999999999)] public decimal? SalaryMaximum { get; set; }
    [StringLength(3)] public string? SalaryCurrencyCode { get; set; }

    public DateTimeOffset? StatusChangedUtc { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
