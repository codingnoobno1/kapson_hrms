# Kapsons Employee App — Scope

## 1. Purpose

This document defines a **second mobile application**: an employee self-service app for
Kapsons staff, drawn from the seven approved mockups in `scopedocs/app2`.

It is not a slice of the HR app. `Vispl.HRManagement.Mobile/screens.md` describes a
112-screen, role-aware HR platform built on a Shell flyout. This app is smaller, flatter and
built for one actor: **the employee, acting for themselves**. It has four bottom tabs, a
scrolling menu of grouped rows, and detail views that open as bottom sheets rather than pushed
pages. The two apps share a backend, a contract library and a design vocabulary. They do not
share navigation, a shell, or a screen inventory.

Four documents define this app:

| Document | Defines |
|---|---|
| `employee-app.md` (this file) | Scope, actors, screen inventory, delivery order, open decisions |
| `backend.md` | Controllers, DTOs, the new layers, and what must be built server-side |
| `widgets.md` | The reusable widget set, design tokens, and widget-to-screen mapping |
| `portal-integration.md` | Corporate rules, and how the HR web portal and this app connect in both directions |

## 2. Why a second app

The HR app's employee-facing surface is empty. `Vispl.HRManagement.Mobile/Employee/` contains
a single `.gitkeep`. Every implemented page in that project — dashboard, jobs, applicants,
interviews — is recruiter work, and its navigation, permissions and information density are
built for a recruiter at a desk.

Meanwhile the backend for employee self-service is largely built and tested. Attendance, leave,
own-profile, gate passes, movement and the employee KPI tiles are real, DB-backed services with
migrations behind them. They have no client.

The mockups make the case plainly: `EmployeeKpiService` already returns the home tiles in
`5.png` and exactly the four attendance tiles in `6.png`, and `EmployeeIdentityCardDto`
already carries the `QrPayload` field that the home identity card renders. The API was written
against these images. This app is the client that was always intended.

## 3. Actors

| Actor | Meaning here | Effect on the app |
|---|---|---|
| Employee | Any signed-in member of staff acting for themselves | The default. Every screen serves them. |
| Manager | An employee who also approves for their reports | Adds two menu rows and four screens. Detected by permission claim, not by a separate build. |
| Security / gate | Scans a gate pass at a door | **Out of scope.** `POST /gate-passes/scan` exists; the scanning surface is a separate concern. |
| HR | Configures and processes | **Out of scope.** HR uses the existing HR app and web portal. |

There is exactly one role switch in this app: a caller holding `LEAVE.APPROVE` sees
*Leave Pending for Approval*, and a caller holding `GATEPASS.APPROVE` sees pending gate passes.
Everything else is identical for every user. Do not build a role-aware shell.

## 4. Reference mockups

`scopedocs/app2` is the visual authority. Where this document and a mockup disagree, the mockup
wins on appearance and this document wins on behaviour and state.

| File | Screen | ID |
|---|---|---|
| `5.png` | Home — identity card, KPI tiles, interview list | REF-A1 |
| `1.png` | Menu — My Profile, Leave Management, Gate Pass | REF-A2 |
| `6.png` | Attendance — stat tiles, recent attendance | REF-A3 |
| `7.png` | Full Month Attendance — stepper, calendar, report actions | REF-A4 |
| `2.png` | Employee Profile sheet | REF-A5 |
| `3.png` | Salary Details sheet | REF-A6 |
| `4.png` | Leave History sheet | REF-A7 |

Patterns the mockups establish, which are mandatory:

- A white app bar carrying the **KAPSONS** wordmark on the left, and on the right a circular
  search button and a circular bell with a red count badge.
- A bottom bar with exactly four labelled destinations and a raised circular red action button
  centred on and overlapping the bar. The mockups label the third slot **Projects**; that slot
  is now **Leave** (see §9.1). Everything else about the bar is as drawn.
- The brand accent is a coral red, `#FF6153`. It carries the identity card, the FAB, and every
  primary button. It is not the HR app's blue.
- The Menu tab is a scrolling list of **emoji-prefixed section headers** over white,
  hairline-bordered rows. Each row is icon, title, one-line subtitle, chevron.
