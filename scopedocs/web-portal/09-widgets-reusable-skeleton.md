# App Menu and Salary Module — widgets and reusable skeleton

Companion to [08-app-menu-design-layout.md](08-app-menu-design-layout.md). That document is the
approved shape; this document is the build inventory — what already exists in the codebase and
can be reused as-is, what is an existing pattern needing a variant, and what is genuinely new.

Notation matches [03-charts-and-widgets.md](03-charts-and-widgets.md): `◆` evidenced in a
reference screen, `○` required but not shown. A fourth marker is added here because this module
sits on top of a partly-built platform: **`●` already implemented in code today**, cited by file
path. Anything marked `●` is not to be rebuilt.

## 1. How to read the "already there" column

Three codebases were checked for existing, reusable material before anything below was marked new:

- `Vispl.HRManagement.UI/Components/Hr/Shared/` — the HR portal's own chrome and chart components
  (`HrPageShell.razor`, `HrNav.razor`, `HrDataTable.razor`, `HrBarChart.razor`,
  `HrDonutChart.razor`, `HrTrendChart.razor`, `HrChartPalette.cs`, `HrPortalCaller.cs`).
- `Vispl.HRManagement.UI/Components/Admin/Shared/` — the admin portal's chrome
  (`AdminNav.razor`, `AccessRolePicker.razor`, `PermissionMatrix.razor`).
- `Vispl.HRManagement.EmployeeApp/Shared/` and the mobile widget catalogues (`widgets.md`,
  `employee-app-catalogue.md`) — for compound cells (identity, currency, status pill) that this
  module's tables visibly reuse the shape of, even across a web/mobile boundary.

A widget already built for the dashboard/leave/attendance slice ([03-charts-and-widgets.md](03-charts-and-widgets.md))
but not yet confirmed present in `Hr/Shared` as an extracted component is marked `◆` (evidenced,
same as before) rather than `●` — evidenced-in-a-screenshot and proven-extracted-as-a-component
are different claims, and this document only makes the second claim where the file was actually
found.

## 2. App-menu chrome widgets

