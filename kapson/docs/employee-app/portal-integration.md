# HR Web Portal ↔ Employee App — Integration Map

## 1. Purpose

This document maps how the **HR web portal** and the **employee mobile app** connect: which HR
feature changes what an employee sees, which employee action feeds which HR screen, and the
corporate rules that govern every one of those crossings.

It answers three questions for every domain:

1. **The corporate rule** — the policy that must hold, whatever the code does.
2. **HR web feature → employee app impact** — what HR does, and what changes on the phone.
3. **Employee app → HR web** — what the employee does, and how HR comes to see it.

It is authoritative for **integration direction, ownership and enforcement point**. Screen
behaviour lives in `employee-app.md`; route and DTO detail lives in `backend.md`.

## 2. The one fact everything else follows from

**The HR web portal does not call the mobile API. The two surfaces meet at the business
service layer, over one database.**

```text
   HR web portal (Blazor)            Employee app (MAUI)
   cookie auth, /hr/*                bearer token, api/mobile/v1
          │                                   │
   HrPortalCaller.GetAsync()          MobileApiControllerBase.Caller
          │                                   │
          └──────────► MobileCallerContext ◄──┘
                              │
                  ┌───────────┴───────────┐
                  │   BM business services │   ← the single set of rules
                  │  LeaveService,         │
                  │  AttendanceService,    │
                  │  MobileEmployeeService │
                  └───────────┬───────────┘
                              │
                       DL repositories
                              │
                          SQL Server
```

`HrPortalCaller` builds the same `MobileCallerContext` from an HR cookie that
`MobileApiControllerBase` builds from a mobile bearer token — same tenant key, same user id,
same `employee_number`, same permission claims. Its own class comment states the rule:

> Both paths end at the same services, which is what keeps the portal and the mobile apps
> enforcing one set of rules rather than two — a page must never make its own authorisation
> decision.

`HrLeave.razor` injects `IMobileLeaveService`. `HrPeople.razor` injects
`IMobileEmployeeService`. `HrAttendance.razor` injects `IMovementService` and
`IGatePassService`. These are the **same instances** the mobile controllers resolve. The
`Mobile` in those names is historical, not a boundary.

**Consequence:** there is no integration to build between the two surfaces, and no
synchronisation to keep. There is one system with two front doors. Every rule below is a rule
about that convergence point, not about a wire between two apps.

## 3. The corporate rules

These are numbered so the tables below can cite them. A rule is binding whether or not the
code currently enforces it; where enforcement is missing it is flagged as a gap.

| # | Rule | Enforced by |
|---|---|---|
| **CR-1** | **One rule set.** A business rule is written once, in a BM service. Neither a Blazor page nor a mobile ViewModel may make an authorisation, calculation or state-transition decision of its own. | Code review; `HrPortalCaller` doc contract |
| **CR-2** | **Identity comes from the caller context, never from a request body.** A service reads tenant, user and employee from `MobileCallerContext` only. | `MobileApiControllerBase`; service signatures |
| **CR-3** | **Portals are separate accounts.** Platform, HR, Employee and Candidate are distinct `AccountPortal` values. One account cannot sign in to another portal, and holding an HR login does not grant a mobile login. | `PortalSeparationTests`; `AccountPortal` enum |
| **CR-4** | **Mobile access is an explicit HR grant.** Being an employee does not create a phone login. Someone must decide this person may carry company data on a device, and be able to revoke it. | `EmployeeMobileAccessService`; `ACCOUNTS.MANAGE` |
| **CR-5** | **One tenant per session.** The tenant claim is fixed at sign-in and every repository call is scoped by `TenantKey`. No cross-tenant read exists. | Every repository signature |
| **CR-6** | **The server computes every number.** Chargeable leave days, balances, present days and holidays are derived server-side. A client that calculates is a client that disagrees. | `LeaveService` calculation token; `EmployeeKpiService` |
| **CR-7** | **Self-service edits are requests, not writes.** An employee changing their own record raises a change request for HR to approve. They never write the master record. | `POST employees/{id}/change-requests` |
| **CR-8** | **Permission gates the data, not just the button.** A hidden control is a courtesy; the service raising a permission failure is the control. | `Require(caller, permission)` in every service |
| **CR-9** | **Sensitive data does not cache.** Personal, compensation and bank data are memory-only on the device and never written to local storage or logs. | `backend.md` §9 cache policy |
| **CR-10** | **Every mutation is replay-safe.** Mutations carry `Idempotency-Key`; edits carry `If-Match` over `RowVersion`. | `MobileApiControllerBase` |
| **CR-11** | **An approval has a named approver and an audit trail.** No state moves without a recorded actor and time. | `AuditEntryDto`; `MutationResultDto.OccurredUtc` |
| **CR-12** | **A feature that does not exist says so.** An unbuilt module answers 501 naming itself. It never returns an empty or invented result. | `AddNotReady` / `MobileFeatureNotReadyException` |