- Detail views open as a **rounded bottom sheet** with a title row and an X close control.
  They are not pushed pages. Full Month Attendance is the one exception and is a pushed page.
- Facts inside a sheet are label-over-value rows on a `#F6F6F6` plate, never a two-column table.
- Status is always a filled pill, coloured from the fixed palette in `widgets.md`.

## 5. Product boundary

**In scope.** Everything an employee does for themselves on a phone: see who they are, see
their attendance, request leave and see its outcome, request a gate pass and show it, read
their own record, and be notified.

**Out of scope, permanently.** Recruitment, employee directory, employee creation, onboarding
administration, probation, offboarding, reports, admin configuration, tenant setup. Those are
the HR app's job and must not leak into this one.

**Out of scope for now, by dependency.** Salary, bank details, payslips, tax, reimbursements,
benefits — every one blocked on the payroll module. See section 9.

**A hard rule.** This app never references `BM`, `DL`, a repository, a `DbContext`, or SQL
Server. It speaks only to `api/mobile/v1` through the typed client defined in `backend.md`.

## 6. Screen inventory

37 screens. IDs are `EMP-nnn` and are stable; a retired screen keeps its number and is never
reissued. EMP-011 and EMP-034 are retired — see §9.1.

### 6.1 Bootstrap and session — 5

| ID | Screen | Notes |
|---|---|---|
| EMP-001 | Splash and session restore | Silent token refresh, then Home or Sign in |
| EMP-002 | Sign in | Username, or tenant plus email, plus password |
| EMP-003 | Tenant resolve | Only when the token carries no tenant claim |
| EMP-004 | Biometric unlock | Local re-auth on resume, over a valid refresh token |
| EMP-005 | Forced update / maintenance | Driven by discovery metadata |

### 6.2 Tab roots — 4

| ID | Screen | Ref |
|---|---|---|
| EMP-010 | Home | REF-A1 |
| EMP-012 | Attendance | REF-A3 |
| EMP-014 | Leave | — (replaces the Projects slot, §9.1) |
| EMP-013 | Menu | REF-A2 |

Tab order left to right is **Home · Attendance · Leave · Menu**. `EMP-011` (Projects) is
retired.

**EMP-014 Leave** is the tab root, not a sheet. It shows the balance cards for every policy the
employee holds, their most recent requests, and an Apply CTA. It absorbs what was EMP-034, which
is retired.

### 6.3 Attendance — 6

| ID | Screen | Ref | Form |
|---|---|---|---|
| EMP-020 | Full Month Attendance | REF-A4 | Page |
| EMP-021 | Day detail | — | Sheet |
| EMP-022 | Punch in / out | — | Sheet (see 9.2) |
| EMP-023 | Attendance correction request | — | Sheet |
| EMP-024 | My corrections | — | Sheet |
| EMP-025 | Attendance analytics | — | Page (see 9.4) |

### 6.4 Leave — 6

| ID | Screen | Ref | Form |
|---|---|---|---|
| EMP-030 | Apply Leave | — | Sheet |
| EMP-031 | Leave review and confirm | — | Sheet |
| EMP-032 | Leave History | REF-A7 | Sheet |
| EMP-033 | Leave request detail | — | Sheet |
| EMP-035 | Leave Pending for Approval | — | Sheet, `LEAVE.APPROVE` |
| EMP-036 | Leave decision | — | Sheet, `LEAVE.APPROVE` |

`EMP-034` (Leave balances) is retired; its content is the EMP-014 tab root.

### 6.5 Profile and record — 5

| ID | Screen | Ref | Form |
|---|---|---|---|
| EMP-040 | Employee Profile | REF-A5 | Sheet |
| EMP-041 | Update Profile | — | Sheet, raises a change request |
| EMP-042 | Employee Onboarding status | — | Sheet, **blocked** |
| EMP-043 | Salary Details | REF-A6 | Sheet, **blocked** |
| EMP-044 | Bank Details | — | Sheet, **blocked, new module** |

### 6.6 Gate pass and movement — 5

| ID | Screen | Form |
|---|---|---|
| EMP-050 | Apply Gate Pass | Sheet |
| EMP-051 | My gate passes | Sheet |
| EMP-052 | Gate pass credential (QR) | Sheet |
| EMP-053 | Declare movement out and return | Sheet |
| EMP-054 | Gate passes pending my approval | Sheet, `GATEPASS.APPROVE` |

