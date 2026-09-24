# Employee App — Phase C Plan: Attendance and Leave

Written 2026-09-16. Extends `employee-app-build-plan.md` (§1 decisions D1–D5 still stand) for the
screens the product owner sent on this date: **Attendance**, **Attendance Analytics** (three
scroll positions), **Home (scrolled)**, **Leave History** and **Leave Pending for Approval**.

- Colours for Attendance and Leave History were pixel-sampled from `scopedocs/app2/6.png` and
  `4.png` (build plan §2.5, §2.4).
- **Analytics, Leave Pending for Approval and the scrolled Home are not on disk.** Their colours
  below are read from the chat screenshots and marked ≈. When those images are saved as
  `scopedocs/app2/8.png…12.png`, every ≈ value is re-sampled and replaced before the tokens are
  frozen.

---

## 1. Widget extraction and colour schema

### 1.1 Attendance — `6.png` (the whole page scrolls; no month stepper on this screen)

| # | Widget | Anatomy | Colours |
|---|---|---|---|
| C01 | `PageBackHeader` | `‹` chevron + title "Attendance", 20sp semibold | text and chevron `#111827` |
| C02 | `AttendanceStatTile` (outlined, 2×2) | icon 24 top-centre · value 26 bold · label 12 | ground `#FFFFFF`, border `#E5E5E5`, radius 12, label `#6B7280` |
| | — Present Days | check-circle (filled) | icon + value `#5EB465` |
| | — Absent Days | x-circle (outline) | icon + value `#FF6153` |
| | — Half Days | hourglass | icon + value `#F6A549` |
| | — Holidays | calendar | icon + value `#6D9BE0` |
| C03 | `SectionTitle` | "Recent Attendance", 15 semibold | `#111827` |
| C04 | `RecentAttendanceRow` | status icon 16 · date `20-Dec-2024` 14 bold · detail line `Present . Check-in: 9:15 AM . Check-out: 6:30 PM` 12 · **chip under the text** (left-aligned, radius 3) · hairline | date `#111827`, detail `#6B7280`, hairline `#E5E7EB` |
| | — chip On Time | | `#84D382`, white text |
| | — chip Late | | `#FF6153`, white text |
| | — chip Half Day | | `#F6A549`, white text |
| | — chip Leave | | `#FBDD59`, text `#111827` |
| | — chip Absent | | `#FF6153`, white text |
| | — chip Holiday | | `#6D9BE0`, white text |
| C05 | `ActionButtonPair` | two equal buttons, radius 4, height 38, leading icon | View Full Month `#F0F0F0` / `#111827`; Analytics `#FF6153` / white |

### 1.2 Attendance Analytics — three scroll positions (≈)