## 4. HR web → employee app

What HR does on the web, and what changes on the employee's phone.

### 4.1 Mobile access — the master switch

| | |
|---|---|
| **Corporate rule** | CR-3, CR-4 |
| **HR web feature** | `/hr/dashboard` → Mobile Access panel. `IEmployeeMobileAccessService`: `GrantAsync`, `SetActiveAsync` (suspend/restore). Needs `EMPLOYEES.VIEW` to see, `ACCOUNTS.MANAGE` to change. |
| **Employee app impact** | **Whether the app works at all.** `NotGranted` → no account, sign-in fails. `AwaitingFirstSignIn` → credentials issued, never used. `Active` → normal. Suspended → sign-in refused and refresh fails, so the session dies at its next refresh. |
| **Reverse** | The employee's first successful sign-in moves their row from `AwaitingFirstSignIn` to `Active`, which is how HR sees adoption. A grant leaves a welcome notification waiting for that first sign-in; a restore notifies immediately; a suspension is deliberately silent (§7.1). |

Accounts are matched to employees **by employee number** — the one identifier both sides carry.
An account with no employee number is deliberately left out of the list rather than guessed at.
This makes `EmployeeNumber` the join key of the whole integration; it must be unique and stable
per tenant.

### 4.2 Leave configuration

| | |
|---|---|
| **Corporate rule** | CR-1, CR-6 |
| **HR web feature** | `/admin` → `LeavePoliciesAdmin.razor`, `HolidaysAdmin.razor`. `ILeaveConfigurationService` writes policies, entitlements, accrual, carry-forward and the holiday calendar. **Platform portal, not HR portal.** |
| **Employee app impact** | Changes the leave types in EMP-030's picker, the balance cards on EMP-014, the chargeable day count `POST leave/requests/calculate` returns, and the **Holidays** tile on EMP-012 — `EmployeeKpiService` reads holidays from the leave calendar, scoped by the employee's `LocationCode`. |
| **Reverse** | Consumption feeds `leave/analytics/*`, which is what `HrLeave.razor` renders. |

A policy change is retroactive to any calculation not yet submitted. The 30-minute calculation
token exists for exactly this: a token issued under the old policy is rejected, and the app
recalculates rather than booking days under a rule that has changed.

### 4.3 Shift configuration

| | |
|---|---|
| **Corporate rule** | CR-1, CR-6 |
| **HR web feature** | `/admin` → `ShiftsAdmin.razor`, `IShiftConfigurationService`. |
| **Employee app impact** | Determines `AttendanceDayDto.ShiftCode` and `Expected` hours, which decide whether a day resolves `Present`, `Partial` or `Absent` — and therefore the four tiles on EMP-012 and every cell colour on EMP-020. |
| **Reverse** | Punches resolve against the assigned shift; deviations become attendance exceptions on `HrAttendance.razor`. |

### 4.4 Roles and permissions

| | |
|---|---|
| **Corporate rule** | CR-3, CR-8 |
| **HR web feature** | `/admin` → `RolesAdmin.razor`, `PermissionMatrix.razor`, `AccountsAdmin.razor`. |
| **Employee app impact** | Permission claims are minted into the mobile token. `LEAVE.APPROVE` makes EMP-035 and EMP-036 appear; `GATEPASS.APPROVE` makes EMP-054 appear; `LEAVE.TEAM` widens `leave/team/requests`. Nothing else in the app is role-conditional. |
| **Reverse** | None. Permissions flow one way. |

**Claims are minted at sign-in.** A permission removed on the web does not reach a live mobile
session until its token refreshes. For a revocation that must bite immediately, suspend mobile
access (§4.1) — that fails the refresh itself.

### 4.5 Leave decisions

