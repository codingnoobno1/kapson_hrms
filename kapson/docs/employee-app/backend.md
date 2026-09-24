# Employee App — Controllers, DTOs and the New Layers

## 1. Purpose

`employee-app.md` defines **what the app shows**. This document defines **what it talks to**:
the controllers it consumes, the DTO contract it binds, the two new layers the solution gains,
and the server work that must be built before the blocked screens can exist.

It is authoritative for **route, contract, permission and layering**. It does not restate screen
behaviour or widget anatomy.

## 2. The layering as it stands

```text
Vispl.HRManagement.Api            controllers, ProblemDetails, auth filters
Vispl.HRManagement.BM(.ITF)       business services and their interfaces
Vispl.HRManagement.DL(.ITF)       EF Core entities, repositories, migrations
Vispl.HRManagement.Contracts      request/response DTOs, shared by API and clients
Vispl.HRManagement.VO             value objects crossing BM/DL
Vispl.HRManagement.Identity       token issuance, permission claims
```

Every mobile controller derives from `MobileApiControllerBase`, which:

- requires the `MobileAuthenticationDefaults.BearerScheme`;
- projects the caller into a `MobileCallerContext` — tenant key, user id, `employee_number`,
  permission claims, correlation id, `Idempotency-Key`, `If-Match`;
- wraps every success in `ApiEnvelope<T>`;
- stamps `MutationResultDto.RowVersion` onto the response `ETag`.

**Business services must never re-read claims or trust an identifier from a request body.**
The caller context is the only identity. This is why `GET /me/summary` needs no employee id in
the route — the token carries it.

Modules with no implementation are registered through `AddNotReady<TService>`, which builds a
`DispatchProxy` whose every member throws `MobileFeatureNotReadyException`. The API maps that to
**501** with a `ProblemDetails` carrying `code: FEATURE_NOT_READY`, `module`, and
`requiredComponents`. A route existing therefore says nothing about whether it works; the
registration in `BM/Mobile/MobileBusinessServiceCollectionExtensions.cs` is the truth.

## 3. The two new layers

### 3.1 `Vispl.HRManagement.EmployeeApp` — new MAUI head

A second MAUI single-project head, peer to `Vispl.HRManagement.Mobile`. Separate app id,
separate icon, separate navigation. It shares `Contracts` and nothing else.

```text
Vispl.HRManagement.EmployeeApp
├─ App.xaml, AppShell.xaml          TabBar with four tabs, no flyout
├─ Features/                        one folder per area: Home, Attendance, Leave, Profile, Passes
│   └─ <Area>/{Pages,Sheets,ViewModels}
├─ Shared/
│   ├─ Controls/                    the widget set in widgets.md
│   ├─ Sheets/                      the bottom-sheet host and base sheet
│   ├─ Converters/
│   └─ Theme/                       tokens from widgets.md §3
└─ MauiProgram.cs                   composition root
```

### 3.2 `Vispl.HRManagement.Mobile.Client` — new, and the important one

This is the layer the solution does not have today and the reason the HR app still runs on
`DemoDataService`. It is a **plain .NET class library**, not MAUI, so it is unit-testable
without a device and can be shared by both app heads.

```text
Vispl.HRManagement.Mobile.Client
├─ Http/
│   ├─ MobileApiClient.cs           HttpClient wrapper: envelope unwrap, problem mapping
│   ├─ MobileApiException.cs        typed: NotReady, Forbidden, Conflict, Validation, Offline
│   ├─ EnvelopeHandler.cs           unwraps ApiEnvelope<T>, surfaces Meta.Warnings
│   ├─ IdempotencyHandler.cs        stamps Idempotency-Key on every non-GET
│   ├─ ETagHandler.cs               carries RowVersion into If-Match
│   └─ AuthHandler.cs               attaches bearer, refreshes once on 401, then signs out
├─ Auth/
│   ├─ IMobileAuthenticator.cs      OIDC Authorization Code + PKCE
│   ├─ ITokenStore.cs               platform secure storage behind an interface
│   └─ MobileSession.cs             tenant, permissions, employee number
├─ Api/                             one typed client per controller (§4)
└─ Caching/
    ├─ ICachePolicy.cs              live / read-cache / draft / no-cache, per §7
    └─ EncryptedCacheStore.cs
```

