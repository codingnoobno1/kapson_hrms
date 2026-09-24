using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

public static class MasterCategories
{
    // Employee Masters
    public const string Gender = "Gender";
    public const string EducationLevel = "EducationLevel";
    public const string Qualification = "Qualification";
    public const string Religion = "Religion";
    public const string Nationality = "Nationality";
    public const string BloodGroup = "BloodGroup";
    public const string Occupation = "Occupation";
    public const string Skill = "Skill";
    public const string SkillCategory = "SkillCategory";

    /// <summary>Loan and Advance Report: Festival, Medical, Travel, Education, Other.</summary>
    public const string AdvanceType = "AdvanceType";

    // Personal & Demographics
    public const string MaritalStatus = "MaritalStatus";
    public const string Relation = "Relation";

    // Payroll & Compensation
    public const string Grade = "Grade";
    public const string AllowanceType = "AllowanceType";
    public const string DeductionType = "DeductionType";

    // Leave & Attendance
    public const string LeaveType = "LeaveType";
    public const string LeavingReason = "LeavingReason";
    public const string ExitType = "ExitType";
    public const string HolidayType = "HolidayType";
    public const string EmploymentStatus = "EmploymentStatus";

    // Organization & Location
    public const string Division = "Division";
    public const string Country = "Country";
    public const string State = "State";
    public const string City = "City";
    public const string DocumentCategory = "DocumentCategory";

    // Recruitment (existing)
    public const string Department = "Department";
    public const string Location = "Location";
    public const string JobType = "JobType";
    public const string Experience = "Experience";
    public const string WorkMode = "WorkMode";

    public static IReadOnlyList<string> All { get; } =
    [
        Gender, EducationLevel, Qualification, Religion, Nationality, BloodGroup, Occupation, Skill, SkillCategory,
        MaritalStatus, Relation, AdvanceType,
        Grade, AllowanceType, DeductionType,
        LeaveType, LeavingReason, ExitType, HolidayType, EmploymentStatus,
        Division, Country, State, City, DocumentCategory,
        Department, Location, JobType, Experience, WorkMode
    ];

    public static string DisplayName(string category) => category switch
    {
        Gender => "Gender",
        EducationLevel => "Education Level",
        Qualification => "Qualification",
        Religion => "Religion",
        Nationality => "Nationality",
        BloodGroup => "Blood Group",
        Occupation => "Occupation",
        Skill => "Skill",
        SkillCategory => "Skill Category",
        AdvanceType => "Advance Type",
        MaritalStatus => "Marital Status",
        Relation => "Relation",
        Grade => "Grade / Level",
        AllowanceType => "Allowance Type",
        DeductionType => "Deduction Type",
        LeaveType => "Leave Type",
        LeavingReason => "Leaving Reason",
        ExitType => "Exit Type",
        HolidayType => "Holiday Type",
        EmploymentStatus => "Employment Status",
        Division => "Division / Office",
        Country => "Country",
        State => "State / Province",
        City => "City",
        DocumentCategory => "Document Category",
        Department => "Department",
        Location => "Location",
        JobType => "Job Type",
        Experience => "Experience Level",
        WorkMode => "Work Mode",
        _ => category
    };

    /// <summary>
    /// The category a value of <paramref name="category"/> is filed under, if any: a Skill belongs
    /// to a Skill Category (the Category column of the client-locked Skills screen).
    /// </summary>
    public static string? ParentOf(string category) => category switch
    {
        Skill => SkillCategory,

        // Sub Department (Report Configuration): a department can name another department of the
        // same category as its parent, rather than needing a second category just for that.
        Department => Department,
        _ => null
    };

    public static IReadOnlyList<(string Group, string Icon, string[] Categories)> Groups { get; } =
    [
        ("Employee Masters",      "👤", [Gender, EducationLevel, Qualification, Religion, Nationality, BloodGroup, Occupation, Skill, SkillCategory]),
        ("Payroll",               "💰", [AdvanceType]),
        ("Personal & Demographics","👨‍👩‍👧", [MaritalStatus, Relation]),
        ("Payroll & Compensation", "💰", [Grade, AllowanceType, DeductionType]),
        ("Leave & Attendance",    "📅", [LeaveType, LeavingReason, ExitType, HolidayType, EmploymentStatus]),
        ("Organization & Location","🏢", [Division, Country, State, City, DocumentCategory]),
        ("Recruitment",           "📋", [Department, Location, JobType, Experience, WorkMode])
    ];
}

public sealed class MasterOptionDto
{
    public long Id { get; set; }
    [Required, RegularExpression("^[a-z0-9-]+$")] public string TenantKey { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Category { get; set; } = string.Empty;
    [Required, RegularExpression("^[A-Z0-9_-]+$")] public string Code { get; set; } = string.Empty;
    [Required, StringLength(160)] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    /// <summary>
    /// The code of this value's parent in <see cref="MasterCategories.ParentOf"/> — a skill's skill
    /// category. Null for categories without a parent, and optional for those with one.
    /// </summary>
    [StringLength(80)] public string? ParentCode { get; set; }

    [StringLength(500)] public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedUtc { get; set; }
}