| | |
|---|---|
| **Corporate rule** | CR-1, CR-11 |
| **HR web feature** | `/hr/leave` → approval queue from `IWorkforceReportService.GetLeaveApprovalQueueAsync`, decision through `IMobileLeaveService`. Needs `LEAVE.APPROVE`. |
| **Employee app impact** | The request's `StatusDto` changes on EMP-032 and EMP-033, and the balance on EMP-014 moves from pending to used, or is released. |
| **Reverse** | The request only exists because the employee raised it — §5.1. |

**The same decision is available from both surfaces.** A manager may approve on
`/hr/leave` or on EMP-036; both call `IMobileLeaveService.DecideAsync` with the same caller
context and the same permission check. This is CR-1 working as intended, and it is why the two
can never disagree.

### 4.6 Employee record changes

| | |
|---|---|
| **Corporate rule** | CR-7, CR-8 |
| **HR web feature** | `/hr/people` → `IMobileEmployeeService`. HR edits personal, employment and organisation sections directly. |
| **Employee app impact** | EMP-040 reflects the change on next read. A department, grade or manager change also moves the employee between HR's own scopes and can change which approver their next request routes to. |
| **Reverse** | The employee raises a change request; HR sees and applies it — §5.4. |

The asymmetry is deliberate and is CR-7: **HR writes, the employee requests.**

### 4.7 Masters

| | |
|---|---|
| **Corporate rule** | CR-1 |
| **HR web feature** | `/admin` → `MastersAdmin.razor`, `IMasterDataService`. |
| **Employee app impact** | Every picker and every code-to-name resolution. `2.png` shows "Operations" and "Kapsons Retail" where the DTOs carry `DepartmentCode` and `CompanyCode`; the app resolves through `GET /masters`, cached against `ApiMetaDto.ConfigurationVersion`. A master rename changes displayed text everywhere without a deploy. |
| **Reverse** | None. |

### 4.8 Tenant configuration

| | |
|---|---|
| **Corporate rule** | CR-3, CR-5 |
| **HR web feature** | `/admin` → `TenantsAdmin.razor`. `TenantEntity.HrPortalEnabled` toggles the HR portal per tenant; `TimeZoneId` sets the tenant clock. |
| **Employee app impact** | The tenant clock decides what "today" means for a punch and for a leave day boundary. `HrPortalEnabled` is HR-portal only and does **not** disable the employee app — the two switches are independent, which is the intent. |
| **Reverse** | None. |

## 5. Employee app → HR web

What the employee does, and how HR comes to see it. This is the direction the product exists
for: **the record does not exist until the employee creates it.**

### 5.1 Leave request

| | |
|---|---|
| **Corporate rule** | CR-6, CR-10, CR-11 |
| **Employee action** | EMP-030 → `POST leave/requests/calculate` → EMP-031 → `POST leave/requests` with the calculation token and an `Idempotency-Key`. |
| **Where it lands** | `LeaveService` writes the request and **stores the charged days as dates**, so a second request cannot book a day already claimed. |
| **HR web sees it in** | `/hr/leave` approval queue (`LEAVE.APPROVE`); `/hr/dashboard` leave analytics (`LEAVE.REPORT`); `/hr/leave` department and employee balance grids. |
| **API equivalent** | `GET leave/approvals`, `GET leave/analytics/balances/{employees,departments}`, `GET leave/analytics/by-{type,date}` |

### 5.2 Punch and attendance

| | |
|---|---|
| **Corporate rule** | CR-2, CR-10 |
| **Employee action** | EMP-022 → `POST attendance/punch` with `DeviceId`, `ClientEventId`, `ClientOccurredUtc`, optional lat/long and an attestation. |
| **Where it lands** | `AttendanceService` records the punch against the caller's own employee record. `ClientEventId` is the replay guard — a retry is the same punch, a new id is a new one. |
| **System step** | `AttendanceDayCloseWorker` runs `IAttendanceDayCloser.CloseAsync`, resolving punches against the shift into a day status and raising exceptions for late arrivals and missing punches. **Nothing HR sees is final until the day closes.** |
| **HR web sees it in** | `/hr/attendance` — late arrivals, miss-punch, attendance overview, movement board, gate pass board; `/hr/dashboard` attendance overview. |
| **API equivalent** | `GET attendance/reports/late-arrivals`, `GET attendance/reports/miss-punch`, `GET dashboard/attendance-overview`, `POST attendance/close/{date}` |

### 5.3 Attendance correction