**Why it is separate.** `PreviewAuthenticationService` and `DemoDataService` in the HR app exist
because there was nowhere to put the real thing. Putting the client in its own library means
the second app does not repeat that mistake, and the first app can be migrated onto it later
without touching its XAML.

**The critical rule:** `MobileApiClient` translates a 501 `ProblemDetails` into a typed
`MobileFeatureNotReadyException(module, requiredComponents)`, and the ViewModel renders the
not-ready panel from it. A 501 must never reach a user as a generic failure.

### 3.3 Resulting solution shape

```text
Vispl.HRManagement.Contracts          ← shared by API and both app heads
Vispl.HRManagement.Mobile.Client      ← NEW: typed clients, auth, cache
Vispl.HRManagement.EmployeeApp        ← NEW: this app
Vispl.HRManagement.Mobile             ← existing HR app, migrates onto the Client later
```

## 4. Controllers the app consumes

Every route below is under `api/mobile/v1`. **Ready** means a real service is registered and
the route is listed in `Vispl.HRManagement.Tests/ImplementedEndpoints.cs`.

### 4.1 Ready today

| Controller | Route | Verb | Screen | Permission |
|---|---|---|---|---|
| `MobileAuthController` | `auth/token` | POST | EMP-002 | anonymous |
| | `auth/refresh` | POST | EMP-001 | anonymous |
| | `auth/revoke` | POST | EMP-065 | bearer |
| `MobileDiscoveryController` | `discovery/metadata` | GET | EMP-005 | anonymous |
| | `discovery/resolve-tenant` | POST | EMP-003 | anonymous |
| `MobileSessionController` | `session/bootstrap` | GET | EMP-001 | bearer |
| | `session/refresh` | POST | EMP-001 | bearer |
| `EmployeeKpiController` | `me/summary` | GET | EMP-010 | self |
| | `me/kpis/attendance` | GET | EMP-012, 020 | self |
| | `me/kpis/leave` | GET | EMP-014 | self |
| `AttendanceController` | `attendance/me` | GET | EMP-012, 020, 021 | self |
| | `attendance/punch` | GET | EMP-022 | self |
| | `attendance/punch` | POST | EMP-022 | self |
| | `attendance/corrections` | GET | EMP-024 | self |
| | `attendance/corrections` | POST | EMP-023 | self |
| | `attendance/corrections/{id}` | GET | EMP-024 | self |
| | `attendance/corrections/{id}/withdraw` | POST | EMP-024 | self |
| | `attendance/corrections/{id}/decision` | POST | — | `ATTENDANCE.APPROVE` |
| `LeaveController` | `leave/me/dashboard` | GET | EMP-014 | `LEAVE.VIEW` |
| | `leave/balances` | GET | EMP-030, 014 | `LEAVE.VIEW` |
| | `leave/requests` | GET | EMP-032 | `LEAVE.VIEW` |
| | `leave/requests/calculate` | POST | EMP-030 | `LEAVE.VIEW` |
| | `leave/requests` | POST | EMP-031 | `LEAVE.VIEW` |
| | `leave/requests/{id}` | GET | EMP-033 | `LEAVE.VIEW` |
| | `leave/requests/{id}/withdraw` | POST | EMP-033 | self |
| | `leave/requests/{id}/comments` | POST | EMP-033 | self |
| | `leave/team/requests` | GET | EMP-035 | `LEAVE.TEAM` |
| | `leave/requests/{id}/decision` | POST | EMP-036 | `LEAVE.APPROVE` |
| | `leave/adjustments` | GET/POST | EMP-014 | `LEAVE.VIEW` |
| `LeavePoliciesController` | `leave/policies` | GET | EMP-030 | `LEAVE.VIEW` |
| | `leave/policies/{id}` | GET | EMP-014 | `LEAVE.VIEW` |
| `EmployeesController` | `employees/{id}` | GET | EMP-040 | self |
| | `employees/{id}/personal` | GET | EMP-040 | self |
| | `employees/{id}/employment` | GET | EMP-040 | self |
| | `employees/{id}/organisation` | GET | EMP-040 | self |
| | `employees/{id}/contacts` | GET | EMP-040 | self |
| | `employees/{id}/timeline` | GET | EMP-040 | self |
| | `employees/{id}/change-requests` | POST | EMP-041 | self |
| | `employees/change-requests/{id}` | GET | EMP-041 | self |
| `GatePassController` | `gate-passes/me` | GET | EMP-051 | self |
| | `gate-passes` | POST | EMP-050 | self |
| | `gate-passes/{id}` | GET | EMP-051 | self |
| | `gate-passes/{id}/credential` | GET | EMP-052 | self |
| | `gate-passes/{id}/withdraw` | POST | EMP-051 | self |
| | `gate-passes/pending` | GET | EMP-054 | `GATEPASS.APPROVE` |
| | `gate-passes/{id}/decision` | POST | EMP-054 | `GATEPASS.APPROVE` |
| `MovementController` | `movement/me` | GET | EMP-053 | self |
| | `movement/out` | POST | EMP-053 | self |
| | `movement/{id}/return` | POST | EMP-053 | self |
| `MobileNotificationsController` | `notifications` | GET | EMP-060 | bearer |
| | `notifications/{id}` | GET | EMP-061 | bearer |
| | `notifications/read` | POST | EMP-060 | bearer |
| | `notifications/{id}` | DELETE | EMP-060 | bearer |

