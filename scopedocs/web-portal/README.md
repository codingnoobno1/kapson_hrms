# HR web portal — scope

The **main web portal**, to be built before the two mobile apps. The apps are clients of the
same API: every endpoint specified here is designed so the mobile HR app and the employee app
can call it later without a second surface being written.

Derived from eight screenshots of the prototype (`references/`).

| Document | Contents |
|---|---|
| [01-kpis.md](01-kpis.md) | Every number on the dashboard, with its definition, source and formula |
| [02-dtos.md](02-dtos.md) | The contracts that carry those numbers |
| [03-charts-and-widgets.md](03-charts-and-widgets.md) | Graphs and the widget inventory |
| [04-api-surface.md](04-api-surface.md) | Controllers and endpoints, with mobile reuse marked |
| [05-rbac.md](05-rbac.md) | Permissions and data scoping, administered from `/admin/roles` |
| [06-delivery-plan.md](06-delivery-plan.md) | Build order, what already exists, what is missing |
| [07-data-issues.md](07-data-issues.md) | Contradictions in the prototype that need a decision |
| [08-app-menu-design-layout.md](08-app-menu-design-layout.md) | The hamburger-triggered app menu and the Salary/Payroll screens behind it — client-locked design, layout only |
| [09-widgets-reusable-skeleton.md](09-widgets-reusable-skeleton.md) | Widgets for the app menu and salary module: what's already built, what extends an existing component, what's net new |
| [10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md) | Routes, DTOs, API surface, RBAC, and a master-by-master check of what the "APP Configuration" catalogue already has in Admin versus what's net new |
| [11-design-tokens-and-color-system.md](11-design-tokens-and-color-system.md) | One canonical colour-token table (reconciling near-duplicate greens/ambers found across screens), a widget-name-to-actual-file reconciliation against the real codebase, and 16 structural findings a colour/widget extraction alone doesn't surface |

## Reference screens

| File | Screen |
|---|---|
| `W01-dashboard-overview.png` | KPI strip, policy cards, birthdays, anniversaries |
| `W02-policy-modal.png` | Policy detail modal with read acknowledgement |
| `W03-late-coming-events.png` | Late Coming List (Today), Upcoming Events & Holidays |
| `W04-leave-balance-analytics.png` | Leave balance employee/department, donut, grouped bars |
| `W05-leave-trend-yearend.png` | Monthly trend, status pie, year-end process cards |
| `W06-leave-by-type-monthly.png` | Leaves by type with month selector, usage by date |
| `W07-going-out-misspunch-gatepass.png` | **MISSING — see below** |
| `W08-misspunch-gatepass-attendance.png` | Miss punch, gate pass approvals, attendance overview |

> **W07 did not save.** `hrweb/6.png` and `hrweb/7.png` are byte-identical (both are the
> leave-by-type screen), so the *Employees Going Out — Personal Work / Official Work* screen has
> no file. It was visible in chat and is transcribed in [01-kpis.md](01-kpis.md) §G, so nothing
> is lost from the specification — but please re-save it for the visual record.

## App menu and salary module references (second capture)

A later, separate capture of the same prototype, sourced from `hrweb/New folder/`, scoped in
[08-app-menu-design-layout.md](08-app-menu-design-layout.md) onward.

| File | Screen |
|---|---|
| `S01-app-menu-employee-tab.png` | App menu overlay, Employee tab |
| `S02-app-menu-salary-tab.png` | App menu overlay, Salary Management tab |
| `S03-app-menu-employee-report-tab.png` | App menu overlay, Employee Report tab |
| `S04-app-menu-configuration-tab.png` | App menu overlay, APP Configuration tab (26-card master catalogue) |
| `S05-designation-master.png` | Designation master — full list page |
| `S06-generate-salary-division-wise.png` | Generate Employee Salary — Division-Wise |
| `S07-generate-salary-department-wise.png` | Generate Employee Salary — Department-Wise (grouped/subtotal) |
| `S08-generate-salary-individual.png` | Generate Employee Salary — Individual |
| `S09-salary-preview-modal.png` | Salary Preview — printable payroll summary document |
| `S10-mark-as-paid-modal.png` | "Mark as Paid" confirmation (mislabelled "Transfer Salary to Bank" in source — see doc 08 §9) |
| `S11-transfer-to-bank-modal.png` | Transfer to Bank — the real bank-transfer execution flow |

## One thing deliberately ignored

`W01` carries a browser banner reading *"Your desktop is currently shared with …"*. That is the
screen-recording tool, not part of the product, and nothing in this scope derives from it.
