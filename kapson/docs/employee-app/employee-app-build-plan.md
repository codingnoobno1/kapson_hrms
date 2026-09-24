# Kapsons Employee App — Build Plan (screen by screen)

Working plan for `Vispl.HRManagement.EmployeeApp`, written 2026-09-15. It sits on top of
`employee-app.md` (scope), `widgets.md` (widgets and tokens), `backend.md` (routes) and
`portal-integration.md` (corporate rules CR-1…CR-12). Where this plan and those documents
disagree, **the decisions in §1 win** — they were taken with the product owner on this date.

The reference images are `scopedocs/app2/1.png`–`7.png` (identical copies are stored as
`Vispl.HRManagement.Mobile/docs/employee-app-references/E01`–`E07`).

---

## 1. Decisions taken 2026-09-15

| # | Question | Decision | Supersedes |
|---|---|---|---|
| D1 | The image's third bottom slot is **Projects**, which has no module | **Leave** takes that slot. Bar order: **Home · Leave · (+) · Attendance · Menu** — the image's exact shape and colours | `employee-app.md` §6.2 order (Home · Attendance · Leave · Menu) |
| D2 | Home tiles 3 and 4 (Active Projects, Performance) have no data | **Pending Requests** (mint) and **Gate Passes this month** (peach). All four tiles are real numbers | §9.1 "two tiles" |
| D3 | Home Interview Schedule card is recruiter content | Shown **only to interviewers** — an employee on an interview panel, or holding the interview permission. Hidden otherwise | §9.5 "cut it" |
| D4 | The raised (+) button has no defined action; no image has a punch control | (+) opens **Quick actions**: Punch in/out (with location), Apply Leave, Apply Gate Pass, Attendance correction. Whether an employee may punch from the phone is an **HR portal setting** | §9.2, §9.3 |
| D5 | Salary and Bank Details were blocked | **Unblocked.** The HR portal now has grade salary structures, generated payroll and an encrypted employee bank account master. The app needs self-scoped read endpoints and two sheets | §9.7, `backend.md` §6.1–6.2 |

Unchanged rules: no mock data (CR-12 not-ready panels), the server computes every number (CR-6),
self-service edits are requests (CR-7), sensitive data is memory-only (CR-9).

---

## 2. Colour grading — sampled from the images

Sampled pixel-by-pixel from `scopedocs/app2` on 2026-09-15 (3×3 averaged). Values marked ≈ are
anti-aliased glyph samples read visually; everything else is a flat fill.

### 2.1 Chrome (every tab root)

| Element | Colour | Image |
|---|---|---|
| Page ground, app bar, tab bar | `#FFFFFF` | all |
| Search / bell circle | `#F4F4F4` | 5 |
| Bell count badge | `#FF6153` fill, white text | 1, 5 |
| Brand accent (identity card, profile header, primary buttons, FAB) | **`#FF6153`** | 2, 5, 6, 7 |
| Tab — active icon and label | `#FF6153` | 5 |
| Tab — inactive icon and label | ≈ `#6B6B6B` | 1, 5 |
| FAB | `#FF6153` circle, white "+", soft shadow, overlaps the bar top edge | 1, 5, 6, 7 |
| Hairline / row border | `#F2F2F2` | 1, 6 |

### 2.2 Home (5.png)

| Element | Colour |
|---|---|
| Identity card | `#FF6153`, white name, white role/department/phone, white QR plate |
| Tile 1 Present Days | ground `#FEE7E8` → `#F8EBE6`; icon chip white, icon ≈ `#F16B85` |
| Tile 2 Leave Balance | ground `#CDD3FE` → `#DFE4FA`; icon ≈ `#2F4AB5` |
| Tile 3 **Pending Requests** (was Active Projects) | ground `#C9EFDB` → `#D8F2E4`; icon ≈ `#1F9D63` |
| Tile 4 **Gate Passes** (was Performance) | ground `#FBDBB2` → `#F8ECDA`; icon ≈ `#F0B342` |
| Tile value / label | `#111827` / `#374151` |
| Interview Schedule card | white card, count badge `#FFE1DE` with `#FF6153` text |
| Interview row | ground `#FFF4F3`, left rule `#FF6153`, "Past Interview" pill `#FF8A80` white text |

### 2.3 Menu (1.png)