| # | Widget | Anatomy | Colours |
|---|---|---|---|
| C01 | `PageBackHeader` | "Attendance Analytics" | `#111827` |
| C06 | `PeriodChips` | label "Select Period" 14 semibold; chips This Month · Last 3 Months · Last 6 Months · This Year, wrap, pill | active `#FF6153` / white; inactive ≈`#EEEEEE` / ≈`#333333` |
| C07 | `KpiBandCard` (full width, stacked, radius 8) | label 12 · value 28 bold · delta line 12 with arrow ↑ ↓ → | delta tone: good ≈`#4A9A5B`, bad ≈`#EE7F84`, same ≈`#4A9A5B` |
| | — Overall Attendance % | | ground `#FFFFFF`, border ≈`#D4D4D4`, label `#6B7280`, value ≈`#333333` |
| | — Days Present | | ground ≈`#EEF8F8`, border ≈`#3E8F6E`, label + value ≈`#3D8B5F` |
| | — Days Absent | | ground ≈`#FDECEC`, border ≈`#F2A7A7`, label + value ≈`#F07C80` |
| | — Half Days | | ground ≈`#FDF6E6`, border ≈`#F3BE66`, label + value ≈`#F2A33C` |
| C08 | `MonthlyBreakdownCard` | "Monthly Breakdown" section title; card title "Attendance Summary - Last 3 Months" 15; column chart (Syncfusion `SfCartesianChart`); month axis; footer `Oct: 82% · Nov: 84% · Dec: 81.8%` | ground ≈`#EDF4FC`, border ≈`#C9DCF0`, title ≈`#6B7280`, axis/footer `#374151`, bars `#6D9BE0` (the mock draws none — colour taken from the Holidays accent, confirm) |
| C09 | `InsightsCard` | "Key Insights" section title; card title "Analytics & Recommendations"; rows: emoji · title 13 semibold · text 12 · divider | ground ≈`#EDF4FC`, border ≈`#BCD3EC`, title `#374151`, row title `#111827`, text `#6B7280`, divider ≈`#D6E2F0`; emoji 📈 ⚠️ 🎯 📊 |
| C10 | `DetailedStatisticsPlate` | "Detailed Statistics"; label/value rows with dividers; first and last rows bold | ground ≈`#F4F4F4`, divider ≈`#E2E2E2`, Present ≈`#4A9A5B`, Absent ≈`#E0544B`, Half ≈`#F2A33C`, Working Days / Rate `#111827` bold |
| C11 | `ActionButtonPair` | Export Report (download icon) · Email Analytics (mail icon) | `#F0F0F0` / `#111827`; `#FF6153` / white |

### 1.3 Home, scrolled (≈)

| # | Widget | Anatomy | Colours |
|---|---|---|---|
| C12 | `HomeServiceSection` | caps header 12 ("HR & PAYROLL", "WORK & PROJECTS") + 2-column tiles | header `#6B7280` |
| C13 | `ServiceTile` | outline icon 22 · title 14 semibold · subtitle 12, radius 10 | ground `#FFFFFF`, border ≈`#E5E5E5`, icon ≈`#6B7280`, title `#111827`, subtitle `#6B7280` |

Tiles in the image: **Salary Slip**, **Attendance** (HR & Payroll); **My Projects**, **Tasks**
(Work & Projects); a third "HR PAYROLL" header is cut off. See decision Q3.

### 1.4 Leave History sheet — `4.png`

| # | Widget | Anatomy | Colours |
|---|---|---|---|
| C14 | `LeaveHistoryRow` | emoji tile (🗓️ Casual, 🏥 Sick, 📅 Earned; policy-driven) · type 15 semibold · `5 Dec 2024 - 7 Dec 2024 (3 days) • Approved` 12 — **status is text, not a pill** | white card, border `#EEEEEE`, radius 10, soft shadow; title `#111827`, line `#6B7280` |

### 1.5 Leave Pending for Approval sheet (≈)

| # | Widget | Anatomy | Colours |
|---|---|---|---|
| C15 | `ApprovalSummaryBanner` | "3 Leave request(s) awaiting approval" | ground ≈`#FDF3DF`, border ≈`#F1D7A2`, text ≈`#A36B16`, radius 6 |
| C16 | `LeaveApprovalCard` | **header strip** (type name · Pending pill · ⓘ) → employee row (initials avatar · name · `Store Manager . Sales & Retail` · EMP ID label + number) → 2×2 facts (FROM · To · Duration · Applied On) → REASON plate → 3 buttons | card white, border ≈`#DDDDDD`, radius 8; pill `#F6A549` white; ⓘ filled ≈`#2F6FD0`; facts label 11 caps `#6B7280`, value 14 semibold `#111827`; reason plate ≈`#F2F2F2`, label `#6B7280`, text `#374151` |
| | — type tone **blue** (Casual) | | strip ≈`#D9E8F8`, strip title + avatar + EMP number ≈`#2F6FD0` |
| | — type tone **pink** (Sick) | | strip ≈`#F5D5E0`, accent ≈`#B8386A` |
| | — type tone **green** (Earned) | | strip ≈`#DDEFDD`, accent ≈`#3F8A46` |
| C17 | `DecisionButtonRow` | three equal buttons, radius 4, height 34, leading glyph | Approve ≈`#5BA55E` ✓; Disapprove ≈`#F4B04E` ⊘; Cancel Request ≈`#8C8C8C` ✕; all white text |