### 6.7 Shell services — 6

| ID | Screen | Notes |
|---|---|---|
| EMP-060 | Notifications | Real today |
| EMP-061 | Notification detail | Real today |
| EMP-062 | Search | **Blocked** |
| EMP-063 | Quick actions (FAB) | See 9.3 |
| EMP-064 | Settings and preferences | **Blocked** |
| EMP-065 | Sign out and device list | Partly blocked |

## 7. Readiness of the inventory

Measured against `Vispl.HRManagement.Tests/ImplementedEndpoints.cs` and the not-ready
registrations in `BM/Mobile/MobileBusinessServiceCollectionExtensions.cs`.

| State | Count | Screens |
|---|---:|---|
| **Ready** — a real, tested endpoint exists today | 27 | EMP-001–005, 010, 012, 013, 014, 020, 021, 023, 024, 030–033, 035, 036, 040, 041, 050–054, 060, 061 |
| **Blocked** — the route exists and answers 501 | 6 | EMP-025, 042, 043, 062, 064, 065 |
| **No module anywhere** — needs new persistence | 1 | EMP-044 |
| **Undecided** — see section 9 | 3 | EMP-022, 025, 063 |

73% of the app can be built against a working backend with no server work at all.

## 8. Delivery phases

### Phase 1 — The app exists and is real

EMP-001–005, 010, 012, 013, 020, 021, 060, 061.

Sign in against real OIDC, restore a session, show the identity card and the two home tiles
from `GET /me/summary`, show the attendance month from `GET /me/kpis/attendance` and
`GET /attendance/me`, page the calendar, and read notifications. No mock data at any point.
This phase is the proof that the typed client, auth and the 501 handling all work.

### Phase 2 — Leave, end to end

EMP-014, 030–033, 035, 036.

EMP-014 is a tab root, so it ships in Phase 2 with a Phase 1 placeholder before that: the tab
exists from day one showing balances from `GET /me/kpis/leave`, and gains apply, history and
approval here.

Apply, review, submit, track, withdraw, comment. Balances. For managers, the pending queue and
the decision. Server-computed day counts only — never send a client-calculated day count, and
never display one. `POST /leave/requests/calculate` returns a signed, 30-minute calculation
token that `POST /leave/requests` requires.

### Phase 3 — Presence

EMP-022–024, 050–054.

Gate passes with the QR credential, movement declaration and return, attendance corrections,
and punch if section 9.2 resolves in favour of it.

### Phase 4 — Money and record

EMP-025, 042, 043, 044, 062, 064.

Gated entirely on server modules that do not exist. Do not start client work on these until
the owning module is registered; a screen built against a 501 cannot be tested.

## 9. Open decisions

These block scope, not code. Each needs an answer before its phase starts.

### 9.1 Projects and Performance — RESOLVED, dropped

**Decision: the Projects tab and the Performance tile are dropped from scope.**

`5.png` showed an **Active Projects** tile reading `5` and a **Performance** tile reading `4.5`,
with **Projects** occupying one of four primary navigation slots. Neither module exists anywhere
in the solution — no entity, no repository, no contract — and `EmployeeKpiService` was already
returning both tiles as `"—"` with a note saying so.

What changes:

| Was | Now |
|---|---|
| Tabs: Home, Projects, Attendance, Menu | Tabs: Home, Attendance, **Leave**, Menu |
| Home grid: 4 tiles | Home grid: **2 tiles** — Present Days, Leave Balance |
| EMP-011 Projects | Retired, number never reissued |
| EMP-034 Leave balances (sheet) | Retired, absorbed into EMP-014 Leave (tab root) |

Server work this creates: remove the `active-projects` and `performance` tiles from
`EmployeeKpiService.GetHomeSummaryAsync`, and mark `EmployeeKpiKeys.ActiveProjects` and
`EmployeeKpiKeys.Performance` obsolete. See `backend.md` §6.5.

`KpiDto.UnavailableReason` and the unavailable-tile treatment stay in the design. They are a general
mechanism, not a Projects workaround, and the next module to arrive half-built will need them.

