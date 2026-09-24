# Contradictions in the prototype

Places where the mock disagrees with itself. These are not nitpicks: each one is a number the
real system has to compute, and copying the prototype would ship the contradiction.

## 1. Late-coming delays do not match arrival times (W03)

| Employee | Expected | Arrival | Delay shown | Delay implied |
|---|---|---|---|---|
| Ananya Das | 09:00 | 09:18 | **50 min** | 18 min |
| Pooja Singh | 09:00 | 09:16 | **17 min** | 16 min |

Every other row is consistent. **Delay must be derived from the punches, never stored or
entered**, which is what the contract in [02-dtos.md](02-dtos.md) does.

Ananya Das is also listed as *Marketing / Jr. Developer* and Varun Kumar as *IT / Finance
manager* — department and designation are crossed in the mock data.

## 2. Employee leave totals do not match their own columns (W04)

| Employee | CL | EL | SL | Total shown | Sum |
|---|---|---|---|---|---|
| Rohan Desai | 8 | 15 | 7 | 30 | 30 ✓ |
| Sneha Iyer | 6 | 14 | 5 | 25 | 25 ✓ |
| Arjun Nair | 10 | 18 | 6 | **36** | 34 |
| Divya Rao | 5 | 16 | 7 | **21** | 28 |
| Karan Singh | 9 | 12 | 3 | **25** | 24 |
| Ananya Das | 7 | 7 | 1 | **21** | 15 |

Four of six wrong. `Total` is a computed property on the DTO for exactly this reason.

The grid also hard-codes CL/EL/SL, while the platform supports any number of policies per tenant
— the real grid renders one column per active policy.

## 3. The leave status pie and its legend measure different things (W05)

The pie is split **Approved / Pending / Rejected**. Its legend lists **Casual 480, Earned 520,
Sick 380** — leave *categories*, which is what the donut in W04 already shows.

Two dimensions in one widget. Pick one; the categories are already covered elsewhere.

## 4. Attendance status bands contradict the percentages (W08)

| Employee | Present | Attendance % | Status |
|---|---|---|---|
| Divya Rao | 16 | 75% | Good |
| Karan Singh | 15 | **90%** | **Fair** |
| Rohan Desai | 18 | 90% | Good |

90% is both "Good" and "Fair", and 75% is "Good" while a higher figure is not. The percentages
also do not follow from the day counts (Karan: 15 present, 2 absent, 2 leave = 19 days, which is
not 90% of anything sensible).

Bands must be **defined, configurable and returned with the data** so the legend and the
calculation cannot drift.

## 5. Three different "current" dates on one screen (W01)

- Header: `CURRENT: Jan 8, 2026`
- Filter: `16 May 2026`
- Events listed: January 2025
- Anniversaries: December 2024

The header date and the filter date are four months apart, and the content is a year older
again. One `AsOf` drives the page, and when it is not today the page says so — hence
`IsHistorical` on `DashboardScopeDto`.

## 6. Half Day listed as a leave type (W06)

`Half Day — 12 half-days — 12/25` sits beside Casual and Earned Leave. On the platform a half day
is a `0.5` portion of a policy that permits it, so counting it as a peer double-counts the same
day: a half-day of casual leave would appear in both bars.

## 7. Department-wise leave columns read as nonsense without units (W04)

`IT: Total Emp 45, Used 285, Available 705`. These are **day totals across the department**, not
per person, but nothing on screen says so. 705 available against 45 employees looks like a bug
until you work it out.

## 8. Maternity leave in a monthly type breakdown (W06)

`Maternity Leave 50/180` in a *monthly* view. 180 days is a statutory per-instance entitlement,
not a monthly or annual allowance, so it does not belong on the same axis as "48 of 74 casual
days this month". Per-instance leave needs separate treatment.

## 9. Miss punch expected times are uniform (W08)

Every check-in row expects 09:00 and every check-out row 06:00 AM — and 06:00 **AM** is almost
certainly meant to be 18:00. Another symptom of the missing shift module.

## 10. Joining dates were ignored by every population — **fixed 2026-09-04**

Not from the mock: found by reading our own dashboard. Nothing consulted `JoinedOn`, so

- somebody hired to start next month was counted in headcount and — because absence is derived
  as rostered minus present — was marked **absent from a job they had not begun**;
- somebody who joined on the 3rd was measured against all 22 working days of the month, which
  makes every new joiner the worst attender in the company for their first weeks;
- the scope picker offered a company headcount the tiles then contradicted.

The fix is one rule in one place: `EmployeeQueryFilter.JoinedOnOrBefore`, set to the as-of date
by both scope resolvers, plus expected days counted from the joining date rather than from the
first of the month. A month somebody was not employed for produces no row at all, and a period
with nothing expected is banded **Not measured** rather than Poor — nothing expected is not the
same as attending nothing.

The directory is deliberately unaffected: a future joiner is a real record HR needs to see. The
rule is about who is *measured*, not who exists.

Held by `scripts/verify-joining-dates.py`.

## 11. Absence was inferred on one screen and counted on another — **fixed 2026-09-05**

Also ours, not the mock's, and the sharpest of the three: the headline tile derived absence as
*rostered minus present*, while the month overview counted `AttendanceDays` rows with status
`Absent`. Nothing ever wrote such a row — days are recomputed on a punch or a correction, and
not turning up produces neither — so the tile shouted "everybody absent" while the table below
it reported nobody absent, forever.

Three changes:

- **`IAttendanceDayCloser`** writes the missing rows for a finished date, filling only gaps, and
  distinguishing `WeeklyOff`, `Leave` and `Absent` in that order. `AttendanceDayCloseWorker`
  runs it hourly per client with a fourteen-day lookback; `POST api/mobile/v1/attendance/close/{date}`
  does it on demand under `ATTENDANCE.MANAGE`.
- **The tile counts directly instead of subtracting**, and nobody is absent until their own
  shift start plus grace has passed. On a day still running it reads *Not In Yet* with a "So far
  today — N not due in yet" caption; on a settled day it reads *Absent* with none.
- **"Today" is the client's**, from `ITenantClock`, on the dashboard, the reports and the two
  portal pages that used to pin it to the server's clock.

Held by `scripts/verify-absence.py`.

## 12. Typographic errors to fix rather than reproduce

- `Prsent` → Present (W08)
- `Total Leaves Takes` → Total Leaves Taken (W05)
- `oct` → Oct (W05)
- `New year Celebration` → New Year Celebration (W03)
- `Utilisation` / `Utilization` — pick one spelling and hold it across the product

---

## What this list is really saying

Six of these ten trace back to one thing: **numbers presented as data that should be
calculations.** Delay, total balance, average days, attendance percentage and status band are all
derivable, and every one of them is wrong in at least one row of the mock precisely because it
was typed rather than computed.

The contracts in [02-dtos.md](02-dtos.md) make the derived ones properties rather than fields, so
the same class of error cannot reach production.