### 1.6 New tokens (added to `Tokens.cs` in one block)

`AttChipOnTime #84D382`, `AttChipLate #FF6153`, `AttChipHalf #F6A549`, `AttChipLeave #FBDD59`,
`AttChipHoliday #6D9BE0`; `StatTileBorder #E5E5E5` (exists); `SecondaryButton #F0F0F0` (exists);
`PeriodChipInactive`, `KpiBand{Overall,Present,Absent,Half}{Ground,Border,Ink}`, `DeltaGood`,
`DeltaBad`, `InfoCardGround`, `InfoCardBorder`, `InfoDivider`, `StatsPlate`, `ApprovalBanner{Ground,Border,Ink}`,
`LeaveTone{Blue,Pink,Green,Purple,Teal,Amber}{Strip,Ink}`, `DecisionApprove`, `DecisionDisapprove`,
`DecisionCancel`. A tone key from the server (`blue`, `pink`…) is the only way a leave type
becomes a colour — the API never sends hex (build plan §2 rule).

---

## 2. Connectivity — Attendance

Every figure is computed on the server (CR-6). "Same data" means the HR portal reads the same
rows through the same service, so the app and the portal cannot disagree.

| Screen control | App client | API | Server | HR web — where it is controlled / seen | State |
|---|---|---|---|---|---|
| 4 stat tiles | `MeApiClient.GetAttendanceKpisAsync` | `GET me/kpis/attendance?month=` | `EmployeeKpiService` over `workforce.AttendanceDays` + holiday calendar | `/hr/attendance`, `/hr/reports/attendance-monthly` (same days) | ready |
| Recent Attendance rows | `AttendanceApiClient.GetMineAsync` | `GET attendance/me?from=&to=` | `AttendanceService` | `/hr/attendance` register | ready — **extend DTO** (below) |
| Row chip (On Time / Late / Half Day / Leave / Absent / Holiday) | — | `AttendanceDayDto.Badge` (**new** `StatusDto`) | derived from day status + first in-punch vs the employee's shift start + grace (`IShiftScheduleService`) | late grace and shift times: **Shifts** master; late list: `/hr/attendance` late arrivals | new field |
| "Check-in 9:15 AM · Check-out 6:30 PM" | — | `AttendanceDayDto.CheckIn`, `CheckOut` (**new**, tenant-local `TimeOnly?`) | first In / last Out punch in tenant time zone | punches on `/hr/attendance` | new fields |
| Row tap → day sheet → Request correction | `RequestCorrectionAsync` | `POST attendance/corrections` | `AttendanceService` (honours *Allow corrections* setting) | corrections queue on `/hr/attendance` (`ATTENDANCE.APPROVE`); decision notifies the employee | ready |
| View Full Month | existing page | `GET me/kpis/attendance`, `GET attendance/me` | — | — | ready |
| Analytics | `MeApiClient.GetAttendanceAnalyticsAsync` (**new**) | `GET me/attendance/analytics?period=this-month\|last-3-months\|last-6-months\|this-year` (**new**) | `AttendanceAnalyticsService` (**new**, BM) | target % set on **Employee App Controls** (`AttendanceTargetPercent`, default 85) | new |
| Export Report | `MeApiClient.DownloadAttendanceReportAsync` (**new**) | `GET me/attendance/report?period=` → `.xlsx` (no-store) | `IExcelExporter` (Syncfusion XlsIO): Summary sheet + Days sheet | same workbook shape as `/hr/reports/attendance-monthly` export | new |
| Email Analytics | `MeApiClient.EmailAttendanceReportAsync` (**new**) | `POST me/attendance/report/email` | `IEmailSender` → employee's work email; **HTML summary** (statistics table + insights). The relay's `api/mail/v1/send` body has no attachment field, so the `.xlsx` is not attached — Export Report is the file route | mail relay: **Admin → Mail settings** | built |

