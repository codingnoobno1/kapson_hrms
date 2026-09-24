# Kapsons employee app — requirements and screen/widget catalogue

Derived from seven screenshots of the live app, held in
[employee-app-references/](employee-app-references/README.md) as `E01`–`E07`. More screens
are coming; §7 lists what is known to be missing.

---

## 1. What this app is, and how it differs from the HR app

The nine references in `screen-references/` are a **recruiter** product: job pipelines,
applicants, interview panels. This is a **workforce self-service** product. One person opens it
to see their own salary, apply for leave, and check their attendance.

Three observations change the architecture:

**It is role-layered, not a separate app.** The Menu shows *"Leave Pending for Approval —
Approve pending leave requests"*, which only a manager can act on, and the Home shows an
**Interview Schedule** listing external candidates, which is recruiter content. So one shell
renders sections according to what the signed-in person may do. That matches the permission
model already built: `SELF.*` for own data, `TEAM.*`/`LEAVE.APPROVE` for reports,
`RECRUITMENT.*` for candidates. **Sections must be driven by permissions, not by a role name**,
or a store manager who also interviews will see the wrong app.

**Detail views are bottom sheets, not pages.** Employee Profile, Salary Details and Leave
History all open as a sheet over a dimmed Menu (E02, E03, E04). The Menu stays mounted
underneath. This is a navigation decision that affects every detail screen below.

**The identity card is the anchor.** Home leads with a coral card carrying photo, name, title,
department, phone, employee number and a **QR code** (E05). The QR is almost certainly the
gate-pass/attendance identity token — it sits in an app that also has "Apply Gate Pass". Its
payload and whether it rotates is an open question (§7).

---

## 2. Transcription of the seven references

Everything below is read directly off the screenshots. Values are the mock's own.

### E01 — Menu

- App bar: `KAPSONS` wordmark with `DOOR TO GLOBAL FASHION` strapline, search button,
  notification bell with badge **5**.
- Section **MY PROFILE**: Employee Profile *(View complete profile information)* · Employee
  Onboarding *(View Onboarding status)* · Salary Details *(View salary structure and
  components)* · Bank Details *(View banking information)*.
- Section **LEAVE MANAGEMENT**: Apply Leave *(Request a leave from work)* · Leave History *(View
  your leave records)* · Leave Pending for Approval *(Approve pending leave requests)*.
- Section **GATE PASS**: Apply Gate Pass *(Request early exit/entry pass)* — **list continues
  below the fold.**
- Bottom tabs: Home · Projects · **(+)** · Attendance · Menu.

### E02 — Employee Profile (bottom sheet)

Coral header card: avatar, **John Doe**, Store Manager, 📍 Operations, `Update Profile` button.
Fields: Employee ID `EMP-001` · Email · Phone `+91-9876543210` · Department `Operations` ·
Company `Kapsons Retail` · Joining Date `15 Jan 2024` · Reporting Manager `Jane Smith`.

### E03 — Salary Details (bottom sheet)

Green hero: **Annual Salary ₹8,00,000**, *Per Month: ₹66,667*.
Components: Base Salary ₹5,00,000 · HRA ₹1,00,000 · Dearness Allowance ₹80,000 · Conveyance
₹60,000 · Medical Allowance ₹40,000 · Bonus/Incentive ₹1,20,000.
Payment Details (left red rule): Frequency Monthly · Mode Bank Transfer · Salary Date *Last
working day of month*.

> The components sum to ₹9,00,000, not the ₹8,00,000 headline. Mock data, but it tells us the
> annual figure is **not** a sum of the listed components — either Bonus sits outside CTC or the
> headline is gross-of-something. This has to be settled before the screen is wired (§7).

### E04 — Leave History (bottom sheet)

Casual Leave, 5 Dec 2024 – 7 Dec 2024 (3 days) • Approved · Sick Leave, 15 Nov 2024 (1 day) •
Approved · Earned Leave, 20 Oct 2024 – 25 Oct 2024 (6 days) • Approved.

### E05 — Home

