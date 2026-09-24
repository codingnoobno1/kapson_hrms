# Delivery plan

The portal ships before the two apps, and the apps then consume the same endpoints. That
ordering is the reason to get the contracts right here rather than later.

## What already exists

Verified working against real data, not scaffolding:

| Capability | State |
|---|---|
| Multi-tenant identity, JWT, refresh rotation | Built |
| Permission catalogue + role admin (`/admin/roles`) | Built, 69 permissions |
| Employee records, contacts, timeline | Built |
| Attendance: punches, days, corrections, exceptions | Built, 19 checks passing |
| Leave: policies, balances, requests, approvals, calendar | Built, 23 checks passing |
| Holiday calendar + admin screens | Built |
| Leave policy admin screens | Built |
| Recruitment: jobs, applications, interviews, onboarding | Built |
| Notifications | Built |
| Employee KPI tiles (`/me/*`) | Built |

Roughly **60% of the dashboard's read model already exists.** The gap is five modules and the
reporting layer over what is there.

## What is missing

| Module | Blocks | Size |
|---|---|---|
| **Shift** | 14 KPIs — every "expected time", lateness, half-day and attendance % | Medium |
| **Policy** | 6 policy cards, the modal, acknowledgement, compliance | Medium |
| **Gate pass** | Approval list, employee apply, guard scan | Medium |
| **Movement** | Both going-out tables, overdue returns | Small |
| **Leave year-end** | Year-end cards, carry-forward, lapse | Medium, **high risk** |
| Company events | Upcoming events table | Small (extends holidays) |
| Reporting layer | Every chart and grid | Large |

---

## Sequence

### Phase 1 — Foundations that everything else is wrong without

1. ~~**Shift module.**~~ **Done (2026-09-03).** `workforce.ShiftDefinitions` and
   `ShiftAssignments`, an `IShiftScheduleService` resolver, admin screen at `/admin/shifts`,
   four seeded shifts, and `SHIFT.VIEW`/`SHIFT.MANAGE`. Attendance, the headline tiles, the
   late-arrivals table and the attendance overview all now read each person's own shift —
   including its own grace — instead of a hard-coded 09:00. Rest days are no longer absences,
   and expected-to-work is the rostered count rather than a Monday-to-Friday assumption.
2. **Dashboard scope.** `DashboardScopeQuery`, `DashboardScopeDto`, scope resolution in the
   repositories, `/dashboard/scopes`. Every later endpoint depends on it.
3. **RBAC additions.** The 24 permissions, the `DataScope` model, and enforcement in services.
   Adding these after the endpoints exist means auditing every endpoint twice.

### Phase 2 — The dashboard that can be built from existing data

> **Items 4, 7 and part of 6 are built and verified live** (2026-09-02): scope resolution,
> the six headline tiles, people moments, leave analytics (category distribution, six-month
> trend, status split), the month attendance overview with bands, and upcoming events.
> Endpoints under `api/mobile/v1/dashboard`. Permissions `DASHBOARD.VIEW`, `DASHBOARD.ORG`,
> `DASHBOARD.EXPORT`, `PEOPLE.MOMENTS.VIEW`, `LEAVE.REPORT`, `LEAVE.EXPORT`,
> `ATTENDANCE.REPORT`, `ATTENDANCE.EXPORT` added to the catalogue (69 -> 77).
>
> Still open in this phase: the late-arrival detail table, miss-punch table, department-wise
> leave grids, by-type and by-date charts, and the employee-wise balance grid.

4. Headline strip (`/dashboard/headline`) — six tiles, most already computable.
5. Attendance reports — late arrivals, month overview, miss punch (reads existing exceptions).
6. Leave analytics — balances, distribution, trends, by-type, by-date. All six charts.
7. People moments — birthdays, anniversaries, and the opt-out.
8. Company events — extend `LeaveHolidays` with type and description.

At the end of Phase 2 the dashboard is real except for policies, gate pass and movement.

### Phase 3 — The new modules

9. Policy module + acknowledgement + compliance report.
10. Movement module.
11. Gate pass module, including the QR decision.

### Phase 4 — The dangerous one

12. Leave year-end: preview, run, audit, reversal window. Built last, deliberately, and behind a
    permission granted to nobody by default.