### 2.1 `GET me/attendance/analytics` response

```text
AttendanceAnalyticsDto
  Period { Key, Label, From, To }                    // "Last 3 Months", 2026-06-01..2026-08-31
  Kpis[4] { Key, Label, Value, Unit, Delta { Text, Direction up|down|same, Tone good|bad|neutral } }
          overall-attendance | days-present | days-absent | half-days
  Months[] { Year, Month, Label "Oct", AttendancePercent, Present, Absent, HalfDays }
  Weeks[]  { Label "Week 1", AttendancePercent }      // current month
  Insights[] { Kind trend|watch-out|target|weekly, Emoji, Title, Text }
  Statistics { WorkingDays, Present, Absent, HalfDays, AttendanceRate }
  TargetPercent, GeneratedUtc
```

Deltas compare with the **previous period of the same length** (This Month vs last month, Last 3
Months vs the three before). Insights are server rules, not free text: *Positive Trend* when
present days rose, *Watch Out* when absences rose, *Target Status* against `TargetPercent`,
*Weekly Breakdown* from `Weeks`. Rate formula: decision Q4.

---

## 3. Connectivity — Leave

| Screen control | App client | API | Server | HR web | State |
|---|---|---|---|---|---|
| Leave tab balances | `LeaveApiClient.GetBalancesAsync` | `GET leave/balances` | `IMobileLeaveService` | entitlements: **Admin → Leave policies**; balance grids on `/hr/leave` (same service) | ready |
| Leave History sheet | `GetMyRequestsAsync` (all pages) | `GET leave/requests?state=` | `IMobileLeaveService.ListRequestsAsync` | employee's requests in `/hr/leave` queue and reports | ready — **extend DTO**: `LeaveTypeName`, `ToneKey`, `Emoji` |
| Row tap → detail sheet | `GetRequestAsync` | `GET leave/requests/{id}` | same | — | ready |
| Withdraw | `WithdrawAsync` | `POST leave/requests/{id}/withdraw` | same; removes it from the HR queue | `/hr/leave` queue shrinks | ready |
| Apply Leave (sheet, from Leave tab and (+)) | `GetPoliciesAsync` → `CalculateAsync` → `SubmitAsync` with `Idempotency-Key` | `GET leave/policies`, `POST leave/requests/calculate`, `POST leave/requests` | `LeaveService` (token-signed chargeable days) → `INotificationPublisher` to approvers | appears in `/hr/leave` queue immediately | ready |
| Leave Pending for Approval sheet | `LeaveApiClient.GetApprovalQueueAsync` (**new client method**) | `GET leave/approvals` (exists) | `IWorkforceReportService.GetLeaveApprovalQueueAsync` — **the exact call `/hr/leave` makes** | `/hr/leave` approval queue | ready — **extend row**: `LeaveTypeName`, `ToneKey`, `DepartmentName`, `RowVersion` |
| Banner "3 Leave request(s) awaiting approval" | — | `LeaveApprovalQueueDto.TotalPending` | same | queue header on `/hr/leave` | ready |
| Approve | `DecideAsync(id, approve)` | `POST leave/requests/{id}/decision` | `LeaveService.DecideAsync` — same method `/hr/leave` calls; re-checks balance, spends it, notifies employee | row leaves `/hr/leave`; balance grids update | ready |
| Disapprove | `DecideAsync(id, reject, comment)` — a reason sheet opens first (server requires it) | same | same | same | ready |
| Cancel Request | decision Q1 | | | | decision |
| ⓘ | `GetRequestAsync` | `GET leave/requests/{id}` | workflow steps, attachments | — | ready |
| Leave tab dot / Home "Pending Requests" tile | `GetApprovalQueueAsync` count; `GET me/summary` | | | | ready |
| Leave type colour and emoji | — | `ToneKey`, `Emoji` on policies, balances, requests, approval rows | `LeavePolicy.ToneKey`, `LeavePolicy.Emoji` (**new columns**) | set where policies are configured (decision Q2) | new |