| | |
|---|---|
| **Corporate rule** | CR-7, CR-11 |
| **Employee action** | EMP-023 → `POST attendance/corrections` with the requested punches and a reason. |
| **Where it lands** | A correction request, not an edit. The attendance row is untouched until someone with `ATTENDANCE.APPROVE` decides. |
| **HR web sees it in** | `/hr/attendance` exception and correction queues. |
| **API equivalent** | `GET attendance/corrections`, `POST attendance/corrections/{id}/decision` |

### 5.4 Profile change request

| | |
|---|---|
| **Corporate rule** | CR-7 |
| **Employee action** | EMP-041 → `POST employees/{id}/change-requests`. |
| **Where it lands** | A pending change request against the employee record. The master record is unchanged. |
| **HR web sees it in** | `/hr/people`, where HR applies or rejects it — and a notification to `EMPLOYEES.EDIT` holders, which is what tells them to look. |
| **API equivalent** | `GET employees/change-requests/{id}` |

### 5.5 Gate pass and movement

| | |
|---|---|
| **Corporate rule** | CR-8, CR-11 |
| **Employee action** | EMP-050 → `POST gate-passes`; EMP-053 → `POST movement/out` and `POST movement/{id}/return`. |
| **Where it lands** | `GatePassService` and `MovementService`. An approved pass yields a signed `VerificationToken` the employee shows at EMP-052. |
| **HR web sees it in** | `/hr/attendance` → movement board, overdue returns, gate pass board and pending decisions. |
| **API equivalent** | `GET movement/out`, `GET movement/overdue`, `GET gate-passes`, `GET gate-passes/pending`, `POST gate-passes/scan` |

**Overdue return is the one live signal in the system.** An employee who declared themselves out
and has not returned appears on `movement/overdue` without anyone submitting anything, and
`OverdueMovementWorker` now pushes it to `MOVEMENT.TEAM` rather than waiting for someone to open
the board. Treat it as a duty-of-care surface, not a compliance one.

### 5.6 First sign-in

| | |
|---|---|
| **Corporate rule** | CR-4 |
| **Employee action** | EMP-002 → `POST auth/token`. |
| **Where it lands** | The account's state moves `AwaitingFirstSignIn` → `Active`. |
| **HR web sees it in** | `/hr/dashboard` mobile access panel. |

## 6. Controller-to-surface map

Which controller serves which surface. Under `api/mobile/v1`. **HR mobile** is the existing
`Vispl.HRManagement.Mobile` app; **HR web** is the Blazor portal, which reaches these services
directly rather than over HTTP.

| Controller / route | HR web | HR mobile | Employee app | Permission |
|---|:---:|:---:|:---:|---|
| `dashboard`, `/headline`, `/scopes`, `/events` | ● | ● | — | `DASHBOARD.VIEW` |
| `dashboard/moments` | ● | ● | — | `PEOPLE.MOMENTS.VIEW` |
| `dashboard/leave-analytics` | ● | ● | — | `LEAVE.REPORT` |
| `dashboard/attendance-overview` | ● | ● | — | `ATTENDANCE.REPORT` |
| `attendance/reports/late-arrivals`, `/miss-punch` | ● | ● | — | `ATTENDANCE.REPORT` |
| `leave/analytics/*` | ● | ● | — | `LEAVE.REPORT` |
| `leave/approvals` | ● | ● | — | `LEAVE.APPROVE` |
| `recruitment/openings/pipeline` | ● | ● | — | `RECRUITMENT.VIEW` |
| `attendance/close/{date}` | ● | — | — | `ATTENDANCE.MANAGE` |
| `employees/mobile-access/*` | ● | — | — | `ACCOUNTS.MANAGE` |
| `me/summary`, `me/kpis/*` | — | — | ● | self |
| `attendance/me`, `attendance/punch` | — | — | ● | self |
| `attendance/corrections` | ● | ● | ● | self / `ATTENDANCE.APPROVE` |
| `attendance/team`, `attendance/register/{date}` | ● | ● | — | `ATTENDANCE.TEAM` |
| `leave/requests*`, `leave/balances` | ● | ● | ● | `LEAVE.VIEW` |
| `leave/team/requests`, `leave/requests/{id}/decision` | ● | ● | ● | `LEAVE.TEAM` / `LEAVE.APPROVE` |
| `employees`, `employees/{id}/*` | ● | ● | ● | `EMPLOYEES.VIEW` / self |
| `movement/*`, `gate-passes/*` | ● | ● | ● | self / `*.TEAM` / `*.APPROVE` |
| `notifications/*` | — | ● | ● | bearer |
| `recruitment/*`, `interviews/*` | ● | ● | — | `RECRUITMENT.*` |

