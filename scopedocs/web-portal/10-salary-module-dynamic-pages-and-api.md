# App Menu and Salary Module — dynamic pages, API, DTOs and masters

Companion to [08-app-menu-design-layout.md](08-app-menu-design-layout.md) (layout) and
[09-widgets-reusable-skeleton.md](09-widgets-reusable-skeleton.md) (widgets). This document is the
data and wiring layer: routes, contracts, endpoints, which of the 26 APP Configuration masters
already exist versus are net-new, and the permissions that gate all of it.

Conventions follow [02-dtos.md](02-dtos.md) and [04-api-surface.md](04-api-surface.md) exactly:
`record` for responses, `class` with data annotations for queries, `DateOnly` for calendar dates,
`decimal` for money, `CursorPage<T>` for anything that can grow, `ApiEnvelope<T>` on every
response, route prefix `api/mobile/v1` kept for the same reuse reason already argued in
[04-api-surface.md](04-api-surface.md) — the web portal, the HR mobile app and the employee app
are three callers of one surface, per `portal-integration.md`'s CR-1.

## 1. Dynamic page and route inventory

| Page | Route (proposed) | Reached from | Reference |
|---|---|---|---|
| App menu overlay | *(not a route — a modal state layered on the current page)* | New header icon, any authenticated HR page | S01–S04 |
| Designation | `/hr/settings/designation` | APP Configuration → Employee Masters → Designation | S05 |
| Department & Sub Department | `/hr/settings/departments` | APP Configuration → Employee Masters | — (no reference; build on `MD-01` shell) |
| Grade | `/hr/settings/grades` | APP Configuration → Payroll & Compensation | — |
| Allowance/Deduction | `/hr/settings/allowances-deductions` | APP Configuration → Payroll & Compensation | — |
| Link Allowances/Deductions With Grade | `/hr/settings/grade-components` | APP Configuration → Payroll & Compensation | — |
| Shift, Link Employee to Shift, Holiday, Leave Type, Leaving Reason | *(existing or near-existing routes)* | APP Configuration → Leave & Attendance | see §5 |
| Marital Status, Relation | *(existing generic masters route)* | APP Configuration → Personal & Demographics | see §5 |
| Division/Office Location, Country, State, City, State City Link | `/hr/settings/geography/*` | APP Configuration → Organization & Location | — |
| Document Category | `/hr/settings/document-categories` | APP Configuration → Document Management | — |
| Occupation | *(existing generic masters route)* | APP Configuration → Other | see §5 |
| Employee Explorer | *(existing — confirm route)* | App menu → Employee | S01, no new screen |
| Organisation Hierarchy | *(existing — confirm route)* | App menu → Employee | S01, no new screen |
| Add New Employee | *(existing — confirm route)* | App menu → Employee | S01, no new screen |
| Generate Employee Salary | `/hr/payroll/generate` | App menu → Salary Management | S06–S08 |
| Salary Preview (modal) | *(modal over `/hr/payroll/generate`)* | "Preview" button | S09 |
| Mark as Paid (modal) | *(modal over `/hr/payroll/generate`)* | "Mark as Paid" button | S10 |
| Transfer to Bank (modal) | *(modal over `/hr/payroll/generate`)* | "Transfer to Bank" button | S11 |
| Loan and Advance | `/hr/payroll/loans` | App menu → Salary Management | none — named only |
| Salary History | `/hr/payroll/history` | "🕐 Salary History" button on the Generate page | none — named only |
| Employee Detail Report | `/hr/reports/employee-detail` | App menu → Employee Report | none — named only |
| Employee Monthly Attendance Report | `/hr/reports/attendance-monthly` | App menu → Employee Report | none — named only |
| Employees Salary Register | `/hr/reports/salary-register` | App menu → Employee Report | none — named only |
| Loan and Advance Report | `/hr/reports/loans` | App menu → Employee Report | none — named only |

Routes marked "named only" have an approved entry point and name but no approved layout — do not
design their tables from imagination; carry them as a stub page with a "not yet available" state
(consistent with `CR-12`: *a feature that does not exist says so*) until a reference is supplied.

## 2. Scope and envelope

