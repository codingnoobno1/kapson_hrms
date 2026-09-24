# KPI catalogue

Every number visible on the dashboard, with what it means, where it comes from, and how it is
computed. **No KPI is stored.** Each is derived at read time from the workforce tables, so a
tile can never disagree with the records it summarises.

## The as-of contract

The header shows `CURRENT: Jan 8, 2026` while the filter bar shows `16 May 2026`. Two dates on
one screen is a bug waiting to happen: half the widgets would follow one and half the other.

**Every KPI takes its period from one `DashboardScope`** — company, unit, and an as-of date.
`CURRENT` is the clock; the filter date is the *as-of* the whole page is computed for. When they
differ the page must say so, because a dashboard silently showing last May's numbers under
today's date is worse than showing nothing.

```
DashboardScope = { TenantKey, CompanyCode?, UnitCode?, AsOfDate, Granularity }
```

`CompanyCode` maps to `employee.Employees.CompanyCode` ("Across 3 Companies"), `UnitCode` to
`DivisionCode`. Both already exist on the employee record.

---

## A. Headline strip (W01) — 6 KPIs

| ID | KPI | Value shown | Definition | Source |
|---|---|---|---|---|
| K-A-01 | Total Employees | 482, "Across 3 Companies" | Active employees in scope on the as-of date | `employee.Employees` where `IsActive` |
| K-A-02 | Present Today | 421, 87.34% | Attendance days with status `Present` or `Partial` | `workforce.AttendanceDays` |
| K-A-03 | On Leave | 28, 5.81% | Employees with an **approved** leave day covering the as-of date | `workforce.LeaveRequestDays` ⋈ `LeaveRequests` |
| K-A-04 | Absent | 19, 3.94% | Expected but no attendance and no approved leave | derived |
| K-A-05 | Late Coming | 14, 2.91% | First punch later than shift start + grace | `AttendancePunches` + shift |
| K-A-06 | Pending Approval | 11, "View All" | Leave requests in `Pending` the caller may decide | `workforce.LeaveRequests` |

**Percentage denominator must be stated.** 421/482 = 87.34%, so the denominator is *total
employees*, not *expected-to-work*. That means on a public holiday the tile reads ~0% present and
looks like a catastrophe. Recommend showing **percent of expected** and labelling it, with the
headcount unchanged.

`Pending Approval` is caller-relative — it is a work queue, not an organisation statistic. It
must be scoped to what this person can actually decide, or the number is a lie for everyone
except an administrator.

---

## B. Policy acknowledgement (W01, W02) — 6 cards + 2 counters

| ID | KPI | Definition |
|---|---|---|
| K-B-01 | Policy read state | Per policy, per employee: `Unread` / `Read` |
| K-B-02 | Policy last-updated | Publication date of the current version |
| K-B-03 | Unread policy count | Policies in scope this employee has not acknowledged |
| K-B-04 | Acknowledgement rate | Employees who have read ÷ employees the policy applies to |

Six policies in the prototype: Leave Policy, Attendance Policy, Work From Home, Code of Conduct,
Benefits & Compensation, Health & Safety. These are **data, not code** — an admin publishes
them.

W02 shows the acknowledgement flow: a modal with the policy body, `✓ You have read this policy`,
and buttons `Cancel` / `✓ Already Read`. Acknowledgement must record **who, which version, and
when** — an acknowledgement that does not name the version is worthless the moment the policy
changes. Re-publishing a policy resets everyone to `Unread`.

---

## C. People moments (W01) — 6 KPIs

| ID | KPI | Definition |
|---|---|---|
| K-C-01 | Birthdays today | `DateOfBirth` month/day = as-of date |
| K-C-02 | Birthdays belated | Within the last N days (N configurable, default 7) |
| K-C-03 | Birthdays upcoming | Within the next N days |
| K-C-04 | Anniversaries today | `JoinedOn` month/day = as-of date |
| K-C-05 | Anniversaries belated | As above, past window |
| K-C-06 | Anniversaries upcoming | As above, future window |

Each row carries name, department, designation, and either *Turning N* or *N Years* plus the
original date. Years of service = as-of year − `JoinedOn` year.

**Date of birth is personal data.** Showing a birthday to the whole company is a choice, not a
default: it needs an employee-level opt-out and a permission (§05). Note the prototype shows the
*joining date* on anniversaries but only the *age* on birthdays — showing a birth year would be
a further disclosure and should stay out.

---

## D. Late coming (W03) — 9 columns, 4 of them KPIs

Table: Employee · Department · Designation · Expected Time · Arrival Time · Delay (Minutes) ·
Total Time Late as on · Total Time Late This Month · Total Time Late This Week.

| ID | KPI | Definition |
|---|---|---|
| K-D-01 | Expected time | Shift start for that employee on that date |
| K-D-02 | Arrival time | First `In` punch |
| K-D-03 | Delay | `Arrival − Expected`, floored at zero, **derived not stored** |
| K-D-04 | Cumulative late — as on | Running total to the as-of date |
| K-D-05 | Cumulative late — this month | Month to date |
| K-D-06 | Cumulative late — this week | Week to date |

**Expected Time is 09:00 for every row in the prototype**, which is exactly the fixed-shift
assumption the employee app already contradicts (its E06 tags a 9:45 arrival "On Time"). The
shift module is a hard prerequisite for this table to be correct — see
[06-delivery-plan.md](06-delivery-plan.md).

*"Total Time Late as on"* is ambiguous. It must be defined as year-to-date or
employment-to-date; the column header should say which.

Two rows contradict their own arrival times (§[07](07-data-issues.md)), which is the argument
for deriving delay rather than storing it.

---

## E. Events and holidays (W03) — 1 list

