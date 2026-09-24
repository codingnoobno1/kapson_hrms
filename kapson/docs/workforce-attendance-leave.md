# Workforce: attendance and leave

How the two workforce modules behave, and why. Both are live against real data — see
`Vispl.HRManagement.Tests/ImplementedEndpoints.cs` for the routes that genuinely answer.

## Who the caller is

Self-service needs to know which employee row belongs to the signed-in account.

The token carries `employee_number` — a display string such as `EMP-00011` — so it cannot be
used as a key. Both services resolve the caller through `IEmployeeRepository.GetByAccountAsync`,
matching `employee.Employees.AccountId` to the identity user id. An account with no employee row
gets `NO_EMPLOYEE_RECORD` rather than silently recording work against nobody.

Ownership checks compare the resolved numeric id. `MobileCallerContext.IsSelf` compares against
the display number and is not used for these paths.

## Attendance

### Punches are append-only

`workforce.AttendancePunches` is never edited. A mistake is fixed by an approved correction that
supersedes the original rows, so what the device actually reported survives any dispute.

- `ClientEventId` is unique per employee (filtered unique index). A retry over a flaky
  connection returns the original punch instead of recording a second one. The rule is enforced
  by the database because a check in code loses a race between two concurrent retries.
- Two ins or two outs in a row are refused (`ALREADY_IN`, `NOT_CLOCKED_IN`) — worked time would
  otherwise be meaningless.
- A client clock more than five minutes from the server's is ignored in favour of server time,
  with a warning on the response. Attendance drives pay, so the device does not get to assert
  when something happened.

### Nobody is measured before the day they start

Every dashboard and report population is "who was employed on the date being shown", not "who
has a row". `EmployeeQueryFilter.JoinedOnOrBefore` carries that rule and both scope resolvers
set it to the as-of date.

It matters more than it sounds because **absence is derived, not recorded**: a day is absent
when somebody was rostered and did not turn up. Counting a future joiner as rostered therefore
marked them absent from a job they had not begun, every single day until they started.

The month overview counts expected days from the joining date rather than the first of the
month, skips anybody who had not joined by the end of the month entirely, and bands a period
with nothing expected as *Not measured* rather than Poor.

The employee directory deliberately ignores the rule: somebody hired to start next month is a
real record HR needs to see. The rule is about who is measured, not who exists.

### Absence is recorded, not inferred

Every other attendance fact is written by an event: a punch, or an approved correction. Not
turning up is the one outcome that produces neither, so for a long time an absence existed only
as a subtraction on a dashboard — *rostered minus present* — and never as a row. The dashboard
tile and the month overview therefore disagreed about the same fact: one inferred it, the other
counted rows that were never written.

`IAttendanceDayCloser` closes a finished date by writing the rows those days should always have
had. It only fills gaps, so a punched day, a correction or an earlier close is never touched,
and running it twice changes nothing. `AttendanceDayCloseWorker` runs it hourly across every
live client with a fourteen-day lookback, so a server that was down over a weekend catches up
rather than leaving a hole. `POST api/mobile/v1/attendance/close/{date}` does the same on demand
(`ATTENDANCE.MANAGE`), for when a shift or a leave request is corrected after the fact.

Three things are deliberately **not** absences, and the order they are checked in matters:

1. a public holiday, or a day the person's shift does not run — `WeeklyOff`;
2. a day covered by approved leave — `Leave`;
3. anything else that was expected and empty — `Absent`, carrying a `NoPunches` exception.

Charging somebody an absence for a Sunday or for leave they had booked is how a workforce stops
trusting the register entirely.

### Nobody is absent until they are due

A day still being worked cannot be closed at all: the closer refuses a date that is not yet over
in the client's own time zone.

The live tile follows the same rule at a finer grain. Somebody is only counted once **their own**
shift start plus its grace has passed — before that they are not due in yet, not absent. Without
this the dashboard opened every morning announcing that the entire company was absent, and a
night-shift worker due at 22:00 was absent from one minute past midnight. While the day can still
change the answer the tile reads *Not In Yet* and carries a "So far today" caption; once the day
is settled it reads *Absent* with no hedge.

"Today" throughout is the **client's** today, from `ITenantClock`. At half past midnight in
Chandigarh a UTC clock still says yesterday, which would open the dashboard on the wrong date and
then judge a finished day as though it were still running.

Held by `scripts/verify-absence.py`.

### Days are derived, never asserted

`workforce.AttendanceDays` holds one computed row per employee-day: worked minutes, expected
minutes, status and exceptions. It is recomputed from the surviving punches after every change,
so the stored day cannot disagree with the record it summarises. It exists only so the register
can be queried without recomputing every row.