Payroll needs its own scope object, parallel to `DashboardScopeQuery`/`DashboardScopeDto` in
[02-dtos.md](02-dtos.md), because its dimensions are different (month+year+salary type, not an
as-of date; division/department, not company/unit):

```csharp
public sealed class PayrollScopeQuery
{
    [Required] public int Month { get; set; }              // 1-12
    [Required] public int Year { get; set; }
    [Required, StringLength(30)] public string SalaryType { get; set; } = "Monthly";
    [StringLength(40)] public string? DivisionCode { get; set; }
    [StringLength(40)] public string? DepartmentCode { get; set; }
}

public sealed record PayrollScopeDto(
    int Month,
    string MonthLabel,
    int Year,
    string SalaryType,
    string? DivisionCode,
    string? DivisionName,
    string? DepartmentCode,
    string? DepartmentName,
    DateTimeOffset GeneratedUtc);
```

## 3. Payroll DTOs

```csharp
/// <summary>
/// The four headline tiles on the Generate Employee Salary page (S06-S08 §6.3) and, with a
/// reduced tile set, the Salary Preview document (S09). <c>Generated + Pending</c> must always
/// equal <c>TotalEmployees</c> for the same scope — this is a hard invariant, not a display
/// convention, and the service must never let the two drift.
/// </summary>
public sealed record PayrollSummaryDto(
    PayrollScopeDto Scope,
    int TotalEmployees,
    int ActiveDivisionCount,
    decimal GrossPayroll,
    int Pending,
    int Generated);

public sealed record PayrollComponentBreakdownDto(
    decimal Basic,
    decimal Hra,
    decimal Conveyance,
    decimal OtherAllowances,
    decimal Gross,             // computed: Basic + Hra + Conveyance + OtherAllowances — never accepted from a client
    decimal Pf,
    decimal Esi,
    decimal Tds,
    decimal Deductions,        // computed: Pf + Esi + Tds — must equal the sum shown wherever only the aggregate appears (08 §7.2, open question 5)
    decimal NetPayable);       // computed: Gross - Deductions

public sealed record DivisionPayrollRowDto(
    string DivisionCode,
    string DivisionName,
    int EmployeeCount,
    int DepartmentCount,
    PayrollComponentBreakdownDto Totals,
    StatusDto Status,                              // Generated | Pending, per division
    IReadOnlyList<ActionAvailabilityDto> Actions);  // e.g. Generate, disabled once Generated unless re-generate is permitted

public sealed record DepartmentPayrollRowDto(
    string DepartmentCode,
    string DepartmentName,
    string DivisionCode,
    string DivisionName,
    int EmployeeCount,
    PayrollComponentBreakdownDto Totals,
    StatusDto Status,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed record DivisionPayrollGroupDto(
    string DivisionCode,
    string DivisionName,
    IReadOnlyList<DepartmentPayrollRowDto> Departments,
    PayrollComponentBreakdownDto Subtotal);          // the shaded subtotal row in 08 §7.2

public sealed record EmployeePayrollRowDto(
    string EmployeeId,
    string DisplayName,
    string? AvatarUrl,
    string DivisionCode,
    string DivisionName,
    string DepartmentCode,
    string DepartmentName,
    string Designation,
    string? ShiftCode,          // e.g. "HO KFPL", "WH 9-7" — location/brand-qualified, see 08 §7.3
    int WorkedDays,
    int ExpectedDays,           // the denominator of the "26/26" ratio (SM-08)
    PayrollComponentBreakdownDto Amounts,
    StatusDto Status,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed record DivisionWisePayrollDto(
    PayrollSummaryDto Summary,
    CursorPage<DivisionPayrollRowDto> Rows,
    PayrollComponentBreakdownDto GrandTotal);

public sealed record DepartmentWisePayrollDto(
    PayrollSummaryDto Summary,
    IReadOnlyList<DivisionPayrollGroupDto> Groups);   // grouped/subtotal shape from 08 §7.2

public sealed record IndividualPayrollDto(
    PayrollSummaryDto Summary,
    CursorPage<EmployeePayrollRowDto> Rows);

public sealed class GeneratePayrollRequest
{
    [Required] public PayrollScopeQuery Scope { get; set; } = new();

    /// <summary>Empty means "all employees in scope." A non-empty list generates only these —
    /// the shape "Generate Pending" (08 §7.3) needs, versus "Generate All" (08 §7.1/7.2).</summary>
    public IReadOnlyList<string> EmployeeIds { get; set; } = [];
}

public sealed record PayrollRunResultDto(
    string RunId,
    PayrollScopeDto Scope,
    int EmployeesProcessed,
    int EmployeesFailed,
    IReadOnlyList<string> FailureReasons,
    DateTimeOffset OccurredUtc);
```

