# Phase D Plan: Presence (HR web + Employee App)

Written 2026-09-16. Follows `phase-c-attendance-leave-plan.md`. Covers the two **Employee Monthly
Attendance Report** screenshots sent today, the locked **W08** attendance page
(`scopedocs/web-portal/references/W08-misspunch-gatepass-attendance.png`), the locked **blue sidebar and
header**, and the app presence surfaces from build plan §4.10, 4.11, 4.19–4.22.

Colours marked **S** are pixel-sampled from W08 (3×3 average); **≈** are read from the chat screenshots,
which are not on disk — re-sampled if saved to `scopedocs/hrweb/`.

---

## 1. Decisions (2026-09-16, all as recommended)

| # | Question | Decision |
|---|---|---|
| D-1 | Edit icon on a report day cell | HR applies the change **immediately with a required reason**, stored as an *approved correction* (original punches kept, who/when/why), day recomputed, employee notified. Needs `ATTENDANCE.MANAGE` |
| D-2 | Punch location | **One GPS fix at the moment of punching**, only asked for when HR's *Require location* is on; refused or unavailable location blocks the punch only then. No background tracking |
| D-3 | Blue sidebar + blue header | **Built now** on every HR page, replacing the current nav |
| D-4 (default) | Day cell codes | `P` present, `A` absent, `W` weekly off / holiday, leave policy code (`SL`, `CL`, `AL`…), `HDF` / `HDS` half day worked in the first / second half of the shift |
| D-5 (default) | Master Roll | Excel register (Syncfusion): one row per employee, code per day, totals, a signature column; Export Excel is the detailed in/out workbook |

---

## 2. Widget extraction and colour schema

### 2.1 HR chrome — sidebar + header (all HR pages)

| # | Widget | Anatomy | Colours |
|---|---|---|---|
| H01 | `HrSidebar` | hamburger; section captions DASHBOARD · EMPLOYEE · ATTENDANCE · LEAVE MANAGEMENT · OUT & PASS · CALENDAR · REPORTS; icon + label rows; active row lighter with a left rule; collapses to icons (W08) | ground `#3375EA` **S**; caption ≈`#CFE0FF` caps 11; label white 14; active row ≈`#4A86F0` with ≈`#7EE3C8` left rule |
| H02 | `HrTopBar` | logo + "HR Management, Dashboard" + tagline · CURRENT date · LAST LOGIN · bell with count · avatar, name, role, chevron · app-menu ☰ | ground `#3375EA` **S**, radius 8, white text; tagline ≈`#D6E4FF`; divider ≈`#6F9CF0`; bell circle ≈`#5A8FEF`; badge `#FF4D4F` white |
| H03 | `HrPageTitle` | blue title + breadcrumb (exists) | title `#2563EB`, breadcrumb `#16202F` |

Sidebar links: Dashboard `/hr/dashboard` · New Employee `/hr/employees/new` · Birthday, Anniversaries `/hr/dashboard#celebrations` · Last Coming `/hr/attendance#late` · Miss Punch `/hr/attendance#miss-punch` · Attendance Overview `/hr/reports/attendance-monthly` · Pending Approval `/hr/leave` · Leave Balance `/hr/leave#balances` · Going Out `/hr/attendance#movement` · Gate Pass `/hr/attendance#gate-pass` · Events & Holidays `/hr/dashboard#events` · Analytics `/hr/dashboard`.

### 2.2 Employee Monthly Attendance Report (two screenshots, ≈)