**Reading the table.** A controller marked in more than one column is a shared service, and a
change to it changes every marked surface. `AttendanceController` and `LeaveController` are the
two that carry all three — treat any change to them as a change to the whole product.

**The naming trap.** `HrDashboardController` and `WorkforceReportsController` live under
`api/mobile/v1` but serve HR, not employees. The route prefix says `mobile`; the audience is
whoever holds the permission. Do not read the prefix as a boundary.

## 7. Where the connection is currently weak

Found by tracing the code rather than the design. The first is now closed; the other two stand.

### 7.1 Notifications — the employee workflow set now publishes

`INotificationPublisher.PublishToPermissionAsync` resolves every account holding a permission
and fans a notification out to each, skipping duplicates for the same subject. It was wired to
recruitment only, so an employee could submit leave and no approver would be told.

**Leave and attendance.** `LeaveService` and `AttendanceService` publish seven events:

| Event | Published to | Category | Priority |
|---|---|---|---|
| Leave request submitted | `LEAVE.APPROVE` | `Leave` | Normal |
| Leave approved | the requester | `Leave` | Normal |
| Leave rejected | the requester, with the reason | `Leave` | High |
| Leave request withdrawn | `LEAVE.APPROVE` | `Leave` | Low |
| Attendance correction requested | `ATTENDANCE.APPROVE` | `Attendance` | Normal |
| Correction approved / rejected | the requester | `Attendance` | Normal / High |
| Correction withdrawn | `ATTENDANCE.APPROVE` | `Attendance` | Low |

Three rules the implementation follows, which any further event must also follow:

1. **Publish after the write.** The notification is raised once the record is safely stored, so
   a delivery problem cannot lose the submission. Both repositories and the notification store
   share the request's `HrManagementDbContext`, so a failure here means the write would have
   failed too.
2. **Address the requester through `EmployeeRecord.AccountId`.** It is null for an employee HR
   has not granted mobile access to. That is a normal state, not an error — the personal
   notification is skipped and the decision still succeeds.
3. **A withdrawal notifies the approvers.** Otherwise the queue keeps an item nobody needs to
   decide, which is how an approval queue stops being trusted.

`SubjectId` is suffixed with the outcome (`{id}:approved`, `{id}:withdrawn`) so the publisher's
duplicate guard scopes to one event per request rather than collapsing every event on it.

**Gate passes and movement.** `GatePassService` publishes three events and `MovementService`
two, one of which is swept rather than raised by a gesture:

| Event | Published to | Category | Priority |
|---|---|---|---|
| Gate pass requested | `GATEPASS.APPROVE` | `GatePass` | Normal |
| Gate pass approved | the requester | `GatePass` | Normal |
| Gate pass rejected | the requester, with the reason | `GatePass` | High |
| Gate pass withdrawn | `GATEPASS.APPROVE` | `GatePass` | Low |
| Returned late | `MOVEMENT.TEAM` | `Movement` | Normal |
| **Overdue return** (swept) | `MOVEMENT.TEAM` | `Movement` | High |

Two judgements are worth keeping:

- **Withdrawing an approved pass notifies the approvers**, not only a pending one. A pending
  withdrawal leaves their queue; an approved withdrawal is a pass the gate should stop
  expecting. The service clears the verification token at the same moment.
- **Going out and returning on time is not notified.** It is the ordinary shape of a working
  day, and a notice for every one would train people to ignore the category — including the
  overdue notices that matter. Only a *late* return raises one.

**Overdue return needs a sweep, not a call.** Every other notification hangs off a gesture; this
one exists precisely because the employee did not make one. It is therefore:

```text
IOverdueMovementNotifier          BM.ITF.Workforce — the system-actor interface, no caller context
  └─ OverdueMovementNotifier      BM.Workforce — finds open, overdue movements and publishes
       └─ OverdueMovementWorker   Worker.Workforce — every 10 minutes, per active tenant
```

It follows `IAttendanceDayCloser` / `AttendanceDayCloseWorker`, the existing system-actor pair:
no `MobileCallerContext`, one tenant's failure logged and skipped rather than stopping the rest.

Three details it depends on:

1. **A 15-minute grace** before an overdue movement is reported. Someone walking back in at the
   expected minute should not be announced as missing.
2. **A two-day lookback.** An open movement older than that was almost certainly never closed
   rather than genuinely still running, and re-announcing it daily helps nobody.
