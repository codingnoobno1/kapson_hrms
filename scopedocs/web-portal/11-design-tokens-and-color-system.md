# App Menu and Salary Module — design tokens, per-image colour extraction, and gap check

Companion to [08-app-menu-design-layout.md](08-app-menu-design-layout.md) (layout),
[09-widgets-reusable-skeleton.md](09-widgets-reusable-skeleton.md) (widget reuse against the real
codebase) and [10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md)
(routes/DTOs/API/RBAC). This document does three things: fixes one canonical colour-token table
from the per-image extraction, maps every proposed widget name onto what already exists in the
codebase (so nothing gets rebuilt under a second name), and lists — image by image — what an
independent read of the same eleven screenshots caught that a widget/colour inventory alone does
not surface.

**On the hex values below**: they are read off rendered screenshots, not sampled with a colour
picker against the original design file. Treat every value here as **build-ready but not
pixel-final** — confirm against the source Figma/PSD (or a colour-picker pass on the PNGs at
native resolution) before it goes into a token file that ships. Two independently eyeballed passes
(the one in the prompt and this one) landed within 1-2% of each other on every token, which is a
reasonable confidence signal, not a substitute for the real source values.

## 1. Canonical colour tokens

Several near-duplicate values appeared across different images for what is almost certainly the
same semantic colour (three "greens" for success/paid/approve, two "ambers" for pending/warning).
**Collapsing these into one token per semantic meaning is a decision, not a formatting choice** —
confirm with the client whether Approve, Mark-as-Paid and a generic "success" state are meant to
be the same green or deliberately distinguished. Until confirmed, this document collapses them and
flags the collapse.

```css
/* Core surfaces */
--hr-primary:            #3375EA;   /* active tab, primary buttons, group-row fill, Execute Transfer (close variant) */
--hr-primary-deep:       #528CDD;   /* Execute Transfer button — confirm vs --hr-primary; may be the same token rendered on a different background */
--hr-page-bg:            #F5F7F9;
--hr-surface:            #FFFFFF;
--hr-surface-muted:      #F5F5F5;   /* mega-menu navigation card */
--hr-tab-inactive-bg:    #F0F0F0;
--hr-panel-tint:         #E4EBFB;   /* Salary Generation Controls bar background */
--hr-table-header-tint:  #ECF3FF;   /* table header row, modal footer surface (salgen.png used #FAFBFF — near-identical, collapse to this token) */

/* KPI tile family — one component (see §2), four palette variants */
--hr-kpi-employee-bg:    #FFE8E8;   /* rose/pink */
--hr-kpi-gross-bg:       #FFF6D3;   /* pale amber */
--hr-kpi-pending-bg:     #D3FFE7;   /* pale green — note this is the KPI-tile tint, NOT the same as the Pending *status pill*, which uses a different, more amber tone (see below). Do not let the two collide */
--hr-kpi-generated-bg:   #E7DEFF;   /* pale lavender */

/* Status pills — Generated / Pending / Paid, used inside tables and modals, distinct from the KPI tiles above */
--hr-status-generated-bg:   #DCEEE0;
--hr-status-generated-fg:   #27A030;  /* "approximately #27A030" and "dark green" both given for this — collapse to one value */
--hr-status-pending-bg:     #FFF2D5;
--hr-status-pending-fg:     #C98A1F;  /* not given directly in the prompt's extraction — inferred to pair with the pale-amber background at a passing contrast ratio; verify against source */

/* Success / paid / approve family — three near-identical greens observed, collapsed to one token pending client confirmation */
--hr-success:            #55CD5E;   /* Mark as Paid button, Confirm button */
--hr-success-header:     #5DBD71;   /* salpaid.png modal title bar — 3% off --hr-success, likely the same token at a different opacity/blend, not a second colour */
--hr-success-alt:        #59A35E;   /* salgen.png Approve & Proceed — noticeably darker/more muted than the other two; CONFIRM whether Approve is deliberately a distinct, more "serious" green than Mark-as-Paid, given it is the higher-stakes action (PAYROLL.APPROVE, see doc 10 §7) */
--hr-success-bg:         #DCEEE0;   /* same as --hr-status-generated-bg — one token, two names in the original extraction */
--hr-success-soft:       #E7FAEC;   /* salpaid.png summary card background */
--hr-success-border:     #7BCB93;

/* Warning / pending-adjacent */
--hr-warning-soft:       #FFF7ED;   /* used in salpaid.png for an *info* panel (Status Change / Payment Mode / Document Type), not an actual warning — flagged in §4.11 as a naming mismatch worth fixing before this becomes a shared token misapplied elsewhere */
--hr-warning-button:     #F8DBA0;   /* Download Advice button */
--hr-action-transfer:    #E78A48;   /* Transfer to Bank button on the Individual tab — a third, distinct amber/orange from the two above; confirm whether this, --hr-warning-button and --hr-status-pending-fg are meant to be one "amber" family with three shades, or accidental drift */

/* Danger / money-out */
--hr-danger:             #F44336;   /* Deductions figures, always red, never a plain-coloured number (08 §7.1) */

/* Money-in */
--hr-money-positive:     #20A43C;   /* Net Payable, "approximately #20A43C" — collapse with --hr-status-generated-fg (#27A030)? These are used in different contexts (a currency figure vs. a status label) — keep as two tokens unless confirmed identical */

/* Transfer-modal specific */
--hr-transfer-bar-bg:    #2A3D61;   /* dark running-total bar, transfertobank.png only */
--hr-transfer-bar-fg:    #FFFFFF;
--hr-transfer-summary-bg:#F7F9FE;
--hr-transfer-list-header:#EEF1FA;
--hr-cancel-bg:          #E7E3E6;   /* the one button colour genuinely consistent across every modal — use as the single Cancel-button token everywhere in this module */

/* Text and structure */
--hr-text-primary:       #202F4A;   /* used inside modals — deeper navy than plain black */
--hr-text-black:         #000000;   /* used in the mega-menu overlay body text — a genuinely different value from --hr-text-primary, confirm this is deliberate (menu vs. modal context) rather than drift */
--hr-text-secondary:     #787878;
--hr-border:             #E4E4E4;
--hr-mega-menu-header-row: #D4E4FC; /* master1.png table header — close to --hr-table-header-tint (#ECF3FF); recommend aligning to the one token unless the Designation table is meant to look visually distinct from every other payroll table */
```