Defaults until a shift module supplies a roster: an eight-hour day from 09:00, a fifteen-minute
late grace, and a fifteen-minute short-hours tolerance.

### Corrections

A correction proposes a replacement set of punches for one day.

- Thirty-day window; one pending correction per employee-day (`ALREADY_PENDING`).
- Punches must alternate in/out and balance (`PUNCHES_UNBALANCED`).
- The person who raised it cannot decide it (`SELF_APPROVAL`).
- Approval supersedes the original punches, rewrites them as `Correction`-sourced, and
  recomputes the day.
- A decided correction cannot be decided again.

### Exceptions

Raised by the day computation (late in, short hours, missing punch). Confirmed by the employee,
resolved by an approver. Recomputing a day refreshes measurement but never erases a human
decision: `RemoveExceptionsAsync` only clears rows still in `Open`. Approved overtime cannot
exceed what was measured.

## Leave

### Days are computed on the server

The number of chargeable days is what the balance is spent in, so it is never accepted from the
client. `POST /leave/requests/calculate` returns the server's answer plus a signed calculation
token; `POST /leave/requests` recomputes from scratch and verifies the token against its own
numbers.

The token is an HMAC over employee, policy code, range, half-day flags, day count and issue
time, valid for thirty minutes. Changing the range, the flags or the day count after calculating
produces a signature that cannot match — `CALCULATION_STALE`. The signing key is generated per
process: a rejected token simply asks the client to recalculate, so there is no secret to
manage. **A multi-instance deployment needs a shared key here.**

### Balances are half-stored, half-derived

`workforce.LeaveBalances` stores only the granted side — entitlement, accrual, carry-forward,
manual adjustment. Used and pending days are derived from the request rows on every read, so a
stored counter can never drift away from the requests it is meant to summarise.

Days are charged against the year they fall in, not the year the request was raised, so a range
crossing new year counts where it is actually taken.

### Charged dates are stored explicitly

`workforce.LeaveRequestDays` holds one row per charged date with its portion (1 or 0.5). This is
what makes overlap detection and the team calendar exact — both read dates rather than
re-deriving them from a range and rules that may since have changed.

Overlap against any live request (Pending or Approved) is refused: `OVERLAPPING_LEAVE`.

### Rules that are enforced

| Rule | Error |
|---|---|
| Range must run forwards | `RANGE_INVALID` |
| Policy forbids half days | `HALF_DAY_NOT_ALLOWED` |
| Long request without evidence | `DOCUMENT_REQUIRED` |
| Balance would go negative (unless the policy allows it) | `INSUFFICIENT_BALANCE` |
| Every day in the range is a holiday or weekend | `NO_CHARGEABLE_DAYS` |
| Decider is the applicant | `SELF_APPROVAL` |
| Rejection with no comment | `REASON_REQUIRED` |
| Already decided or withdrawn | `NOT_PENDING` |
| Withdrawn by someone else | `NOT_OWNER` |

Short notice is a **warning**, not a block — the manager decides, not the system.

Balance is re-checked at approval as well as at submission, because other requests may have been
approved in between and approval is what actually spends it.

### Administering the rules

`/admin/leave-policies` edits every rule on this page; `/admin/holidays` edits the calendar,
per client and per year, with an optional location so a regional holiday applies only there.

Two guards the panel adds on top of storage:

- Carry-forward cannot exceed the annual entitlement, and a monthly accrual needs an
  entitlement to spread — either would produce a policy that can never credit anyone.
- A policy cannot be deactivated while requests on it are awaiting a decision, which would
  otherwise strand them: nobody can approve what nobody can price.

A policy code is permanent once saved, because employees apply against it. Editing rules affects
only requests raised afterwards — submitted requests keep the day count computed for them, and
removing a holiday does not re-price leave already booked.

### Seeded configuration

`Vispl.HRManagement.Host/SeedData/kapson-leave-policies.json` — AL, SL, CL, ML, PL, LWP.
`Vispl.HRManagement.Host/SeedData/kapson-holidays.json` — only the fixed-date national holidays
(Republic Day, Independence Day, Gandhi Jayanti, Christmas). Festival dates move every year and
vary by location, so they are left for the tenant to configure rather than guessed.

Both seed additively by natural key: a tenant that has edited a policy keeps its edit.

## Not built yet

- Accrual does not run on a schedule. `LeaveBalances` rows are granted by whatever writes them;
  nothing yet posts monthly accrual for `AccrualMode: Monthly`, and there is no admin screen for
  granting or adjusting an individual balance.
- Leave adjustments (encash, carry-forward) are raised and consume their credits, but there is
  no approval endpoint for them.
- Attendance still assumes a fixed 09:00 eight-hour day; the shift module will replace that.
- Approved leave does not yet mark the corresponding attendance days as `Leave`.