Permissions: the approval sheet shows only with `LEAVE.APPROVE`; the server enforces it on every
call, and `SELF_APPROVAL` stops anyone deciding their own request.

---

## 4. Work list

### 4.1 Server
1. `AttendanceDayDto` + `Badge`, `CheckIn`, `CheckOut`; badge rules in `AttendanceService` using the shift start + grace.
2. `AttendanceAnalyticsService` + `GET me/attendance/analytics`; unit tests for deltas, rate and each insight rule.
3. `GET me/attendance/report` (xlsx) and `POST me/attendance/report/email`; `EmailMessage` gains optional `Attachments`, the relay sender forwards them (verify the relay API accepts attachments — fall back to HTML summary + portal link if not).
4. `EmployeeAppSettings.AttendanceTargetPercent` (migration) + field on the Employee App Controls page.
5. `LeavePolicy.ToneKey`, `Emoji` (migration, seeded CL blue 🗓️, SL pink 🏥, EL green 📅); flow into `LeaveBalanceDto`, `LeaveRequestListItemDto`, `LeavePolicySummaryDto`, `LeaveApprovalRowDto`.
6. `LeaveApprovalRowDto` + `LeaveTypeName`, `DepartmentName`, `ToneKey`, `RowVersion`.
7. Cancel Request per Q1.
8. `ImplementedEndpoints` entries for every new route; typed client methods.

### 4.2 App
1. Tokens block (§1.6) and a `LeaveTone` resolver.
2. Attendance page rebuilt to 6.png: whole-page scroll, C01–C05, chips under the text, no stepper.
3. `AttendanceAnalyticsPage` (pushed route `analytics`): C06–C11, period switch reloads, Export saves to cache and opens the share sheet, Email shows the address it went to.
4. Leave History sheet C14 (paged), detail sheet with Withdraw.
5. Apply Leave sheet: type picker, dates, half-day toggles, reason, Calculate panel, Submit; stale token (412) recalculates silently.
6. Leave Pending for Approval sheet C15–C17 with the reason sheet for Disapprove; refreshes the tab dot and Home tile.
7. Home scrolled sections C12–C13 per Q3.
8. Menu rows Apply Leave / Leave History / Leave Pending for Approval open these sheets (today they jump to the tab).

### 4.3 HR web
1. `/hr/leave` queue shows the same type tone and emoji as the app cards.
2. Leave policy colour and emoji editor (Q2).
3. Employee App Controls: *Attendance target %*.

### 4.4 "It works" — verification
- Unit tests: analytics maths, badge rules, decision paths (approve spends balance, reject needs reason, self-approval refused, cancel per Q1).
- Endpoint registry test covers every new route; 401 without a token.
- Round trip on the dev database: submit a leave request as an employee → it appears on `/hr/leave` → approve in the app client call → balance and Home tile move → notification row written. Checked with SQL and the HR portal page.
- App: Windows build; screenshots of each screen compared against the reference images. A live sign-in needs an employee password, which the product owner enters.

---

## 5. Decisions — taken with the product owner 2026-09-16 (all four as recommended)

| # | Question | Decision |
|---|---|---|
| Q1 | What does **Cancel Request** do on an approver's card? The server knows only approve and reject. | Approver cancel → state **Cancelled** with a required reason, balance untouched, employee notified |
| Q2 | Where are leave-type colours set? | A tone + emoji picker on the leave policy (HR portal), seeded CL blue / SL pink / EL green |
| Q3 | Home "WORK & PROJECTS" (My Projects, Tasks) has no module | Show **HR & PAYROLL** (Salary Slip → Salary Details, Attendance) only; hide Work & Projects until a module exists |
| Q4 | Attendance % formula — the mock's 81.8 % is 18 ÷ 22 (present ÷ days recorded) | (Present + ½ × Half Days) ÷ working days elapsed in the period, excluding holidays and leave |
