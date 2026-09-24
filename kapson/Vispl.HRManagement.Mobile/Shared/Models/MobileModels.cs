namespace Vispl.HRManagement.Mobile.Shared.Models;

public sealed record KpiMetric(string Title, string Value, string Accent, string Symbol);

public sealed record InterviewSummary(
    long Id,
    string CandidateName,
    string JobTitle,
    DateTimeOffset StartsAt,
    string Round,
    string Status);

public sealed record JobSummary(
    long Id,
    string Title,
    string Department,
    string Location,
    string EmploymentType,
    string Experience,
    string Salary,
    string Status,
    DateTimeOffset ExpiresAt,
    string Summary,
    IReadOnlyList<string> Responsibilities,
    IReadOnlyList<string> Requirements);

public sealed record ApplicantSummary(
    long Id,
    string Name,
    string JobTitle,
    string Department,
    string Email,
    string Phone,
    string Status,
    decimal Rating,
    string Experience,
    string Education,
    string Location,
    DateTimeOffset AppliedAt);

public sealed record NotificationSummary(
    long Id,
    string Title,
    string Message,
    DateTimeOffset CreatedAt,
    string Category,
    bool IsRead);
