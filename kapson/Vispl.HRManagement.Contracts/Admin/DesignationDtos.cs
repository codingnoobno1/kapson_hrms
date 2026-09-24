using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

// Grade and department are codes into their masters; the resolved names travel with them.
public sealed record DesignationDto(
    string Id,
    string Code,
    string Name,
    string GradeCode,
    string? GradeName,
    string DepartmentCode,
    string? DepartmentName,
    string? Description,
    int SortOrder,
    bool IsActive,
    DateTimeOffset UpdatedUtc);

public sealed class SaveDesignationRequest
{
    public string? Id { get; set; }

    [Required, StringLength(40)]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Code may only contain letters, numbers, underscores and hyphens.")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a grade.")]
    public string GradeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a department.")]
    public string DepartmentCode { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