### 4.2 Route exists, service not registered — answers 501

| Controller | Route | Screen | Missing service |
|---|---|---|---|
| `PayrollController` | `payroll/salary-structure` | EMP-043 | `IMobilePayrollService` |
| | `payroll/payslips` | Phase 4 | `IMobilePayrollService` |
| `MobileOnboardingController` | `onboarding/{employeeId}/checklist` | EMP-042 | `IMobileOnboardingService` |
| | `onboarding/tasks/{taskId}` | EMP-042 | `IMobileOnboardingService` |
| `MobileSearchController` | `search`, `search/suggest` | EMP-062 | `IMobileSearchService` |
| `ProfileController` | `profile/preferences` | EMP-064 | `IMobileProfileService` |
| | `profile/notifications` | EMP-064 | `IMobileProfileService` |
| `MobileDevicesController` | `devices`, `devices/push-token` | EMP-065 | `IMobileDeviceService` |
| `ExportsController` | `exports`, `exports/{id}/ticket` | EMP-020 report actions | `IExportService` |
| `ProtectedFilesController` | `files/tickets` | EMP-020, 043 | `IProtectedFileService` |
| `MobileHomeController` | `home/employee` | EMP-010 | see §5 — **stale** |

### 4.3 No controller, no contract, no entity

| Need | Screen |
|---|---|
| Employee bank account | EMP-044 |

Projects and Performance were previously listed here. Both are **out of scope** — see
`employee-app.md` §9.1 and §6.5 below.

## 5. Defect: `home/employee` is a stale 501

`MobileHomeService.GetEmployeeHomeAsync` throws:

```csharp
throw new MobileFeatureNotReadyException(
    "Employee Home", "attendance module", "leave module");
```

Both named modules now exist and are registered — `AttendanceService` and `LeaveService` are
real, DB-backed and covered by `ImplementedEndpoints`. The comment explaining the throw
describes a state that is no longer true.

The app does not need this route. `GET /me/summary` on `EmployeeKpiService` already assembles
the identity card and the four home tiles, and it is the endpoint the mockups match. Two
options, in order of preference:

1. **Delete `GetEmployeeHomeAsync` and the `home/employee` route.** `me/summary` supersedes it,
   and two endpoints for one screen is how they drift apart.