| # | Widget | Anatomy | Colours |
|---|---|---|---|
| R01 | `ReportFilterBar` | DEPARTMENT · EMPLOYEE · MONTH selects (caps labels 12 bold), **More Filters** (outline, gear), **Apply Filter** (solid, sliders) | card white radius 6; select border ≈`#D1D5DB`; More Filters border ≈`#9CA3AF`; Apply ≈`#3563E9` white |
| R02 | `ReportKpiTile` ×4 | caps title 12 · value 28 · caption 12 | Present ≈`#DCFCE9` (border ≈`#B7EBCB`); Absent ≈`#F4E4E6`; Leave ≈`#F3EEE6`; Rate ≈`#E3E7EC`; text `#374151`, value `#111827` |
| R03 | `AttendanceCalendar` | title "Monthly Attendance Calendar"; **← Prev** · month · **Next →** pill buttons; weekday header row; day cells with date + four count dots P · A · L · W | header cell ≈`#E8EEF8` text ≈`#6B7C93`; day cell ≈`#E9F0FB` border ≈`#3B6FD8`; empty cell white border ≈`#E5E7EB`; dot P ≈`#4CAF50`, A ≈`#E0443A`, L ≈`#E08A2E`, W ≈`#8E9AAF` (white letter + count) ; Prev/Next border ≈`#3B82F6` text ≈`#3B82F6` |
| R04 | `CalendarLegend` | square swatch + label: Present · Absent · Leave · Weekend/Holiday | plate ≈`#F7F8FA`; swatches ≈`#DCFCE7`/`#22C55E`, ≈`#FDE2E2`/`#EF4444`, ≈`#FEF1DC`/`#F59E0B`, white/`#6B7280` |
| R05 | `ReportToolbar` | "Detailed Attendance Records (April 2026)" · **Analytics** (outline) · **Master Roll** (green) · **Export Excel** (blue) · **Print** (outline blue) | month text ≈`#3B82F6`; Master Roll ≈`#22A15A`; Export ≈`#3B7DDD`; Print border/text ≈`#3B7DDD` |
| R06 | `AttendanceGrid` | checkbox · Employee ID · Name · Company · Department · Designation · Shift (`09:00–18:30 (Flexible)` blue) · one column per day | header ground `#ECF3FF` **S** (same as W08), header text ≈`#2563EB`, rule `#75A2F1` **S**; shift text ≈`#2563EB`; selected row ≈`#F7F9FC` |
| R07 | `DayCell` | code (bold) · in time · out time (blue) · edit icon plate | P ground ≈`#E3F7E8` code ≈`#2E9E4F`; leave (SL…) ground ≈`#FCE4EC` code ≈`#E0457B`; W ground ≈`#E6ECF5` code ≈`#6B7C93`; HDF/HDS ground ≈`#FFF3CD` code ≈`#E0A100`; A ground ≈`#FDE2E2` code ≈`#E0443A`; times ≈`#1D4ED8`; edit plate white, icon ≈`#4B5563` |
| R08 | `DayEditModal` | employee + date · status · in · out · reason (required) · Save | standard `hr-modal` |

### 2.3 Attendance page — W08 (sampled **S**)

| # | Widget | Anatomy | Colours |
|---|---|---|---|
| A01 | `MissPunchList` | ⚠ title; Employee · Department · Designation · Date · Punch Time · Expected Time pill | card `#FFFFFF` **S** on `#F5F7F9` **S**; header `#ECF3FF` **S**, rule `#75A2F1` **S**; CHECK-IN pill `#FEF5D6` **S** text ≈`#B7791F`; CHECK-OUT pill `#EBF4FE` **S** text ≈`#2563EB` |
| A02 | `GatePassApprovalList` | 🎫 title; Employee · Department · Designation · Pass Type pill · Purpose · **Approve / Reject** (added: the list is an approval list) | DAY PASS `#EBF4FE` **S** text ≈`#2563EB`; MULTI-DAY `#DCEEE0` **S** text ≈`#2F855A` |
| A03 | `AttendanceOverviewTable` | 👥 title "(Current Month)"; Employee · Department · Designation · Present · Absent · On Leave · Attendance % (value over bar) · Status (dot + word) | bar fill `#2962C8` **S**, track `#F0F0F0` **S**; dot Good/Excellent `#5BAE61` **S**, Fair `#F9CA54` **S**, Poor ≈`#E0443A` |
| A04 | Late arrivals, Off site now | kept below, restyled to the same table look | — |

### 2.4 Employee app presence (no images; existing coral design language)

| # | Widget | Anatomy |
|---|---|---|
| E01 | `PunchSheet` (update) | live clock · state · coral Check in/out · location line ("Location will be recorded" when required) |
| E02 | `CorrectionSheet` | date · missing side (in/out/both) · requested in/out times · reason picker + comment · Submit |
| E03 | `MyCorrectionsSheet` | rows: date · requested times · status pill · Withdraw |
| E04 | `ApplyGatePassSheet` | Day pass / Multi-day segmented · dates · from/to time · purpose · destination · Submit |
| E05 | `MyGatePassesSheet` | cards: type pill (DAY PASS `#EBF4FE`/blue, MULTI-DAY `#DCEEE0`/green — same as web) · window · purpose · status · **Show pass** |
| E06 | `PassCredentialSheet` | large QR of the verification token · validity window · employee name/number (memory only) |
| E07 | `MovementSheet` | Going out: kind (Personal/Official) · purpose · expected return · Declare; when out: timer + **I'm back** |
| E08 | `PresenceApprovalsSheet` | tabs Gate passes · Corrections; cards with Approve (green `#5BA55E`) / Reject (coral) + reason |