3. **Repetition is free.** The notification is keyed `{movementId}:overdue`, so the publisher's
   own duplicate guard reports it once per recipient however often the sweep runs. That is what
   makes a 10-minute interval safe — a person still out an hour later is not announced six
   more times.

**Profile changes and mobile access.** `MobileEmployeeService` and
`EmployeeMobileAccessService` publish three more:

| Event | Published to | Category | Priority |
|---|---|---|---|
| Profile change requested | `EMPLOYEES.EDIT` | `Employee` | Normal |
| Mobile access granted | the employee | `Account` | Normal |
| Mobile access restored | the employee | `Account` | Normal |

Three judgements here:

- **The change request is the only route CR-7 leaves open.** An employee never writes their own
  master record, so this notification is the whole mechanism by which their correction reaches
  HR. Without it the request sits `Pending` until somebody happens to open their file.
- **A grant notifies an account that has never been used.** Nobody reads it at the time; it is
  waiting at first sign-in. The credentials are handed over out of band by whoever granted
  access — this says what the login is *for*, which a username and a temporary password do not.
- **A suspension is deliberately silent.** The only account it could be addressed to is the one
  just switched off, so the person cannot sign in to read it. Telling someone through a channel
  they have just lost is worse than not telling them; whoever suspended the account owns saying
  so. Restore notifies, because by then they can read it.

Restore fires on the **transition** — the account's `IsActive` is captured before the write, so
restoring an already-active account stays a no-op rather than sending a spurious notice.

**Still outstanding.** Nothing in the employee workflow set. The remaining silence is the
employee change request's *decision*: `IMobileEmployeeService` exposes create and read but no
decide, so HR applies the change through the section endpoints and the request stays `Pending`
with nobody told. That is a missing workflow, not a missing notification, and it belongs with
the approvals module.

**Known coarseness.** `PublishToPermissionAsync` addresses *everyone in the tenant* holding the
permission, not the one approver who owns the request. Narrowing it needs approver resolution,
which lives in the not-ready `IApprovalService`. Until that exists, a tenant with many approvers
will see the same item in several inboxes. This is recorded in the call sites as a comment.

**Shared resolution.** All four services need the same two facts about the same person — a
display name, and an account to address. `IEmployeeRepository.DescribeAsync` in `BM/Shared`
returns both. `EmployeeRecord.AccountId` was already there and is the recipient; it is null for
an employee HR has not granted mobile access to, which is a normal state, so the personal
notification is skipped and the decision still succeeds.

**Regression guard.** `NotificationCoverageTests` asserts that each workflow service still
depends on `INotificationPublisher`, and that `IOverdueMovementNotifier` resolves to a real
implementation rather than the not-ready proxy. Neither failure would break the build or any
other test: the endpoints keep answering 200, an unregistered hosted service simply never runs,
and the notifications quietly stop.

### 7.2 Permission revocation is not immediate

Claims are minted into the token at sign-in (CR-3, §4.4). Removing a permission on the web does
not reach a live session until refresh. Either shorten the access-token lifetime, or have
`session/refresh` re-read permissions and let the app apply the returned set. Until one of those
lands, suspending mobile access is the only immediate revocation.

### 7.3 Attendance is only true after the day closes

`AttendanceDayCloseWorker` is what turns punches into a day status. Before it runs, the
employee's EMP-012 and HR's `/hr/attendance` can legitimately show different pictures of the
same day — not a bug, but it must be visible. Both surfaces should show the close state of the
day they are rendering, and neither should present an open day as settled.

## 8. Rules for changing anything here

1. **Change the service, not the surface.** A rule that needs changing lives in a BM service.
   Changing it in a Blazor page or a ViewModel creates the second rule set CR-1 forbids.
2. **Check the controller-to-surface map before editing a shared controller.** Three of them
   serve all three surfaces.
3. **A new employee-facing endpoint needs its HR-facing counterpart considered at the same
   time.** Every submission needs a queue; every queue needs a notification (§7.1).
4. **Never add a route that lets one portal act as another.** CR-3 is covered by a smoke test
   against a running host precisely because a policy change can weaken it without a compile
   error.
5. **New permission constants go in the catalogue and the matrix together**, or HR cannot grant
   what the app requires and the feature is unreachable.
6. **When a module moves from not-ready to built**, register the real service *before*
   `AddMobileBusiness()`, add its routes to `ImplementedEndpoints.Routes` and its interface to
   `RealServices`. That list is what stops a working endpoint silently reverting to 501.