## 4. Salary Preview, Mark as Paid, Transfer to Bank DTOs

```csharp
/// <summary>
/// The printable document behind S09. <c>Generated + Pending = TotalEmployees</c> holds here too,
/// against whatever subset the preview was scoped to — the sample (15 employees, 8+7) shows this
/// is not always the same denominator as the page-level 148, and the service must accept that a
/// preview can legitimately be scoped narrower than the whole tenant.
/// </summary>
public sealed record PayrollPreviewDto(
    string TenantDisplayName,           // "Kapsons Fashion" letterhead
    string DepartmentLabel,             // "Kapsons Fashion . Payroll Department"
    PayrollScopeDto Scope,
    int TotalEmployees,
    decimal TotalGrossPayroll,
    decimal TotalNetPayable,
    decimal TotalDeductions,
    int Generated,
    int Pending,
    IReadOnlyList<EmployeePayrollRowDto> Rows);

public sealed class ApprovePayrollPreviewRequest
{
    [Required] public string RunId { get; set; } = string.Empty;

    /// <summary>Same replay-safety contract as every other mutation on the platform (CR-10).</summary>
    [Required] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class MarkSalaryPaidRequest
{
    [Required] public IReadOnlyList<string> PayrollLineIds { get; set; } = [];
    [Required] public DateOnly PaymentDate { get; set; }
    [Required, StringLength(30)] public string PaymentMode { get; set; } = "Bank Transfer";
    [Required, StringLength(30)] public string DocumentType { get; set; } = "Monthly Payroll";
}

public sealed record MarkSalaryPaidResultDto(
    int EmployeesUpdated,
    decimal TotalAmount,
    DateTimeOffset OccurredUtc);

/// <summary>The employee's own account, masked, as it appears in the Transfer to Bank checklist
/// (S11). Sourced from an Employee Bank Details record that — per the employee-app scope notes —
/// has no backend module yet at all. This DTO cannot be populated for real until that exists.</summary>
public sealed record EmployeeBankAccountSummaryDto(
    string BankName,
    string MaskedAccountNumber);   // "HDFC •••• 4821" — last 4 digits only, per SM-18

public sealed record BankTransferCandidateDto(
    string EmployeeId,
    string DisplayName,
    string Designation,
    EmployeeBankAccountSummaryDto? BankAccount,   // null if bank details are missing — must block selection, not silently transfer to nowhere
    decimal Amount,
    StatusDto Status);

public sealed record CompanyBankAccountDto(     // the picker in S11 — a net-new master, see §5
    string Id,
    string BankName,
    string AccountLabel,        // "SBI - KAPSONS0034"
    bool IsDefault);

public sealed record BankTransferBatchSummaryDto(
    int TotalEmployees,
    decimal TotalAmount,
    int AlreadyTransferredThisMonth,
    int PendingTransfer,
    IReadOnlyList<CompanyBankAccountDto> AvailableAccounts,
    CursorPage<BankTransferCandidateDto> Candidates);

public sealed class ExecuteBankTransferRequest
{
    [Required] public IReadOnlyList<string> PayrollLineIds { get; set; } = [];
    [Required] public DateOnly TransferDate { get; set; }
    [Required, StringLength(40)] public string CompanyBankAccountId { get; set; } = string.Empty;
    [Required, StringLength(20)] public string TransferMode { get; set; } = "IMPS";  // IMPS | NEFT | RTGS

    /// <summary>Confirm with the client whether this is ever user-editable (08 §9.2, open question 7).
    /// Modelled here as server-assigned and returned, not accepted, until confirmed otherwise.</summary>
    [Required] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed record BankTransferResultDto(
    string BatchId,
    string ReferenceNo,          // server-assigned, e.g. "TXN-28404375"
    decimal TotalAmount,
    int EmployeesTransferred,
    string AdviceDocumentUrl);   // the "Download Advice" artefact — a file, not a live gateway callback, unless confirmed otherwise
```