Identity card (coral): photo, **John Doe**, Store Manager, 📍 Operations, `+91 9876544320`, QR
code, `EMP-001`.
KPI grid 2×2: **Present Days 18** (rose) · **Leave Balance 3** (lavender) · **Active Projects 5**
(mint) · **Performance 4.5** (peach).
**Interview Schedule** card, badge **2**, collapsible: Rajesh Kumar — Store Manager — `Past
Interview` — 2024-12-29 10:30 — Round 1 - Screening; Amit Patel — Sales Executive — `Past
Interview` — 2024-12-29 10:30 — Round 2 - Technical.

> The phone differs between E02 (`+91-9876543210`) and E05 (`+91 9876544320`) — mock drift, but
> a reminder that phone must come from one field with one formatter.

### E06 — Attendance

Stat tiles: **Present Days 18** (green ✓) · **Absent Days 2** (red ✗) · **Half Days 4** (amber
hourglass) · **Holidays 3** (blue calendar).
Recent Attendance: 20-Dec-2024 Present · in 9:15 AM · out 6:30 PM `On Time` · 19-Dec-2024
Present · in 9:45 AM · out 6:15 PM `On Time` · 18-Dec-2024 Half Day · in 2:00 AM · out 6:10 PM
`Half Day` · 17-Dec-2024 Absent - Approved Leave `Leave`.
Actions: `View Full Month` (secondary) · `Analytics` (primary coral).

> 19-Dec check-in is 9:45 AM and still tagged **On Time**, while my attendance engine flags late
> after a 15-minute grace from 09:00. And the 18-Dec check-in reads **2:00 AM**. Both say the
> same thing: **the shift, not a fixed 09:00, defines lateness**, and this app expects a shift
> module (§7).

### E07 — Full Month Attendance

Month stepper `‹ December 2025 ›`, the same four tiles, a `December 2024 Calendar` grid
(Sun–Sat), and `Download report` · `Email Report`.

**The cell colours are the four attendance statuses, using the same palette as the tiles above
them** — there is no separate colour for a weekend:

| Cell | Status | Tile it matches | Count |
|---|---|---|---|
| Green | Present | Present Days (green ✓) | 18 |
| Rose | Absent | Absent Days (red ✗) | 2 |
| Amber | Half day | Half Days (amber hourglass) | 4 |
| Blue | Holiday | Holidays (blue calendar) | 3 |

Reading the visible cells: amber on 6, 20, 27 (Saturdays); blue on 13, 21, 26; rose on 15; every
other cell green. Blue appears exactly three times, matching the Holidays tile — which confirms
the mapping rather than leaving it a guess.

> **The stepper is right and the heading is wrong.** The grid starts with an empty Sunday cell
> and puts the 1st on Monday. 1 December fell on a Monday in **2025**; in 2024 it was a Sunday.
> So the calendar is drawn for December 2025 and only the `December 2024 Calendar` label is
> stale. The real screen must derive stepper, heading and grid from one selected month.

> Sundays 7 and 14 render green (present), so the mock does not treat weekends as non-working in
> the grid. Whether a rest day should have its own cell state is a design question, not a data
> one — see §7.

---

## 3. Screen catalogue

`◆` seen in a reference · `◇` named in the Menu but not yet seen · `○` inferred, needs
confirmation.

### Shell and identity

| # | Screen | Ref | Notes |
|---|---|---|---|
| EMP-S-01 | Splash / brand gate | ○ | |
| EMP-S-02 | Sign in | ○ | Employee credentials; must not reach `/admin` |
| EMP-S-03 | Forced password change | ○ | `MustChangePassword` already enforced server-side |
| EMP-S-04 | Biometric / PIN unlock | ○ | Salary and bank data justify a local lock |
| EMP-S-05 | Home | ◆ E05 | Identity card, KPI grid, Interview Schedule |
| EMP-S-06 | Menu | ◆ E01 | Sectioned launcher; sections gated by permission |
| EMP-S-07 | Global search | ◆ (entry) | Search button present; results screen unseen |
| EMP-S-08 | Notifications | ◆ (badge 5) | List screen unseen |
| EMP-S-09 | Notification detail | ○ | |
| EMP-S-10 | Quick-create sheet (FAB) | ◆ (button) | Targets unseen — likely apply leave / gate pass / punch |
| EMP-S-11 | Settings | ○ | |
| EMP-S-12 | About / app version | ○ | |