| Element | Colour |
|---|---|
| Section header (emoji + CAPS) | `#6B7280` |
| Row | white, 1pt border `#EEEEEE`, radius 14 |
| Row icon tile | `#F1F0F0` rounded square, full-colour emoji |
| Title / subtitle / chevron | `#111827` / `#6B7280` / `#9CA3AF` |

### 2.4 Sheets (2.png, 3.png, 4.png)

| Element | Colour |
|---|---|
| Scrim | black ≈ 45 % |
| Sheet | white, top radius 22, title `#111827`, X `#111827` |
| Profile header card | `#FF6153`; name ≈ `#FFD37A`; role and department white; "Update Profile" white pill, dark text |
| Field plate | `#F6F6F6`; label `#6B7280`; value `#111827` |
| Salary hero | **`#78D17B`**, white text |
| Salary components plate | `#F6F6F6`, row divider `#E9E4E4` |
| Payment details plate | `#FCF1F0`, left rule `#FF6153` |
| Leave history row | white card, border `#EEEEEE`, emoji type icon |

### 2.5 Attendance (6.png) and Full Month (7.png)

| Element | Colour |
|---|---|
| Stat tile | white, border `#E5E5E5`, radius 12 |
| Present icon + value | `#5EB465` |
| Absent icon + value | `#FF6153` |
| Half Days icon + value | `#F6A549` |
| Holidays icon + value | `#6D9BE0` |
| Chip "On Time" | `#84D382`, white text |
| Chip "Half Day" | `#F6A549`, white text |
| Chip "Leave" | `#FBDD59`, dark text |
| Secondary button (View Full Month, Download report) | `#F0F0F0`, dark text |
| Primary button (Analytics, Email Report) | `#FF6153`, white text |
| Month stepper plate | `#F6F6F6` |
| Calendar ground | `#F7F7F7` |
| Cell Present | **`#9DD697`** |
| Cell Absent | **`#EC968F`** |
| Cell Half day | **`#F8E27E`** (yellow — the image's "amber" cells; a fix to `Tokens.CalHalfDay`) |
| Cell Holiday | **`#7397D9`** |
| Weekday header | `#4A6FD1` |

**Token changes this plan makes:** `CalHalfDay` → `#F8E27E` (the image has no separate weekend
colour — see catalogue E07); add tile icon colours, interview card colours, tab inactive colour,
secondary button colour and the salary payment rule as named tokens.

---

## 3. Shell

### 3.1 Bottom bar — `EmployeeTabBar` + `RaisedActionButton` (EW-004, EW-005)

MAUI's Shell `TabBar` cannot host a button that overlaps the bar, so the stock bar is hidden
(`Shell.TabBarIsVisible = false`) and each tab root places one shared `EmployeeTabBar` at the
bottom. Its slots navigate with `Shell.Current.GoToAsync("//home")` etc.

```text
┌────────────────────────────────────────────────┐
│  🏠 Home   📝 Leave    ( + )   👤 Attendance  ☰ Menu │
└────────────────────────────────────────────────┘
```

- Active slot: `#FF6153` icon and label; others `#6B6B6B`.
- (+): 56pt coral circle, white "+", raised 20pt above the bar, shadow. Opens §3.2.
- Leave slot carries a small coral dot when the caller has approvals waiting.

### 3.2 Quick actions sheet — EMP-063 (EW-006)

A bottom sheet, four large tiles in a 2×2 grid:

| Tile | Opens | Visible when |
|---|---|---|
| ⏱ Punch in / out | EMP-022 | HR setting *Allow app punch* is on for the employee |
| 📝 Apply Leave | EMP-030 | always (needs `LEAVE.VIEW`) |
| 🚪 Apply Gate Pass | EMP-050 | HR setting *Allow gate pass requests* is on |
| 🛠 Attendance correction | EMP-023 | HR setting *Allow corrections* is on |

A hidden tile is replaced by nothing; the grid reflows. The server still refuses the action (CR-8).

### 3.3 App bar — `BrandAppBar` (EW-001)

Unchanged: KAPSONS wordmark + "DOOR TO GLOBAL FASHION", circular search (not-ready panel until
search exists), circular bell with the live unread count.

---

## 4. Screens

Each screen lists **layout**, **data**, **states**, **backend work** and **HR portal link**.
"Ready" means the endpoint is real today.

### 4.1 EMP-010 Home — `5.png`

**Layout** (top to bottom)
1. App bar.
2. Identity card (coral): avatar (photo or initials), name, job title, 📍 department, phone,
   QR plate with employee number under it.
3. 2×2 tile grid: Present Days · Leave Balance · Pending Requests · Gate Passes.
   Tapping a tile goes to its screen (Attendance, Leave, Leave history, My gate passes).
4. Interview Schedule card — only for interviewers (D3): title, count badge, collapse chevron,
   rows with name, role, pill (Upcoming / Past Interview), date, time, round.
5. Bottom bar.

**Data** `GET me/summary` (identity + tiles); interview card `GET me/interviews` (new).

**Backend**
- `EmployeeKpiService.GetHomeSummaryAsync`: replace Active Projects and Performance with
  `pending-requests` (open leave + correction + gate pass requests) and `gate-passes`
  (passes this month), with a `TargetRoute` each.
- New `GET me/interviews`: interviews where the caller is a panel member, next 14 days and last
  7; empty for non-interviewers. The card hides when the list is empty *and* the caller holds no
  interview permission.
- QR: rendered from `QrPayload` as a **visual identity badge only** (§9.6) — the gate scans the
  gate pass credential, never this QR.

**HR portal** — photo, name, title, department and phone come from **People** (`/hr/people`);
tiles move when HR approves leave (`/hr/leave`) or gate passes (`/hr/attendance`); interview
panels are set in **Recruitment**.

### 4.2 EMP-014 Leave tab (slot 2)

**Layout**: app bar; approver strip (managers); balance cards per policy (Available · Used ·
Pending); "Recent requests" (3 rows, "See all" → EMP-032); coral **Apply Leave** button; bar.

**Data** ready: `leave/balances`, `leave/requests`, `leave/team/requests`.

**HR portal** — policies and entitlements (Admin → Leave Policies), holiday calendar
(Admin → Holidays), approval queue (`/hr/leave`).

### 4.3 EMP-030 / 031 Apply Leave (sheet)

**Layout**: leave type picker (from `leave/policies`) · From / To date · half-day toggles ·
reason · **Calculate** → server panel (requested days, chargeable days, excluded dates,
balance before → after, warnings) · **Submit** (coral).

**Data** ready: `POST leave/requests/calculate` (30-min token) then `POST leave/requests` with
an `Idempotency-Key`. Stale token (412) → recalculate silently.

**HR portal** — lands in `/hr/leave` approval queue; approvers notified (`LEAVE.APPROVE`).

### 4.4 EMP-032 Leave History (sheet) — `4.png`

**Layout**: rows of emoji type icon, type name, `5 Dec 2024 - 7 Dec 2024 (3 days) • Approved`.
Tap → EMP-033.

**Data** ready: `leave/requests` (paged).

### 4.5 EMP-033 Leave request detail (sheet)

Field plate (type, dates, days, reason, submitted, approver, decision note), status pill,
**Withdraw** when `Actions` allows it.

### 4.6 EMP-035 / 036 Leave Pending for Approval (sheet, `LEAVE.APPROVE`)

Queue rows; tap → decision sheet with Approve (green) / Reject (coral, reason required).
Same service as `/hr/leave` (CR-1).

### 4.7 EMP-012 Attendance (slot 4) — `6.png`

**Layout**: app bar; "‹ Attendance" header; 2×2 white stat tiles (Present, Absent, Half Days,
Holidays with their icon colours); "Recent Attendance" rows — status icon, date, `Present ·
Check-in 9:15 AM · Check-out 6:30 PM`, chip (On Time / Half Day / Leave / Late); buttons
**View Full Month** (grey) and **Analytics** (coral); bar.

**Data** ready: `me/kpis/attendance`, `attendance/me`. The chip comes from the day status and its
exceptions (late arrival → "Late", coral).

**HR portal** — shifts (Admin → Shifts) decide on-time vs late; `/hr/attendance` shows late
arrivals and missed punches from the same days.

### 4.8 EMP-020 Full Month Attendance (pushed page) — `7.png`

**Layout**: "‹ Full Month Attendance"; month stepper `‹ December 2025 ›`; the four tiles;
"December 2025 Calendar" (heading derived from the stepper — the image's stale "2024" is a
mock error); Sun–Sat grid with the §2.5 cell colours; legend; **Download report** (grey),
**Email Report** (coral).

**Backend** — report download and email: a server-rendered monthly attendance CSV/PDF
(`GET me/attendance/report?month=`) and `POST me/attendance/report/email` to the employee's work
email via the existing mail sender. Phase F.

### 4.9 EMP-021 Day detail (sheet)

Status, shift, check-in/out, worked vs expected hours, exceptions; **Request correction** →
EMP-023 when the day allows it.

### 4.10 EMP-022 Punch (sheet, from (+))

Live clock, current state (Checked in at 9:15 / Not checked in), big coral **Check in** /
**Check out**, location disclosure line (EW-057), last punch.

**Data** ready: `GET/POST attendance/punch` (`ClientEventId` once per gesture, device id, lat/long).

**HR portal** — new **Employee App settings**: allow app punch (on/off, per location),
require location. Punches appear on `/hr/attendance` after the day closes.

### 4.11 EMP-023 / 024 Attendance correction (sheet)

Date, requested in/out times, reason, submit → `POST attendance/corrections`; "My corrections"
list with Withdraw. **HR portal**: correction queue on `/hr/attendance` (`ATTENDANCE.APPROVE`).

### 4.12 EMP-025 Attendance analytics (pushed page)

Month-over-month present / absent / half-day bars, on-time rate, average check-in time — all
computed server-side by a new `GET me/attendance/analytics?months=6`. Phase F.

### 4.13 EMP-013 Menu (slot 5) — `1.png`

Sections and rows exactly as the image, all now live:

| Section | Row | Opens |
|---|---|---|
| 👤 MY PROFILE | Employee Profile | EMP-040 |
| | Employee Onboarding | EMP-042 |
| | Salary Details | EMP-043 |
| | Bank Details | EMP-044 |
| 🗓 LEAVE MANAGEMENT | Apply Leave | EMP-030 |
| | Leave History | EMP-032 |
| | Leave Pending for Approval | EMP-035 (only with `LEAVE.APPROVE`) |
| 🚪 GATE PASS | Apply Gate Pass | EMP-050 |
| | My Gate Passes | EMP-051 |
| | Passes Pending my Approval | EMP-054 (only with `GATEPASS.APPROVE`) |
| ⏱ ATTENDANCE | Punch in / out · My corrections | EMP-022 · EMP-024 |
| ⚙ ACCOUNT | Notifications · Sign out | EMP-060 · — |

Rows hidden by an HR setting are removed, not greyed.

### 4.14 EMP-040 Employee Profile (sheet) — `2.png`

**Layout**: coral header card (avatar, name in yellow, title, 📍 department, **Update Profile**
white pill); field plate: Employee ID, Email, Phone, Department, Company, Joining Date,
Reporting Manager. Names are resolved from masters, never raw codes.

**Data** ready: `employees/{id}`, `/personal`, `/organisation`.

### 4.15 EMP-041 Update Profile (sheet)

Editable request fields: phone, personal email, address, emergency contact, marital status.
Submit raises `POST employees/{id}/change-requests` (CR-7). The sheet states that HR applies it.

**HR portal — new**: **Change Requests** queue on `/hr/people` — see old vs new value,
**Apply** (writes through `IMobileEmployeeService`) or **Reject** with reason; employee notified.
This closes the gap recorded in `portal-integration.md` §7.1.

### 4.16 EMP-042 Employee Onboarding (sheet)

Stage, completion %, overdue count, task list with status pills.

**Backend** — `GET me/onboarding` over the real `IOnboardingService` (`backend.md` §6.3).
**HR portal**: Recruitment → onboarding checklist.

### 4.17 EMP-043 Salary Details (sheet) — `3.png`

**Layout**: green hero (Annual Salary `₹8,00,000`, Per Month `₹66,667`); "Salary Components"
plate (Basic, HRA, Conveyance, Medical, Special … annual amounts); "Deductions" plate
(PF, ESI, PT … monthly); Payment Details plate with coral rule (📅 Monthly, 🏦 Bank Transfer,
📆 Salary date).

**Backend — new** `GET me/salary`:
- the employee's grade → `GradeSalaryStructure` → `SalaryCalculator` at full attendance;
- annual = monthly gross × 12; the headline and the parts are the **same** numbers (fixes the
  image's ₹8L vs ₹9L mismatch);
- payment terms from the company payroll settings (frequency Monthly, mode Bank Transfer when the
  employee has a salary account, pay date rule);
- `no-store`, memory only (CR-9).

**HR portal** — grade and basic in **Grade Salary** (`/hr/settings/masters/link-allowances-deductions-with-grade`),
the employee's grade on **People**, visibility switch *Show salary in app* in Employee App settings.

### 4.18 EMP-044 Bank Details (sheet)

**Layout**: field plate — Bank, Account holder, Account `•••• 4821`, IFSC, Account type,
Last updated; **Request change** button.

**Backend — new** `GET me/bank-account` over `IBankAccountService` (masked only) and
`POST me/bank-account/change-requests` (new account details, encrypted on arrival, never echoed).

**HR portal** — HR maintains accounts on **Bank Accounts** (`/hr/payroll/bank-accounts`); the
employee's change request lands in the **Change Requests** queue (§4.15) and applying it writes
the encrypted account. Visibility switch *Show bank details in app*.

### 4.19 EMP-050 Apply Gate Pass (sheet)

Pass type (Day pass / Multi-day), from–to date, from–to time, purpose, destination → `POST
gate-passes`. **HR portal**: gate pass board and decisions on `/hr/attendance` (`GATEPASS.APPROVE`).

### 4.20 EMP-051 / 052 My Gate Passes + credential (sheets)

Cards with type, window, purpose, status pill; approved pass → **Show pass** opens a large QR
of the signed `VerificationToken` with its validity window (memory only).

### 4.21 EMP-053 Movement (sheet)

Declare going out (purpose, expected return) and **I'm back**. Overdue returns alert
`MOVEMENT.TEAM` on the HR side.

### 4.22 EMP-054 Passes pending my approval (sheet, `GATEPASS.APPROVE`)

Queue and Approve / Reject — same service as `/hr/attendance`.

### 4.23 EMP-060 / 061 Notifications

Pushed page; unread rows bold with a coral dot; mark all read; detail sheet with a deep link to
the related screen.

### 4.24 EMP-001 / 002 Splash and Sign in

Coral splash with the wordmark; sign in with username + password; first sign-in forces a password
change (server `MustChangePassword`). **HR portal**: Mobile Access panel on `/hr/dashboard`
grants, suspends and restores.

---

## 5. What HR controls, and where it is visible on the portal

### 5.1 Existing controls

| Control | Portal location | Changes in the app |
|---|---|---|
| Grant / suspend / restore app access | `/hr/dashboard` → Employee app access | Whether sign-in works at all |
| Employee record (name, title, department, manager, phone, photo, grade, division) | `/hr/people` | Identity card, Profile sheet, approval routing |
| Leave policies, entitlements, holidays | Admin → Leave Policies, Holidays | Leave balances, apply picker, Holidays tile |
| Leave approvals | `/hr/leave` | Request status, balances |
| Shifts | Admin → Shifts | On-time / late chips, calendar colours |
| Attendance exceptions, late arrivals, missed punches | `/hr/attendance` | — (HR view of app punches) |
| Gate pass and movement decisions | `/hr/attendance` | Pass status, credential availability |
| Grade salary structures | `/hr/settings/masters/link-allowances-deductions-with-grade` | Salary Details sheet |
| Employee bank accounts | `/hr/payroll/bank-accounts` | Bank Details sheet |
| Payroll generation and approval | `/hr/payroll/generate` | (future) payslips |
| Roles and permissions | Admin → Roles | Approver rows (leave, gate pass) appear |
| Recruitment interview panels | `/hr/recruitment` | Interview Schedule card |

### 5.2 New HR portal features this plan adds

| Feature | Where | Why |
|---|---|---|
| **Employee App settings** (per tenant): allow app punch, require location, allow gate pass requests, allow attendance corrections, show salary, show bank details, allow profile change requests | `/hr/settings` → new "Employee App" card | Makes D4 and the salary/bank visibility controllable; app reads them in `me/summary` |
| **Change Requests queue** (profile + bank) with apply / reject | `/hr/people` → Change Requests tab | Closes the CR-7 loop; today requests are raised and never decided |
| **App adoption strip**: granted / awaiting first sign-in / active in last 7 days | `/hr/dashboard` mobile access panel | HR sees the app is actually used |
| **Punch source column** (App / Device) on attendance boards | `/hr/attendance` | HR can tell a phone punch from a biometric one |

---

## 6. Build phases

| Phase | Contents | Server | App |
|---|---|---|---|
| **A — Look and shell** ✅ built 2026-09-15 | Token fixes (§2), custom bottom bar with raised (+), quick actions sheet with working Punch, Home 4 tiles, identity card with real QR (QRCoder 1.8.0), interview card, Menu icon tiles as the image | Home tiles (D2), `me/interviews` | Shell, Home, Menu, Attendance, Leave |
| **B — Money and record** ✅ built 2026-09-15 | Salary Details, Bank Details, Onboarding; Employee App settings (HR portal → Settings → Employee App Controls, enforced by the API for punch, corrections and gate pass requests); every HR portal export is now a real .xlsx (Syncfusion XlsIO, licence from `SYNCFUSION_LICENSE_KEY`) | `me/app-settings`, `me/salary`, `me/bank-account`, `me/onboarding`, `platform.EmployeeAppSettings` + page | 3 sheets, menu rows and (+) actions follow the switches |
| **C — Leave end to end** ✅ built 2026-09-16 (plan: `phase-c-attendance-leave-plan.md`) | Apply + calculate, history (4.png), detail + withdraw, approvals card with Approve / Disapprove / Cancel Request; leave type colours set by HR; **plus** the Attendance screen rebuilt to 6.png and Attendance Analytics with Export (.xlsx) and Email (pulled forward from F) | `leave/approvals` rows gain type name, department, tone; decision `cancel`; `LeavePolicy.ToneKey/Emoji`; `AttendanceDayDto.Badge/CheckIn/CheckOut`; `me/attendance/analytics`, `report`, `report/email`; attendance target % | LeaveFlows sheets, Attendance, Analytics page, Home HR & Payroll tiles |
| **D — Presence** ✅ built 2026-09-16 (plan: `phase-d-presence-plan.md`) | Punch with GPS when HR requires it, corrections + my corrections, gate pass apply / my passes / QR credential, going out / I'm back, approvals (passes + corrections); **HR web**: locked blue sidebar + top bar, Employee Monthly Attendance Report redesigned (calendar, day cells, HR day edit, Master Roll, Export), W08 attendance page with gate pass approvals | `ApplyHrCorrectionAsync`, report cells / totals / summary, miss-punch times, location enforcement, `PreviousLoginUtc`, correction queue names | PresenceFlows sheets, menu + (+) + day sheet wiring |
| **E — Profile requests** ✅ built 2026-09-16 | Update Profile (Phone, Preferred Name, Marital Status — the only Personal fields this record actually carries; there is no Address/Personal-email column) and a Bank Details "Request change" form, both raised as change requests (CR-7); HR **Change Requests** queue at `/hr/people/change-requests` (linked from `/hr/people`) with Approve/Reject, applying Personal changes through `UpdatePersonalAsync` and bank changes through `IBankAccountService.SaveEmployeeAccountAsync` — the same paths HR's own screens use | `IMobileEmployeeService.ListChangeRequestsAsync` / `DecideChangeRequestAsync`; the requested bank account number is encrypted with `ISensitiveValueProtector` before it is stored and only ever shown masked (`•••• last4`), even to HR reviewing the queue; `UpdateEmployeePersonalRequest.Phone` added (the column existed, the write path didn't) | Menu → Employee Profile "Update Profile" button, Menu → Bank Details "Request change" button |
| **F — Reports** ✅ built 2026-09-16 | Full Month Attendance's Download report / Email report buttons (EMP-020) now call the same analytics report pipeline Attendance Analytics uses (Phase C), for one exact calendar month instead of a rolling period | `AttendanceAnalyticsQuery.Month` (yyyy-MM) — wins over `Period` when both are set; `ResolveWindow` grew a month branch, everything downstream (the .xlsx build, the email) is unchanged | `FullMonthPage`'s two buttons wired to `MeApiClient.GetAttendanceReportForMonthAsync`/`EmailAttendanceReportForMonthAsync` |

Each phase ends with: Windows build of the app, host build, test suite, and the HR portal page
checked in the browser.
