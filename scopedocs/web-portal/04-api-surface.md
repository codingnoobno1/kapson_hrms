# API surface

Controllers needed for the portal. **The portal is the first client, not the only one** — every
endpoint here is shaped so the mobile HR app and the employee app call the same route later.
Nothing web-specific goes in the response.

Route prefix `api/mobile/v1` is kept despite the name, because it is the existing versioned
surface and forking a second prefix would double the maintenance. (Renaming it to
`api/v1` with a redirect is worth considering before the portal ships — see
[06-delivery-plan.md](06-delivery-plan.md).)

**Mobile column**: `HR` = the HR app calls it, `EMP` = the employee app calls it, `—` = portal
only.

---

## 1. `DashboardController` — `api/mobile/v1/dashboard`

The aggregate the portal opens with.

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/dashboard` | `DASHBOARD.VIEW` | HR | Whole page in one call (`HrDashboardDto`) |
| GET | `/dashboard/headline` | `DASHBOARD.VIEW` | HR | Six headline tiles alone, for polling |
| GET | `/dashboard/scopes` | `DASHBOARD.VIEW` | HR | Companies and units this caller may filter by |

The composite exists so the first paint is one round trip. The individual endpoints exist so a
refresh does not re-fetch six charts to update one number. Both are needed; neither is enough.

---

## 2. `PolicyController` — `api/mobile/v1/policies`

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/policies` | authenticated | HR, EMP | Cards with this caller's read state |
| GET | `/policies/{id}` | authenticated | HR, EMP | Body sections for the modal |
| POST | `/policies/{id}/acknowledge` | authenticated | HR, EMP | Records who, which version, when |
| GET | `/policies/{id}/compliance` | `POLICY.REPORT` | — | Acknowledgement rate and who has not read |
| GET | `/policies/admin` | `POLICY.MANAGE` | — | Including unpublished |
| POST | `/policies` | `POLICY.MANAGE` | — | Create / update draft |
| POST | `/policies/{id}/publish` | `POLICY.MANAGE` | — | Bumps version, resets everyone to unread |

Publishing resetting acknowledgement is the whole point of the feature. It must be explicit in
the endpoint, and the UI must warn before it happens.

---

## 3. `PeopleMomentsController` — `api/mobile/v1/moments`

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/moments/birthdays` | `PEOPLE.MOMENTS.VIEW` | HR, EMP | Today / belated / upcoming |
| GET | `/moments/anniversaries` | `PEOPLE.MOMENTS.VIEW` | HR, EMP | Today / belated / upcoming |
| PUT | `/moments/preferences` | authenticated | EMP | The caller's own opt-out |

The opt-out is not optional. Publishing a date of birth to colleagues is a disclosure the person
should be able to decline, and adding it later means retrofitting consent to data already shown.

---

## 4. `AttendanceReportController` — `api/mobile/v1/attendance/reports`

Extends the attendance module that already exists.

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/attendance/reports/late-arrivals` | `ATTENDANCE.TEAM` | HR | W03 table |
| GET | `/attendance/reports/overview` | `ATTENDANCE.TEAM` | HR | W08 per-employee month overview |
| GET | `/attendance/reports/miss-punch` | `ATTENDANCE.TEAM` | HR | W08 — reads existing `MissingPunch` exceptions |
| GET | `/attendance/reports/daily-summary` | `ATTENDANCE.TEAM` | HR | Present/absent/leave counts for a date |
| GET | `/attendance/reports/export` | `ATTENDANCE.EXPORT` | — | CSV/XLSX of any of the above |

Existing and unchanged: `/attendance/dashboard`, `/attendance/me`, `/attendance/team`,
`/attendance/register/{date}`, `/attendance/punch`, `/attendance/corrections/*`,
`/attendance/exceptions/*`.

---