### My profile

| # | Screen | Ref | Notes |
|---|---|---|---|
| EMP-S-13 | Employee Profile sheet | ◆ E02 | Read-only view |
| EMP-S-14 | Update Profile | ◇ | Button visible in E02; form unseen. Which fields self-serve vs. need HR approval is open |
| EMP-S-15 | Employee Onboarding status | ◇ | Onboarding engine already built |
| EMP-S-16 | Onboarding task detail | ○ | |
| EMP-S-17 | Salary Details sheet | ◆ E03 | |
| EMP-S-18 | Payslip list | ○ | Implied by salary; not in the visible menu |
| EMP-S-19 | Payslip detail / download | ○ | |
| EMP-S-20 | Bank Details | ◇ | Account number must be masked |
| EMP-S-21 | Bank Details change request | ○ | High-fraud-risk change; needs approval + notification |
| EMP-S-22 | Documents / letters | ○ | |

### Leave

| # | Screen | Ref | Notes |
|---|---|---|---|
| EMP-S-23 | Apply Leave | ◇ | Type, range, half-day, reason, attachment |
| EMP-S-24 | Leave calculation preview | ○ | Server computes days — already built |
| EMP-S-25 | Leave History | ◆ E04 | |
| EMP-S-26 | Leave request detail | ○ | Trail + withdraw |
| EMP-S-27 | Leave balance breakdown | ◆ (KPI) | Per policy |
| EMP-S-28 | Leave Pending for Approval | ◇ | Manager queue |
| EMP-S-29 | Leave approval detail | ○ | Approve / reject with reason |
| EMP-S-30 | Team leave calendar | ○ | Endpoint already exists |
| EMP-S-31 | Holiday calendar (employee view) | ○ | Admin side already built |

### Attendance

| # | Screen | Ref | Notes |
|---|---|---|---|
| EMP-S-32 | Attendance summary | ◆ E06 | |
| EMP-S-33 | Full Month Attendance | ◆ E07 | |
| EMP-S-34 | Attendance analytics | ◆ (button) | Destination unseen |
| EMP-S-35 | Day detail | ○ | Punches for one day |
| EMP-S-36 | Punch in / out | ○ | Engine built; screen unseen |
| EMP-S-37 | Regularisation request | ○ | Corrections engine built |
| EMP-S-38 | Regularisation approval | ○ | |
| EMP-S-39 | Report export sheet | ◆ E07 | Download / Email |

### Gate pass

| # | Screen | Ref | Notes |
|---|---|---|---|
| EMP-S-40 | Apply Gate Pass | ◇ | Early exit / late entry |
| EMP-S-41 | Gate pass history | ○ | |
| EMP-S-42 | Gate pass approval queue | ○ | |
| EMP-S-43 | Gate pass QR / guard scan | ○ | Explains the QR on the identity card |

### Projects (bottom tab — entirely unseen)

| # | Screen | Ref | Notes |
|---|---|---|---|
| EMP-S-44 | Projects list | ◇ | Tab exists; **no module exists server-side** |
| EMP-S-45 | Project detail | ○ | |
| EMP-S-46 | Task / assignment list | ○ | |
| EMP-S-47 | Timesheet entry | ○ | |

### Performance and other

| # | Screen | Ref | Notes |
|---|---|---|---|
| EMP-S-48 | Performance overview | ◆ (KPI 4.5) | **No module exists server-side** |
| EMP-S-49 | Goals / review cycle | ○ | |
| EMP-S-50 | Interview schedule (recruiter slice) | ◆ E05 | Already built |
| EMP-S-51 | Interview feedback | ○ | Already built |
| EMP-S-52 | Directory / org chart | ○ | |
| EMP-S-53 | Help & support | ○ | |

**53 screens: 12 confirmed, 10 named, 31 inferred.**