2. Implement it as a projection over `EmployeeKpiService` and add it to `ImplementedEndpoints`.

Do not leave it as it is. A 501 that names satisfied dependencies will be read as a bug.

## 6. New backend work

### 6.1 Employee bank account — the one genuinely new module

Grep confirms nothing exists: no bank, IFSC or account field in `Contracts`, `VO` or `DL`.

**DL** — `Vispl.HRManagement.DL/Employee/EmployeeBankEntities.cs`, plus migration
`AddEmployeeBankModule`:

```text
employee.EmployeeBankAccounts
  Id, TenantKey, EmployeeId, AccountHolderName,
  BankName, BranchName, Ifsc, AccountNumberLast4,
  AccountNumberEncrypted, AccountType, IsPrimary,
  VerifiedOn, VerifiedBy, RowVersion
  IX (TenantKey, EmployeeId, IsPrimary)
```

Store the full account number encrypted at rest and index only the last four. Nothing that
returns a full account number may be cached, logged, or exported.

**Contracts** — `Contracts/Mobile/Employee/BankContracts.cs`:

```csharp
public sealed record EmployeeBankAccountDto(
    string Id,
    string AccountHolderName,
    string BankName,
    string? BranchName,
    string Ifsc,
    string AccountNumberMasked,      // "XXXXXX4821"
    string AccountTypeCode,
    bool IsPrimary,
    StatusDto VerificationStatus,
    IReadOnlyList<FieldAccessDto> FieldAccess,
    string? RowVersion);

public sealed class SaveEmployeeBankAccountRequest { /* full number, holder, IFSC, type */ }
```

`FieldAccessDto` already exists and is the right mechanism: it carries `CanView`, `CanEdit`,
`IsMasked` and a `Reason` per field, so the sheet renders from the contract rather than from a
client-side permission guess.

**BM.ITF / BM** — `IEmployeeBankService` with `GetAsync`, `SaveAsync`, `RemoveAsync`. Registered
in `BusinessServiceCollectionExtensions` **before** `AddMobileBusiness()`, following the pattern
in that file, and added to `ImplementedEndpoints.RealServices`.

**API** — `Employee/Controllers/EmployeeBankController.cs`, route
`api/mobile/v1/employees/{employeeId}/bank-accounts`, GET / PUT / DELETE. Self-service by
default; a caller acting on another employee needs `EMPLOYEES.EDIT`.

New permission: `EMPLOYEES.BANK_VIEW`, `EMPLOYEES.BANK_MANAGE`.

### 6.2 Salary details — extend the payroll contract

`SalaryStructureDto` carries only `EffectiveFrom`, `AnnualTotal` and `Components`. `3.png` also
shows a monthly figure and a payment block — frequency, mode, salary date. Add:

```csharp
public sealed record SalaryPaymentTermsDto(
    string FrequencyCode,      // Monthly
    string PaymentModeCode,    // BankTransfer
    string PayDateRule);       // "Last working day of month"
```

and extend `SalaryStructureDto` with `MoneyDto MonthlyTotal` and
`SalaryPaymentTermsDto PaymentTerms`. Compute the monthly figure server-side — the mockup's
₹66,667 is a rounded division and the app must not be the thing that rounds it.

This is contract work only; the screen stays blocked until `IMobilePayrollService` is registered.

### 6.3 Onboarding status for the employee

`MobileOnboardingController` is not registered, but `OnboardingService` in `BM/Recruitment` is
real and already serves `GET api/mobile/v1/onboarding/{id}` for candidates. Both controllers
share the `api/mobile/v1/onboarding` prefix and differ only by route constraint — the
recruitment one uses `{id:long}`, the mobile one uses `{employeeId}/checklist`. That works
today, but it is fragile, and `ImplementedEndpoints` already records that a route has been
re-declared over a working one once before.

The cheapest correct fix for EMP-042 is a self-scoped read on the existing real service rather
than implementing `IMobileOnboardingService` wholesale:

```text
GET api/mobile/v1/me/onboarding  ->  OnboardingSummaryDto
```