**If a third tile is wanted later**, the honest candidate from data that already exists is
*Pending Requests* — the caller's open leave requests plus open attendance corrections plus
open gate passes, all readable from real repositories today. It is a small server change, not a
new module. It is not in scope until asked for.

### 9.2 There is no punch control in any mockup

`6.png` shows check-in 9:15 AM and check-out 6:30 PM, but no screen in the set records them.
The punch API is fully built and is the most careful code in the workforce module: signed,
device-bound, geo-tagged and idempotent through `ClientEventId`.

Either attendance is captured by biometric hardware and this app only ever reads it, or a punch
screen is missing from the mockups. This is the highest-frequency action an employee takes; it
should not be settled by inference. EMP-022 is specified but not scheduled until it is.

### 9.3 The FAB has no defined action

A raised red action button sits on the bottom bar in every tab. Nothing in the mockups says
what it does. The natural reading is a quick-action sheet over the three things an employee
starts most: Apply Leave, Apply Gate Pass, and Punch — the last of which depends on 9.2.

### 9.4 Analytics, Download report and Email Report are undefined

`6.png` offers **Analytics** beside View Full Month. `7.png` offers **Download report** and
**Email Report**. None of the three has a defined output.

Download and email both need `IExportService` and `IProtectedFileService`, neither of which is
registered. Email additionally needs a route into `Vispl.HRManagement.MailSender`. Analytics
needs a specification before it needs an endpoint.

### 9.5 The Home interview list is HR content

`5.png` shows an **Interview Schedule** card listing candidates — Rajesh Kumar for Store
Manager, Amit Patel for Sales Executive. That is recruiter content, and it is the same card
that appears on the HR app's dashboard.

Either the employee is a hiring manager and the card is permission-gated on
`RECRUITMENT.INTERVIEWS`, or it is a leftover from the HR mockup and should be cut. Cutting it
is the recommendation: an employee self-service home is the wrong place for a hiring pipeline.

### 9.6 The QR payload is not a credential

`EmployeeKpiService` sets `QrPayload` to the raw employee number, in plaintext. If the QR on the
home identity card is meant to be scanned at a gate, it must instead be the signed
`VerificationToken` from `GatePassCredentialDto`, which `POST /gate-passes/scan` already
validates. If it is only a visual identity badge, it should be documented as such and never
accepted by a scanner.

### 9.7 Bank Details needs a new module

**Bank Details** is the only menu row with nothing behind it at any layer. There are no bank,
IFSC or account fields anywhere in `Contracts`, `VO` or `DL`. It needs an entity, a migration,
a repository, a service, a contract and a controller — and it is the most sensitive data in the
app, so it needs masking, field-level permission and a no-cache rule. See `backend.md` §6.

## 10. Cross-cutting rules

1. **No mock data, ever.** The HR app shipped a `DemoDataService`; this app must not. A screen
   with no endpoint shows the not-ready panel, not invented numbers.
2. **501 is a first-class state.** `MobileFeatureNotReadyException` produces a `ProblemDetails`
   carrying `code`, `module` and `requiredComponents`. The app renders that as a calm panel
   naming the feature — "Salary is not available yet" — never as an error.
3. **Every mutation carries `Idempotency-Key`.** A retry on a flaky connection must not create
   a second leave request.
4. **Every edit carries `If-Match`.** `MutationResultDto.RowVersion` comes back as an ETag;
   send it on the next write.
5. **The server owns every number.** Leave days, present days, balances and holidays are all
   computed server-side. The app formats; it does not calculate.
6. **PII does not cache.** Personal, compensation and bank data live in memory for the life of
   the screen. Attendance and leave summaries may cache encrypted and time-limited.
7. **Permission shapes the UI, but the server enforces it.** A hidden row is a courtesy; the
   403 is the control.
8. **One tenant per session.** The tenant claim is fixed at sign-in.

## 11. Definition of done, per screen

A screen is done when it has a routed page or sheet, a ViewModel with compiled bindings, real
endpoint wiring, and a rendered state for each of: loading, empty, error, offline, not-ready
(501), and permission-denied (403). Plus automation IDs, 44pt touch targets, dynamic-text
tolerance, and no PII in logs.
