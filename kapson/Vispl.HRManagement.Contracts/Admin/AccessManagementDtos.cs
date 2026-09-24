using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

public enum AccountPortal
{
    Platform = 0,
    Hr = 1,
    Employee = 2,

    /// <summary>
    /// External applicant. Holds no HR permission and can only reach its own
    /// applications, documents and onboarding checklist.
    /// </summary>
    Candidate = 3
}

public sealed class AccessAccountDto
{
    public string Id { get; set; } = string.Empty;

    [Required]
    public string TenantKey { get; set; } = string.Empty;

    public AccountPortal Portal { get; set; } = AccountPortal.Hr;

    [Required, StringLength(160, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(60)]
    public string? EmployeeNumber { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Whether this account may sign in to a mobile app. Off unless granted.</summary>
    public bool MobileAccessEnabled { get; set; }

    public bool MustChangePassword { get; set; } = true;
    public bool IsLocked { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public DateTimeOffset? LastLoginUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public List<string> RoleIds { get; set; } = [];
    public List<string> RoleNames { get; set; } = [];
}

public sealed class CreateAccessAccountRequest
{
    [Required]
    public string TenantKey { get; set; } = string.Empty;

    public AccountPortal Portal { get; set; } = AccountPortal.Hr;

    [Required, StringLength(160, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 3)]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, underscores or hyphens only.")]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(60)]
    public string? EmployeeNumber { get; set; }

    [Required, StringLength(100, MinimumLength = 10)]
    public string TemporaryPassword { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    /// <summary>Whether this account may sign in to a mobile app. Off unless granted.</summary>
    public bool MobileAccessEnabled { get; set; }
    public List<string> RoleIds { get; set; } = [];
}

public sealed class UpdateAccessAccountRequest
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(60)]
    public string? EmployeeNumber { get; set; }

    public bool IsActive { get; set; } = true;
    /// <summary>Whether this account may sign in to a mobile app. Off unless granted.</summary>
    public bool MobileAccessEnabled { get; set; }
    public List<string> RoleIds { get; set; } = [];
}

/// <summary>
/// Moves one account to another portal, which <see cref="UpdateAccessAccountRequest"/>
/// deliberately cannot do: a portal is what somebody is to the company, not a detail of their
/// record. The case this exists for is a candidate who gets hired — an email address belongs to
/// one account, so the applicant's login becomes the employee's login rather than a second
/// account being created against the same person.
/// </summary>
public sealed class TransferAccountPortalRequest
{
    [Required]
    public string AccountId { get; set; } = string.Empty;

    public AccountPortal Portal { get; set; } = AccountPortal.Employee;

    /// <summary>How they will sign in from now on. The portals do not share a username shape.</summary>
    [Required, StringLength(80, MinimumLength = 3)]
    [RegularExpression("^[a-zA-Z0-9._@-]+$", ErrorMessage = "Use letters, numbers, dots, underscores, hyphens or @ only.")]
    public string UserName { get; set; } = string.Empty;

    [StringLength(60)]
    public string? EmployeeNumber { get; set; }

    /// <summary>
    /// Whether the transferred account may use a mobile app. Re-decided with the portal: an
    /// account moving from Candidate to Employee is a different thing in every way that
    /// matters, and its old grant should not follow it across.
    /// </summary>
    public bool MobileAccessEnabled { get; set; }

    /// <summary>
    /// Replaces the password. The account is changing hands between two different logins, so
    /// the credential that reached the old one must not keep working on the new one.
    /// </summary>
    [Required, StringLength(100, MinimumLength = 10)]
    public string TemporaryPassword { get; set; } = string.Empty;

    /// <summary>Roles in the destination portal. The old portal's roles are dropped.</summary>
    public List<string> RoleIds { get; set; } = [];
}

public sealed class ResetAccountPasswordRequest
{
    [Required]
    public string AccountId { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 10)]
    public string TemporaryPassword { get; set; } = string.Empty;
}

public sealed class AccessRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string TenantKey { get; set; } = string.Empty;
    public AccountPortal Portal { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public int AccountCount { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public List<string> PermissionKeys { get; set; } = [];
}

public sealed class SaveAccessRoleRequest
{
    public string? Id { get; set; }

    [Required]
    public string TenantKey { get; set; } = string.Empty;

    public AccountPortal Portal { get; set; } = AccountPortal.Hr;

    [Required, StringLength(60, MinimumLength = 2)]
    [RegularExpression("^[A-Z0-9_]+$", ErrorMessage = "Use uppercase letters, numbers and underscores only.")]
    public string RoleCode { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public List<string> PermissionKeys { get; set; } = [];
}

public sealed record PermissionDefinitionDto(
    string Key,
    string Group,
    string Name,
    string Description,
    AccountPortal Portal);

public static class HrPermissionCatalog
{
    public static readonly IReadOnlyList<PermissionDefinitionDto> All =
    [
        new("HR.DASHBOARD.VIEW", "General", "View HR dashboard", "Open the client HR dashboard and summary cards.", AccountPortal.Hr),
        new("EMPLOYEES.VIEW", "People", "View employees", "Search and read employee profiles.", AccountPortal.Hr),
        new("EMPLOYEES.CREATE", "People", "Create employees", "Create employee records and onboarding profiles.", AccountPortal.Hr),
        new("EMPLOYEES.EDIT", "People", "Edit employees", "Update employment and personal information.", AccountPortal.Hr),
        new("EMPLOYEES.DEACTIVATE", "People", "Deactivate employees", "End or suspend employee access without deleting history.", AccountPortal.Hr),
        new("EMPLOYEES.EMPLOYMENT_MANAGE", "People", "Manage employment terms", "Change joining date, employment type, status, grade and probation.", AccountPortal.Hr),
        new("EMPLOYEES.ORGANISATION_MANAGE", "People", "Manage reporting and placement", "Change department, location, cost centre and reporting line.", AccountPortal.Hr),
        new("EMPLOYEES.DOCUMENTS", "People", "Manage employee documents", "Upload, verify and expire employee documents.", AccountPortal.Hr),
        new("RECRUITMENT.VIEW", "Recruitment", "View recruitment", "View vacancies, candidates and application pipelines.", AccountPortal.Hr),
        new("RECRUITMENT.MANAGE", "Recruitment", "Manage recruitment", "Create vacancies and manage candidate stages.", AccountPortal.Hr),
        new("RECRUITMENT.PUBLISH", "Recruitment", "Publish jobs", "Publish or withdraw job openings from career sites.", AccountPortal.Hr),
        new("RECRUITMENT.APPLICATIONS", "Recruitment", "Process applications", "Review, shortlist, reject and progress applications.", AccountPortal.Hr),
        new("MASTERS.VIEW", "Configuration", "View HR masters", "Read the client's controlled master data.", AccountPortal.Hr),
        new("MASTERS.MANAGE", "Configuration", "Manage HR masters", "Create, edit and retire controlled master values.", AccountPortal.Hr),
        new("POLICY.VIEW", "Configuration", "View company policies", "Read published company policies and record having read them.", AccountPortal.Hr),
        new("POLICY.MANAGE", "Configuration", "Manage company policies", "Write, version and publish company policies.", AccountPortal.Hr),
        new("ATTENDANCE.VIEW", "Workforce", "View attendance", "Read attendance, shifts and exceptions.", AccountPortal.Hr),
        new("ATTENDANCE.MANAGE", "Workforce", "Manage attendance", "Correct attendance and manage shifts or exceptions.", AccountPortal.Hr),
        new("LEAVE.VIEW", "Workforce", "View leave", "Read employee leave balances and requests.", AccountPortal.Hr),
        new("LEAVE.APPROVE", "Workforce", "Approve leave", "Approve, reject or return leave requests.", AccountPortal.Hr),
        new("LEAVE.CONFIGURE", "Workforce", "Configure leave", "Manage policies, calendars and entitlements.", AccountPortal.Hr),
        new("LEAVE.REPORT", "Workforce", "Leave reporting", "Balances, distribution and trends across the organisation.", AccountPortal.Hr),
        new("LEAVE.EXPORT", "Workforce", "Export leave data", "Download leave records. Separate from reading them on screen.", AccountPortal.Hr),
        new("ATTENDANCE.REPORT", "Workforce", "Attendance reporting", "Late arrivals, monthly overview and missing punches.", AccountPortal.Hr),
        new("SHIFT.VIEW", "Workforce", "View shifts", "See shift definitions and who works them.", AccountPortal.Hr),
        new("SHIFT.MANAGE", "Workforce", "Manage shifts", "Create shifts and assign employees to them.", AccountPortal.Hr),
        new("MOVEMENT.TEAM", "Workforce", "See who is out", "View employees off site during working hours.", AccountPortal.Hr),
        new("MOVEMENT.MANAGE", "Workforce", "Correct movement records", "Close a movement on behalf of someone who did not.", AccountPortal.Hr),
        new("GATEPASS.TEAM", "Workforce", "See gate passes", "View gate passes across the team.", AccountPortal.Hr),
        new("GATEPASS.APPROVE", "Workforce", "Approve gate passes", "Approve or reject a gate pass request.", AccountPortal.Hr),
        new("GATEPASS.VERIFY", "Workforce", "Verify a pass at a gate", "Scan a pass and record the crossing. Grant to security only.", AccountPortal.Hr),
        new("ONDUTY.TEAM", "Workforce", "See on duty requests", "View on duty requests across the team.", AccountPortal.Hr),
        new("ONDUTY.APPROVE", "Workforce", "Approve on duty", "Approve or reject an on duty request.", AccountPortal.Hr),
        new("ATTENDANCE.EXPORT", "Workforce", "Export attendance data", "Download attendance records.", AccountPortal.Hr),
        new("DASHBOARD.VIEW", "Dashboard", "View dashboard", "Open the HR dashboard for the caller's own scope.", AccountPortal.Hr),
        new("DASHBOARD.ORG", "Dashboard", "Organisation-wide dashboard", "See figures across every company and unit rather than an assigned scope.", AccountPortal.Hr),
        new("DASHBOARD.EXPORT", "Dashboard", "Export dashboard panels", "Download any dashboard table or chart.", AccountPortal.Hr),
        new("PEOPLE.MOMENTS.VIEW", "Dashboard", "View birthdays and anniversaries", "See colleague birthdays and work anniversaries, subject to each person's own opt-out.", AccountPortal.Hr),
        new("PAYROLL.VIEW", "Payroll", "View payroll", "Read payroll runs and salary information.", AccountPortal.Hr),
        new("PAYROLL.MANAGE", "Payroll", "Manage payroll", "Prepare, validate and close payroll runs.", AccountPortal.Hr),
        new("PAYROLL.APPROVE", "Payroll", "Approve payroll", "Approve a month's generated payroll, locking it against regeneration. Grant deliberately.", AccountPortal.Hr),
        new("PAYROLL.LOAN.VIEW", "Payroll", "View loans and advances", "Read loan and salary advance vouchers, schedules and recoveries.", AccountPortal.Hr),
        new("PAYROLL.LOAN.MANAGE", "Payroll", "Manage loans and advances", "Issue, approve, reject and cancel loans and advances, and record manual repayments.", AccountPortal.Hr),
        new("PAYROLL.TRANSFER", "Payroll","Transfer salaries to bank", "Record bank transfers and download bank advice with full account numbers. Moves real money; never grant by default.", AccountPortal.Hr),
        new("REPORTS.VIEW", "Reports", "View reports", "Open HR and workforce reports.", AccountPortal.Hr),
        new("REPORTS.EXPORT", "Reports", "Export reports", "Download reports and employee data.", AccountPortal.Hr),
        new("ACCOUNTS.VIEW", "Security", "View accounts", "Read client account and role assignments.", AccountPortal.Hr),
        new("ACCOUNTS.MANAGE", "Security", "Manage accounts", "Create, reset, enable and disable client accounts.", AccountPortal.Hr),
        new("ROLES.VIEW", "Security", "View roles", "Read roles and their permission grants.", AccountPortal.Hr),
        new("ROLES.MANAGE", "Security", "Manage roles", "Create custom roles and change permission grants.", AccountPortal.Hr),

        // Recruitment, finer grained than VIEW/MANAGE.
        new("RECRUITMENT.SCREENING", "Recruitment", "Screen applications", "Record screening notes and scorecards.", AccountPortal.Hr),
        new("RECRUITMENT.DECISION", "Recruitment", "Decide on applications", "Shortlist or reject an applicant with a reason.", AccountPortal.Hr),
        new("RECRUITMENT.ANALYTICS", "Recruitment", "View recruitment analytics", "Open funnel, ageing and source reporting.", AccountPortal.Hr),
        new("RECRUITMENT.COMPENSATION", "Recruitment", "Manage job compensation", "Set and view the salary range on a job opening.", AccountPortal.Hr),
        new("RECRUITMENT.CANDIDATE_PII", "Recruitment", "View candidate personal data", "See a candidate's contact details and resume.", AccountPortal.Hr),
        new("RECRUITMENT.CANDIDATE_DOCUMENTS", "Recruitment", "Manage candidate documents", "Request, view and manage documents a candidate supplied.", AccountPortal.Hr),
        new("RECRUITMENT.DOCUMENT_VERIFY", "Recruitment", "Verify candidate documents", "Mark a candidate document verified or rejected.", AccountPortal.Hr),
        new("RECRUITMENT.INTERVIEWS", "Recruitment", "View interviews", "See the interview schedule and evaluations.", AccountPortal.Hr),
        new("RECRUITMENT.INTERVIEWS_MANAGE", "Recruitment", "Manage interviews", "Schedule, reschedule and cancel interviews.", AccountPortal.Hr),

        new("REQUISITIONS.MANAGE", "Recruitment", "Manage requisitions", "Raise, edit, submit and cancel hiring requisitions.", AccountPortal.Hr),

        new("OFFERS.VIEW", "Recruitment", "View offers", "See offers and their approval state.", AccountPortal.Hr),
        new("OFFERS.MANAGE", "Recruitment", "Manage offers", "Draft and amend offers.", AccountPortal.Hr),
        new("OFFERS.APPROVE", "Recruitment", "Approve offers", "Approve an offer before it is sent.", AccountPortal.Hr),
        new("OFFERS.SEND", "Recruitment", "Send offers", "Release an approved offer to the candidate.", AccountPortal.Hr),

        // Employee lifecycle.
        new("EMPLOYEES.DOCUMENT_VERIFY", "People", "Verify employee documents", "Mark an employee document verified or rejected.", AccountPortal.Hr),
        new("ONBOARDING.VIEW", "People", "View onboarding", "See joiner checklists and their progress.", AccountPortal.Hr),
        new("ONBOARDING.MANAGE", "People", "Manage onboarding", "Reassign, comment on and close onboarding tasks.", AccountPortal.Hr),
        new("PROBATION.VIEW", "People", "View probation", "See probation reviews and goals.", AccountPortal.Hr),
        new("PROBATION.REVIEW", "People", "Record probation reviews", "Enter goals, ratings and feedback.", AccountPortal.Hr),
        new("PROBATION.DECIDE", "People", "Decide probation outcomes", "Confirm, extend or fail a probation.", AccountPortal.Hr),
        new("OFFBOARDING.VIEW", "People", "View offboarding", "See exits and clearance progress.", AccountPortal.Hr),
        new("OFFBOARDING.MANAGE", "People", "Manage offboarding", "Initiate exits, run clearance and close the record.", AccountPortal.Hr),

        // Workforce.
        new("ATTENDANCE.TEAM", "Workforce", "View team attendance", "See the attendance register for a reporting team.", AccountPortal.Hr),
        new("ATTENDANCE.APPROVE", "Workforce", "Approve attendance corrections", "Decide on attendance correction requests.", AccountPortal.Hr),
        new("SHIFTS.VIEW", "Workforce", "View shifts", "See rosters and shift assignments.", AccountPortal.Hr),
        new("SHIFTS.MANAGE", "Workforce", "Manage shifts", "Assign, change and swap shifts.", AccountPortal.Hr),
        new("SHIFTS.APPROVE", "Workforce", "Approve shift changes", "Decide on swap and change requests.", AccountPortal.Hr),
        new("LEAVE.TEAM", "Workforce", "View team leave", "See leave for a reporting team and make adjustments.", AccountPortal.Hr),

        // Pay.
        new("PAYROLL.TAX_VERIFY", "Payroll", "Verify tax declarations", "Check and approve declared tax investments.", AccountPortal.Hr),
        new("REIMBURSEMENTS.APPROVE", "Payroll", "Approve reimbursements", "Decide on expense claims.", AccountPortal.Hr),

        // Operations.
        new("REQUESTS.FULFIL", "Operations", "Fulfil HR requests", "Action and close employee service requests.", AccountPortal.Hr),
        new("HOME.HR", "General", "View HR home", "Open the HR home dashboard.", AccountPortal.Hr),
        new("HOME.MANAGER", "General", "View manager home", "Open the manager home dashboard.", AccountPortal.Hr),


        new("SELF.PROFILE.VIEW", "My profile", "View own profile", "View personal and employment profile information.", AccountPortal.Employee),
        new("SELF.PROFILE.EDIT", "My profile", "Edit own profile", "Update permitted personal and contact information.", AccountPortal.Employee),
        new("SELF.DOCUMENTS.VIEW", "My profile", "View own documents", "View employment and personal documents.", AccountPortal.Employee),
        new("SELF.ATTENDANCE.VIEW", "Self service", "View own attendance", "View attendance, shifts and exceptions.", AccountPortal.Employee),
        new("SELF.LEAVE.VIEW", "Self service", "View own leave", "View leave balance and request history.", AccountPortal.Employee),
        new("SELF.LEAVE.REQUEST", "Self service", "Request leave", "Create or withdraw a leave request.", AccountPortal.Employee),
        new("SELF.MOVEMENT.REQUEST", "Self service", "Declare going out", "Record leaving the premises and returning.", AccountPortal.Employee),
        new("SELF.GATEPASS.REQUEST", "Self service", "Request a gate pass", "Apply for permission to be off site.", AccountPortal.Employee),
        new("SELF.ONDUTY.REQUEST", "Self service", "Request on duty", "Apply for business travel that still counts as working time.", AccountPortal.Employee),
        new("SELF.PAYSLIP.VIEW", "Self service", "View own payslips", "View and download personal payslips.", AccountPortal.Employee),
        new("TEAM.VIEW", "Manager", "View reporting team", "View employees assigned to the manager.", AccountPortal.Employee),
        new("TEAM.LEAVE.APPROVE", "Manager", "Approve team leave", "Approve or reject leave for direct reports.", AccountPortal.Employee),
        new("TEAM.ONDUTY.APPROVE", "Manager", "Approve team on duty", "Approve or reject on duty requests for direct reports.", AccountPortal.Employee)
    ];

    public static IReadOnlyList<PermissionDefinitionDto> ForPortal(AccountPortal portal) =>
        All.Where(permission => permission.Portal == portal).ToList();

    public static bool IsValid(string permissionKey, AccountPortal portal) =>
        All.Any(permission => permission.Portal == portal &&
            string.Equals(permission.Key, permissionKey, StringComparison.OrdinalIgnoreCase));
}