### 1.1 Decisions this token list forces before build

| # | Question | Where it shows up |
|---|---|---|
| 1 | Is there one "success green" or three (Mark-as-Paid, modal header, Approve)? | `--hr-success` vs `--hr-success-header` vs `--hr-success-alt` |
| 2 | Is there one "action amber" or three (pending status, Download Advice, Transfer to Bank)? | `--hr-status-pending-fg` vs `--hr-warning-button` vs `--hr-action-transfer` |
| 3 | Is `--hr-primary` (#3375EA) and the Execute Transfer blue (#528CDD) the same brand blue at different weights, or two blues? | Doc 08 §9.2's Execute Transfer button |
| 4 | Should the Designation master's table header (#D4E4FC) match every other table header (#ECF3FF) in the module? | master1.png vs every payroll table |
| 5 | `--hr-warning-soft` (#FFF7ED) is applied to a plain **information** panel in salpaid.png (Status Change / Payment Mode / Document Type — none of which is a warning). Rename the token by usage (`--hr-info-soft`) or restyle the panel neutral before it ships under a name that misdescribes it |
| 6 | `--hr-text-black` (mega menu) vs `--hr-text-primary` (#202F4A, inside modals) — one primary-text token or two, by surface? |

None of these are cosmetic nitpicks — every one of them becomes an inconsistent screenshot in QA
if two developers pick different existing tokens for what looks, to the eye, like "the same
green."

## 2. Widget-name reconciliation against the actual codebase

The widget names proposed against each image are functionally correct, but several map onto
components that **already exist in the codebase under different names** (confirmed by direct file
inspection while writing [09-widgets-reusable-skeleton.md](09-widgets-reusable-skeleton.md)).
Building a second component under the proposed name would fork the shell in two places.

| Proposed name | Maps onto (already in repo) | Action |
|---|---|---|
| `HrWebShell` | `Vispl.HRManagement.UI/Components/Hr/Shared/HrPageShell.razor` | Reuse — do not create `HrWebShell` |
| `SideNavigation` | `Vispl.HRManagement.UI/Components/Hr/Shared/HrNav.razor` | Reuse |
| `Breadcrumb`, `PageHeader`, `PageTitle` | Already rendered by `HrPageShell.razor` on every existing HR page (confirmed by S05's own breadcrumb: `Dashboard › System Settings › Designation`) | Reuse — extend the shell's title/breadcrumb slot, don't rebuild it |
| `DataGrid` / `MasterDataTable` / `PayrollDataGrid` | `Vispl.HRManagement.UI/Components/Hr/Shared/HrDataTable.razor` | One base table (`SM-05`/`MD-01` in doc 09), with the grouped/subtotal variant (`SM-06`) and the single-action variant (`SM-11`) as extensions of it, not three separate grids |
| `MasterCrudTable<T>` (from master1.png's analysis) | Same `HrDataTable.razor`, generalised | Correct instinct in the prompt — this *should* become the generic base, exactly as proposed. Implement it by extending the existing file, not writing a parallel generic table |
| `KpiCard` / `PayrollKpiCard` | New palette on the existing dashboard KPI-tile shape already scoped as `W-11`/`W-12` in [03-charts-and-widgets.md](03-charts-and-widgets.md) | One `KpiTile` component, parameterised by palette (`dashboard` vs `payroll`), not two components |
| `StatusPill` / `StatusBadge` / `SalaryStatusBadge` / `PaymentStatusSummary` | The status-pill shape already specified as `RW-24`/`W-26` for the mobile apps and reused across every HR table | One `StatusPill` component with a token map (`Active`, `Generated`, `Pending`, `Paid`, `Inactive`), not a badge per screen |
| `HrChartPalette.cs` | Already exists at `Vispl.HRManagement.UI/Components/Hr/Shared/HrChartPalette.cs` | This is where the new `Generated`/`Pending`/`Paid` status tones and the four KPI palette tints belong — **add to this file**, do not create a second palette source |
| `WorkflowModal` / `LargeWorkflowModal` / `PayrollPreviewModal` / `TransferSalaryModal` / `MarkSalaryPaidModal` | No shared modal shell exists yet for this module, and (per doc 08 §9) the three modals currently have **three different title-bar treatments** (blue title bar / plain white / green title bar) | Build **one** `WorkflowModal` shell (Header + Toolbar-slot + Content-slot + StickyFooter-slot, exactly as proposed) and drive Preview/Transfer/MarkPaid off it with a `tone` prop (`neutral`/`success`), rather than three modals that happen to look similar. This directly resolves doc 08's open question 6 |
| `EmployeeIdentityCell` / `EmployeeAvatarInitial` | Same compound-cell shape already specified for mobile as `RW-32`/`EMP-W-14`/`W-19` | Port the shape, do not re-derive it a third time (already flagged in doc 09 §3, `SM-07`) |
| `CurrencyCell` | The ₹-lakh-grouping rule already specified as `RW-36`/`EMP-W-24` for the mobile apps | Port the *formatting rule* into a shared formatter consumed by web and mobile, not a web-only cell renderer (doc 09 §3, `SM-09`) |
| `BankAccountMaskBadge` | The masking convention already specified as `WID-108`/`SensitiveValueReveal` for the mobile apps | Same rule, ported (doc 09 §3, `SM-18`) |
| `HrPortalCaller` (not in the prompt's list, but load-bearing) | `Vispl.HRManagement.UI/Components/Hr/Shared/HrPortalCaller.cs` | **Every new page in this module — Designation, Generate Salary, the geography masters — must call its BM service through this caller**, exactly as `HrLeave.razor`/`HrPeople.razor` already do, per `portal-integration.md`'s CR-1. This is not a UI widget but it is the single most important reuse point in the whole module, and the prompt's widget inventory (correctly) never mentions it because it isn't visible in a screenshot |

**Net effect**: of the ~26 widget families in the prompt's final catalogue, roughly 10 are new
implementations, and the remaining ~16 are either already-built components to reuse outright or
existing components that need a new palette/variant rather than a new file. This matches — and
slightly sharpens — the 8-reuse / 6-extend / 22-new split already computed in
[09-widgets-reusable-skeleton.md](09-widgets-reusable-skeleton.md) §6, once the modal family is
correctly counted as "one new shell, three configurations" rather than three new modals.

## 3. Per-image widget + colour table, reconciled

The four mega-menu tabs (images 1–3 and 9) share one palette and one widget set — repeated once
here, not four times, per the "same widget, different content" principle both passes already
agreed on.

### 3.1 Mega menu overlay — all four tabs (2.png, 3.png, 4.png, Screenshot…003710.png)

| Widget | Reconciled name | Notes |
|---|---|---|
| Overlay shell | `GlobalMegaMenuOverlay` (new, `AM-02`) | One instance, four tab states |
| Backdrop | `BackdropOverlay` | Dark translucent — do not sample the dimmed sidebar blue as a real colour token, it is the sidebar's normal colour composited under the backdrop |
| Tab bar | `MegaMenuTabBar` + `MegaMenuTab` (new, `AM-03`) | 4 tabs, icon+label |
| Close | `MegaMenuCloseButton` | Reuse the platform's one existing modal-close icon component |
| Section label | `MegaMenuSection` (new, `AM-05`) | Bold caps label, no card wrapper |
| Card | `MegaMenuNavigationCard` (new, `AM-06`) | Icon + Title + Subtitle + `OnClick`/`Route`; this is correctly identified in the prompt as the single component that "can drive every item across all four tabs" — agreed, and it is also the same underlying shape as the mobile app's `EMP-W-06`/`EMP-W-07` (doc 09 §2) |
| Grid (Configuration tab only) | `MegaMenuSectionGrid` | 4-column, only needed on the Configuration tab — the other three tabs are single-column and do not need this wrapper |

| Token | Value |
|---|---|
| Modal surface | `--hr-surface` #FFFFFF |
| Active tab | `--hr-primary` #3375EA, text/icon `--hr-surface` |
| Inactive tab | `--hr-tab-inactive-bg` #F0F0F0, text `--hr-text-black` |
| Navigation card | `--hr-surface-muted` #F5F5F5 |
| Primary text | `--hr-text-black` #000000 |
| Secondary text | `--hr-text-secondary` #787878 |
| Divider/border | `--hr-border` #E4E4E4 |
| Backdrop | dark translucent, value not extractable from a rendered screenshot — use the platform's existing modal-backdrop opacity, don't invent a new one |

### 3.2 Designation master (master1.png)

| Widget | Reconciled name |
|---|---|
| Page shell | `HrPageShell.razor` (existing — reuse) |
| Table | `HrDataTable.razor`, generalised toward `MasterCrudTable<T>` |
| Toolbar | New: refresh/search/overflow icon buttons + `AddMasterButton` + `SetOrderButton` |
| Status | `StatusPill` (`Active` token, existing shape) |
| Row actions | Existing edit/delete icon-button pattern already used on other Admin CRUD pages |

| Token | Value | Note |
|---|---|---|
| Header row | #D4E4FC | Recommend aligning to `--hr-table-header-tint` (#ECF3FF) — see §1.1 Q4 |
| Add button | `--hr-primary` #3375EA, text `--hr-surface` |
| Active status | `--hr-status-generated-bg`-family #DCEEE0, dark green text |
| Body text | ~#404040 — a fourth grey, distinct from `--hr-text-secondary` (#787878); confirm this isn't meant to just be `--hr-text-primary` |
| Border | `--hr-border` |

### 3.3 Generate Employee Salary — shared shell (sal1.png, deptwisesal.png, individualwisesal.png)

| Token | Value |
|---|---|
| Page background | `--hr-page-bg` #F5F7F9 |
| Control panel | `--hr-panel-tint` #E4EBFB |
| Table header | `--hr-table-header-tint` #ECF3FF |
| Group/division row fill (Department-Wise only) | `--hr-primary` #3375EA, text white |
| KPI: Total Employees | `--hr-kpi-employee-bg` #FFE8E8 |
| KPI: Gross Payroll | `--hr-kpi-gross-bg` #FFF6D3 |
| KPI: Pending | `--hr-kpi-pending-bg` #D3FFE7 |
| KPI: Generated | `--hr-kpi-generated-bg` #E7DEFF |
| Status: Generated | bg `--hr-status-generated-bg` #DCEEE0, fg `--hr-status-generated-fg` #27A030 |
| Status: Pending | bg `--hr-status-pending-bg` #FFF2D5, fg amber/orange |
| Deductions figure | `--hr-danger` #F44336, always |
| Net Payable figure | `--hr-money-positive` #20A43C |

Per-tab widget deltas (all three share the shell above):

- **Division-Wise** (sal1.png): `MinimumEmployeesInput`, `GrandTotalRow`, `SalaryHistoryButton`
  (named-only destination, doc 08 §6.1/§10 item 4-adjacent), `GenerateAllButton`.
- **Department-Wise** (deptwisesal.png): `GroupedTableRow` + subtotal row — this is the one table
  in the module that is **not** a flat `HrDataTable.razor` instance; it needs the grouped variant
  (`SM-06` in doc 09), and PF/ESI/TDS columns replace the single Deductions column seen elsewhere
  (flagged as an open reconciliation question in doc 10 §8, not caught in the prompt's per-image
  breakdown).
- **Individual** (individualwisesal.png): `EmployeeIdentityCell`, `WorkDaysCell` +
  `WorkDaysProgressIndicator` (a day-count ratio underline, not a percentage bar — distinct from
  any existing progress widget, `SM-08` in doc 09), `ShiftSelect` filter, and the three footer
  actions with their own distinct colours:

| Action | Token |
|---|---|
| Generate Pending | `--hr-primary` #3375EA |
| Transfer to Bank | `--hr-action-transfer` #E78A48 |
| Mark as Paid | `--hr-success` #55CD5E |

### 3.4 Salary Preview modal (salgen.png)

| Token | Value |
|---|---|
| Title bar | `--hr-primary` #3375EA, text white |
| Body | `--hr-surface` |
| Table header | `--hr-table-header-tint` |
| Footer surface | `--hr-table-header-tint` (collapsing the extracted #FAFBFF into this token — a ~1% difference, not a deliberate second surface) |
| Approve & Proceed | `--hr-success-alt` #59A35E — see §1.1 Q1 before treating this as the same green as Mark-as-Paid |
| Net Payable | `--hr-money-positive` |
| Deductions | `--hr-danger` |
| Pending count | amber, same family as `--hr-status-pending-fg` |

Widget-wise this is the `WorkflowModal` shell (§2) in its `neutral`/blue-header configuration, with
a `ViewModeDropdown` (the "Summary View" selector) that implies at least one unbuilt second view —
flagged in doc 08 §8 as an open `○`, not mentioned in the prompt's widget list.

### 3.5 Transfer to Bank modal (transfertobank.png)

| Token | Value |
|---|---|
| Header | `--hr-primary` |
| Summary-card background | `--hr-transfer-summary-bg` #F7F9FE |
| Employee-list header | `--hr-transfer-list-header` #EEF1FA |
| Running total bar | `--hr-transfer-bar-bg` #2A3D61, text white — the one colour in this entire module with no reused counterpart anywhere else; confirm it's intentional to introduce a fifth "brand" colour just for this bar |
| Cancel | `--hr-cancel-bg` #E7E3E6 |
| Download Advice | `--hr-warning-button` #F8DBA0 |
| Execute Transfer | `--hr-primary-deep` #528CDD — confirm vs `--hr-primary` (§1.1 Q3) |
| Generated / Pending rows | same status-pill tokens as §3.3 |

This is the `WorkflowModal` shell (§2) plus the net-new `SelectableEmployeeTransactionList`
(correctly identified in the prompt as a genuinely new composite — checkbox list + running total +
masked bank account + status pill, reused between this modal and Mark-as-Paid with a prop for
which per-row fields to render, doc 09 §3 `SM-15`).

### 3.6 Mark Salary as Paid modal (salpaid.png)

| Token | Value |
|---|---|
| Header | `--hr-success-header` #5DBD71 — see §1.1 Q1 |
| Summary cards | `--hr-success-soft` #E7FAEC, border `--hr-success-border` #7BCB93 |
| Info panel (Status Change / Payment Mode / Document Type) | extracted as `--hr-warning-soft` #FFF7ED — **this is a naming mismatch, not a warning state; rename before it ships** (§1.1 Q5) |
| Confirm / Mark as Paid | `--hr-success` #55CD5E |
| Cancel | `--hr-cancel-bg` |
| Text | `--hr-text-primary` #202F4A |

This modal shares `SelectableEmployeeTransactionList` with §3.5, in its reduced (no bank-account
column) configuration.

## 4. What an independent pass caught that the widget/colour inventory did not

Numbered so each can be checked off. None of these change the widget or colour catalogue above —
they are structural/data-integrity findings the "extract widgets and colours" exercise, by design,
does not surface, because they live in what the screens *mean*, not what they *look like*.

1. **`Employee` and `Employee Report` tab card subtitles duplicate their titles** (2.png, 4.png) —
   confirm whether real copy is pending. (Doc 08 §3.4, §10 item 3.)
2. **`Employee Monthly Attendance Report` has no subtitle at all**, unlike its three siblings on
   the same tab — the title wraps to two lines instead. (Doc 08 §3.4.)
3. **`Loan and Advance` (Salary Management tab) and all four Employee Report destinations have no
   reference screen anywhere.** Do not design their internal layout from the salary-generation
   pattern — a loan has a principal/schedule/balance shape a payroll run does not. (Doc 08 §10
   item 4; doc 10 §1 marks these routes "named only.")
4. **The Designation table's two adjacent "Department" columns share one header label** — the
   second is a free-text description, not a duplicate department name. Rename before build.
   (Doc 08 §5.)
5. **`Set Order` on the Designation toolbar implies a manual, cross-cutting ordering concern**: the
   order set here is presumably the order Designation appears in *every dropdown elsewhere in the
   app* that references it, not just this table's own row order. Confirm which other masters (Grade
   almost certainly) need the same control. (Doc 08 §5; doc 09 `MD-06`.)
6. **Shift codes on the Individual payroll tab (`HO KFPL`, `WH 9-7`, `London Fog`) read as
   location/brand-qualified**, not generic Morning/Evening/Night shift names — this has a real
   consequence for how the Shift master (APP Configuration → Leave & Attendance → Shift) needs to
   be modelled. (Doc 08 §7.3.)
7. **PF + ESI + TDS (shown on Department-Wise and Individual) must always equal the single
   "Deductions" figure shown on Division-Wise and the Salary Preview** — this is an arithmetic
   invariant across three different screens' worth of aggregation, not just a column-count
   difference. Modelled as a computed property in `PayrollComponentBreakdownDto`
   (doc 10 §3) specifically so it cannot drift. Not mentioned in either widget/colour pass.
8. **`Total Employees = Generated + Pending` is a hard invariant** on both the Generate Salary page
   (148 = 125 + 23) and the Salary Preview modal (15 = 8 + 7), at whatever scope each is computed
   for. Flag any future state (e.g. employees excluded from payroll for a joining-date reason) as
   needing a named third bucket, not a silent breakage of the equality. (Doc 08 §6.3, §8.)
9. **Two modals share the identical title "Transfer Salary to Bank"** despite one being a pure
   status change (Mark as Paid) and the other real money movement — both passes independently
   caught this, which is a strong signal it is a real defect to fix, not a subjective read.
   (Doc 08 §9; prompt's own image-11 summary.)
10. **The three modals in this module currently use three unrelated title-bar treatments** (blue /
    plain white / green) — beyond the naming clash in #9, this is a chrome-consistency question the
    widget list doesn't surface because each modal was catalogued independently. Resolved in §2 by
    specifying one `WorkflowModal` shell with a `tone` prop.
11. **A `Company Bank Account` picker (`SBI - KAPSONS0034`) appears in the Transfer to Bank modal
    but has no corresponding card anywhere in the 26-card APP Configuration catalogue.** This
    master must be added to the catalogue, not just built as a hidden dependency of one modal.
    (Doc 10 §4, §8.)
12. **The masked bank-account chip on every employee row in the Transfer to Bank modal depends on
    an Employee Bank Account backend module that does not exist anywhere in the platform today**
    (confirmed absent from both the main backend module inventory and the employee-app scope
    notes). This blocks the entire Transfer to Bank feature at the data layer, independent of any
    UI work — a null bank account must block selection, not silently transfer to nowhere. (Doc 10
    §4, `BankTransferCandidateDto.BankAccount`.)
13. **Of the 26 APP Configuration cards, only 3 map onto a confirmed, already-built Admin page**
    (Shift, Holiday, and Leave Type under the existing name "Leave Policies") **and at least 10 —
    everything payroll (Grade, Allowance/Deduction, grade-linking) and everything geography
    (Division/Office Location, Country, State, City, State City Link) plus Document Category —
    have no backend at all.** This is the single largest gap between "the design looks like a
    settings page" and "the settings page has something behind it," and it is invisible from a
    widget/colour inventory of the screenshots alone. (Doc 10 §5, full card-by-card table.)
14. **`PAYROLL.TRANSFER` and `PAYROLL.APPROVE` are the two highest-severity new permissions in the
    platform** and should never appear in a default role, on the same reasoning already applied to
    `LEAVE.YEAREND.RUN` elsewhere in this scope corpus. Not a design concern, but it belongs beside
    the design because the Approve/Transfer buttons are the two the design must make hardest to
    click by accident (confirmation, no bulk-select-all-by-default, explicit amounts shown before
    commit). (Doc 10 §7.)
15. **The mega-menu overlay's click-outside-to-close behaviour is not confirmed by any
    screenshot** — every capture shows the overlay already open or already closing via the `✕`.
    Treat as `○` rather than assuming standard modal behaviour, since this overlay is explicitly
    *not* a standard modal (it sits over a fully interactive page, not a blocking dialog). (Doc 08
    §3.2.)
16. **`HrPortalCaller.cs` is the one piece of required "reuse" that a screenshot can never show.**
    Every new Blazor page in this module (Designation, Generate Salary, every new master) must
    route its data access through it, per CR-1 in `portal-integration.md` — otherwise the module
    quietly becomes a second rule set the corporate rules explicitly forbid. (§2 above.)

## 5. Summary

- **1 canonical token file**, collapsing the several near-duplicate greens/ambers found across
  images into named decisions the client needs to confirm (§1.1) rather than three implementers
  independently guessing which "green" was meant.
- **~26 widget families in total** for this module, of which reconciliation against the actual
  codebase drops the net-new count to roughly **10 real new components** (mega-menu chrome, the
  one shared `WorkflowModal` shell, the grouped/subtotal table variant, the work-days ratio cell,
  the selectable employee-transaction list, and the bank-account picker/chip) — the rest are
  either outright reuse of `HrPageShell.razor`/`HrNav.razor`/`HrDataTable.razor`/
  `HrChartPalette.cs`/`HrPortalCaller.cs`, or a new palette/variant on an existing component.
- **16 structural findings** (§4) that a pure widget-and-colour extraction does not surface,
  ranging from copy issues (duplicated subtitles) to data-integrity invariants (PF+ESI+TDS,
  Generated+Pending) to the two module-defining facts: most of the "already there in Admin" claim
  does not hold for payroll or geography (§4.13), and the Transfer to Bank feature is blocked on a
  bank-account backend that has not been built (§4.12).
