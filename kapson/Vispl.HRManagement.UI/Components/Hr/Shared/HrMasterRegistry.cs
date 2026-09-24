using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.UI.Components.Hr.Shared;

/// <summary>
/// What a card in the "APP Configuration" catalogue is actually backed by.
///
/// <c>GenericMaster</c> means the card is one or more <see cref="MasterCategories"/> values,
/// already stored and served by the exact same <c>IMasterDataService</c> the Admin portal's
/// Masters page (`/admin/masters`) uses — dynamic routes in this HR portal read and write through
/// that same service, never a second copy of the rule.
///
/// <c>DedicatedPage</c> means the card has its own relational entity and its own page, which
/// claims the card's /hr/settings/masters/{key} address with a literal route.
///
/// <c>ExistingModule</c> means the card names a real feature that already exists, but as its own
/// module (Shift, the holiday calendar) rather than a flat value list — it doesn't fit
/// <c>MasterOptionDto</c>'s shape and doesn't have an HR-portal route yet.
///
/// <c>NotBuilt</c> means there is no backend at all — a relational entity (a Department/
/// Sub-Department hierarchy) or a linking table (Grade↔Allowance, State↔City) that a flat
/// code/name list cannot represent.
/// </summary>
public enum HrMasterKind
{
    GenericMaster,
    DedicatedPage,
    ExistingModule,
    NotBuilt
}

/// <summary>One card in the App Configuration menu (scopedocs/web-portal/08-app-menu-design-layout.md §4).</summary>
public sealed record HrMasterEntry(
    string Key,
    string Title,
    string Subtitle,
    string Icon,
    string Section,
    HrMasterKind Kind,
    IReadOnlyList<string> Categories)
{
    public bool IsReady => Kind is HrMasterKind.GenericMaster or HrMasterKind.DedicatedPage;

    /// <summary>What one value is called in the "{ItemLabel} Name" column, e.g. "Skill" on the Skills page.</summary>
    public string? ItemLabel { get; init; }

    /// <summary>Header of the parent column (MasterCategories.ParentOf), e.g. "Category".</summary>
    public string? ParentLabel { get; init; }

    public bool ShowDescription { get; init; }

    /// <summary>The extra columns a client-locked screen shows beyond S.No., Name, Code, Status.</summary>
    public HrMasterEntry WithColumns(string itemLabel, string? parentLabel = null, bool description = false) =>
        this with { ItemLabel = itemLabel, ParentLabel = parentLabel, ShowDescription = description };

    public static HrMasterEntry Generic(string key, string title, string subtitle, string icon, string section, params string[] categories) =>
        new(key, title, subtitle, icon, section, HrMasterKind.GenericMaster, categories);

    public static HrMasterEntry Dedicated(string key, string title, string subtitle, string icon, string section) =>
        new(key, title, subtitle, icon, section, HrMasterKind.DedicatedPage, []);

    public static HrMasterEntry Module(string key, string title, string subtitle, string icon, string section) =>
        new(key, title, subtitle, icon, section, HrMasterKind.ExistingModule, []);

    public static HrMasterEntry Missing(string key, string title, string subtitle, string icon, string section) =>
        new(key, title, subtitle, icon, section, HrMasterKind.NotBuilt, []);
}

/// <summary>
/// The 26-card App Configuration catalogue, mapped onto what the backend has today. This is the
/// one place that mapping is recorded — a page never guesses which of the 26 cards has real data
/// behind it, it asks the registry.
///
/// Section order matches the client-locked design's four-column layout exactly
/// (scopedocs/web-portal/08-app-menu-design-layout.md §4): Employee Masters, Payroll &amp;
/// Compensation, Leave &amp; Attendance, Personal &amp; Demographics, Organization &amp; Location,
/// Document Management, Other.
/// </summary>
public static class HrMasterRegistry
{
    public const string EmployeeMasters = "Employee Masters";
    public const string PayrollAndCompensation = "Payroll & Compensation";
    public const string LeaveAndAttendance = "Leave & Attendance";
    public const string PersonalAndDemographics = "Personal & Demographics";
    public const string OrganizationAndLocation = "Organization & Location";
    public const string DocumentManagement = "Document Management";
    public const string Other = "Other";

    /// <summary>
    /// The fifth column: configuration the product needs that the client-locked 26-card design does
    /// not name. Kept apart so the four locked columns stay exactly as approved.
    /// </summary>
    public const string OtherConfigurations = "Other Configurations";

    /// <summary>
    /// The locked column layout: which sections stack in which column, top to bottom. Column 5 is
    /// ours, not the design's.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> ColumnLayout { get; } =
    [
        [EmployeeMasters],
        [PayrollAndCompensation, LeaveAndAttendance],
        [PersonalAndDemographics],
        [OrganizationAndLocation, DocumentManagement, Other],
        [OtherConfigurations]
    ];