---

## 4. Widget catalogue

| # | Widget | Ref | Notes |
|---|---|---|---|
| EMP-W-01 | Brand app bar | E01,E05,E06 | Wordmark + strapline, search, bell |
| EMP-W-02 | Notification bell with count badge | all | Red badge, count 5 |
| EMP-W-03 | Search icon button | all | |
| EMP-W-04 | Screen header with back chevron | E06,E07 | `‹ Attendance` |
| EMP-W-05 | Section header (emoji + caps label) | E01 | `👤 MY PROFILE` |
| EMP-W-06 | Navigation list row | E01 | Icon tile, title, subtitle, chevron |
| EMP-W-07 | Tinted icon tile | E01 | Rounded square, per-item tint |
| EMP-W-08 | Bottom tab bar with centre notch | E01,E05,E06 | 4 tabs |
| EMP-W-09 | Centre FAB | E01,E05,E06 | Coral, elevated |
| EMP-W-10 | Bottom sheet container | E02,E03,E04 | Title + close, scrollable, dims parent |
| EMP-W-11 | Employee identity card | E05 | Coral, photo, name, role, dept, phone, QR, number |
| EMP-W-12 | Profile header card (compact) | E02 | Same card without QR; hosts `Update Profile` |
| EMP-W-13 | QR identity badge | E05 | |
| EMP-W-14 | Avatar (photo / initials fallback) | E02,E05 | |
| EMP-W-15 | Inline card action button | E02 | `Update Profile` on tinted card |
| EMP-W-16 | Field list (label over value) | E02 | |
| EMP-W-17 | Linked email field | E02 | |
| EMP-W-18 | Location/department pin line | E02,E05 | 📍 prefix |
| EMP-W-19 | KPI tile | E05 | Icon chip, big value, caption, pastel tint |
| EMP-W-20 | KPI grid 2×2 | E05,E06,E07 | |
| EMP-W-21 | Semantic stat tile | E06,E07 | Status icon drives colour |
| EMP-W-22 | Amount hero card | E03 | Green gradient, primary + secondary amount |
| EMP-W-23 | Amount row list | E03 | Label left, ₹ right |
| EMP-W-24 | Indian currency formatter | E03 | Lakh grouping `₹8,00,000` |
| EMP-W-25 | Accent-bordered info panel | E03 | Left coral rule |
| EMP-W-26 | Icon-prefixed info line | E03 | Emoji + `label: value` |
| EMP-W-27 | Leave history row | E04 | Type icon, range, days, status |
| EMP-W-28 | Status pill | E04 | Approved / Pending / Rejected / Withdrawn |
| EMP-W-29 | Date-range + duration formatter | E04 | `5 Dec 2024 - 7 Dec 2024 (3 days)` |
| EMP-W-30 | Collapsible card with count badge | E05 | `Interview Schedule` + `2` + chevron |
| EMP-W-31 | Interview row | E05 | Name, role, pill, date, time, round |
| EMP-W-32 | Past/Upcoming pill | E05 | |
| EMP-W-33 | Attendance day row | E06 | Date, status, in/out, tag |
| EMP-W-34 | Attendance tag chip | E06 | On Time / Half Day / Leave |
| EMP-W-35 | Check-in/out time pair | E06 | |
| EMP-W-36 | Dual action bar | E06,E07 | Secondary + primary side by side |
| EMP-W-37 | Month stepper | E07 | `‹ December 2025 ›` |
| EMP-W-38 | Attendance calendar grid | E07 | Sun–Sat header + cells |
| EMP-W-39 | Calendar day cell (state colour) | E07 | Present / absent / half day / holiday — same palette as the tiles |
| EMP-W-40 | Calendar legend | ○ | Required — E07 shows four cell colours with no key |
| EMP-W-41 | Export action pair | E07 | Download / Email |
| EMP-W-42 | Empty state | ○ | |
| EMP-W-43 | Loading skeleton | ○ | KPI tiles and rows both need one |
| EMP-W-44 | Error + retry state | ○ | |
| EMP-W-45 | Offline banner | ○ | Punching must degrade honestly |
| EMP-W-46 | Pull to refresh | ○ | |
| EMP-W-47 | Form field (text / number) | ○ | |
| EMP-W-48 | Date picker | ○ | |
| EMP-W-49 | Date-range picker with half-day toggles | ○ | Apply Leave needs it |
| EMP-W-50 | Select / picker sheet | ○ | Leave type, pass type |
| EMP-W-51 | Attachment picker + chip | ○ | Evidence above policy threshold |
| EMP-W-52 | Submit bar (with saving state) | ○ | |
| EMP-W-53 | Approve / Reject action pair | ○ | Rejection requires a reason |
| EMP-W-54 | Confirmation dialog | ○ | |
| EMP-W-55 | Toast / snackbar | ○ | |
| EMP-W-56 | Balance meter | ○ | Used vs available |
| EMP-W-57 | Masked value with reveal | ○ | Bank account number |
| EMP-W-58 | Search result row | ○ | |
| EMP-W-59 | Notification row (read/unread) | ○ | |
| EMP-W-60 | Permission-gated section wrapper | E01 | Hides Approvals for a non-manager |