## 5. Masters — what already exists, what is genuinely new

**Superseded by direct code inspection — corrected 2026-09-14.** The table below was originally
written from the Admin *pages* alone and understated what exists; reading
`Vispl.HRManagement.Contracts/Admin/MasterOptionDto.cs` directly shows the backend is far ahead of
what any Admin screenshot suggested. `MasterCategories` already declares **26 categories**
spanning Employee Masters, Personal & Demographics, Payroll & Compensation, Leave & Attendance,
Organization & Location and Recruitment — served today by `IMasterDataService` (used directly by
`/admin/masters`, and now also by the HR portal's own `/hr/settings/masters/{key}`, added in this
change — see §9). **Roughly 20 of the 26 App Configuration cards map onto an existing category
one-to-one.** Only relational entities (Designation, a real Department/Sub-Department hierarchy)
and pure linking tables (Grade↔Allowance/Deduction, State↔City, Employee↔Shift) remain unbuilt —
a flat `MasterOptionDto` genuinely cannot represent those, no matter how the Admin UI is arranged.

| # | Card | Status | `MasterCategories` constant |
|---|---|---|---|
| 1 | Designation | **Not built** — relational entity (Designation → Grade, Designation → Department), no flat master can represent it | — |
| 2 | Department & Sub Department | **Built** — flat list, no sub-department hierarchy yet | `Department` |
| 3 | Gender | **Built** | `Gender` |
| 4 | Qualification | **Built** | `Qualification` |
| 5 | Skills | **Not built** — no category exists for it | — |
| 6 | Education Level | **Built** | `EducationLevel` |
| 7 | Religion | **Built** | `Religion` |
| 8 | Nationality | **Built** — distinct from Country, not a duplicate | `Nationality` |
| 9 | Blood Group | **Built** | `BloodGroup` |
| 10 | Allowance/Deduction | **Built** — two categories, one card | `AllowanceType`, `DeductionType` |
| 11 | Grade | **Built** | `Grade` |
| 12 | Link Allowances/Deductions With Grade | **Not built** — a mapping table, not a value list; depends on 10 and 11 existing, which they now do | — |
| 13 | Shift | **Built, separate module** | not a `MasterOptionDto` — `ShiftsAdmin.razor` / `IShiftConfigurationService` |
| 14 | Link Employee to Shift | **Not built** as its own HR-portal route | — |
| 15 | Holiday | **Built, separate module** (the calendar itself) | not a `MasterOptionDto` — `HolidaysAdmin.razor`; `HolidayType` exists separately as a classification sub-master |
| 16 | Leave Type | **Built** | `LeaveType` — richer policy configuration remains in `LeavePoliciesAdmin.razor` |
| 17 | Leaving Reason | **Built** — two related categories | `LeavingReason`, `ExitType` |
| 18 | Marital Status | **Built** | `MaritalStatus` |
| 19 | Relation | **Built** | `Relation` |
| 20 | Division/Office Location | **Built** | `Division` |
| 21 | Country | **Built** | `Country` |
| 22 | State | **Built** | `State` |
| 23 | City | **Built** | `City` |
| 24 | State City Link | **Not built** — a mapping table, not a value list | — |
| 25 | Document Category | **Built** as a value list; the *documents themselves* still route through the stubbed `EmployeeDocumentsController` | `DocumentCategory` |
| 26 | Occupation | **Built** | `Occupation` |