on `EmployeeKpiController`, backed by `IOnboardingService`, resolving the employee from the
caller context. `OnboardingSummaryDto` already exists and carries stage, completion percent and
overdue count — exactly what "View Onboarding status" needs.

### 6.4 Attendance report download and email

Both `7.png` actions need `IExportService` and `IProtectedFileService`. Neither is registered,
and protected files is the wider blocker — payslips and employee documents route their
downloads through the same one-time ticket mechanism. Sequence protected files first.

Email additionally needs a job that hands the rendered report to
`Vispl.HRManagement.MailSender`. That is a worker concern, not a controller concern.

### 6.5 Remove the Projects and Performance tiles

Following the decision in `employee-app.md` §9.1, `EmployeeKpiService.GetHomeSummaryAsync`
returns two tiles, not four. Delete the `ActiveProjects` and `Performance` entries and the
comment above them:

```csharp
// Delete both of these from the kpis list in GetHomeSummaryAsync.
new(EmployeeKpiKeys.ActiveProjects, "Active Projects", "—", ...),
new(EmployeeKpiKeys.Performance,    "Performance",    "—", ...)
```

Mark the two constants obsolete rather than deleting them, so any client still binding by key
fails at compile time rather than silently rendering nothing:

```csharp
[Obsolete("Dropped from scope; no projects module. See docs/employee-app/employee-app.md §9.1.")]
public const string ActiveProjects = "active-projects";

[Obsolete("Dropped from scope; no performance module. See docs/employee-app/employee-app.md §9.1.")]
public const string Performance = "performance";
```

`EmployeeKpiKeys.PresentDays` and `.LeaveBalance` are unchanged, as are all four attendance
keys. `GetAttendanceKpisAsync` and `GetLeaveKpisAsync` are untouched.

This is the only server change the decision requires. No migration, no contract break —
`EmployeeHomeSummaryDto.Kpis` is already a list, and a client binding by key rather than by
index absorbs the change without edit.

## 7. DTO contract the app binds

### 7.1 Shared primitives — already exist, do not redefine

| DTO | Shape | Used for |
|---|---|---|
| `ApiEnvelope<T>` | `Data`, `Meta` | every success response |
| `ApiMetaDto` | `CorrelationId`, `ServerUtc`, `ConfigurationVersion`, `Warnings` | logging, config invalidation |
| `ApiWarningDto` | `Code`, `Message`, `Field?` | inline non-blocking warnings |
| `CursorPage<T>` | `Items`, `NextCursor`, `HasMore`, `PageSize`, `ApproximateTotal?` | every list |
| `MobileListQuery` | `Query`, `Cursor`, `PageSize` (1–100, default 25) | every list query |
| `MutationResultDto` | `ResourceId`, `State`, `RowVersion`, `OccurredUtc`, `Message?` | every write |
| `StatusDto` | `Code`, `Name`, `Tone`, `ChangedUtc?` | every status pill |
| `MoneyDto` | `Amount`, `CurrencyCode`, `IsMasked` | every currency value |
| `KpiDto` | `Key`, `Title`, `Value`, `Unit?`, `Trend?`, `Tone`, `TargetRoute?`, `UnavailableReason?` | every tile |
| `ActionAvailabilityDto` | `Action`, `IsAllowed`, `Reason?`, `RequiresOnline`, `RequiresReason` | every button's enabled state |
| `FieldAccessDto` | `Field`, `CanView`, `CanEdit`, `IsMasked`, `Reason?` | field-level permission |
| `PersonReferenceDto` | `Id`, `DisplayName`, `AvatarUrl?`, `EmployeeNumber?` | manager, approver, actor |
| `MasterReferenceDto` | `Category`, `Code`, `Name`, `IsActive` | every picker |

**`Tone` is a semantic string, not a colour.** The app maps tone to a token from `widgets.md`
§3.3. Never send a hex value from the server.

**`KpiDto.UnavailableReason` is the not-ready signal.** A tile with a non-null `UnavailableReason` renders as unavailable.
Active Projects and Performance used this mechanism before they were dropped; it stays in the
contract for the next module that arrives half-built.

