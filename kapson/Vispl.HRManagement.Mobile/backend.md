# HR Management Mobile — Backend Controllers and DTO Contract

## 1. Purpose

This document defines the backend required by the 112-screen .NET MAUI application in `screens.md`. It lists the mobile API controllers, routes, operations, request/response DTOs, authentication rules, tenant boundaries, caching, concurrency, files, and the implementation order.

This is an **implementation plan**, not a statement that the APIs already exist.

## 2. Current Backend Status

The current solution already has:

- one runnable server: `Vispl.HRManagement.Host` on port `7000`;
- SQL Server persistence for tenants, career-site configuration, masters, media, jobs, and Identity;
- Platform Admin cookie authentication;
- tenant-scoped HR and Employee accounts, roles, and permission grants;
- business/repository services for existing Admin and job-opening functions;
- module composition extensions for Admin, Recruitment, Candidate, and Employee.

The current solution does **not** yet have:

- mobile controllers under `/api/mobile/v1`;
- OAuth/OIDC access-token issuance for HR and Employee accounts;
- employee, attendance, leave, payroll, approval, notification, or report data models;
- the application services and repositories needed by most mobile screens;
- mobile OpenAPI and generated API clients;
- device sessions, refresh-token rotation, push registration, or mobile sync storage.

Therefore, the controller list below is a target contract. Controllers must only be implemented after their BM interfaces, persistence ownership, permissions, and DTOs exist.

## 3. Deployment and Project Boundary

Do not create another server for the first release. Add one presentation library:

```text
Vispl.HRManagement.Api                       Class library, not executable
├── Shared
│   ├── Authentication
│   ├── Authorization
│   ├── Controllers
│   ├── Filters
│   ├── OpenApi
│   └── Serialization
├── Admin
│   └── MobileConfiguration
├── Recruitment
│   └── Controllers
└── Employee
    ├── Controllers
    ├── Attendance
    ├── Leave
    └── Payroll

Vispl.HRManagement.Contracts
└── Mobile
    ├── Shared
    ├── Bootstrap
    ├── Recruitment
    ├── Employee
    ├── Attendance
    ├── Leave
    ├── Payroll
    ├── Approvals
    └── Reports
```

Dependency direction:

```text
MAUI -> HTTPS -> API controllers -> BM.ITF -> BM -> DL.ITF -> DL -> SQL Server
                         |
                         `-----------> Contracts
```

Rules:

- `Vispl.HRManagement.Api` references Contracts and BM.ITF, never DL or EF entities.
- `Vispl.HRManagement.Host` references the API assembly and remains the only executable.
- Controllers validate HTTP input, invoke one application use case, map results, and return HTTP responses. They do not contain business rules.
- DTOs are never EF entities, Identity entities, BM records, or the existing Admin edit models.
- Future module hosts can load only the controller/application slices they own without changing public routes.

Initial start remains:

```powershell
dotnet run --project Vispl.HRManagement.Host
```

All web pages and mobile APIs initially use the same process and port:

```text
http://localhost:7000/api/mobile/v1/...
```

## 4. Host Configuration

Target Host registration:

```csharp
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(MobileApiAssemblyMarker).Assembly);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("mobile-v1");
builder.Services.AddMobileBearerAuthentication(builder.Configuration);
builder.Services.AddMobileAuthorization();
builder.Services.AddMobileRateLimiting();
builder.Services.AddMobileApi();

// Existing modular services remain registered.
builder.Services.AddHrManagement(builder.Configuration);
```

Target pipeline additions:

```csharp
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHrManagement();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
}
```

OpenAPI must be generated for the mobile API and used to generate a typed MAUI client. The interactive API UI, if added, stays disabled in production.

## 5. Controller Standard

```csharp
[ApiController]
[Route("api/mobile/v1/recruitment/jobs")]
[Authorize(AuthenticationSchemes = MobileAuthenticationDefaults.BearerScheme)]
[EnableRateLimiting(MobileRateLimitPolicies.StandardRead)]
public sealed class MobileJobsController(IJobMobileService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("RECRUITMENT.VIEW")]
    [ProducesResponseType<ApiEnvelope<CursorPage<JobListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<CursorPage<JobListItemDto>>>> GetAsync(
        [FromQuery] JobListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(User.GetTenantContext(), query, cancellationToken));
}
```

Controller requirements:

- `[ApiController]` for binding and automatic validation responses;
- explicit route, permission, success type, and error responses;
- `CancellationToken` on every asynchronous action;
- no controller accepts `TenantKey` for an authenticated tenant operation;
- no controller reads `DbContext` or repository directly;
- no unbounded `GET all` operation;
- every mutation is audited and has concurrency/idempotency behaviour;
- collection endpoints use cursor paging unless the dataset is a small versioned master list.

## 6. HTTP Contract

### 6.1 Success and error shapes

```csharp
public sealed record ApiEnvelope<T>(T Data, ApiMetaDto Meta);

public sealed record ApiMetaDto(
    string CorrelationId,
    DateTimeOffset ServerUtc,
    long ConfigurationVersion,
    IReadOnlyList<ApiWarningDto> Warnings);

public sealed record CursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore,
    int PageSize,
    long? ApproximateTotal);