**Corrected summary: of 26 cards, 20 already resolve to a real, populated `MasterCategories`
value today** (confirmed by reading `Vispl.HRManagement.Contracts/Admin/MasterOptionDto.cs`
directly, not inferred from Admin screenshots), **2 are built as their own separate modules**
(Shift, Holiday) rather than flat lists, and **4 have no backend of any kind** — Designation
(relational), Skills, and two linking tables (Grade↔Allowance/Deduction, State↔City). "Masters are
already there in admin" turns out to be **true for most of this catalogue**, including payroll
(Grade, Allowance/Deduction) and geography (Division, Country, State, City) — the earlier version
of this table, written before the contracts file was read directly, badly understated this. See
§9 for the HR-portal-side dynamic route now built against these 20 categories.

## 6. API surface

Following the numbered-controller convention of [04-api-surface.md](04-api-surface.md). **Mobile
column**: `HR` = HR mobile app could reuse, `EMP` = employee app could reuse (e.g. viewing one's
own payslip once Payroll exists at all), `—` = portal only for now.

### 6.1 `PayrollController` — `api/mobile/v1/payroll`

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/payroll/summary` | `PAYROLL.VIEW` | — | The 4-tile summary (08 §6.3) |
| GET | `/payroll/division-wise` | `PAYROLL.VIEW` | — | S06 |
| GET | `/payroll/department-wise` | `PAYROLL.VIEW` | — | S07, grouped shape |
| GET | `/payroll/individual` | `PAYROLL.VIEW` | — | S08 |
| POST | `/payroll/generate` | `PAYROLL.GENERATE` | — | `GeneratePayrollRequest` → `PayrollRunResultDto` |
| GET | `/payroll/preview` | `PAYROLL.VIEW` | — | `PayrollPreviewDto` behind the "Preview" button |
| POST | `/payroll/preview/{runId}/approve` | `PAYROLL.APPROVE` | — | The single most consequential action in the module — see §7 |
| GET | `/payroll/history` | `PAYROLL.VIEW` | — | "Salary History" button, no reference layout |
| GET | `/payroll/export` | `PAYROLL.EXPORT` | — | Separate permission from view, matching the platform's existing export/view split |

### 6.2 `SalaryPaymentController` — `api/mobile/v1/payroll/payments`

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| POST | `/payroll/payments/mark-paid` | `PAYROLL.MARKPAID` | — | S10, `MarkSalaryPaidRequest` |
| GET | `/payroll/payments/bank-transfer/batch` | `PAYROLL.TRANSFER` | — | S11's summary + candidate list, `BankTransferBatchSummaryDto` |
| POST | `/payroll/payments/bank-transfer/execute` | `PAYROLL.TRANSFER` | — | S11's "Execute Transfer" |
| GET | `/payroll/payments/bank-transfer/advice/{batchId}` | `PAYROLL.TRANSFER` | — | S11's "Download Advice" |
| GET | `/payroll/company-bank-accounts` | `PAYROLL.TRANSFER` | — | Populates `SM-17`'s picker — depends on the new Company Bank Account master (§5, card 20-adjacent, not itself in the 26 but required by S11) |

### 6.3 `LoanAdvanceController` — `api/mobile/v1/payroll/loans`

Named only (§1) — endpoints below are placeholders shaped by the platform's general request
pattern, not by any reference screen, and must be revisited once a layout exists.

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/payroll/loans` | `PAYROLL.LOAN.VIEW` | — | List |
| POST | `/payroll/loans` | `PAYROLL.LOAN.MANAGE` | — | Issue a loan/advance |
| GET | `/payroll/loans/{id}` | `PAYROLL.LOAN.VIEW` | — | Detail, schedule, running balance |
| POST | `/payroll/loans/{id}/repayment` | `PAYROLL.LOAN.MANAGE` | — | Record or schedule a deduction |