---

## 3. Connectivity

### 3.1 HR web

| Control | Service call | Data | Permission | State |
|---|---|---|---|---|
| Report filters, KPI tiles, calendar, grid | `IEmployeeReportService.GetMonthlyAttendanceAsync` (**extended**: department, employee, company filters; `Summary`; `DailyTotals`; per-cell code/in/out/tone; company, designation, shift label) | `workforce.AttendanceDays` + punches + leave days + shift schedule | `REPORTS.VIEW` + `ATTENDANCE.VIEW` | extend |
| Day cell edit | `IAttendanceService.ApplyHrCorrectionAsync` (**new**) → correction row `Approved` + punches + day recompute + notification | corrections, punches, days | `ATTENDANCE.MANAGE` | new |
| Master Roll / Export Excel / Print | `IExcelExporter` in the page; print via `hrPayroll.print` | report DTO | `REPORTS.EXPORT` | extend |
| Analytics button | → `/hr/attendance` (W08) | — | — | link |
| Miss punch list | `IWorkforceReportService.GetMissingPunchesAsync` (**extend row**: punch time, expected time) | exceptions + punches + shift | `ATTENDANCE.REPORT` | extend |
| Gate pass approval list | `IGatePassService.GetBoardAsync(State=Pending)` + `DecideAsync` — **the same calls the app's approvals sheet makes** | `workforce.GatePasses` | `GATEPASS.TEAM` / `GATEPASS.APPROVE` | ready |
| Attendance overview | `IHrDashboardService.GetAttendanceOverviewAsync` | days | `DASHBOARD.VIEW` | ready |
| Header last login, bell | identity `LastLoginUtc`; notifications unread for the HR account | identity, notifications | signed in | new query |

### 3.2 Employee app ↔ API ↔ HR web

| App control | Client → API | Server | Seen / decided on HR web | State |
|---|---|---|---|---|
| Punch (+ GPS when required) | `AttendanceApiClient.PunchAsync(lat, long)` → `POST attendance/punch` | `AttendanceService.PunchAsync` — **rejects a punch without coordinates when `RequireLocationForPunch`** | report grid in/out, late list, punch source | extend |
| Request correction | `RequestCorrectionAsync` → `POST attendance/corrections` | `CreateCorrectionAsync` (honours *Allow corrections*) | corrections queue; report cell updates after approval | ready |
| My corrections / Withdraw | `GetMyCorrectionsAsync`, `WithdrawCorrectionAsync` | | | ready |
| Correction approvals (`ATTENDANCE.APPROVE`) | `GetCorrectionQueueAsync` (**new client**) → `GET attendance/corrections?awaitingMyDecisionOnly=true`; `DecideCorrectionAsync` → `POST attendance/corrections/{id}/decision` | `DecideCorrectionAsync` | report cell and miss-punch list clear | ready |
| Apply gate pass | `RequestPassAsync` → `POST gate-passes` | `GatePassService.RequestAsync` (honours *Allow gate pass requests*), notifies approvers | W08 Gate Pass Approval List | ready |
| My passes / Withdraw | `GetMyPassesAsync`, `WithdrawPassAsync` | | | ready |
| Show pass (QR) | `GetCredentialAsync` → `GET gate-passes/{id}/credential` | token issued on approval | gate scan `POST gate-passes/scan` (`GATEPASS.VERIFY`) | ready |
| Pass approvals (`GATEPASS.APPROVE`) | `GetPendingPassesAsync` (**new client**) → `GET gate-passes/pending`; `DecidePassAsync` → `POST gate-passes/{id}/decision` | `DecideAsync` — same as W08 buttons | row leaves the W08 list | ready |
| Going out / I'm back | `DeclareOutAsync`, `RecordReturnAsync` → `POST movement/out`, `movement/{id}/return` | `MovementService` | "Off site now", overdue | ready |

---

## 4. Build order

1. **Server**: report DTO extension; `ApplyHrCorrectionAsync`; miss-punch row times; location enforcement; header query; tests.
2. **HR web**: sidebar + header; report page (R01–R08); attendance page (A01–A04).
3. **App**: punch GPS (+ Android/iOS permission entries); corrections; gate pass apply / list / credential; movement; approvals; menu and (+) wiring.
4. **Verify**: host, tests, app build; SQL round trip for an HR day edit; portal pages in the browser once signed in.