### Phase 5 — Portal completeness

> **The portal is no longer one page** (2026-09-04). `HrPageShell` frames five sections behind
> `HrNav`, each route client-scoped as `/hr/{tenant}/…`:
>
> | Page | Reads | Writes |
> |---|---|---|
> | Dashboard | the composite, charts, drill-downs, openings board, app access | grant/suspend mobile access |
> | People | `IMobileEmployeeService` list + overview, cursor paging | — |
> | Recruitment | openings board + `IApplicationService.GetSummariesAsync` | stage moves, from the same transition table the service enforces |
> | Attendance | late arrivals, miss punch, month overview, movement, gate passes | — |
> | Leave | the approval queue, balance grids | approve/reject |
>
> New behind the Leave page: `IWorkforceReportService.GetLeaveApprovalQueueAsync` and
> `GET api/mobile/v1/leave/approvals`. It is the client's whole queue rather than the reader's
> direct reports — the dashboard's Pending Approval tile now has a list behind it. It carries
> `LEAVE.APPROVE` rather than a report permission, and marks the reader's own rows so the
> self-approval refusal is visible before the click rather than after it.
>
> 14 (loading, empty and error states per widget) is done: `HrDataTable` carries all four, and
> panels fail independently of their page.

13. Sorting, column chooser, export, bulk approval. *(Cursor paging is done on People.)*
14. ~~Loading, empty and error states per widget.~~ **Done.**
15. Print/PDF.

### Fixed since

- **Joining dates are now honoured by every population** (2026-09-04). See
  [07-data-issues.md §10](07-data-issues.md). Headcount, presence, absence, the late and
  miss-punch reports, the month overview and the scope picker all measure who was employed on
  the date being shown. The directory still lists future joiners.
- **Absence is recorded rather than inferred** (2026-09-05). See
  [07-data-issues.md §11](07-data-issues.md). A day-close worker writes the attendance rows no
  device ever creates, the tile no longer calls people absent before their shift has started,
  and every date now comes from the client's clock rather than the server's. This also fills the
  first entry in `AddHrWorkers`, which had been an empty registration point since scaffolding.

### Gaps this uncovered

- **No API grants an opening leave balance.** Entitlement rows have to be written directly, so
  a newly hired employee cannot raise leave at all until somebody touches the database. The
  leave year-end work (item 12) is the natural home for it.
- **HR accounts are not employees.** `devhr2` and `hr.priya` carry no `EmployeeNumber`, so they
  cannot use any self-service endpoint. That is defensible — an HR login is not a person's
  employee record — but it means an HR user who is also staff needs two logins today.

### Phase 6 — The apps

16. HR mobile app against the same endpoints.
17. Employee app against the same endpoints.

---

## Decisions needed before Phase 1

These block work rather than merely informing it:

1. **Does a Team Manager see the whole organisation?** (§[05](05-rbac.md)) Determines whether the
   dashboard is an org report or a team report.
2. **What are the real shifts?** Fixed 09:00–18:00 for everyone, per-department, or rostered?
   Retail almost certainly means rostered, and that changes the shift module's size.
3. **Percent-of-what on the headline tiles?** Of headcount, or of expected-to-work? On a holiday
   the first reads 0% present.
4. **Half Day: type or portion?** The platform says portion; the prototype shows a type.
5. **Attendance status bands.** The prototype's own numbers contradict each other.
6. **Year-end rules.** What lapses, what carries, who approves, what the employee is told.

## Decisions needed before Phase 3

7. **QR payload.** Identifier or credential? Determines whether gate pass needs a token service.
8. **Policy acknowledgement scope.** Per company, per department, or everyone?
9. **Personal vs official movement.** Does personal time out deduct from working hours or leave?

---

## A naming question worth settling early

The API prefix is `api/mobile/v1`, but the web portal is now its primary consumer. Serving a web
dashboard from a route called `mobile` will confuse every future reader.

Options: keep it and accept the misnomer; or introduce `api/v1` as the canonical prefix with
`api/mobile/v1` kept as an alias until the apps migrate. The second costs little **now** and a
great deal once three clients depend on the old path.

Recommendation: settle it before Phase 2, not after.