### 6.4 `DesignationController` — `api/mobile/v1/masters/designations`

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/masters/designations` | `MASTERS.VIEW` | HR, EMP | S05's table |
| POST | `/masters/designations` | `MASTERS.MANAGE` | — | "+ Add Designation" |
| PUT | `/masters/designations/{id}` | `MASTERS.MANAGE` | — | Edit |
| DELETE | `/masters/designations/{id}` | `MASTERS.MANAGE` | — | Delete |
| PUT | `/masters/designations/order` | `MASTERS.MANAGE` | — | "Set Order" |

### 6.5 `GeographyController` — `api/mobile/v1/masters/geography`

Entirely new module (§5, cards 20–24).

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET/POST/PUT/DELETE | `/masters/geography/divisions` | `MASTERS.MANAGE` | — | Division/Office Location |
| GET/POST/PUT/DELETE | `/masters/geography/countries` | `MASTERS.MANAGE` | — | Country |
| GET/POST/PUT/DELETE | `/masters/geography/states` | `MASTERS.MANAGE` | — | State |
| GET/POST/PUT/DELETE | `/masters/geography/cities` | `MASTERS.MANAGE` | — | City |
| GET/POST | `/masters/geography/state-city-links` | `MASTERS.MANAGE` | — | State City Link |

### 6.6 Report endpoints (named-only, minimal contract)

| Method | Route | Permission | Serves |
|---|---|---|---|
| GET | `/reports/employee-detail` | `REPORT.EMPLOYEE.VIEW` | Employee Detail Report |
| GET | `/reports/attendance-monthly` | `REPORT.ATTENDANCE.VIEW` | Employee Monthly Attendance Report |
| GET | `/reports/salary-register` | `REPORT.PAYROLL.VIEW` | Employees Salary Register |
| GET | `/reports/loans` | `REPORT.PAYROLL.VIEW` | Loan and Advance Report |

## 7. RBAC — new permissions

Following the format and severity language of [05-rbac.md](05-rbac.md), which already treats
`LEAVE.YEAREND.RUN` as "the most destructive permission on the platform." Two new permissions in
this module deserve exactly that framing.

| Permission | Purpose | Severity |
|---|---|---|
| `PAYROLL.VIEW` | See payroll summaries, previews, individual rows | Normal |
| `PAYROLL.GENERATE` | Run payroll generation for a scope | High — writes payroll figures for real employees |
| `PAYROLL.APPROVE` | Approve a payroll preview (S09's "Approve & Proceed") | **High — this is the payroll equivalent of `LEAVE.YEAREND.RUN`.** Once approved, the run is presumably no longer provisional. Should default to no role, same as year-end |
| `PAYROLL.MARKPAID` | Mark a generated salary as paid outside the system | Normal-High — a status change with financial implication, needs `CR-11`'s named-approver-and-audit-trail treatment |
| `PAYROLL.TRANSFER` | Execute or advise a bank transfer | **Highest in this module — real money leaves a real bank account.** Must never be granted by default, must require a second approver in the same spirit as year-end, and must be logged with the same rigour as `MOBILE.PLATFORM.SECURITY`-class platform-only permissions in `admin.md` |
| `PAYROLL.LOAN.VIEW` / `PAYROLL.LOAN.MANAGE` | Loan and Advance module | High for `MANAGE` — same reasoning as `PAYROLL.GENERATE` |
| `PAYROLL.EXPORT` | Export payroll data | Separate from `VIEW`, per the platform's existing view/export split |
| `MASTERS.VIEW` / `MASTERS.MANAGE` | Covers every generic master, including the 20 confirmed in §5 | **Confirmed to already exist** — `Vispl.HRManagement.Contracts/Admin/AccessManagementDtos.cs` already declares both, under category "Configuration". No new key needed. `/hr/settings/masters/{key}` (§9) gates its Add/Edit form on `MASTERS.MANAGE` via `HrPortalCaller.HasAsync`, verified live: the permission was previously declared but unused by any page — this is the first real enforcement point for it |
| `REPORT.EMPLOYEE.VIEW`, `REPORT.ATTENDANCE.VIEW`, `REPORT.PAYROLL.VIEW` | The four named-only report destinations | Normal, but `REPORT.PAYROLL.VIEW` should not be granted alongside every `REPORT.*` grant by default, since it exposes compensation |

**`PAYROLL.TRANSFER` should never appear in a default role.** Treat its provisioning exactly like
`LEAVE.YEAREND.RUN` in [05-rbac.md](05-rbac.md): explicit grant, ideally dual-control, definitely
audited, and never bundled into a general "Payroll Manager" role without the grantor understanding
that role now moves real money.

## 8. Open items carried from doc 08, with a recommendation

| Open item | Recommendation |
|---|---|
| Two modals share "Transfer Salary to Bank" | Rename S10's modal to **"Mark Salary as Paid"** and reserve "Transfer Salary to Bank" for S11 only |
| Reference No. editability | Model as server-assigned in `ExecuteBankTransferRequest`/`BankTransferResultDto` (§4) unless the client confirms otherwise — an editable transaction reference is a reconciliation risk `CR-10`'s idempotency contract is meant to prevent |
| Download Advice vs Execute Transfer as system of record | Model both as first-class outcomes on `BankTransferResultDto`, and confirm with the client whether "Execute Transfer" implies a live payment-gateway call or is purely a status change alongside the advice file |
| PF/ESI/TDS sum vs single "Deductions" figure | Enforced as a computed property in `PayrollComponentBreakdownDto` (§3) — the aggregate can never be sent independently of its components, closing the gap the layout doc flagged |
| Company Bank Account master | Added as `CompanyBankAccountDto` (§4) and its own controller route (§6.2) — this master is required by S11 but was not present anywhere in the 26-card APP Configuration catalogue, and must be added there too, not just in the API |
| Employee bank details for the masked chip | Blocked on a backend Employee Bank Account module that does not exist yet (confirmed absent in both the mobile employee-app scope and the main backend module inventory) — `BankTransferCandidateDto.BankAccount` is nullable for exactly this reason, and a null value must block selection rather than silently transferring to nowhere |

## 9. Shipped — the HR-portal dynamic master route

Built against the corrected §5 findings, not the earlier speculative version.

- `Vispl.HRManagement.UI/Components/Hr/Shared/HrMasterRegistry.cs` — the 26-card catalogue as
  code: each entry names its `MasterCategories` key(s) (one for a simple master, two for
  Allowance/Deduction, two for Leaving Reason/Exit Type), or is marked `ExistingModule` (Shift,
  Holiday) or `NotBuilt` (Designation, Skills, and the two linking tables). Adding a 27th card to
  the design means adding one line here, not a new page.
- `Vispl.HRManagement.UI/Components/Hr/Pages/HrMasterCatalog.razor` — `/hr/settings` and
  `/hr/{TenantKey}/settings`. The seven-section grid from doc 08 §4, rendered from the registry. A
  card whose kind isn't `GenericMaster` still renders and still links — landing on a clear
  "not built yet" panel is the point (CR-12), not a hidden menu item.
- `Vispl.HRManagement.UI/Components/Hr/Pages/HrMasterDetail.razor` — **one page for all 20 built
  masters**, at `/hr/settings/masters/{MasterKey}` and `/hr/{TenantKey}/settings/masters/{MasterKey}`.
  Reads and writes go through `IMasterDataService` directly — the same service `/admin/masters`
  calls, not a second copy of the rule (CR-1). Write access is gated behind a new
  `MASTERS.MANAGE` permission check via `HrPortalCaller.HasAsync`, giving that permission (§7) real
  effect for the first time rather than only existing on paper.
- The overlay chrome itself (`AM-01`–`AM-08` in doc 09 — the hamburger trigger, the four-tab
  mega-menu shell shared with Employee/Salary Management/Employee Report) is **not** part of this
  change. `/hr/settings` ships as a normal routed page reachable from `HrNav.razor`'s new
  "Settings" link; wrapping the whole App Configuration tab in the client-locked overlay is a
  separate, larger follow-up.
- **A pre-existing bug was found and fixed while wiring this in**: `HrIdentityExtensions.cs`'s
  `OnRedirectToLogin` handler guessed whether the second path segment of an unauthenticated
  `/hr/{segment}` request was a tenant key or a page name using an incomplete hardcoded list. Every
  top-level HR page *not* on that list — `/hr/people`, `/hr/recruitment`, `/hr/attendance`,
  `/hr/leave`, and now `/hr/settings` — redirected to itself with an ever-growing `returnUrl` query
  string on every unauthenticated visit, an infinite loop. Fixed by adding all five to the reserved
  list, with a comment tying it to `HrNav.razor` so the next new top-level route doesn't silently
  reintroduce it. Confirmed live: all five now redirect to sign-in in a single hop.
