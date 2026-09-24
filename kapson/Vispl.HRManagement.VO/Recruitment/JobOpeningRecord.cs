namespace Vispl.HRManagement.VO.Recruitment;

public sealed class JobOpeningRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string Slug { get; set; }
    public required string Title { get; set; }
    public required string Department { get; set; }
    public required string Location { get; set; }
    public required string EmploymentType { get; set; }
    public required string WorkMode { get; set; }
    public required string Experience { get; set; }
    public required string Summary { get; set; }
    public required string ResponsibilitiesJson { get; set; }
    public required string RequirementsJson { get; set; }
    /// <summary>Draft, Active, Inactive or Expired. Authoritative; IsPublished mirrors it.</summary>
    public string Status { get; set; } = "Draft";

    public bool IsPublished { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public decimal? SalaryMinimum { get; set; }
    public decimal? SalaryMaximum { get; set; }
    public string? SalaryCurrencyCode { get; set; }
    public string RequiredDocumentCodesJson { get; set; } = "[]";
    public string ScreeningQuestionsJson { get; set; } = "[]";
    public DateTimeOffset? StatusChangedUtc { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