**`ActionAvailabilityDto` drives button state.** Do not compute whether a leave request can be
withdrawn on the client; read `Actions` on the detail DTO.

### 7.2 Home and KPI

```csharp
EmployeeIdentityCardDto(EmployeeId, EmployeeNumber, DisplayName, JobTitle?,
                        DepartmentName?, Phone?, AvatarUrl?, QrPayload)

EmployeeHomeSummaryDto(Identity, Kpis, PeriodFrom, PeriodTo, GeneratedUtc)
AttendanceKpiSummaryDto(Year, Month, MonthName, Kpis, WorkingDays, GeneratedUtc)
LeaveKpiSummaryDto(Year, Kpis, GeneratedUtc)
```

`EmployeeKpiKeys` fixes the tile keys, and the app must bind by key rather than by index:

| Constant | Key | Screen |
|---|---|---|
| `PresentDays` | `present-days` | EMP-010, EMP-012 |
| `LeaveBalance` | `leave-balance` | EMP-010 |
| ~~`ActiveProjects`~~ | `active-projects` | **Removed** — see §6.5 |
| ~~`Performance`~~ | `performance` | **Removed** — see §6.5 |
| `AbsentDays` | `absent-days` | EMP-012 |
| `HalfDays` | `half-days` | EMP-012 |
| `Holidays` | `holidays` | EMP-012 |

`AttendanceKpiQuery.Month` is `yyyy-MM`, regex-validated. `AttendanceKpiSummaryDto` echoes the
year and month back so the stepper and the grid cannot disagree about which month is shown.

### 7.3 Attendance

```csharp
AttendanceDayDto(Date, ShiftCode?, Status, Punches, Worked, Expected, Exceptions, Actions)
AttendancePunchDto(...)
PunchStateDto(State, LastPunch?, NextAction?, ServerUtc, PolicySummary?)
PunchRequest{ Action("in"|"out"), DeviceId, ClientEventId, ClientOccurredUtc, Lat?, Lng?, Attestation? }
AttendanceCorrectionDto(...)
```

`Status.Code` values the calendar and the row pills key off: `Present`, `Partial`, `Absent`,
`Leave`, `Holiday`, `Weekend`. `Partial` is what the mockups label **Half Day** — the app
translates, the server does not rename.

`ClientEventId` is the idempotency key for a punch. Generate it once per user gesture and reuse
it across retries; a new id is a new punch.

### 7.4 Leave

```csharp
LeaveBalanceDto(...)              // per policy: accrued, carried forward, used, pending, available
LeaveRequestListItemDto(...)      // EMP-032 rows
LeaveRequestDetailDto(...)        // EMP-033
LeaveCalculationRequest / LeaveCalculationDto
CreateLeaveRequest
LeavePolicySummaryDto / LeavePolicyDetailDto
LeaveAdjustmentCreditDto / LeaveAdjustmentRequest
```

The calculate-then-submit handshake is mandatory. `POST leave/requests/calculate` returns a
signed token valid for 30 minutes; `POST leave/requests` rejects a submission without it. The
chargeable day count is computed from policy, holidays and already-claimed dates — the app must
never derive it, and must never display a count it computed itself.

### 7.5 Employee record

```csharp
EmployeeOverviewDto(Summary, Manager?, JoinedOn?, AvailableSections, Actions, RowVersion?)
EmployeeListItemDto(Id, EmployeeNumber, DisplayName, AvatarUrl?, JobTitle, Department, Location, Status, Presence?)
EmployeePersonalDto(..., FieldAccess, RowVersion?)
EmployeeEmploymentDto(..., JoinedOn, EmploymentTypeCode, EmploymentStatusCode, GradeCode?, ProbationEndsOn?)
EmployeeOrganisationDto(..., DepartmentCode, LocationCode, Manager?, CostCentre?)
EmployeeChangeRequestDto
```