```

Failures use `ProblemDetails`/`ValidationProblemDetails` with these extensions:

```json
{
  "type": "https://api.hr.example.com/problems/concurrency-conflict",
  "title": "The record was changed by another user.",
  "status": 412,
  "code": "CONCURRENCY_CONFLICT",
  "correlationId": "...",
  "traceId": "...",
  "errors": { "effectiveDate": ["The date is outside the active period."] }
}
```

### 6.2 Status codes

| Code | Use |
|---:|---|
| 200 | Successful read or mutation returning a representation |
| 201 | Resource created; include `Location` |
| 202 | Export/document/background job accepted |
| 204 | Successful mutation with no body |
| 304 | Client cache still valid after `If-None-Match` |
| 400 | Invalid syntax/query/model binding |
| 401 | Missing, expired, invalid-issuer or invalid-audience access token |
| 403 | Authenticated but missing portal, tenant, scope or permission |
| 404 | Resource absent **or outside the caller's tenant/scope** |
| 409 | Business-state conflict or duplicate idempotency key with different payload |
| 412 | `If-Match`/row-version conflict |
| 422 | Semantically invalid workflow request |
| 429 | Rate limit exceeded; return `Retry-After` |

### 6.3 Standard headers

| Header | Direction | Rule |
|---|---|---|
| `Authorization: Bearer` | Request | OAuth access token only; never a portal cookie or ID token |
| `X-Correlation-ID` | Both | Accept safe client ID or generate one; return it on every response |
| `X-Device-ID` | Request | Registered device identifier, verified against token/session |
| `X-App-Version` | Request | Semantic app version used by release policy |
| `X-Platform` | Request | `android`, `ios`, or approved development target |
| `Accept-Language` | Request | Locale selection; server still stores invariant values |
| `Idempotency-Key` | Mutation | Required for create, decision, punch, submit and send operations |
| `If-Match` | Mutation | Base64/opaque row-version required for editable resources |
| `ETag` | Response | Configuration, masters and mutable detail resources |
| `Retry-After` | Response | Rate limit, maintenance or temporary unavailable response |

## 7. Authentication, Claims and Tenant Isolation

The existing Platform Admin cookie login is not a mobile-token authority. Mobile requires a standards-compliant OpenID Connect/OAuth authority using **Authorization Code with PKCE**. Do not create a custom username/password-to-JWT endpoint.

Required mobile client registration:

```text
Client ID:          vispl.hr.mobile
Client type:        public/native
Grant:              authorization_code
PKCE:               required, S256
Refresh tokens:     rotating, reuse detection enabled
Redirect URIs:      visplhr://oauth/callback plus verified universal/app links
Post logout URI:    visplhr://oauth/logout-complete
Scopes:             openid profile offline_access hr.mobile employee.mobile
```

Required access-token claims:

| Claim | Purpose |
|---|---|
| `sub` | Immutable Identity user ID |
| `tenant_id` | Server-owned tenant identifier/key |
| `account_portal` | `Hr` or `Employee` |
| `employee_id` | Employee record link when applicable |
| `permission` | Effective permission keys from all active roles |
| `scope` | Approved API scopes |
| `client_id` | Must equal registered native client |
| `device_id` | Bound registered device/session where enabled |
| `security_stamp` or session version | Immediate revocation/account-change validation strategy |

The server obtains the tenant from the validated principal. Client-supplied tenant keys are accepted only for anonymous tenant discovery and Platform Admin operations. Tenant filtering is applied before looking up an entity so cross-tenant identifiers return `404`.

## 8. Controller Inventory — 38 Controllers

| ID | Controller | Base route | Main operations | Screen coverage |
|---|---|---|---|---|
| MBK-001 | `MobileDiscoveryController` | `/api/mobile/v1/discovery` | resolve company, public auth metadata, app availability | MOB-001–003 |
| MBK-002 | `MobileSessionController` | `/api/mobile/v1/session` | bootstrap session, refresh profile state, logout/revoke, offline grant | MOB-004, 008–010, 112 |
| MBK-003 | `MobileDevicesController` | `/api/mobile/v1/devices` | register, list, rename, revoke, push token | MOB-005–007, 109 |
| MBK-004 | `MobileConfigurationController` | `/api/mobile/v1/configuration` | published theme/modules/navigation/dashboard/release policy | MOB-001, 011–013, 020, 107–112 |
| MBK-005 | `MobileMastersController` | `/api/mobile/v1/masters` | small versioned lookup sets and changes | All forms and filters |
| MBK-006 | `MobileHomeController` | `/api/mobile/v1/home` | HR, employee and manager home projections | MOB-011–013 |
| MBK-007 | `MobileSearchController` | `/api/mobile/v1/search` | suggestions and permission-shaped federated results | MOB-014–015 |
| MBK-008 | `MobileNotificationsController` | `/api/mobile/v1/notifications` | list, detail, read/delete, preferences summary | MOB-016–017, 108 |
| MBK-009 | `MobileCalendarController` | `/api/mobile/v1/calendar` | unified event ranges and event details | MOB-018 |
| MBK-010 | `MobileTasksController` | `/api/mobile/v1/tasks` | task list/detail, complete, comment | MOB-019 |
| MBK-011 | `RecruitmentDashboardController` | `/api/mobile/v1/recruitment/dashboard` | recruitment KPIs and funnel | MOB-021 |
| MBK-012 | `RequisitionsController` | `/api/mobile/v1/recruitment/requisitions` | list, detail, draft, submit, cancel, approvals | MOB-022–025 |
| MBK-013 | `JobsController` | `/api/mobile/v1/recruitment/jobs` | list/filter, CRUD draft, validate, publish, analytics | MOB-026–033 |
| MBK-014 | `ApplicationsController` | `/api/mobile/v1/recruitment/applications` | list/pipeline, detail, stage, decision, rating | MOB-034–036, 040–042 |
| MBK-015 | `CandidatesController` | `/api/mobile/v1/recruitment/candidates` | authorised profile and résumé summary | MOB-037–038 |
| MBK-016 | `CandidateDocumentsController` | `/api/mobile/v1/recruitment/candidate-documents` | list, upload request, preview/download, verify/request | MOB-038–039 |
| MBK-017 | `InterviewsController` | `/api/mobile/v1/recruitment/interviews` | schedule, availability, detail, reschedule, evaluation | MOB-043–046 |
| MBK-018 | `OffersController` | `/api/mobile/v1/recruitment/offers` | list, detail, generate, approve/reject, send | MOB-047–048 |
| MBK-019 | `EmployeesController` | `/api/mobile/v1/employees` | directory, detail sections, create/update, timeline | MOB-049–056, 058–062 |
| MBK-020 | `EmployeeDocumentsController` | `/api/mobile/v1/employees/{employeeId}/documents` | list, upload, preview/download, verify/remove | MOB-057, 066 |
| MBK-021 | `OnboardingController` | `/api/mobile/v1/onboarding` | cohorts, checklist, tasks, evidence, reassignment | MOB-063–066 |
| MBK-022 | `ProbationController` | `/api/mobile/v1/probation` | review, goals, confirmation/extension | MOB-067 |
| MBK-023 | `OffboardingController` | `/api/mobile/v1/offboarding` | initiate, checklist, clearance and closure | MOB-068 |
| MBK-024 | `AttendanceController` | `/api/mobile/v1/attendance` | dashboards, own/team/register, punch, corrections, exceptions | MOB-069–075, 078 |
| MBK-025 | `ShiftsController` | `/api/mobile/v1/shifts` | roster, assignment, edit, swap and acknowledge | MOB-076–077 |
| MBK-026 | `LeaveController` | `/api/mobile/v1/leave` | dashboard, balances, requests, decisions, calendar, adjustments | MOB-079–086, 088 |
| MBK-027 | `LeavePoliciesController` | `/api/mobile/v1/leave/policies` | eligible policy details and versions | MOB-080, 087 |
| MBK-028 | `PayrollController` | `/api/mobile/v1/payroll` | payslips, tax, declarations and salary structure | MOB-089–093 |
| MBK-029 | `ReimbursementsController` | `/api/mobile/v1/reimbursements` | list, detail, draft/submit, receipts | MOB-094–095 |
| MBK-030 | `BenefitsController` | `/api/mobile/v1/benefits` | plans, comparison, dependants and enrolment | MOB-096 |
| MBK-031 | `ApprovalsController` | `/api/mobile/v1/approvals` | inbox, detail, decide and delegation | MOB-097–098, 102 |
| MBK-032 | `HrRequestsController` | `/api/mobile/v1/requests` | catalog, schema, submit, detail, comment/cancel | MOB-099–101 |
| MBK-033 | `ReportsController` | `/api/mobile/v1/reports` | catalog, filter schema, execute and drill down | MOB-103–105 |
| MBK-034 | `ExportsController` | `/api/mobile/v1/exports` | create export, status, protected download, delete | MOB-033, 072, 103–106 |
| MBK-035 | `ProfileController` | `/api/mobile/v1/profile` | own profile, preferences, notification settings, avatar | MOB-107–109 |
| MBK-036 | `SupportController` | `/api/mobile/v1/support` | help articles, ticket and redacted diagnostics upload | MOB-111–112 |
| MBK-037 | `SyncController` | `/api/mobile/v1/sync` | delta token, approved read changes, outbox result/status | MOB-009, 110 |
| MBK-038 | `ProtectedFilesController` | `/api/mobile/v1/files` | short-lived file tickets, ranged stream, explicit delete | MOB-038–039, 057, 089–092, 095, 106 |

## 9. Controller Operation Contract

### 9.1 Bootstrap, session and configuration

| Controller | Operations | Request DTOs | Response DTOs |
|---|---|---|---|
| Discovery | `GET /metadata`, `POST /resolve-tenant` | `ResolveTenantRequest` | `PublicMobileMetadataDto`, `TenantDiscoveryDto` |
| Session | `GET /me`, `POST /logout`, `POST /offline-grant`, `POST /heartbeat` | `LogoutRequest`, `OfflineGrantRequest`, `SessionHeartbeatRequest` | `MobileSessionDto`, `OfflineGrantDto`, `SessionHeartbeatDto` |
| Devices | `POST /`, `GET /`, `PATCH /{id}`, `DELETE /{id}`, `PUT /{id}/push-token` | `RegisterDeviceRequest`, `RenameDeviceRequest`, `UpdatePushTokenRequest` | `RegisteredDeviceDto`, `DeviceRegistrationResultDto` |
| Configuration | `GET /`, `GET /theme`, `GET /navigation` | `ConfigurationQuery` | `MobileConfigurationDto`, `MobileThemeDto`, `MobileNavigationDto` |
| Masters | `GET /`, `GET /changes` | `MasterDataQuery`, `MasterChangesQuery` | `MasterDataBundleDto`, `MasterChangesDto` |

### 9.2 Global experience

| Controller | Operations | Request DTOs | Response DTOs |
|---|---|---|---|
| Home | `GET /hr`, `GET /employee`, `GET /manager` | `HomeQuery` | `HrHomeDto`, `EmployeeHomeDto`, `ManagerHomeDto` |
| Search | `GET /suggestions`, `GET /` | `SearchSuggestionQuery`, `GlobalSearchQuery` | `SearchSuggestionDto`, `GlobalSearchResultDto` |
| Notifications | `GET /`, `GET /{id}`, `PUT /{id}/read`, `PUT /read-all`, `DELETE /{id}` | `NotificationListQuery`, `MarkAllReadRequest` | `NotificationListItemDto`, `NotificationDetailDto`, `UnreadCountDto` |
| Calendar | `GET /events`, `GET /events/{id}` | `CalendarRangeQuery` | `CalendarEventDto`, `CalendarEventDetailDto` |
| Tasks | `GET /`, `GET /{id}`, `POST /{id}/complete`, `POST /{id}/comments` | `TaskListQuery`, `CompleteTaskRequest`, `AddTaskCommentRequest` | `TaskListItemDto`, `TaskDetailDto`, `TaskMutationResultDto` |

### 9.3 Recruitment

| Controller | Operations | Request DTOs | Response DTOs |
|---|---|---|---|
| Dashboard | `GET /` | `RecruitmentDashboardQuery` | `RecruitmentDashboardDto` |
| Requisitions | `GET /`, `GET /{id}`, `POST /`, `PUT /{id}`, `POST /{id}/submit`, `POST /{id}/cancel`, `GET /{id}/approvals` | `RequisitionListQuery`, `SaveRequisitionRequest`, `RequisitionActionRequest` | `RequisitionListItemDto`, `RequisitionDetailDto`, `RequisitionApprovalEntryDto` |
| Jobs | `GET /`, `GET /{id}`, `POST /`, `PUT /{id}`, `POST /{id}/validate`, `POST /{id}/publish`, `POST /{id}/withdraw`, `POST /{id}/duplicate`, `GET /{id}/analytics` | `JobListQuery`, `SaveJobDraftRequest`, `PublishJobRequest`, `DuplicateJobRequest`, `JobAnalyticsQuery` | `JobListItemDto`, `JobDetailDto`, `JobValidationDto`, `JobAnalyticsDto` |
| Applications | `GET /`, `GET /pipeline`, `GET /{id}`, `POST /{id}/stage`, `POST /{id}/decision`, `PUT /{id}/rating`, `GET/PUT /{id}/screening` | `ApplicationListQuery`, `PipelineQuery`, `ChangeApplicationStageRequest`, `ApplicationDecisionRequest`, `UpdateRatingRequest`, `SaveScreeningRequest` | `ApplicationListItemDto`, `ApplicationPipelineDto`, `ApplicationDetailDto`, `ScreeningScorecardDto` |
| Candidates | `GET /{id}`, `GET /{id}/resume-summary` | `CandidateProfileQuery` | `CandidateProfileDto`, `ResumeSummaryDto` |
| Candidate documents | `GET /application/{id}`, `POST /upload-intent`, `POST /{id}/verify`, `POST /{id}/request`, `POST /{id}/file-ticket` | `DocumentUploadIntentRequest`, `VerifyCandidateDocumentRequest`, `RequestCandidateDocumentRequest`, `FileTicketRequest` | `CandidateDocumentDto`, `UploadIntentDto`, `ProtectedFileTicketDto` |
| Interviews | `GET /`, `GET /{id}`, `GET /availability`, `POST /`, `PUT /{id}/schedule`, `POST /{id}/cancel`, `GET/PUT /{id}/evaluation`, `POST /{id}/evaluation/submit` | `InterviewListQuery`, `AvailabilityQuery`, `ScheduleInterviewRequest`, `SaveInterviewEvaluationRequest` | `InterviewListItemDto`, `InterviewDetailDto`, `InterviewAvailabilityDto`, `InterviewEvaluationDto` |
| Offers | `GET /`, `GET /{id}`, `POST /{id}/generate`, `POST /{id}/decision`, `POST /{id}/send`, `POST /{id}/file-ticket` | `OfferListQuery`, `GenerateOfferRequest`, `OfferDecisionRequest`, `SendOfferRequest` | `OfferListItemDto`, `OfferDetailDto`, `OfferActionResultDto`, `ProtectedFileTicketDto` |

### 9.4 Employees and lifecycle

| Controller | Operations | Request DTOs | Response DTOs |
|---|---|---|---|
| Employees | `GET /`, `GET /{id}`, section reads/updates, `POST /`, `GET /{id}/timeline`, contact/dependant CRUD | `EmployeeListQuery`, `CreateEmployeeRequest`, section update requests, `SaveContactRequest`, `SaveDependantRequest` | `EmployeeListItemDto`, `EmployeeOverviewDto`, section DTOs, `EmployeeTimelineEntryDto` |
| Employee documents | `GET /`, `POST /upload-intent`, `POST /{documentId}/verify`, `DELETE /{documentId}`, `POST /{documentId}/file-ticket` | `EmployeeDocumentUploadIntentRequest`, `VerifyEmployeeDocumentRequest`, `DeleteDocumentRequest` | `EmployeeDocumentDto`, `UploadIntentDto`, `ProtectedFileTicketDto` |
| Onboarding | `GET /`, `GET /{employeeId}`, `GET /tasks/{id}`, `POST /tasks/{id}/complete`, `POST /tasks/{id}/reassign`, `POST /tasks/{id}/evidence` | `OnboardingListQuery`, `CompleteOnboardingTaskRequest`, `ReassignTaskRequest`, `AddTaskEvidenceRequest` | `OnboardingSummaryDto`, `OnboardingChecklistDto`, `OnboardingTaskDetailDto` |
| Probation | `GET /{employeeId}`, `PUT /{employeeId}/review`, `POST /{employeeId}/confirm`, `POST /{employeeId}/extend` | `SaveProbationReviewRequest`, `ConfirmProbationRequest`, `ExtendProbationRequest` | `ProbationDetailDto`, `ProbationActionResultDto` |
| Offboarding | `GET /{employeeId}`, `POST /`, `PUT /{id}/tasks/{taskId}`, `POST /{id}/close` | `InitiateOffboardingRequest`, `UpdateClearanceTaskRequest`, `CloseOffboardingRequest` | `OffboardingDetailDto`, `ClearanceTaskDto`, `OffboardingActionResultDto` |

### 9.5 Attendance, shifts and leave

| Controller | Operations | Request DTOs | Response DTOs |
|---|---|---|---|
| Attendance | dashboards, own/team/register reads, `POST /punch`, correction CRUD/actions, exception detail/resolve | `AttendanceRangeQuery`, `AttendanceRegisterQuery`, `PunchRequest`, `CreateAttendanceCorrectionRequest`, `AttendanceCorrectionDecisionRequest`, `ResolveAttendanceExceptionRequest` | dashboard/day/register DTOs, `PunchResultDto`, correction/exception DTOs |
| Shifts | `GET /roster`, `GET /assignments/{id}`, `PUT /assignments/{id}`, `POST /assignments/{id}/swap`, `POST /assignments/{id}/acknowledge` | `ShiftRosterQuery`, `UpdateShiftAssignmentRequest`, `RequestShiftSwapRequest` | `ShiftRosterDto`, `ShiftAssignmentDto`, `ShiftSwapResultDto` |
| Leave | dashboard/balances, request CRUD, review calculation, decision, team requests/calendar, comp-off/encashment | `LeaveBalanceQuery`, `CreateLeaveRequest`, `LeaveCalculationRequest`, `LeaveDecisionRequest`, `TeamLeaveQuery`, `LeaveAdjustmentRequest` | leave dashboard/balance/request/decision/calendar DTOs |
| Leave policies | `GET /`, `GET /{id}` | `LeavePolicyQuery` | `LeavePolicySummaryDto`, `LeavePolicyDetailDto` |

### 9.6 Payroll, approvals, requests and reports

| Controller | Operations | Request DTOs | Response DTOs |
|---|---|---|---|
| Payroll | payslip list/detail/ticket, tax summary, declarations read/save/submit, salary structure | payroll period queries, `SaveTaxDeclarationRequest`, `SubmitTaxDeclarationRequest` | `PayslipListItemDto`, `PayslipDetailDto`, `TaxSummaryDto`, `TaxDeclarationDto`, `SalaryStructureDto` |
| Reimbursements | list/detail, create/update draft, submit/withdraw, receipt upload | `ReimbursementListQuery`, `SaveReimbursementRequest`, `SubmitReimbursementRequest` | `ReimbursementListItemDto`, `ReimbursementDetailDto`, `ReimbursementMutationDto` |
| Benefits | plans, comparison, current coverage, enrollment submit | `BenefitPlansQuery`, `BenefitComparisonRequest`, `SubmitBenefitEnrollmentRequest` | `BenefitPlanDto`, `BenefitComparisonDto`, `BenefitCoverageDto`, `BenefitEnrollmentResultDto` |
| Approvals | inbox/detail, approve/reject/return, delegation CRUD | `ApprovalInboxQuery`, `ApprovalDecisionRequest`, `SaveApprovalDelegationRequest` | `ApprovalListItemDto`, `ApprovalDetailDto`, `ApprovalDecisionResultDto`, `ApprovalDelegationDto` |
| HR requests | catalog/schema, create, detail, comment, cancel, attachment | `RequestCatalogQuery`, `CreateHrRequest`, `AddRequestCommentRequest`, `CancelHrRequest` | `HrServiceDefinitionDto`, `DynamicFormSchemaDto`, `HrRequestDetailDto` |
| Reports | catalog/schema, run, page/drilldown | `ReportCatalogQuery`, `RunReportRequest`, `ReportPageQuery`, `ReportDrilldownRequest` | `ReportDefinitionDto`, `ReportFilterSchemaDto`, `ReportResultDto`, `ReportDrilldownDto` |
| Exports | create, list/status, ticket, remove | `CreateExportRequest`, `ExportListQuery`, `FileTicketRequest` | `ExportJobDto`, `ProtectedFileTicketDto` |

### 9.7 Profile, support, sync and files

| Controller | Operations | Request DTOs | Response DTOs |
|---|---|---|---|
| Profile | own profile, avatar, preferences, notification preferences | `UpdateOwnProfileRequest`, `UpdateAppPreferencesRequest`, `UpdateNotificationPreferencesRequest`, `AvatarUploadIntentRequest` | `OwnProfileDto`, `AppPreferencesDto`, `NotificationPreferencesDto` |
| Support | help categories/articles/search, ticket, diagnostic consent upload | `HelpSearchQuery`, `CreateSupportTicketRequest`, `SubmitDiagnosticsRequest` | `HelpArticleSummaryDto`, `HelpArticleDto`, `SupportTicketResultDto` |
| Sync | bootstrap delta, approved entity changes, outbox result lookup | `SyncChangesQuery`, `SyncMutationStatusQuery` | `SyncManifestDto`, `SyncChangePageDto`, `SyncMutationStatusDto` |
| Protected files | redeem one-time ticket, ranged stream, delete local/export artifact | ticket in route/header | binary stream with strict content headers; `FileMetadataDto` |

## 10. DTO Catalogue

All DTOs below live under `Vispl.HRManagement.Contracts.Mobile`. Request types use validation attributes for transport constraints; business validation remains in BM. `TenantKey` is intentionally absent from authenticated tenant DTOs.

### 10.1 Shared DTOs

| DTO | Key fields |
|---|---|
| `ApiEnvelope<T>` | `Data`, `Meta` |
| `ApiMetaDto` | `CorrelationId`, `ServerUtc`, `ConfigurationVersion`, `Warnings` |
| `ApiWarningDto` | `Code`, `Message`, `Field` |
| `CursorPage<T>` | `Items`, `NextCursor`, `HasMore`, `PageSize`, `ApproximateTotal` |
| `SortDto` | `Field`, `Direction` |
| `DateRangeDto` | `From`, `To`, `TimeZoneId` |
| `MoneyDto` | decimal `Amount`, ISO `CurrencyCode`, optional `IsMasked` |
| `PersonReferenceDto` | opaque `Id`, `DisplayName`, `AvatarUrl`, `EmployeeNumber` when permitted |
| `MasterReferenceDto` | `Category`, `Code`, `Name`, `IsActive` |
| `StatusDto` | `Code`, `Name`, `Tone`, `ChangedUtc` |
| `FieldAccessDto` | `Field`, `CanView`, `CanEdit`, `IsMasked`, `Reason` |
| `ActionAvailabilityDto` | `Action`, `IsAllowed`, `Reason`, `RequiresOnline`, `RequiresReason` |
| `AuditEntryDto` | `Id`, `Action`, `Actor`, `OccurredUtc`, `Summary` |
| `WorkflowStepDto` | `Code`, `Name`, `State`, `Actor`, `DueUtc`, `CompletedUtc` |
| `AttachmentMetadataDto` | `Id`, `FileName`, `ContentType`, `Size`, `State`, `ExpiresUtc` |
| `UploadIntentDto` | `UploadId`, `UploadUrl` or API route, headers, size/type constraints, expiry |
| `ProtectedFileTicketDto` | one-time `Ticket`, `DownloadUrl`, `ExpiresUtc`, `File` |
| `FileMetadataDto` | `FileName`, `ContentType`, `Size`, `Sha256`, `ExpiresUtc`, `IsProtected` |
| `MutationResultDto` | `ResourceId`, `State`, `RowVersion`, `OccurredUtc`, `Message` |
| `LookupBundleVersionDto` | `Version`, `ETag`, `GeneratedUtc` |

### 10.2 Bootstrap, identity and configuration DTOs

| DTO | Key fields |
|---|---|
| `ResolveTenantRequest` | `CompanyCodeOrDomain` |
| `TenantDiscoveryDto` | `TenantPublicId`, `DisplayName`, `LogoUrl`, `Authority`, `SignInProviders`, `IsAvailable` |
| `PublicMobileMetadataDto` | `ApiVersion`, `MinimumAppVersion`, `SupportedPlatforms`, `StatusUrl`, public legal links |
| `MobileSessionDto` | `User`, `Tenant`, `Portal`, `Roles`, `Permissions`, `Scopes`, `Modules`, `SessionExpiresUtc`, `MustChangePassword` |
| `SessionUserDto` | `UserId`, `EmployeeId`, `DisplayName`, `EmailMasked`, `AvatarUrl` |
| `SessionTenantDto` | `TenantId`, `TenantKey`, `DisplayName`, `LogoUrl`, `TimeZoneId`, `Locale`, `Currency` |
| `LogoutRequest` | `DeviceId`, `RevokeAllDeviceTokens` |
| `OfflineGrantRequest` | `DeviceId`, requested limited duration |
| `OfflineGrantDto` | `GrantId`, `AllowedUntilUtc`, permitted read areas, signed integrity token |
| `SessionHeartbeatRequest` | `DeviceId`, `AppVersion`, `Platform`, `ConfigurationVersion` |
| `SessionHeartbeatDto` | session state, refresh/config/release actions |
| `RegisterDeviceRequest` | installation public key/attestation, model, OS, app version, push capability |
| `RegisteredDeviceDto` | `Id`, name/model/platform, trust state, current flag, last seen, push state |
| `DeviceRegistrationResultDto` | device plus server challenge/result and policy |
| `RenameDeviceRequest` | `Name`, `RowVersion` |
| `UpdatePushTokenRequest` | provider, token, environment, app bundle ID |
| `MobileConfigurationDto` | version, tenant, theme, modules, navigation, dashboards, security, offline, release, support |
| `MobileThemeDto` | logo/splash assets, semantic colours, typography scale, radius/density, dark-mode policy |
| `MobileModuleDto` | module code, enabled/licensed state, home route, permission, unavailable message |
| `MobileNavigationDto` | role/persona navigation groups and items |
| `MobileNavigationItemDto` | key, label, icon, route/deep link, order, required permission/module, badge source |
| `MobileDashboardLayoutDto` | persona, ordered widget definitions and size/visibility |
| `MobileSecurityPolicyDto` | PIN/biometric/offline/screenshot/session/device requirements |
| `MobileOfflinePolicyDto` | cache TTLs, maximum storage, approved offline areas/actions |
| `MobileReleasePolicyDto` | minimum/recommended version, force update, store URLs, maintenance message |
| `MasterDataBundleDto` | bundle version and category dictionaries |
| `MasterChangesDto` | from/to version, upserts and retired codes |

### 10.3 Home, search, notification, calendar and task DTOs

| DTO | Key fields |
|---|---|
| `KpiDto` | key, title, value, unit, trend, tone, target route |
| `HrHomeDto` | KPIs, alerts, interviews, approvals, recruitment summary, quick actions |
| `EmployeeHomeDto` | attendance today, leave balances, payslip summary, tasks, announcements, quick actions |
| `ManagerHomeDto` | team presence, approvals, leave conflicts, probation, interviews, quick actions |
| `QuickActionDto` | code, label, icon, route, permission, online requirement |
| `SearchSuggestionDto` | text, type, recent/suggested state |
| `GlobalSearchResultDto` | grouped result collections and facets |
| `SearchResultItemDto` | type, opaque ID, title, subtitle, route, icon, status, matched text |
| `NotificationListItemDto` | ID, category, title, preview, priority, read state, occurred date, action route |
| `NotificationDetailDto` | title/body, safe rich content, actor, timestamps, actions, related resource |
| `UnreadCountDto` | total and category counts |
| `CalendarEventDto` | ID, type, title, start/end, all-day, status, route, masked participant summary |
| `CalendarEventDetailDto` | event plus location/mode, participants, actions |
| `TaskListItemDto` | ID, type, title, assignee, due date, priority, progress, route |
| `TaskDetailDto` | description, steps, evidence, comments, actions, row version |
| `TaskCommentDto` | author, safe content, occurred date |

### 10.4 Recruitment DTOs

| DTO | Key fields |
|---|---|
| `RecruitmentDashboardDto` | KPIs, funnel stages, source metrics, ageing, upcoming interviews |
| `RequisitionListItemDto` | ID, title, department/location, headcount, owner, status, age, row version |
| `RequisitionDetailDto` | summary plus justification, budget range, approvers, dates, actions, audit |
| `SaveRequisitionRequest` | title/master codes, headcount, justification, budget, owner, dates, row version |
| `RequisitionActionRequest` | action, reason/comment, row version |
| `RequisitionApprovalEntryDto` | step, approver, decision, comment, timestamps |
| `JobListItemDto` | ID, title, department/location/type/experience, status, applications, expiry, row version |
| `JobDetailDto` | basics, content, compensation visibility, channels, publication, actions, row version |
| `SaveJobDraftRequest` | requisition ID, basics, content, compensation, dates, master codes, row version |
| `PublishJobRequest` | channels, publish/schedule date, expiry, acknowledgment, row version |
| `DuplicateJobRequest` | new title/slug, copied sections, keep draft flag |
| `JobValidationDto` | valid state, field errors, warnings, missing masters/content |
| `JobAnalyticsDto` | range, views, applications, conversion, sources, stage ageing, time series |
| `ApplicationListItemDto` | application ID, candidate summary, job, stage, rating, age, flags, actions |
| `ApplicationPipelineDto` | job summary, stages containing cursor pages/counts, allowed transitions |
| `ApplicationDetailDto` | candidate/job/stage/source/rating, screening, activity, documents, actions, row version |
| `ChangeApplicationStageRequest` | target stage, reason, effective date, notify flag, row version |
| `ApplicationDecisionRequest` | shortlist/reject/return, reason code/comment, template, notify, row version |
| `UpdateRatingRequest` | value, note, row version |
| `ScreeningCriterionDto` | code, title, weight, score, max score, comment, required flag |
| `ScreeningScorecardDto` | application, criteria, weighted total, recommendation, submitted state, row version |
| `SaveScreeningRequest` | criteria scores/comments, recommendation, draft/submit, row version |
| `CandidateProfileDto` | permission-shaped personal, contact, professional, address and consent sections |
| `CandidateContactDto` | masked/unmasked email/phone plus access metadata |
| `CandidateExperienceDto` | employer, title, date range, responsibilities, current flag |
| `CandidateEducationDto` | institution, qualification, subject, dates/grade |
| `ResumeSummaryDto` | skills, total experience, education, languages, highlights, résumé document ID |
| `CandidateDocumentDto` | metadata, category, verification state, expiry, actions |
| `VerifyCandidateDocumentRequest` | decision, reason, extracted-field corrections, row version |
| `RequestCandidateDocumentRequest` | category, message template, due date |
| `InterviewListItemDto` | ID, candidate/job/round, date/time, mode, state, panel summary, actions |
| `InterviewDetailDto` | full schedule, candidate summary, panel, join details, evaluation status, row version |
| `InterviewAvailabilityDto` | time zone, dates and available slots with panel conflicts |
| `ScheduleInterviewRequest` | application, round, slot, duration, mode/location/link, panel, notify, row version |
| `InterviewEvaluationDto` | criteria, scores, recommendation, notes, submitted state, row version |
| `SaveInterviewEvaluationRequest` | score values, recommendation, notes, draft/submit, row version |
| `OfferListItemDto` | ID, candidate/job, status, expiry, approval state, masked total, actions |
| `OfferDetailDto` | components permitted to caller, terms, approvals, document, actions, row version |
| `GenerateOfferRequest` | template, effective/start dates, components, terms, row version |
| `OfferDecisionRequest` | approve/reject/return, reason/comment, row version |
| `SendOfferRequest` | channel/template, expiry, message, row version |

### 10.5 Employee and lifecycle DTOs

| DTO | Key fields |
|---|---|
| `EmployeeListItemDto` | ID, employee number, display name, avatar, title, department/location, status, presence |
| `EmployeeOverviewDto` | identity, assignment, contacts, status, milestones, permitted sections/actions |
| `EmployeePersonalDto` | legal/preferred name, demographics, identifiers masked, field access, row version |
| `UpdateEmployeePersonalRequest` | editable personal fields and row version |
| `EmployeeEmploymentDto` | employee number, dates, type/status/grade/probation, row version |
| `UpdateEmployeeEmploymentRequest` | master codes, dates, grade/status and row version |
| `EmployeeOrganisationDto` | company/unit/division/department/location/manager/cost centre, row version |
| `UpdateEmployeeOrganisationRequest` | organisation IDs/codes, effective date, reason, row version |
| `CompensationSnapshotDto` | effective date, package/components as allowed, currency, history permission |
| `EmployeeContactDto` | ID, type, value masked, primary flag, verification state, row version |
| `EmployeeDependantDto` | ID, name, relation, DOB masked, benefit eligibility, row version |
| `SaveContactRequest` | type, value, primary flag, row version |
| `SaveDependantRequest` | identity/relation/eligibility fields, row version |
| `EmployeeTimelineEntryDto` | event type, title, effective date, summary, actor, route |
| `CreateEmployeeRequest` | identity, employment, organisation, access assignment and idempotency key context |
| `CreateEmployeeResultDto` | employee ID, account ID, onboarding ID, invitation state |
| `EmployeeDocumentDto` | document metadata/category/status/expiry/verification/actions |
| `VerifyEmployeeDocumentRequest` | decision, reason, expiry, row version |
| `OnboardingSummaryDto` | employee, joining date, stage, progress, overdue count, owner |
| `OnboardingChecklistDto` | employee summary, stages, tasks, progress, row version |
| `OnboardingTaskDetailDto` | instructions, owner, due date, dependencies, evidence, comments, actions |
| `CompleteOnboardingTaskRequest` | outcome, comment, evidence IDs, row version |
| `ReassignTaskRequest` | new owner ID, reason, due-date change, row version |
| `ProbationDetailDto` | employee, period, goals, reviews, feedback, recommendation, actions, row version |
| `SaveProbationReviewRequest` | goal ratings, feedback, recommendation, row version |
| `ConfirmProbationRequest` | confirmation/effective date, comment, row version |
| `ExtendProbationRequest` | new end date, reason, revised goals, row version |
| `OffboardingDetailDto` | employee, reason/dates, approvals, clearance tasks, settlement state, row version |
| `InitiateOffboardingRequest` | employee, type/reason, dates, comment, checklist template, row version |
| `ClearanceTaskDto` | task, owner, due date, state, evidence and row version |

### 10.6 Attendance and shift DTOs

| DTO | Key fields |
|---|---|
| `AttendanceDashboardDto` | date/range, present/late/absent/exception KPIs, trend, locations |
| `MyAttendanceDto` | employee, month, day summaries and totals |
| `AttendanceDayDto` | date, shift, status, punches, worked/expected duration, exceptions, actions |
| `TeamAttendanceDto` | manager scope, date, employee presence items and totals |
| `AttendanceRegisterDto` | date, cursor page of register rows and totals |
| `AttendanceRegisterRowDto` | employee, shift, first/last punch, duration, status, exceptions |
| `PunchStateDto` | current status, last punch, eligible next action, server time, policy summary |
| `PunchRequest` | action, device ID, client event ID/time, approved location evidence, attestation |
| `PunchResultDto` | accepted server time, state, punch ID, warning and next action |
| `AttendanceCorrectionDto` | request, original/requested punches, reason, workflow, actions, row version |
| `CreateAttendanceCorrectionRequest` | date, requested punches, reason, attachment IDs |
| `AttendanceCorrectionDecisionRequest` | decision, comment, row version |
| `AttendanceExceptionDto` | type, evidence, calculated duration, resolution, actions, row version |
| `ResolveAttendanceExceptionRequest` | resolution/decision, duration, comment, row version |
| `ShiftRosterDto` | range, assignments grouped by employee/date, unassigned shifts |
| `ShiftAssignmentDto` | shift code, start/end/time zone, location, employee, recurrence, row version |
| `UpdateShiftAssignmentRequest` | employee/shift/date/time/location/recurrence, reason, row version |
| `RequestShiftSwapRequest` | target employee/assignment, reason, row version |
| `ShiftSwapResultDto` | request ID, approval state, affected assignments |

### 10.7 Leave DTOs

| DTO | Key fields |
|---|---|
| `LeaveDashboardDto` | balances, upcoming, pending, history summary, quick actions |
| `LeaveBalanceDto` | policy/type, year, entitled/accrued/used/pending/available, unit |
| `LeaveRequestListItemDto` | ID, type, date range, days, status, approver, submitted date |
| `LeaveRequestDetailDto` | dates/partials, reason, attachment metadata, balance impact, workflow, actions, row version |
| `LeaveCalculationRequest` | type, dates, partial-day selections, employee ID when authorised |
| `LeaveCalculationDto` | requested/chargeable days, holidays/weekends, balance before/after, warnings/conflicts |
| `CreateLeaveRequest` | policy/type, dates, partial-day data, reason, attachment IDs, calculation token |
| `LeaveDecisionRequest` | approve/reject/return, reason/comment, row version |
| `LeaveDecisionResultDto` | new status, balance result, workflow, notification state |
| `TeamLeaveItemDto` | employee, request summary, balance, conflicts, priority/actions |
| `TeamLeaveCalendarDto` | range, employee groups and permission-shaped leave events |
| `LeavePolicySummaryDto` | ID, code/name, unit, eligibility and version |
| `LeavePolicyDetailDto` | entitlement, accrual, carry-forward, notice, limits, documents, version |
| `LeaveAdjustmentRequest` | comp-off/encashment type, source/amount/dates, reason |
| `LeaveAdjustmentDto` | ID, type, quantity/value, status, workflow, actions |

### 10.8 Payroll, reimbursement and benefits DTOs

| DTO | Key fields |
|---|---|
| `PayslipListItemDto` | ID, period, published date, protected availability, masked net pay |
| `PayslipDetailDto` | employee/period, earnings, deductions, taxes, net, YTD, row version |
| `PayComponentDto` | code/name, amount, category, taxable flag, YTD amount |
| `TaxSummaryDto` | year, regime, taxable income, tax paid/due, deductions, projection |
| `TaxDeclarationDto` | year, regime, sections, evidence status, workflow, row version |
| `TaxDeclarationSectionDto` | code/title, declared/accepted limits and items |
| `SaveTaxDeclarationRequest` | regime, item values, attachment IDs, row version |
| `SalaryStructureDto` | effective period, annual/monthly totals, components, access metadata |
| `ReimbursementListItemDto` | ID, type, date, amount, status, submitted/paid dates |
| `ReimbursementDetailDto` | claim fields, receipts, workflow, comments, actions, row version |
| `SaveReimbursementRequest` | type, amount/currency, date, merchant, description, receipt IDs, row version |
| `ReimbursementMutationDto` | ID, status, workflow and row version |
| `BenefitPlanDto` | ID, category/name, provider, coverage summary, employee cost, eligibility |
| `BenefitComparisonDto` | selected plans and normalised coverage/cost rows |
| `BenefitCoverageDto` | current plan, dependants, effective dates, documents |
| `SubmitBenefitEnrollmentRequest` | plan, dependant IDs, options, acknowledgements, row version |
| `BenefitEnrollmentResultDto` | enrollment ID, status, effective date, workflow |

### 10.9 Approval, request, report and export DTOs

| DTO | Key fields |
|---|---|
| `ApprovalListItemDto` | ID, type, subject, requester, submitted/due dates, priority, summary |
| `ApprovalDetailDto` | subject, before/after comparison, evidence, chain, actions, row version |
| `ApprovalDecisionRequest` | approve/reject/return, comment/reason, row version |
| `ApprovalDecisionResultDto` | new state, next step, occurred date, notification state |
| `ApprovalDelegationDto` | ID, delegate, date range, scopes, active state, row version |
| `SaveApprovalDelegationRequest` | delegate ID, dates, scopes, reason, row version |
| `HrServiceDefinitionDto` | code/name/category, eligibility, SLA, icon, form schema version |
| `DynamicFormSchemaDto` | schema version, sections, fields, conditions, validation and attachment constraints |
| `DynamicFieldDto` | key/type/label/help/required/options/validation/visibility expression |
| `CreateHrRequest` | service code/schema version, typed values, attachment IDs, idempotency key context |
| `HrRequestDetailDto` | request values, status/SLA, timeline, comments, attachments, actions, row version |
| `RequestCommentDto` | ID, author, safe content, date, attachments |
| `ReportDefinitionDto` | code/name/category, permission, supported visualisations/exports |
| `ReportFilterSchemaDto` | fields, defaults, permitted organisation scope, max date range |
| `RunReportRequest` | report code/schema version, filter values, sort and page size |
| `ReportResultDto` | columns, rows/cards/series, summary, cursor, generated date, redaction metadata |
| `ReportDrilldownDto` | title, applied context, columns, rows and cursor |
| `ExportJobDto` | ID, report/type, format, state/progress, created/expiry dates, error, actions |
| `CreateExportRequest` | source type/ID, format, filters, locale/time zone |

### 10.10 Profile, support and sync DTOs

| DTO | Key fields |
|---|---|
| `OwnProfileDto` | user/employee reference, permitted contact/profile fields, avatar, preferences |
| `UpdateOwnProfileRequest` | only self-editable fields and row version |
| `AppPreferencesDto` | locale, time zone, theme mode, density, accessibility options |
| `UpdateAppPreferencesRequest` | changed preference values |
| `NotificationPreferencesDto` | event groups, channels, quiet hours, mandatory events |
| `UpdateNotificationPreferencesRequest` | permitted event/channel toggles and quiet hours |
| `HelpArticleSummaryDto` | ID, title, category, keywords, updated date |
| `HelpArticleDto` | safe rich content, related articles, support action |
| `CreateSupportTicketRequest` | category/subject/body, attachment IDs, consented diagnostics IDs |
| `SupportTicketResultDto` | ticket ID/reference, state, expected response |
| `SyncManifestDto` | delta token, approved entity sets, retention, server/config time |
| `SyncChangePageDto` | entity set, upserts, tombstones, next cursor/delta token |
| `SyncMutationStatusDto` | idempotency key, state, resource ID/version, retry/conflict details |

## 11. Data and Service Ownership

| API area | BM.ITF folder | DL schema/owner | Notes |
|---|---|---|---|
| Discovery/config/masters | `Admin/Mobile` | `platform` | Published configuration only for mobile reads |
| Session/devices | `Identity/Mobile` contracts | `identity` | Tokens/device sessions owned by Identity |
| Recruitment | `Recruitment/Mobile` | `recruitment` | Candidate PII accessed through explicit Candidate contract if separate |
| Employee/onboarding | `Employee/Mobile` | `employee` | No Recruitment DbContext access |
| Attendance/shifts/leave/payroll | `Employee/*` initially | `employee` subareas | May extract as product modules later |
| Notifications/tasks/exports | owning module plus integration contracts | owning schema/outbox | Worker performs delivery/rendering |
| Reports | query services with permission-shaped read models | read projections | Must not expose arbitrary SQL/report expressions |

Cross-module example:

```text
Recruitment offer accepted
  -> recruitment outbox event CandidateHired
  -> Employee handler creates onboarding record idempotently
  -> mobile sees processing/completed status
```

No controller starts a transaction across Recruitment and Employee schemas.

## 12. Concurrency and Idempotency

- Mutable detail DTOs return an opaque `RowVersion` and `ETag`.
- Update/decision actions require `If-Match`; stale clients receive `412` plus current version metadata.
- Create/submit/punch/approve/send/export actions require `Idempotency-Key`.
- The server stores key, user, tenant, route, request hash, response status/body reference, and expiry.
- Repeating the same key and same payload returns the original result.
- Repeating a key with a different payload returns `409`.
- Mobile-generated client timestamps are evidence only. Server timestamps control workflow and attendance acceptance.

## 13. File and Media Rules

1. The client first requests an upload intent containing allowed content types, maximum size, expiry, and upload route/URL.
2. The server verifies magic bytes, extension, content type, size, tenant/user access, and optional malware scan.
3. Metadata is stored in the owning module; binary storage is accessed through `IMediaStorage`/an equivalent protected-file abstraction.
4. Downloads require a short-lived, one-time file ticket. Authorisation is rechecked when the ticket is redeemed.
5. Sensitive files use `Cache-Control: no-store`, content disposition, safe filenames, and range processing only where justified.
6. Public career media and private HR files use separate storage policies/containers.
7. Mobile logs, analytics, crash reports, and support diagnostics never include file contents, tokens, full paths, or PII.

## 14. Caching and Offline Backend Support

- Configuration and masters: `ETag`, version, `If-None-Match`, private cache where tenant-specific.
- Lists: cursor paging; no offset paging for changing operational datasets.
- Dashboards: server-computed projections with short private caching.
- Sensitive employee/candidate/payroll details: `Cache-Control: no-store` by default.
- Sync API supports only explicitly approved read models and narrow outbox mutations.
- A sync tombstone contains only entity ID/type/version/deleted date; it must not leak deleted record content.
- Sync delta tokens are tenant, user, permission-set, device, and schema-version bound.
- A permission or account change invalidates prior sync grants and cached-data eligibility.

## 15. Rate-Limit Classes

| Policy | Examples | Initial logical partition |
|---|---|---|
| `mobile-auth` | discovery/session/device | IP plus tenant/client; strict |
| `mobile-read` | lists/details/config | tenant + user + device |
| `mobile-search` | global/candidate/employee search | tenant + user; lower sustained rate |
| `mobile-mutation` | saves, decisions, punch | tenant + user + route; no retry storm |
| `mobile-upload` | file intents/uploads | tenant + user; size/concurrency based |
| `mobile-export` | report/export generation | tenant + user; very low concurrency |

Values belong in environment configuration and require load testing. Do not make tenant-specific rate limits editable by ordinary tenant admins unless licensing explicitly supports it.

## 16. Observability and Audit

- Structured logs include correlation ID, route template, status, duration, tenant ID, subject ID hash/reference, device ID, app version, and error code.
- Logs exclude tokens, cookies, passwords, PINs, document bodies, salary, government identifiers, full search terms containing PII, and raw request bodies.
- Audit events record actor, tenant, permission, resource, action, before/after safe summary, timestamp, device/session, and correlation ID.
- Metrics cover latency, error rate, 401/403/412/429 counts, token refresh failures, sync failures, push registration, uploads, exports, and queue age.
- Traces propagate through BM, database and Worker/outbox processing.

## 17. OpenAPI and Mobile Client Generation

- Generate a dedicated `mobile-v1` OpenAPI document.
- Every action has a stable operation ID and tag matching its module.
- The CI build validates that the document changes intentionally.
- Generate typed client interfaces into `Vispl.HRManagement.Mobile.Infrastructure`; do not hand-maintain 38 raw `HttpClient` wrappers.
- Generated transport clients are wrapped by feature-facing use-case clients so UI code does not depend on generator-specific types.
- Contract-breaking changes require `/v2`; additive optional fields remain in `/v1`.

## 18. Screen-to-Backend Coverage Check

| Screen area | Backend coverage |
|---|---|
| MOB-001–010 | Discovery, Session, Devices, Configuration; PIN/biometric prompts remain local |
| MOB-011–020 | Home, Search, Notifications, Calendar, Tasks, Configuration |
| MOB-021–048 | All eight Recruitment controllers plus Exports/Files |
| MOB-049–068 | Employees, Employee Documents, Onboarding, Probation, Offboarding |
| MOB-069–078 | Attendance and Shifts |
| MOB-079–088 | Leave and Leave Policies |
| MOB-089–096 | Payroll, Reimbursements, Benefits and Files |
| MOB-097–102 | Approvals and HR Requests |
| MOB-103–112 | Reports, Exports, Profile, Support, Sync, Configuration and Session |

All 112 planned screens have either a backend controller or an intentionally local/native responsibility.

## 19. Recommended Implementation Order

1. Add API presentation project, common contracts, `ProblemDetails`, correlation IDs, OpenAPI and generated client pipeline.
2. Implement standards-based OIDC/OAuth authority, bearer validation, device sessions, tenant claims and permission policies.
3. Implement published mobile configuration, masters, bootstrap/session and profile endpoints.
4. Implement Home, notifications, search and task projections.
5. Implement Recruitment controllers against new focused BM interfaces.
6. Add Employee schema/services and employee/onboarding controllers.
7. Add Leave and Attendance before Payroll.
8. Add approvals, requests, reports, exports, protected files and approved sync.
9. Run tenant-isolation, object-level-authorisation, concurrency, idempotency, load and mobile contract tests.
10. Prove a module can be hosted separately while the public `/api/mobile/v1/...` route stays unchanged.

## 20. Definition of Done for a Controller

A controller is complete only when:

- its permission and tenant isolation tests pass;
- request/response DTOs are in Contracts and no EF/entity type is serialised;
- OpenAPI contains correct operation IDs, schemas, errors and security requirements;
- success, validation, 401, 403, 404, conflict and cancellation paths are tested;
- mutation audit/idempotency/concurrency rules are implemented;
- pagination and query limits are enforced;
- PII logging/redaction and cache headers are verified;
- the generated MAUI client consumes it in at least one vertical screen flow.

## 21. Platform References

- [ASP.NET Core JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-9.0)
- [ASP.NET Core API error handling and Problem Details](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-9.0)
- [ASP.NET Core OpenAPI document generation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0)
- [ASP.NET Core model binding and automatic API validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding?view=aspnetcore-9.0)
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0)