    public static IReadOnlyList<HrMasterEntry> Entries { get; } =
    [
        // Employee Masters
        HrMasterEntry.Dedicated("designation", "Designation", "Manage job designations", "🔤", EmployeeMasters),
        HrMasterEntry.Generic("department-sub-department", "Department & Sub Department", "Configure department structure", "🏛️", EmployeeMasters, MasterCategories.Department)
            .WithColumns("Department", parentLabel: "Parent Department"),
        HrMasterEntry.Generic("gender", "Gender", "Configure gender categories", "👥", EmployeeMasters, MasterCategories.Gender),
        HrMasterEntry.Generic("qualification", "Qualification", "Manage qualifications", "🎓", EmployeeMasters, MasterCategories.Qualification),
        HrMasterEntry.Generic("skills", "Skills", "Manage employee skills", "⭐", EmployeeMasters, MasterCategories.Skill)
            .WithColumns("Skill", parentLabel: "Category", description: true),
        HrMasterEntry.Generic("education-level", "Education Level", "Configure education levels & certifications", "🎓", EmployeeMasters, MasterCategories.EducationLevel),
        HrMasterEntry.Generic("religion", "Religion", "Manage religion classifications", "🕉️", EmployeeMasters, MasterCategories.Religion),
        HrMasterEntry.Generic("nationality", "Nationality", "Manage nationalities and countries", "🌍", EmployeeMasters, MasterCategories.Nationality),
        HrMasterEntry.Generic("blood-group", "Blood Group", "Manage blood groups and health info", "🩸", EmployeeMasters, MasterCategories.BloodGroup),

        // Payroll & Compensation
        HrMasterEntry.Generic("allowance-deduction", "Allowance/Deduction", "Manage salary allowances & deductions", "💰", PayrollAndCompensation, MasterCategories.AllowanceType, MasterCategories.DeductionType),
        HrMasterEntry.Generic("grade", "Grade", "Configure employee grades", "📊", PayrollAndCompensation, MasterCategories.Grade),
        HrMasterEntry.Dedicated("link-allowances-deductions-with-grade", "Link Allowances / Deductions With Grade", "Allocate Allowances/Deductions with Grade", "📊", PayrollAndCompensation),

        // Leave & Attendance
        HrMasterEntry.Module("shift", "Shift", "Configure work shifts", "💰", LeaveAndAttendance),
        HrMasterEntry.Missing("link-employee-to-shift", "Link Employee to Shift", "Assign employees to shifts", "🔗", LeaveAndAttendance),
        HrMasterEntry.Module("holiday", "Holiday", "Manage holidays and festivals", "🎉", LeaveAndAttendance),
        HrMasterEntry.Generic("leave-type", "Leave Type", "Manage leave types and policies", "⛱️", LeaveAndAttendance, MasterCategories.LeaveType),
        HrMasterEntry.Generic("leaving-reason", "Leaving Reason", "Manage exit reasons & resignation categories", "🚪", LeaveAndAttendance, MasterCategories.LeavingReason, MasterCategories.ExitType),

        // Personal & Demographics
        HrMasterEntry.Generic("marital-status", "Marital Status", "Configure marital status options", "🎎", PersonalAndDemographics, MasterCategories.MaritalStatus),
        HrMasterEntry.Generic("relation", "Relation", "Configure family relationships", "🧍", PersonalAndDemographics, MasterCategories.Relation),

        // Organization & Location
        HrMasterEntry.Generic("division-office-location", "Division/Office Location", "Configure office divisions and locations", "🎎", OrganizationAndLocation, MasterCategories.Division),
        HrMasterEntry.Generic("country", "Country", "Manage country data", "🏳️", OrganizationAndLocation, MasterCategories.Country),
        HrMasterEntry.Generic("state", "State", "Manage state/province data", "🗺️", OrganizationAndLocation, MasterCategories.State),
        HrMasterEntry.Generic("city", "City", "Manage city data", "🌆", OrganizationAndLocation, MasterCategories.City),
        HrMasterEntry.Missing("state-city-link", "State City Link", "Link states with cities", "🔗", OrganizationAndLocation),

        // Document Management
        HrMasterEntry.Generic("document-category", "Document Category", "Manage document categories & subcategories", "📂", DocumentManagement, MasterCategories.DocumentCategory),

        // Other
        HrMasterEntry.Generic("occupation", "Occupation", "Configure occupation categories", "💼", Other, MasterCategories.Occupation),

        // Other Configurations — not in the locked design (fifth column)
        HrMasterEntry.Dedicated("employee-app", "Employee App Controls", "Punching, corrections, gate passes and what employees see", "📱", OtherConfigurations),
        HrMasterEntry.Dedicated("leave-colours", "Leave Type Colours", "Colour and icon for each leave type in the app", "🎨", OtherConfigurations),
        HrMasterEntry.Generic("advance-type", "Advance Type", "Categories for salary advances (Festival, Medical, Travel…)", "🏷️", OtherConfigurations, MasterCategories.AdvanceType),
        HrMasterEntry.Generic("skill-category", "Skill Category", "Group skills into categories", "🗂️", OtherConfigurations, MasterCategories.SkillCategory)
            .WithColumns("Skill Category"),
    ];

    /// <summary>Sections in source order (matches the design's column order), each with its cards.</summary>
    public static IReadOnlyList<(string Section, IReadOnlyList<HrMasterEntry> Items)> Sections { get; } =
        Entries
            .GroupBy(entry => entry.Section)
            .Select(group => (group.Key, (IReadOnlyList<HrMasterEntry>)group.ToList()))
            .ToList();

    /// <summary><see cref="ColumnLayout"/> filled with its sections; a section with no cards is skipped.</summary>
    public static IReadOnlyList<IReadOnlyList<(string Section, IReadOnlyList<HrMasterEntry> Items)>> Columns { get; } =
        ColumnLayout
            .Select(column => (IReadOnlyList<(string Section, IReadOnlyList<HrMasterEntry> Items)>)column
                .SelectMany(name => Sections.Where(section => section.Section == name))
                .ToList())
            .ToList();

    public static HrMasterEntry? Find(string key) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
}