| ID | Widget | Ref | Status | Notes |
|---|---|---|---|---|
| AM-01 | App-menu trigger icon | ◆ S02–S04 | ○ new | Hamburger glyph, header far-right, after the profile block. Visually near-identical to the existing left rail toggle — **do not reuse the same component instance**, they control different surfaces and must not share state. |
| AM-02 | Mega-menu overlay shell | ◆ S01–S04 | ○ new | Full-width modal, dims/blurs the page behind it, no confirmed click-outside-to-close behaviour (flag `○`) |
| AM-03 | Tab strip (4 tabs, icon + label) | ◆ S01–S04 | ○ new, but same visual family as `AdminNav.razor`'s top-level grouping | Filled-blue active / pale-grey inactive, thin rule beneath |
| AM-04 | Overlay close button | ◆ all | reuse | Same `✕` control as every other modal in the platform — do not create a second close-icon component |
| AM-05 | Section header (bold caps label above a card run) | ◆ S01–S04 | ○ new | Distinct from `W-33`'s emoji section heading — this one is plain bold text, no emoji, no card wrapper of its own |
| AM-06 | Launcher card (icon + title + subtitle) | ◆ S01–S04 | ○ new | See §3 below — this is the single most-reused new widget in the module, appearing 26+3+2+4 = 35 times across the four tabs |
| AM-07 | 4-column responsive card grid | ◆ S04 only | ○ new | Only the Configuration tab needs multi-column packing; the other three tabs are single-column lists of 2–4 cards and can lay out with a simple stack |
| AM-08 | Breadcrumb | ● `HrPageShell.razor` (pattern already used on every HR page, e.g. S05's `Dashboard › System Settings › Designation`) | reuse | |

### AM-06 — Launcher card, full spec

```text
┌────────────────────────────────────┐
│ [icon-tile]  Title (14–15px, 600)   │
│              Subtitle (12–13px,     │
│              400, grey, 1–2 lines)  │
└────────────────────────────────────┘
```

- Bindable: `Icon`, `Title`, `Subtitle`, `Route` (or `Command`), `IsEnabled` (a card whose target
  module is not built yet should render `IsEnabled=false` with a "coming soon" affordance rather
  than navigating to a broken page — no reference shows this state, mark `○`).
- Hover/press state not shown in any reference; treat as a standard card hover (subtle elevation
  or background tint) consistent with the rest of the portal.
- This is functionally identical to `EMP-W-06` (Navigation list row) and `EMP-W-07` (Tinted icon
  tile) already catalogued for the employee app's Menu screen — **same underlying shape, ported to
  a card layout instead of a full-width row.** If a shared design-token library exists between the
  web portal and the MAUI apps, this is the component to unify first; if not, build it once here
  and treat it as the reference implementation for a future web/mobile shared kit.

## 3. Salary/Payroll domain widgets

This whole module is new — no payroll backend exists yet (see
[10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md) §5) — so
every widget in this section is `○` unless a general-purpose ancestor is cited.

| ID | Widget | Ref | Status | Notes |
|---|---|---|---|---|
| SM-01 | Salary Generation Controls bar | ◆ S06–S08 | ○ new | Light-blue card, label+helper left, 5 dropdowns + 2 buttons right. Structurally close to `W-08` Scope filter bar — same "one bar sets the scope for the whole page below it" contract — but with five filters instead of two-and-a-date, and it owns the primary generate/preview actions, which `W-08` does not |
| SM-02 | Payroll stat-tile row (4 tiles, fixed colour order) | ◆ S06–S08 | reuse pattern of `W-11`/`W-12` | Same KPI-card shape already scoped for the dashboard headline strip; only the colour tokens and captions differ. **Do not create a second KPI-tile component** — parameterise the existing one with a payroll palette (rose/amber/green/lavender) instead of the dashboard's own |
| SM-03 | Segmented tab row with count badges | ◆ S06 | ○ new, close to `W-18` Tabbed list card | `W-18` is Today/Belated/Upcoming inside one card; this is three independent data views behind one filter bar. Build as a new `SegmentedDataTabBar`, reuse `W-18`'s pill visual only |
| SM-04 | Dense table toolbar (refresh, search, export, print icons) | ◆ S06–S08 | ○ new, partially covered by `W-55` Export menu | `W-55` only names export; this also needs standalone refresh and print actions as first-class icon buttons, not tucked into an overflow menu |
| SM-05 | Flat financial data table (Division-Wise) | ◆ S06 | ○ new variant of `W-21` | Same sticky-header dense table, but every numeric column right-aligned with a currency formatter, plus a **bold Grand Total row pinned to the table's own footer** — `W-21` as scoped has no documented totals-row behaviour |
| SM-06 | Grouped financial data table with subtotal rows (Department-Wise) | ◆ S07 | ○ new | Group header row (division), N department rows, one subtotal row, repeat. This is materially different from every table scoped in 03 — none of them group rows or carry a subtotal. Needs its own component, `GroupedFinancialTable`, built from `SM-05` plus a group/subtotal renderer |
| SM-07 | Compound identity cell (avatar + name + employee ID, 2-line) | ◆ S08 | ○ new for web, but same shape as `RW-32`/`EMP-W-14` avatar patterns and `W-19` person-moment row already built for mobile | Colour-coded initials avatar, not a photo, in the sample data — confirm whether a real photo is ever available and what the fallback rule is |
| SM-08 | Work-days ratio cell with inline underline (`26/26`) | ◆ S08 | ○ new | Distinct from `W-25` (percentage progress bar in a cell) — this is a day-count fraction with a thin underline proportional to the ratio, not a filled bar. Do not reuse `W-25`'s component for this; build a small sibling |
| SM-09 | Currency formatter (₹, Indian digit grouping) | ◆ S06–S11 throughout | ● `RW-36`/`EMP-W-24` already specified for the mobile apps (`₹8,00,000` lakh grouping) | **Reuse the formatting rule, not necessarily the component** — this is a web Blazor context, so port the *logic* (culture-aware Indian grouping) into a shared formatter used by both the portal and the MAUI clients, rather than duplicating the grouping rule a third time |
| SM-10 | Status pill (Generated / Pending / Paid / Active / Inactive) | ◆ throughout | ○ new palette, reuse shape | The shape is the same pill already scoped as `RW-24`/`W-26`/`StatusPill` in every other part of the platform. **New tokens needed**: `Generated` (green dot), `Pending` (amber dot) — these are payroll-specific states layered onto the existing status-pill component, not a new component |
| SM-11 | Row action button cluster (view/edit/delete, or Generate) | ◆ S05–S08 | ● `HrDataTable.razor` likely already renders an actions column (confirm) | If `HrDataTable.razor` does not yet support a single-button "Generate" action variant (as opposed to the edit/delete pair seen on the Designation table), extend it rather than forking a second table component |
| SM-12 | Table footer bar (left: count label, right: action button cluster) | ◆ S06, S08 | ○ new | `"6 Departments"` / `"15 Employees"` left, up to three buttons right (`Generate All`, `Generate Pending`, `Transfer to Bank`, `Mark as Paid`) — the button set is **contextual to the tab**, so this must be a slot-based footer, not a fixed 1–2-button bar |
| SM-13 | Printable payroll summary document (Salary Preview) | ◆ S09 | ○ new | Letterhead block (tenant name, department, period) + 6-tile stat grid + reduced-column table + Cancel/Approve footer, rendered inside a full-bleed modal with its own title bar. This is closer to a **document template** than a UI widget — build it as a printable partial that can also be the source for "Export Excel" and "Send" (email), not three separate renderers |
| SM-14 | Approval gate footer (Cancel / Approve & Proceed) | ◆ S09 | ○ new, but same contract as `W-46` Approve/Reject pair | `W-46` was scoped for leave/gate-pass decisions with a required rejection reason; this is a single-direction approval (no reject path shown — cancelling just closes the preview without committing). Confirm whether "Cancel" should also support "return for correction" the way `W-46`'s reject does, or whether payroll genuinely has no reject-with-reason step |
| SM-15 | Selectable employee checklist with running total | ◆ S10, S11 | ○ new | Checkbox list, a "Select All" header row with a live "N selected" counter, and a footer/total-tile that recomputes as selections change. Shared between both bank-related modals (§9 of doc 08) — build once, use twice, with a prop for which per-row fields to show (S10 shows name+role+amount; S11 adds masked bank account + status pill) |
| SM-16 | Payment/transfer stat-tile row (2-tile and 4-tile variants) | ◆ S10, S11 | reuse `SM-02`'s component, different tile count | Confirms `SM-02` must support 2, 4, and 6 tiles (Preview modal has 6) as one flexible row, not three hard-coded layouts |
| SM-17 | Bank account picker (tenant's own accounts) | ◆ S11 | ○ new | Not the employee's bank account (masked, read-only, per row) — this is the **company's own disbursing account**, e.g. `SBI - KAPSONS0034`. No master for this exists anywhere in the APP Configuration catalogue; a `Company Bank Account` master must be added before this picker can be built for real |
| SM-18 | Masked bank-account chip | ◆ S11 | ○ new, same masking convention as `SensitiveValueReveal`/`WID-108` already specified for mobile | `HDFC •••• 4821` — last-4-digits pattern. Port the masking rule from the mobile widget catalogue rather than inventing a second one |
| SM-19 | Search + scope-filter row above a checklist | ◆ S11 | ○ new, close to `W-58` Search box + `W-57` Multi-select filter | Combine the two already-scoped primitives rather than building a bespoke combo |
| SM-20 | Running total footer bar (dark bar, live-updating amount) | ◆ S11 | ○ new | Visually distinct from `SM-12`'s lighter footer — this one is a dark full-width bar reserved for a single running money total, used only in the Transfer to Bank modal |
| SM-21 | Dual commit-action footer (Download Advice / Execute Transfer) | ◆ S11 | ○ new | Two non-equivalent primary-weight actions side by side is unusual — most of the platform's footers have one primary and one secondary (`Cancel` + one action). Do not default to "last button = primary" styling; both `Download Advice` and `Execute Transfer` need deliberate, distinguishable weight |
| SM-22 | Payment-date field with required-marker | ◆ S10 | ● standard date picker already used across the portal (leave dates, holiday dates) | Just needs a `Required` prop wired to the existing date-input component |

## 4. Master-data catalogue widgets (Designation page and its 25 siblings)

| ID | Widget | Ref | Status | Notes |
|---|---|---|---|---|
| MD-01 | Master list page shell (breadcrumb, H1, toolbar, table) | ◆ S05 | ● `HrPageShell.razor` + existing Admin page pattern (e.g. `HolidaysAdmin.razor`, `LeavePoliciesAdmin.razor`, `MastersAdmin.razor` all already follow this shape) | Confirmed reusable — build the 26 APP Configuration destinations on this shell, not a new one |
| MD-02 | Table toolbar (refresh, search, overflow menu, primary Add button, secondary named action) | ◆ S05 | ● pattern already present on existing Admin CRUD pages | The **"Set Order"** secondary button is the one net-new toolbar action — a manual reordering affordance not seen on the existing Admin pages checked. If none of them support drag-reorder yet, this is new capability on top of an old shell, not a new shell |
| MD-03 | Foreign-key picker (Department, Grade) | ○ S05 implies it, not directly captured | ○ new only where the target master (Grade) is itself new | Designation's own Add/Edit form is not shown, but the table's Department and Grade columns prove the form needs both as pickers |
| MD-04 | Status pill (Active/Inactive) | ◆ S05 | reuse `SM-10`/`RW-24` | Only `ACTIVE` is shown; the pill component must already support an inactive/disabled tone from other parts of the platform |
| MD-05 | Icon-button row (edit pencil, delete bin) | ◆ S05 | ● existing Admin table action-column pattern | |
| MD-06 | Manual sort/reorder control ("Set Order") | ◆ S05 | ○ new | Needed wherever a master's display order feeds a dropdown elsewhere in the app (Designation certainly; confirm which of the other 25 also need it — Grade almost certainly does, since grade ordering (A > B > C) is meaningful, not just alphabetical) |

## 5. Reusable base already in the platform — do not rebuild

Pulled together from the checks in §1, for a single at-a-glance list before implementation starts:

| Existing component | File | Reused by this module for |
|---|---|---|
| `HrPageShell.razor` | `Vispl.HRManagement.UI/Components/Hr/Shared/` | Every full-page destination in this scope (Designation, Generate Employee Salary, and the four unspecified report pages) |
| `HrNav.razor` | same | Left rail — untouched by the app menu, confirms the two navigation systems are independent (§3.1 of doc 08) |
| `HrDataTable.razor` | same | Base for `SM-05`, `SM-06` (extended), `MD-01`'s table |
| `HrPortalCaller.cs` | same | The convergence point (per `portal-integration.md` CR-1) any new `IPayrollService`/`IDesignationService` must be called through from Blazor pages, exactly as `HrLeave.razor`/`HrPeople.razor` already do for their domains |
| `HrChartPalette.cs` | same | Source of the status-tone colour tokens (`Generated`/`Pending` need adding here, not redefined locally in a new component) |
| `AdminNav.razor`, `PermissionMatrix.razor`, `AccessRolePicker.razor` | `Vispl.HRManagement.UI/Components/Admin/Shared/` | Pattern reference only — the app menu is not the Admin portal's nav, but any new `PAYROLL.*`/`DESIGNATION.*` permissions (see doc 10 §7) slot into the existing `PermissionMatrix.razor` the same way today's permissions do |
| Currency/₹ formatting rule | specified in `RW-36` (`Vispl.HRManagement.Mobile/widgets.md`) and `EMP-W-24` (`employee-app-catalogue.md`) | Port the *rule*, not a UI control, into a shared formatter |
| Sensitive-value masking rule | `WID-108`/`SensitiveValueReveal` (mobile) | Port the *rule* for `SM-18`'s masked bank-account chip |
| Identity/avatar compound cell shape | `RW-32`, `EMP-W-14`, `W-19` | Reference shape for `SM-07` |

## 6. Gap summary

| Category | Count | Notes |
|---|---:|---|
| Reused outright (`●`), no new component work | 8 | Page shell, nav, base table, caller pattern, chart palette hook, admin permission matrix, action-column icons, date picker |
| Existing pattern extended with a new variant | 6 | KPI tile (new palette), status pill (new tokens), export toolbar (add refresh/print), table with totals row, table action column (single-button variant), currency/masking rules ported from mobile |
| Genuinely new, no ancestor in the platform | 22 | Everything under §2's AM- series bar AM-04/AM-08, plus the grouped/subtotal table, work-days ratio cell, printable payroll document, approval-gate footer, selectable checklist, bank-account picker/chip, dual commit-action footer, running-total bar, manual reorder control |

**36 widgets identified for this module: 8 pure reuse, 6 extensions of existing components, 22 net
new.** The concentration of new work is exactly where the module is genuinely new product
(payroll generation, preview, and bank transfer) — the chrome around it (page shell, nav, table
base, permissioning) is not new work and must not be re-implemented from scratch.
