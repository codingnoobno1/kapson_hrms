namespace Vispl.HRManagement.DL.Recruitment;

public sealed class JobOpeningEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public string Experience { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ResponsibilitiesJson { get; set; } = "[]";
    public string RequirementsJson { get; set; } = "[]";

    /// <summary>
    /// Lifecycle state: Draft, Active, Inactive or Expired. Authoritative.
    /// IsPublished is kept in sync (Active means published) so existing career-site
    /// queries keep working; it should be retired once those move onto Status.
    /// </summary>
    public string Status { get; set; } = "Draft";

    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }

    /// <summary>After this date the opening stops accepting applications.</summary>
    public DateOnly? ExpiresOn { get; set; }

    public decimal? SalaryMinimum { get; set; }
    public decimal? SalaryMaximum { get; set; }
    public string? SalaryCurrencyCode { get; set; }

    /// <summary>DocumentCategory master codes the applicant must upload, as a JSON array.</summary>
    public string RequiredDocumentCodesJson { get; set; } = "[]";

    /// <summary>Screening questions shown on the apply form, as a JSON array.</summary>
    public string ScreeningQuestionsJson { get; set; } = "[]";

    public DateTimeOffset? StatusChangedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