## 5. `LeaveAnalyticsController` — `api/mobile/v1/leave/analytics`

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/leave/analytics` | `LEAVE.REPORT` | HR | G-01, G-02, G-03, G-04 in one call |
| GET | `/leave/analytics/balances/employees` | `LEAVE.REPORT` | HR | W04 employee grid |
| GET | `/leave/analytics/balances/departments` | `LEAVE.REPORT` | HR | W04 department grid |
| GET | `/leave/analytics/by-type` | `LEAVE.REPORT` | HR | G-05, month-scoped |
| GET | `/leave/analytics/by-date` | `LEAVE.REPORT` | HR | G-06, month-scoped |
| GET | `/leave/analytics/year-end` | `LEAVE.YEAREND.VIEW` | — | W05 department cards |
| GET | `/leave/analytics/export` | `LEAVE.EXPORT` | — | |

Existing and unchanged: the whole `/leave/*` request lifecycle, already built and verified.

---

## 6. `LeaveYearEndController` — `api/mobile/v1/leave/year-end`

New and dangerous. Carry-forward and lapse destroy balance, so the process is staged, previewed
and reversible until committed.

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/leave/year-end/preview` | `LEAVE.YEAREND.RUN` | — | What *would* carry, lapse and encash |
| POST | `/leave/year-end/run` | `LEAVE.YEAREND.RUN` | — | Commits, writing an audit row per employee |
| GET | `/leave/year-end/runs` | `LEAVE.YEAREND.VIEW` | — | History |
| GET | `/leave/year-end/runs/{id}` | `LEAVE.YEAREND.VIEW` | — | Per-employee detail of one run |

**No endpoint deletes balance without a preview first**, and a run is idempotent per year and
scope so a double-click cannot lapse twice.

---

## 7. `MovementController` — `api/mobile/v1/movement`

New module: employees leaving the premises during work hours.

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/movement/out` | `MOVEMENT.TEAM` | HR | W07 both tables, filtered by `kind` |
| POST | `/movement/out` | `SELF.MOVEMENT.REQUEST` | EMP | Declare going out |
| POST | `/movement/{id}/return` | `SELF.MOVEMENT.REQUEST` | EMP | Record return |
| GET | `/movement/overdue` | `MOVEMENT.TEAM` | HR | Expected return passed, nobody back |
| GET | `/movement/me` | authenticated | EMP | Own history |

`Kind` is `Personal` or `Official`, and it is not cosmetic: official time out is working time and
personal time out may not be. It must be captured at declaration, not decided afterwards.

---

## 8. `GatePassController` — `api/mobile/v1/gate-passes`

New module. Connects to the employee app's "Apply Gate Pass" and its identity QR.

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/gate-passes` | `GATEPASS.TEAM` | HR | W08 approval list |
| GET | `/gate-passes/pending` | `GATEPASS.APPROVE` | HR | Approval queue |
| POST | `/gate-passes` | `SELF.GATEPASS.REQUEST` | EMP | Apply |
| GET | `/gate-passes/{id}` | contextual | HR, EMP | Detail |
| POST | `/gate-passes/{id}/decision` | `GATEPASS.APPROVE` | HR | Approve / reject with reason |
| POST | `/gate-passes/{id}/withdraw` | owner | EMP | |
| GET | `/gate-passes/me` | authenticated | EMP | Own passes |
| POST | `/gate-passes/{id}/scan` | `GATEPASS.VERIFY` | — | Guard records use |

The scan endpoint is where the QR question gets settled. **An employee number in a QR identifies;
it does not authenticate.** If a pass is enforced at a physical gate, the scan must resolve a
short-lived pass token, not a static employee number that anyone could photograph.

---

## 9. `ShiftController` — `api/mobile/v1/shifts`

Prerequisite for anything involving *expected* time.

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/shifts` | `SHIFT.VIEW` | HR | Definitions |
| POST | `/shifts` | `SHIFT.MANAGE` | — | Create / update |
| GET | `/shifts/roster` | `SHIFT.VIEW` | HR | Assignments for a range |
| POST | `/shifts/assign` | `SHIFT.MANAGE` | — | Assign employees |
| GET | `/shifts/me` | authenticated | EMP | Own roster |

Fourteen KPIs are wrong without this. It is not an enhancement.

---

## 10. `CompanyEventController` — `api/mobile/v1/events`

| Method | Route | Permission | Mobile | Serves |
|---|---|---|---|---|
| GET | `/events/upcoming` | authenticated | HR, EMP | W03 table |
| GET | `/events` | `EVENT.MANAGE` | — | All, including past |
| POST | `/events` | `EVENT.MANAGE` | — | Create / update |

Extends `workforce.LeaveHolidays`, which needs a **type** and **description** column to serve
this — it currently has neither.

---

## Cross-cutting

**Every list endpoint** takes `DashboardScopeQuery` plus `MobileListQuery` (cursor, page size)
and returns `CursorPage<T>`. **Every export** is a separate permission from the read: seeing 20
rows on screen and downloading 482 rows of salary-adjacent data are different acts.

**Every endpoint returns `ApiEnvelope<T>`**, matching the existing surface.

## Summary

| Controller | New endpoints | Depends on a module that does not exist |
|---|---|---|
| Dashboard | 3 | partly (shift, gate pass) |
| Policy | 7 | **yes — policy module** |
| PeopleMoments | 3 | no |
| AttendanceReport | 5 | partly (shift) |
| LeaveAnalytics | 7 | no |
| LeaveYearEnd | 4 | **yes — year-end process** |
| Movement | 5 | **yes — movement module** |
| GatePass | 8 | **yes — gate pass module** |
| Shift | 5 | **yes — shift module** |
| CompanyEvent | 3 | extends holidays |
| **Total** | **50** | **5 new modules** |