| ID | KPI | Definition |
|---|---|---|
| K-E-01 | Upcoming events | Name, date, type (`HOLIDAY` / company event), description, within the next N days |

Backed by `workforce.LeaveHolidays`, which already exists and has admin screens. The prototype's
`Type` column implies at least two kinds — national holiday and company anniversary — so the
holiday record needs a **type** and a **description**, which it currently lacks.

---

## F. Leave analytics (W04, W05, W06) — 18 KPIs

### Employee-wise balance (W04)

| ID | KPI | Definition |
|---|---|---|
| K-F-01 | CL balance | Available casual leave |
| K-F-02 | EL balance | Available earned leave |
| K-F-03 | SL balance | Available sick leave |
| K-F-04 | Total balance | **Sum of the above — derived, never entered** |

The prototype's Total disagrees with its own columns in four of six rows. The column set is also
hard-coded to CL/EL/SL while the platform supports arbitrary policies, so this must render **one
column per active policy**, not three fixed ones.

### Department-wise balance (W04)

| ID | KPI | Definition |
|---|---|---|
| K-F-05 | Total employees | Headcount in department |
| K-F-06 | Used days | Approved leave days consumed this year |
| K-F-07 | Available days | Granted − used − pending |
| K-F-08 | Utilisation | `Used ÷ (Used + Available)` as a bar |

Used and Available are **day totals across the department**, not per person — the headers should
say "days" or the numbers read as absurd (45 employees, 705 available).

### Distribution and usage

| ID | KPI | Definition |
|---|---|---|
| K-F-09 | Leave days by category | Donut: Casual 480, Earned 520, Sick 380, Special 120 |
| K-F-10 | Department usage — used | Grouped bar, per department |
| K-F-11 | Department usage — available | Grouped bar, per department |
| K-F-12 | Monthly leave trend | Total leave days per month, last 6 months |
| K-F-13 | Leave status split | Approved / Pending / Rejected |

### Year-end process (W05)

| ID | KPI | Definition |
|---|---|---|
| K-F-14 | Pending requests per department | Count awaiting the year-end run |
| K-F-15 | Total days pending | Sum of days |
| K-F-16 | Average days | `Total ÷ Count` — derived |

"Year end process" is **carry-forward and lapse**, and no such process exists on the platform
today. It needs its own scope: what lapses, what carries, who signs it off, and what the employee
sees. It is the highest-risk item in this whole document because it silently destroys balance.

### Leaves by type, monthly (W06)

| ID | KPI | Definition |
|---|---|---|
| K-F-17 | Used vs entitled per type | `48/74`, rendered as a filled bar |
| K-F-18 | Daily usage by type | Stacked bars per calendar date, one series per type |

Six types shown: Casual, Earned, Sick, Special, Half Day, Maternity. **Half Day is a modifier,
not a leave type** — the platform models it as a 0.5 portion on any policy that allows it.
Listing it as a peer of Casual Leave double-counts. Decide: either the report groups by
*portion*, or half-days are excluded from the type bars and reported separately.

The date-wise chart omits weekends from the x-axis (Jan 1,2,3,5,6,7,…), so the axis is *working
days*, not calendar days.

---

## G. Movement and gate pass (W07, W08) — 8 KPIs

### Going out — personal work (W07)

| ID | KPI | Definition |
|---|---|---|
| K-G-01 | Going-out time | Recorded exit |
| K-G-02 | Expected return | Declared return |
| K-G-03 | Reason | Free text (Doctor Appointment, Bank Work, Home Emergency, Vehicle Service…) |
| K-G-04 | Overdue returns | Expected return passed with no return punch — **not in the prototype, but the point of the widget** |

### Going out — official work (W07)

Same columns with **Purpose** instead of Reason (Client Meeting, Bank Audit, Field Sales,
Recruitment, Vendor Meeting, Event Management). The distinction matters because official
absence is working time and personal absence may not be.

### Miss punch (W08)

| ID | KPI | Definition |
|---|---|---|
| K-G-05 | Missing punch | Date, the punch that exists, and which side is missing (`CHECK-IN` / `CHECK-OUT`) |

This maps directly onto the **attendance exception** engine already built
(`MissingPunch` exception type). The chip shows *which* punch is missing.

### Gate pass approvals (W08)

| ID | KPI | Definition |
|---|---|---|
| K-G-06 | Pass type | `DAY PASS` / `MULTI-DAY PASS` |
| K-G-07 | Purpose | Free text |
| K-G-08 | Pending pass count | Awaiting the caller's decision |

**No gate-pass module exists.** This is new: entity, workflow, approval, and the link to the
employee app's "Apply Gate Pass" and its QR code.

---

## H. Attendance overview (W08) — 5 KPIs

| ID | KPI | Definition |
|---|---|---|
| K-H-01 | Present days | Month to date |
| K-H-02 | Absent days | Month to date |
| K-H-03 | On leave days | Month to date |
| K-H-04 | Attendance % | `Present ÷ Expected working days` |
| K-H-05 | Attendance status | Banded label: Excellent / Good / Fair / Poor |

**The bands must be defined and configurable.** In the prototype 75% is "Good" while 90% is
"Fair", which cannot both be right. Proposed default, to be confirmed: ≥95 Excellent, ≥85 Good,
≥75 Fair, below that Poor — with the thresholds held as tenant configuration, not constants.

---

## Totals

**48 distinct KPIs** across 8 groups. Of these:

- **21** can be computed today from existing tables (attendance, leave, employee, holiday)
- **14** need the **shift module** to be correct (everything involving *expected* time)
- **8** need the **gate pass / movement module**, which does not exist
- **5** need **policy acknowledgement**, which does not exist
