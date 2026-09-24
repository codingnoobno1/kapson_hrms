# RBAC and data scoping

Administered from `/admin/roles`, which already exists: role editor, permission matrix, and a
catalogue currently holding 69 permissions. Everything below is added to that catalogue and
granted through the same screen — **no new admin surface is needed for access control.**

## The two questions

Authorisation here answers two separate things, and conflating them is the classic HR-portal
breach:

1. **May this person perform this action?** — a permission.
2. **Over whose records?** — a scope.

`LEAVE.APPROVE` says a person may approve leave. It does not say *whose*. A store manager with
`LEAVE.APPROVE` and no scope limit could approve leave for the entire company.

## The scoping problem in the prototype

The signed-in user is **"Tarun Singh, Team Manager (IT)"** and the dashboard shows **482
employees across 3 companies**, a full late-coming list, and every department's leave balance.

Either the prototype is showing an administrator's view under a manager's name, or a team manager
genuinely sees the whole organisation. **This must be decided before anything is built**, because
it determines whether the dashboard is an org-wide report or a team report.

Recommendation: the same dashboard, scoped. A team manager sees their reports; an HR business
partner sees their companies; an administrator sees everything. One screen, three scopes — the
numbers change, the layout does not.

## Scope model

```
DataScope = Self | DirectReports | ReportingTree | Department | Unit | Company | Tenant
```

Attached to the **role**, resolved per request, and applied in the repository — never in the
controller and never in the UI. A scope enforced only by hiding a menu item is not enforced.

The employee record already carries every field this needs: `CompanyCode`, `DivisionCode`,
`DepartmentCode`, `LocationCode`, `ManagerEmployeeId`.

`/dashboard/scopes` returns only the companies and units the caller may filter by, so the filter
bar cannot offer a company whose data would then be refused.

---

## Permissions to add

Grouped as the permission matrix groups them.

### Dashboard

| Key | Grants |
|---|---|
| `DASHBOARD.VIEW` | Open the dashboard within the caller's scope |
| `DASHBOARD.ORG` | See organisation-wide figures rather than own scope |
| `DASHBOARD.EXPORT` | Export any dashboard panel |

### Policies

| Key | Grants |
|---|---|
| `POLICY.VIEW` | Read published policies (grant to everyone) |
| `POLICY.MANAGE` | Draft, edit and publish |
| `POLICY.REPORT` | See who has and has not acknowledged |

### People moments

| Key | Grants |
|---|---|
| `PEOPLE.MOMENTS.VIEW` | See birthdays and anniversaries |

Subject to the employee's own opt-out, which **overrides the permission**. A permission grants
access to data the organisation publishes; it does not override a personal choice the
organisation offered.

### Attendance reporting

| Key | Grants |
|---|---|
| `ATTENDANCE.REPORT` | Late arrivals, overview, miss punch |
| `ATTENDANCE.EXPORT` | Download attendance data |

Existing and unchanged: `ATTENDANCE.VIEW`, `ATTENDANCE.TEAM`, `ATTENDANCE.APPROVE`,
`ATTENDANCE.MANAGE`.

### Leave reporting and year end

| Key | Grants |
|---|---|
| `LEAVE.REPORT` | Balances, distribution, trends |
| `LEAVE.EXPORT` | Download leave data |
| `LEAVE.YEAREND.VIEW` | See the year-end position and past runs |
| `LEAVE.YEAREND.RUN` | Preview and commit a year-end run |

Existing: `LEAVE.VIEW`, `LEAVE.TEAM`, `LEAVE.APPROVE`, `LEAVE.CONFIGURE`.

`LEAVE.YEAREND.RUN` is the most destructive permission on the platform — it lapses balance
across the organisation. It should sit in **no default role**, be granted deliberately, and its
runs are audited per employee.

### Movement and gate pass

| Key | Grants |
|---|---|
| `MOVEMENT.TEAM` | See who is out |
| `MOVEMENT.MANAGE` | Correct a movement record |
| `SELF.MOVEMENT.REQUEST` | Declare going out (employee) |
| `GATEPASS.TEAM` | See passes |
| `GATEPASS.APPROVE` | Decide a pass |
| `GATEPASS.VERIFY` | Scan at a gate (security role) |
| `SELF.GATEPASS.REQUEST` | Apply (employee) |

### Shifts

| Key | Grants |
|---|---|
| `SHIFT.VIEW` | See definitions and rosters |
| `SHIFT.MANAGE` | Create shifts and assign people |

### Events

| Key | Grants |
|---|---|
| `EVENT.MANAGE` | Maintain the company calendar |

**24 new permissions**, taking the catalogue from 69 to 93.

---

## Suggested default roles

Composed in `/admin/roles`; none is hard-coded.

| Role | Scope | Notable grants |
|---|---|---|
| Employee | Self | `POLICY.VIEW`, `SELF.*` |
| Team Manager | DirectReports | + `LEAVE.APPROVE`, `ATTENDANCE.TEAM`, `GATEPASS.APPROVE`, `DASHBOARD.VIEW` |
| Department Head | Department | + `LEAVE.REPORT`, `ATTENDANCE.REPORT`, `MOVEMENT.TEAM` |
| HR Executive | Company | + `EMPLOYEE.*`, `POLICY.REPORT`, `LEAVE.EXPORT` |
| HR Manager | Company | + `POLICY.MANAGE`, `SHIFT.MANAGE`, `LEAVE.CONFIGURE`, `EVENT.MANAGE`, `DASHBOARD.ORG` |
| HR Director | Tenant | + `LEAVE.YEAREND.RUN` |
| Security | Tenant, gate only | `GATEPASS.VERIFY` only |
| Administrator | Tenant | Platform configuration, **not** employee personal data by default |

The last two matter. A gate guard needs to verify a pass and nothing else — giving them an HR
role because it is convenient is how a retail chain leaks its payroll. And a platform
administrator configuring tenants does not automatically need to read every employee's salary;
those should be separable grants.

---

## Rules

**Permission checks live in the business service**, taking the resolved caller — the existing
`MobileCallerContext` pattern. Controllers annotate for documentation and cheap rejection;
services enforce.

**A caller who lacks a section gets `null` for it in the composite response**, never a 403 for
the whole page. One forbidden widget must not blank the dashboard.

**Scope is applied in the repository query**, not by filtering an already-fetched list. Fetching
482 rows and showing 12 still loaded 482 rows into memory and into any log that records them.

**Self-approval is refused everywhere**, as it already is in leave and attendance. That rule
extends to gate passes and movement records.

**Export is always a separate permission from read.** Seeing a screen and taking a file home are
different acts with different consequences.