`2.png` renders from `EmployeeOverviewDto` plus `EmployeePersonalDto` and
`EmployeeOrganisationDto`. Note the mockup shows resolved display names — Operations, Kapsons
Retail, Jane Smith — while the DTOs carry codes. Resolve codes through
`GET /masters` (`MasterReferenceDto`), cached by `ConfigurationVersion`; do not display a raw
code.

`Update Profile` is not a PUT. Self-service edits raise
`POST employees/{id}/change-requests`, which HR approves. The sheet must say so.

### 7.6 Gate pass and movement

```csharp
GatePassRowDto(...)  GatePassBoardDto(...)  GatePassCredentialDto(PassId, VerificationToken, ValidFrom, ValidTo)
RequestGatePassRequest  GatePassDecisionRequest  ScanGatePassRequest  GatePassScanResultDto
MovementRowDto(...)  DeclareMovementRequest  ReturnMovementRequest
```

`GatePassCredentialDto.VerificationToken` is the only value that belongs in a scannable QR.
See `employee-app.md` §9.6.

## 8. Error contract the client must handle

| Status | Meaning | App behaviour |
|---|---|---|
| 200 | `ApiEnvelope<T>` | render; surface `Meta.Warnings` inline |
| 400 | validation `ProblemDetails` | field-level errors on the form |
| 401 | token expired | refresh once through `AuthHandler`; on second failure sign out |
| 403 | `TENANT_CLAIM_REQUIRED` or permission | hide or lock the surface, explain why |
| 404 | not found or not visible to caller | empty state, never "deleted" |
| 409 | `If-Match` mismatch | reload, show what changed, ask again |
| 412 | stale calculation token | recalculate silently, then resubmit |
| 501 | `FEATURE_NOT_READY` | not-ready panel naming `module` |
| 503 / offline | no connection | cached read where policy allows, else offline state |

## 9. Cache policy per endpoint

Cache classes match `screens.md` §9 so the two apps behave alike.

| Endpoint | Class |
|---|---|
| `me/summary`, `me/kpis/*` | read cache, 15 min, encrypted |
| `attendance/me` | read cache, 60 min, encrypted, per month |
| `leave/balances`, `leave/policies` | read cache, invalidated on `ConfigurationVersion` |
| `leave/requests` list | read cache, 15 min, no attachments |
| `employees/{id}/personal`, `/organisation` | **no cache** — PII |
| `payroll/*`, bank accounts | **no cache, memory only** |
| `gate-passes/{id}/credential` | memory only, never persisted |
| every mutation | live only; queue nothing offline in Phase 1–3 |
| `masters` | versioned cache keyed by `ConfigurationVersion` |

## 10. Build order for server work

1. Remove the Projects and Performance tiles from `EmployeeKpiService` (§6.5). Minutes, and it
   is the only server change the scope decision requires.
2. Delete or implement `home/employee` (§5). One-line change, removes a false signal.
3. `GET me/onboarding` on `EmployeeKpiController` over the real `IOnboardingService` (§6.3).
   Unblocks EMP-042 without a new module.
4. Employee bank module: entity, migration, repository, service, contract, controller (§6.1).
   Unblocks EMP-044.
5. `IProtectedFileService`. Unblocks every download in both apps.
6. `IMobilePayrollService` plus the contract extension in §6.2. Unblocks EMP-043.
7. `IExportService`, then the mail hand-off. Unblocks the EMP-020 report actions.

Steps 1–3 are hours. Step 4 is a day. Steps 5–7 are module work and belong to Phase 4.

## 11. Definition of done, per endpoint

An endpoint is done for this app when: the route is registered; a real service backs it and
appears in `ImplementedEndpoints.RealServices`; the route appears in
`ImplementedEndpoints.Routes`; the permission is enforced server-side; mutations honour
`Idempotency-Key` and `If-Match`; the DTO carries `Actions` and `FieldAccess` where the UI needs
to decide what to enable; and a typed client method exists in `Vispl.HRManagement.Mobile.Client`
with a test that covers the 200, 403 and 501 paths.