**60 widgets: 41 evidenced, 19 required but not yet shown.**

---

## 5. What the backend already supports

| Screen area | Status |
|---|---|
| Leave apply / history / detail / approve / balance | **Built and verified** — see [workforce-attendance-leave.md](../../docs/workforce-attendance-leave.md) |
| Attendance punch / days / corrections / exceptions | **Built and verified** |
| Holiday calendar | **Built**, with admin screens |
| Employee profile fields | **Built** — `employee.Employees` + contacts |
| Onboarding status | **Built** |
| Interview schedule | **Built** |
| Notifications | **Built** |
| Home KPI tiles | **Added in this change** — §6 |
| Salary / payslip | **Not built** — deliberately 501 |
| Bank details | **Not built** |
| Gate pass | **Not built** — no module, no entity |
| Projects | **Not built** — no module, no entity |
| Performance | **Not built** — no module, no entity |

---

## 6. KPI endpoints

New surface at `api/mobile/v1/me`, built in this change:

| Method | Route | Serves |
|---|---|---|
| `GET` | `/me/summary` | E05 identity card + the four Home tiles |
| `GET` | `/me/kpis/attendance?month=yyyy-MM` | E06/E07 tiles: present, absent, half, holidays |
| `GET` | `/me/kpis/leave?year=yyyy` | Leave balance tiles per policy |

**Two Home tiles cannot be computed.** Active Projects and Performance have no module behind
them. They are returned as tiles with a null value and an `unavailableReason`, so the app can
render the tile in a "not yet" state rather than showing an invented number. When those modules
land, the tiles fill in with no contract change.

---

## 7. Open questions

1. **Menu below the fold** — E01 is cut off after Apply Gate Pass. At least one more section
   exists.
2. **Salary headline vs. components** — E03's parts sum to ₹9,00,000 against a ₹8,00,000
   headline. Which is authoritative, and is Bonus inside or outside the annual figure?
3. **Shift definition** — E06 tags a 9:45 AM check-in `On Time` and shows a 2:00 AM check-in.
   Attendance currently assumes a fixed 09:00 eight-hour day. What are the real shifts?
4. **QR payload** — what does the identity-card QR encode, who scans it, and does it rotate? If
   it is a static employee number it is not a credential and must not gate anything.
5. **Update Profile** — which fields can an employee change without approval? Bank details and
   phone are the fraud-sensitive ones.
6. **Projects tab** — is this real work tracking, or retail store tasks? It has no server module
   at all, so it needs its own scope.
7. **Performance 4.5** — what scale, whose rating, and is it visible to the employee before a
   review is published?
8. **Gate pass** — approval chain, and whether a pass is enforced at a physical gate.
9. **Rest days in the calendar** — cell colours are the four attendance statuses (resolved
   from E07 at full resolution), so a weekend has no state of its own and renders green.
   Should a rest day be visually distinct from a worked day?
10. **Offline** — must punch and leave-apply work without a connection?
