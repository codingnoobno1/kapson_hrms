# App Menu and Salary Module — design layout

**Status: client-locked design.** Every label, column, tab, stat tile, and button named below is
taken verbatim from the supplied screenshots (`references/S01`–`S11`) and is not a placeholder to
be improved on. Where this document proposes something not visible in a screenshot, it is marked
`○` exactly as in [03-charts-and-widgets.md](03-charts-and-widgets.md), and nothing marked `○` may
be treated as optional — the client has approved the surrounding screen, and a `○` item is a gap
inside an already-approved shape, not a design choice still open.

This document is layout only: what exists, where, and in what order. Reusable component
extraction is [09-widgets-reusable-skeleton.md](09-widgets-reusable-skeleton.md). Routes, DTOs,
API and RBAC are [10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md).

## 1. What this adds to the portal already scoped in 01–07

[01-kpis.md](01-kpis.md) through [07-data-issues.md](07-data-issues.md) scope the **dashboard,
leave, attendance, movement and gate-pass** slice of the HR portal, sourced from `hrweb/1.png`–
`8.png`. The eleven screenshots here are a **second, later capture of the same prototype**,
sourced from `hrweb/New folder/`, and they add two things the first capture did not show:

1. A **global app-menu overlay** — a second hamburger control, distinct from the existing sidebar
   rail, that opens a full-width tabbed launcher over whatever page is currently open.
2. A **Salary/Payroll module** reached through that launcher — generation, preview, and bank
   transfer — plus a **restructured master-data catalogue** ("APP Configuration") that consolidates
   employee masters, payroll masters, leave/attendance masters, personal/demographic masters,
   organisation/location masters, and document masters into one place.

Neither the Salary/Payroll module nor most of the location/geography masters exist in the backend
today (see [10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md)
§5 for the module-by-module reality check). This is new scope, not a restatement of 01–07.

## 2. Reference index

| Ref | File | Screen |
|---|---|---|
| S01 | `S01-app-menu-employee-tab.png` | App menu, Employee tab active |
| S02 | `S02-app-menu-salary-tab.png` | App menu, Salary Management tab active |
| S03 | `S03-app-menu-employee-report-tab.png` | App menu, Employee Report tab active |
| S04 | `S04-app-menu-configuration-tab.png` | App menu, APP Configuration tab active (full 4-column grid) |
| S05 | `S05-designation-master.png` | Designation master — list page, no overlay |
| S06 | `S06-generate-salary-division-wise.png` | Generate Employee Salary — Division-Wise tab |
| S07 | `S07-generate-salary-department-wise.png` | Generate Employee Salary — Department-Wise tab |
| S08 | `S08-generate-salary-individual.png` | Generate Employee Salary — Individual tab |
| S09 | `S09-salary-preview-modal.png` | Salary Preview — full-screen payroll summary sheet |
| S10 | `S10-mark-as-paid-modal.png` | Mark as Paid confirmation modal |
| S11 | `S11-transfer-to-bank-modal.png` | Transfer to Bank — execution modal |

## 3. The app menu — global overlay

### 3.1 Trigger and chrome

There are now **two hamburger icons** in the portal, and they are not the same control:

| Icon | Position | Behaviour | Status |
|---|---|---|---|
| Existing rail toggle | Far left of the gradient header, before the logo | Collapses/expands the left navigation drawer (`W-01`/`W-02` in [03-charts-and-widgets.md](03-charts-and-widgets.md)) | Already scoped, already built |
| **New app-menu trigger** | Far right of the gradient header, **after** the profile block and its chevron | Opens the full-width tabbed launcher described below, as a modal overlay above the current page | **New** |

Evidence: S06–S08 and S05 all show the left rail toggle. S02–S04 show both the left rail toggle
**and** the right-edge trigger simultaneously, with the overlay already open — confirming the two
are independent controls, not one icon that moved. The launcher can therefore be opened from any
page in the portal, not just the dashboard.

### 3.2 Overlay anatomy

```text
┌───────────────────────────────────────────────────────────────────────────┐
│  [Tab] APP Configuration   [Tab] Employee   [Tab] Salary Mgmt  [Tab] Report │  ✕
│  ───────────────────────────────────────────────────────────────────────  │
│                                                                             │
│   Section Header                    Section Header      Section Header    │
│   ┌──────────────┐                  ┌──────────────┐    ┌──────────────┐  │
│   │ icon  Title   │                 │ icon  Title   │    │ icon  Title  │  │
│   │       Subtitle│                 │       Subtitle│    │       Subtitle│ │
│   └──────────────┘                  └──────────────┘    └──────────────┘  │
│   ┌──────────────┐                                                        │
│   │ ...           │                                                       │
│   └──────────────┘                                                        │
└───────────────────────────────────────────────────────────────────────────┘
        (background page dimmed/blurred beneath, click-outside not confirmed)
```

Fixed structure across all four tabs (S01–S04):

1. **Tab strip**, top of the panel: exactly four tabs, each with a leading icon — `⚙ APP
   Configuration`, `👤 Employee`, `💰 Salary Management`, `👥 Employee Report`. The active tab is
   filled solid blue with white text; inactive tabs are pale grey with dark text. A thin rule
   separates the tab strip from the content below it.
2. **Close control** (`✕`), top-right corner of the panel, outside the tab strip.
3. **Content area**, below the rule: one or more **named sections** (bold caps-style label, e.g.
   "Employee Masters", "Salary Management", "Employee Management"), each containing a vertical or
   grid run of **launcher cards**.
4. The panel itself is a large white sheet, left-aligned to roughly where the sidebar ends,
   right-aligned near the browser edge, vertically centred-ish near the top of the viewport — not
   full-bleed, not a right-hand drawer. The page behind it is visibly dimmed.

### 3.3 Launcher card anatomy

Every card in every tab (S01–S04) follows one shape:

```text
┌───────────────────────────────────┐
│ [icon]  Title (bold)               │
│         Subtitle (grey, small)     │
└───────────────────────────────────┘
```

- Leading icon in a small tile, one per card, semantically matched to the destination (a document
  icon for "Employee Explorer", a bar-chart icon for "Organisation Hierarchy", a plus icon for
  "Add New Employee", a money-bag icon for "Generate Employee Salary", a bank icon for "Loan and
  Advance", a person icon for "Employee Detail Report", and so on).
- Bold title line, sentence case, matches the destination page's own H1 where one is shown (e.g.
  "Generate Employee Salary" card → S06's page title is literally "Generate Employee Salary").
  This 1:1 naming is deliberate and must be preserved — the card is the page title.
- Grey subtitle line beneath the title, one short sentence, restating or lightly expanding the
  title (e.g. "Individual Employee-wise payroll generation" under "Generate Employee Salary").
- Cards sit in a light-grey rounded container, no visible border, with generous internal padding.
  On the APP Configuration tab (S04) they pack tighter (smaller font, tighter line-height) because
  there are 26 of them; on the other three tabs they are noticeably larger and roomier because
  there are only 2–4 per tab.

### 3.4 Tab-by-tab content

#### Tab 1 — APP Configuration (S04)

Four columns of named sections, 26 cards total. This is the master-data catalogue and is treated
in full in §4.

#### Tab 2 — Employee (S01)

One section, **"Employee"**, three cards, single column:

| Card | Subtitle |
|---|---|
| Employee Explorer | Employee Explorer |
| Organisation Hierarchy | Organisation Hierarchy |
| Add New Employee | Add New Employee |

Note the subtitles are **identical to the titles** for all three cards here — the only tab where
that happens. Every other tab has a distinct, expanded subtitle. Flag this for the client: either
the subtitle content for these three is still pending, or a repeated subtitle is acceptable for
self-explanatory destinations. Do not invent expanded copy without confirming which.

#### Tab 3 — Salary Management (S02)

One section, **"Salary Management"**, two cards, single column:

| Card | Subtitle |
|---|---|
| Generate Employee Salary | Individual Employee-wise payroll generation |
| Loan and Advance | Loan and advanced voucher |

`Generate Employee Salary` is fully specified by S06–S11 (§5–§7 below). **`Loan and Advance` has
no reference screen at all** — the card exists, the destination does not. Treat it as a named,
approved-but-unspecified page; do not guess its layout from the salary generation flow, they are
different financial instruments (a loan has a principal, a schedule and a running balance; a
salary run does not).

#### Tab 4 — Employee Report (S03)

One section, **"Employee Management"**, four cards, single column:

| Card | Subtitle |
|---|---|
| Employee Detail Report | Employee Detail report |
| Employee Monthly Attendance Report | *(none shown — title wraps to two lines instead)* |
| Employees Salary Register | Employee Salary report |
| Loan and Advance Report | Loan and Advance Report |

None of the four report destinations have their own reference screen. They are named, scoped as
report **outputs** (§6.6 of [10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md)
gives each a minimal contract), but their exact table columns are not client-approved yet and must
not be designed as if they were — only the entry point and the name are locked.

## 4. APP Configuration tab — full catalogue (S04)

Four columns, seven named sections, 26 cards. Transcribed exactly, left to right, top to bottom.

### Column 1 — Employee Masters (9 cards)

| # | Card | Subtitle |
|---|---|---|
| 1 | Designation | Manage job designations |
| 2 | Department & Sub Department | Configure department structure |
| 3 | Gender | Configure gender categories |
| 4 | Qualification | Manage qualifications |
| 5 | Skills | Manage employee skills |
| 6 | Education Level | Configure education levels & certifications |
| 7 | Religion | Manage religion classifications |
| 8 | Nationality | Manage nationalities and countries |
| 9 | Blood Group | Manage blood groups and health info |

### Column 2 — Payroll & Compensation (3 cards), then Leave & Attendance (5 cards)

**Payroll & Compensation**

| # | Card | Subtitle |
|---|---|---|
| 10 | Allowance/Deduction | Manage salary allowances & deductions |
| 11 | Grade | Configure employee grades |
| 12 | Link Allowances / Deductions With Grade | Allocate Allowances/Deductions with Grade |

**Leave & Attendance**

| # | Card | Subtitle |
|---|---|---|
| 13 | Shift | Configure work shifts |
| 14 | Link Employee to Shift | *(subtitle truncated in source — "…")* |
| 15 | Holiday | Manage holidays and festivals |
| 16 | Leave Type | Manage leave types and policies |
| 17 | Leaving Reason | Manage exit reasons & resignation categories |

### Column 3 — Personal & Demographics (2 cards)

| # | Card | Subtitle |
|---|---|---|
| 18 | Marital Status | Configure marital status options |
| 19 | Relation | Configure family relationships |

### Column 4 — Organization & Location (5 cards), Document Management (1 card), Other (1 card)

**Organization & Location**

| # | Card | Subtitle |
|---|---|---|
| 20 | Division/Office Location | Configure office divisions and locations |
| 21 | Country | Manage country data |
| 22 | State | Manage state/province data |
| 23 | City | Manage city data |
| 24 | State City Link | Link states with cities |

**Document Management**

| # | Card | Subtitle |
|---|---|---|
| 25 | Document Category | Manage document categories & subcategories |

**Other**

| # | Card | Subtitle |
|---|---|---|
| 26 | Occupation | Configure occupation categories |

Column layout is a simple 4-up CSS grid at desktop width; section headers are bold, sit directly
above their card run, and a column may host more than one section stacked vertically (column 2 and
column 4 both do). This must reflow, not truncate, below desktop width — at 26 cards, a fixed
4-column grid on a narrow viewport is unusable, and no reference shows a responsive breakpoint for
this screen. Flag as `○` for a tablet-width behaviour decision.

## 5. Designation master page (S05)

Reached from APP Configuration → Employee Masters → Designation. This is a full routed page, not
a modal — the overlay closes and the main content area replaces itself.

```text
Dashboard  ›  System Settings  ›  Designation                         (breadcrumb)

Designation                                          [⟳] [🔍] [⋮] [+ Add Designation] [⚙ Set Order]

┌───┬──────────────────────┬──────┬───────┬────────────┬──────────────────────────────┬────────┬─────────┐
│ # │ Designation Name     │ Code │ Grade │ Department │ Department (description)     │ Status │ Actions │
├───┼──────────────────────┼──────┼───────┼────────────┼──────────────────────────────┼────────┼─────────┤
│ 1 │ Software Engineer    │ SE   │ A     │ IT         │ Develops and maintains apps  │ ACTIVE │ ✎  🗑   │
│ 2 │ Senior Software Eng. │ SSE  │ B     │ IT         │ Leads software dev projects  │ ACTIVE │ ✎  🗑   │
│ 3 │ Project Manager      │ PM   │ A     │ IT         │ Manages project execution    │ ACTIVE │ ✎  🗑   │
│ 4 │ HR Manager           │ HRM  │ C     │ HR         │ Manages HR functions         │ ACTIVE │ ✎  🗑   │
│ 5 │ Accounts Executive   │ AE   │ A     │ Finance    │ Handles accounting & finance │ ACTIVE │ ✎  🗑   │
└───┴──────────────────────┴──────┴───────┴────────────┴──────────────────────────────┴────────┴─────────┘
```

Exact column set, left to right: **S.No.**, **Designation Name**, **Code**, **Grade**,
**Department**, **Department** (a second, wider column — this is the department's own descriptive
text, e.g. "Develops and maintains software applications", not a duplicate of the department
name; the source screenshot genuinely repeats the header "Department" for both — flag as a header
label issue, the second should read something like "Description"), **Status** (pill, `ACTIVE`
green in every visible row — no other state is shown, but a designation retirement/deprecation
flow implies at least `INACTIVE` must exist), **Actions** (edit pencil, delete bin, one icon
button each).

Toolbar, right-aligned above the table: a **refresh** icon, a **search** icon, an **overflow**
(`⋮`) menu, a primary **"+ Add Designation"** button, and a secondary **"Set Order"** button (gear
icon) — the last implies designations are manually orderable, not just alphabetically or
ID-sorted, which matters for how they populate dropdowns elsewhere (the order set here should be
the order every designation picker in the rest of the app uses).

The **Grade** and **Department** columns are foreign keys into masters that are configured
elsewhere in this same catalogue (`Grade` under Payroll & Compensation, `Department & Sub
Department` under Employee Masters) — this page cannot be built before those two exist, since its
own "Add Designation" form necessarily needs both as pickers.

## 6. Generate Employee Salary — shared shell (S06–S08)

All three tabs of this page share one page shell and one control bar; only the table beneath
changes. Transcribed once here; per-tab table differences are in §7.

```text
Dashboard  ›  System Settings  ›  Generate Employee Salary                      [🕐 Salary History]

Generate Employee Salary

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙ Salary Generation Controls                 MONTH  YEAR  SALARY TYPE  DIVISION  DEPARTMENT│
│   Select month, year and scope, then generate  [Apr▾][2025▾][Monthly▾][All Div▾][All Dept▾]│
│                                                              [📊 Generate Salary] [👁 Preview]│
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌───────────────┐┌────────────────┐┌────────────────┐┌────────────────┐
│ 👥 TOTAL EMP.  ││ 💰 GROSS PAYROLL││ ⏳ PENDING      ││ ✓ GENERATED    │
│    148         ││    ₹42.6L       ││    23           ││    125          │
│ Active 4 divs. ││ This month      ││ Not yet gen'd   ││ Ready to disb.  │
└───────────────┘└────────────────┘└────────────────┘└────────────────┘  (rose)(amber)(green)(lavender)

┌─────────────────────────────────────────────────────────────┐
│ [📊 Division-Wise (4)]  [📁 Department-Wise (6)]  [👤 Individual (15)] │  ← segmented tabs, count badges
└─────────────────────────────────────────────────────────────┘

  <filters row — differs slightly per tab, see §7>            [⟳][🔍][Export][Print]

  <table — differs per tab, see §7>

  <footer bar — count label left, action buttons right>
```

### 6.1 Header row

- Breadcrumb: `Dashboard › System Settings › Generate Employee Salary`.
- Page title, bold blue, large: **"Generate Employee Salary"**.
- Top-right of the breadcrumb row: a single secondary button, **"🕐 Salary History"** — a link to
  past runs, not shown in any reference. Treat as a named-but-unspecified destination like `Loan
  and Advance`.

### 6.2 Salary Generation Controls bar

A single light-blue card spanning the page width, split left/right:

- **Left**: a gear icon, the label **"Salary Generation Controls"**, and helper text **"Select
  month, year and scope, then generate payroll"**.
- **Right**: five labelled dropdowns in a row — **MONTH** (`April`), **YEAR** (`2025`), **SALARY
  TYPE** (`Monthly Salary`), **DIVISION** (`All Divisions`), **DEPARTMENT** (`All Departments`) —
  followed by two buttons: a filled primary **"📊 Generate Salary"** and an outline secondary
  **"👁 Preview"**.

This bar is the single source of scope for everything below it on the page — the four stat tiles
and all three tabbed tables are computed for whatever Month/Year/Salary Type/Division/Department
combination is currently selected here. Changing a dropdown must refresh the whole page body, not
just the active tab.

### 6.3 The four stat tiles

Fixed order, fixed colour, one row of four equal cards:

| Order | Icon | Label | Value (example) | Caption | Tint |
|---|---|---|---|---|---|
| 1 | 👥 | TOTAL EMPLOYEES | 148 | "Active 4 divisions" | rose/pink |
| 2 | 💰 | GROSS PAYROLL | ₹42.6L | "This month" | pale yellow |
| 3 | ⏳ | PENDING | 23 | "Not yet generated" | pale green |
| 4 | ✓ | GENERATED | 125 | "Ready to disburse" | pale lavender |

**148 ≠ 125 + 23** by design in the sample (125 + 23 = 148 — they do agree). Keep this identity as
a hard invariant: `Total Employees = Generated + Pending` for the selected scope, always. If a
future state introduces employees excluded from payroll entirely (e.g. an employee who joined
after the period, per the joining-date rules already enforced elsewhere in the platform), a fifth
bucket must be added rather than silently breaking the equality — do not let "Total" quietly mean
something different from "Generated + Pending".

### 6.4 Segmented tab row

Three pill-style segments, each with a leading icon and a **count badge**: `📊 Division-Wise (4)`,
`📁 Department-Wise (6)`, `👤 Individual (15)`. The counts are the number of divisions, departments
and (visible-page of) employees respectively — **not the same denominator**, and must not be
rendered as if they were comparable numbers.

### 6.5 Table toolbar (per tab)

Every tab repeats the same right-aligned icon cluster above its table: **refresh**, **search**,
**Export**, **Print**. To their left, tab-specific filter dropdowns (§7). The row also carries the
table's own left-aligned filter set — this is a genuinely dense control row, more so than any
table scoped in [03-charts-and-widgets.md](03-charts-and-widgets.md), and needs the sortable
header, pagination and column-chooser widgets already flagged there (`W-22`–`W-24`) even though
none of S06–S08 show a page-2, since 148 employees will not fit on one page.

## 7. Generate Employee Salary — per-tab tables

### 7.1 Division-Wise (S06)

Filters: **Status** (`All Status`), **Min Employees** (free-text, placeholder `e.g. 20`).

| Division | Employees | Departments | Basic | HRA | Allowances | Gross | Deductions | Net Payable | Status | Actions |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| Retail | 62 | 3 | ₹18,20,000 | ₹5,46,000 | ₹3,64,000 | ₹27,30,000 | ₹2,43,000 | ₹24,87,000 | Generated | Generate |
| Warehouse | 34 | 2 | ₹8,50,000 | ₹2,55,000 | ₹1,70,000 | ₹12,75,000 | ₹1,21,000 | ₹11,54,000 | Generated | Generate |
| Head Office | 28 | 4 | ₹11,20,000 | ₹3,36,000 | ₹2,24,000 | ₹16,80,000 | ₹1,62,000 | ₹15,18,000 | Pending | Generate |
| IT & Digital | 24 | 2 | ₹9,80,000 | ₹2,94,000 | ₹1,96,000 | ₹14,70,000 | ₹1,45,000 | ₹13,25,000 | Pending | Generate |
| **Grand Total** | | | **₹47,70,000** | **₹14,31,000** | **₹9,54,000** | **₹71,55,000** | **₹6,71,000** | **₹64,84,000** | | |

Verified arithmetic (this is the one payroll screen in the whole scope corpus that is internally
consistent — contrast with the leave dashboard's known errors in
[07-data-issues.md](07-data-issues.md)): every row's `Basic + HRA + Allowances = Gross`, and every
column sums correctly to the Grand Total row. **Deductions is red-coloured text**, the only column
styled that way — deductions are always shown as a reduction, never as a plain number, and that
colour convention must carry through every payroll table in this module.

`Status` is a coloured dot + label (`● Generated` green, `● Pending` amber), and `Actions` is a
single outline **"Generate"** button per row — greyed/disabled implicitly once a row is already
`Generated` (no reference shows a generated row's button state explicitly; treat as `○`, confirm
before build whether a generated row still exposes a re-generate action or only a view action).
Footer: **"4 Departments"** — wait, this is division count context, actually the left-aligned
footer text under the table reads department count for context, and the bottom-right shows a
single filled **"Generate All"** button.

### 7.2 Department-Wise (S07)

Filters: **Division** (`All Divisions`), **Department** (`All Departments`), **Status** (`All
Status`).

Rows are **grouped by division**, each group opening with a shaded division header row (e.g.
`🏢 Head Office`) followed by one row per department in that division, followed by a **division
subtotal row** (repeats the division name, no employee/department count, bold totals), before the
next division group begins.

| Department | Division | Employees | Basic | HRA | Conv. | Other | Gross | PF | ESI | TDS | Net Payable | Status | Actions |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| *(group header)* **Head Office** | | | | | | | | | | | | | |
| HR | Head Office | 8 | ₹3,20,000 | ₹96,000 | ₹32,000 | ₹48,000 | ₹4,96,000 | ₹38,400 | ₹12,800 | ₹22,000 | ₹4,22,800 | Generated | Generate |
| Accounts | Head Office | 6 | ₹2,70,000 | ₹81,000 | ₹27,000 | ₹40,000 | ₹4,18,000 | ₹32,400 | ₹10,800 | ₹18,000 | ₹3,56,800 | Generated | Generate |
| **Head Office (subtotal)** | | | ₹5,90,000 | ₹1,77,000 | ₹59,000 | ₹88,000 | ₹9,14,000 | ₹70,800 | ₹23,600 | ₹40,000 | ₹7,79,600 | | |
| *(group header)* **Retail** | | | | | | | | | | | | | |
| Sales | Retail | 22 | ₹6,60,000 | ₹1,98,000 | ₹66,000 | ₹99,000 | ₹10,23,000 | ₹79,200 | ₹26,400 | ₹46,000 | ₹8,71,400 | Generated | Generate |
| *(table is cut off below the fold; "6 Departments" and "Generate All" sit at the very bottom)* | | | | | | | | | | | | | |

**Three new deduction columns appear here that Division-Wise does not show**: **PF**, **ESI**,
**TDS** — replacing the single "Deductions" column. Division-Wise's `Deductions` for a given
division must equal `PF + ESI + TDS` summed across that division's departments here (Head Office:
₹70,800+₹23,600+₹40,000 = ₹1,34,400... but Division-Wise showed Head Office's own Deductions
column at the division level as part of the ₹6,71,000 grand-total-only breakdown — Division-Wise
does not show Head Office's own deduction figure as a named row, only the grand total, so this
cannot be cross-checked from the rows actually visible. Flag as `○`: confirm PF+ESI+TDS is exactly
the composition of the single "Deductions" figure everywhere else in the module, not an
approximation, before the two tabs are allowed to disagree.**

`Conv.` = Conveyance allowance, `Other` = a catch-all allowance bucket — both are components of
Allowances shown as one figure in the Division-Wise tab. `Gross = Basic + HRA + Conv. + Other`
holds for every row shown.

### 7.3 Individual (S08)

Filters: **Division** (`All Divisions`), **Department** (`All Departments`), **Status** (`All
Status`), **Shift** (`All Shifts`).

| Avatar+Employee (Name/ID) | Division | Department | Designation | Shift | Work Days | Basic | HRA | Conv. | Other | Gross | PF | ESI | TDS | Net |
|---|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| RS Rahul Sharma / E001 | Head Office | HR | Manager | HO KFPL | 26/26 | ₹55,000 | ₹16,500 | ₹5,500 | ₹8,250 | ₹85,250 | ₹6,600 | ₹0 | ₹5,000 | ₹73,… |
| PS Priya Singh / E002 | Head Office | Accounts | Accountant | Store | 25/26 | ₹42,000 | ₹12,600 | ₹4,200 | ₹6,300 | ₹65,100 | ₹5,040 | ₹1,680 | ₹3,000 | ₹55,… |
| AV Amit Verma / E003 | IT & Digital | IT | Developer | WH 9-7 | 26/26 | ₹68,000 | ₹20,400 | ₹6,800 | ₹10,300 | ₹1,05,400 | ₹8,160 | ₹0 | ₹7,500 | ₹89,… |
| SK Sunita Kapoor / E004 | Retail | Sales | Executive | Store 17 | 24/26 | ₹32,000 | ₹9,600 | ₹3,200 | ₹4,800 | ₹49,600 | ₹3,840 | ₹1,280 | ₹0 | ₹44,… |
| DJ Deepak Joshi / E005 | Warehouse | Warehouse | Supervisor | London Fog | 26/26 | ₹38,000 | ₹11,400 | ₹3,800 | ₹5,700 | ₹58,900 | ₹4,560 | ₹1,520 | ₹0 | ₹52,… |

Net Payable column is cut off at the right edge of the screenshot in every visible row — the
values above are read from the visible leading digits plus the comma, not confirmed to the last
digit. Confirm exact net figures before using them as fixture data.

New elements only present in this tab:

- **Compound identity cell**: a two-letter colour-coded avatar circle (initials, one solid colour
  per person, not a photo) beside a two-line Name/Employee-ID stack. This is the same shape as the
  applicant/interview identity cells already scoped for the mobile apps (`RW-32`/`WID-012` family)
  — reuse, do not re-derive.
- **`Shift` column** with free-text-looking values (`HO KFPL`, `Store`, `WH 9-7`, `Store 17`,
  `London Fog`) — these read as **shift codes tied to a specific work location/brand line**, not a
  generic "Morning/Evening/Night" shift name. This must resolve against the same Shift master
  configured in APP Configuration → Leave & Attendance → Shift, and the values shown here suggest
  the Shift master needs a location/brand qualifier, not just a name and timing.
- **`Work Days` fractional cell with an inline progress underline** (e.g. `26/26` fully underlined,
  `24/26` partially) — a new widget, `SM-08` in
  [09-widgets-reusable-skeleton.md](09-widgets-reusable-skeleton.md), not the same as the existing
  `W-25` inline progress-bar-in-cell (that one is a percentage bar; this one is a day-count ratio
  with a thin underline, visually distinct).

Footer: **"15 Employees"** left-aligned, and three action buttons right-aligned — outline
**"📊 Generate Pending"**, filled brown/amber **"🏦 Transfer to Bank"**, filled green **"✅ Mark as
Paid"**. These three buttons are the entry points to §8's two modals.

## 8. Salary Preview (S09)

Triggered by the **"👁 Preview"** button in the Salary Generation Controls bar (§6.2). Renders as a
**full-width modal with its own blue title bar**, distinct in chrome from every other modal in
this module (S10/S11 use a plain white title bar with a coloured accent instead — flag this
inconsistency, both should probably share one modal chrome).

```text
┌──────────────────────────── Salary Preview - April 2025 ───────────────────────────────┐  ✕
│  Payroll Preview - April 2025      [Summary View ▾]  [🖨 Print] [⬇ Export Excel] [✉ Send] │
│                                                                                          │
│  Kapsons Fashion                                                          April 2025    │
│  Kapsons Fashion · Payroll Department                                PAYROLL SUMMARY    │
│                                                                                          │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐                                        │
│  │ Total        │ │ Total Gross │ │ Total Net   │                                        │
│  │ Employees    │ │ Payroll     │ │ Payable     │                                        │
│  │ 15           │ │ ₹9,71,850   │ │ ₹8,49,330   │                                        │
│  └─────────────┘ └─────────────┘ └─────────────┘                                        │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐                                        │
│  │ Total        │ │ Generated   │ │ Pending     │                                        │
│  │ Deductions   │ │             │ │             │                                        │
│  │ ₹1,22,520    │ │ 8           │ │ 7           │                                        │
│  └─────────────┘ └─────────────┘ └─────────────┘                                        │
│                                                                                          │
│  Employee | Dept | Division | Work Days | Basic | Gross | Deductions | Net | Status      │
│  (10 rows visible, e.g. Priya Singh E002 · HR · Head Office · 26/26 · ₹55,000 · ₹55,000  │
│   · ₹11,600 · ₹73,650 · Generated)                                                       │
│                                                                                          │
│                                                          [Cancel]  [✅ Approve & Proceed] │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

Key details:

- **Letterhead block**: tenant name ("Kapsons Fashion") as a document title, sub-line "Kapsons
  Fashion · Payroll Department", period on the right ("April 2025", "PAYROLL SUMMARY"). This is a
  **printable/exportable document**, not just a screen — the toolbar has Print, Export Excel and
  Send (presumably email) actions, and a **"Summary View"** dropdown implying at least one other
  view mode exists (a detail/line-item view is the obvious candidate; not shown, mark `○`).
- **Six stat tiles**, 2 rows × 3 columns, plain white bordered cards (not colour-tinted like §6.3's
  tiles): Total Employees, Total Gross Payroll, Total Net Payable / Total Deductions, Generated,
  Pending. Same `Generated + Pending = Total Employees` invariant as §6.3 (8 + 7 = 15 ✓).
- **Table columns are a reduced set** of the Individual tab's columns — no PF/ESI/TDS breakdown
  here, just a single `Deductions` figure — appropriate for a summary document, but confirms the
  detail-level breakdown (PF/ESI/TDS) is a drill-down concern, not a summary-document concern.
- Row-level **Status** pill repeats `Generated`/`Pending`, same colours as everywhere else in the
  module.
- Footer actions: **"Cancel"** (outline) and **"✅ Approve & Proceed"** (filled green) — this is an
  **approval gate**, not a passive preview. Clicking it presumably commits the run and is the
  moment the payroll figures stop being provisional. This is the single most consequential button
  in the whole module and needs the same treatment as `LEAVE.YEAREND.RUN` in
  [05-rbac.md](05-rbac.md) — see
  [10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md) §7.

## 9. Mark as Paid (S10) and Transfer to Bank (S11) — two different modals, one shared title

**Both modals are titled "Transfer Salary to Bank" in the source screenshots. They are not the
same flow and must not be built as one dialog with two buttons.** This is the single clearest
labelling defect in this capture and must be corrected in the delivered UI — see
[10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md) §8 for the
recommended distinct titles.

### 9.1 "Mark as Paid" confirmation (S10)

Reached from the Individual tab's **"✅ Mark as Paid"** button. Small, centred modal, green title
bar.

```text
┌──────────────────── Transfer Salary to Bank ─────────────────────┐  ✕
│  ┌────────────────┐  ┌────────────────┐                          │
│  │ Total Employees │  │ Total Amount    │                         │
│  │ 2                │  │ ₹1,26,470       │                         │
│  │ Employees        │  │ Net Payable     │                         │
│  └────────────────┘  └────────────────┘                          │
│                                                                    │
│  ☐ Select All Generated Salaries                    2 selected    │
│  ☑ Priya Singh   E001 - HR Manager             ₹73,650            │
│  ☐ Amit Verma    E002 - Accounts Account       ₹55,380            │
│  ☐ Sunita Kapoor E003 - Sales Executive        ₹89,740            │
│  ☐ Deepak Joshi  E004 - Sales Executive        ₹44,480             │
│                                                                    │
│  Status Change:      Generated → Paid                             │
│  Payment Mode:        Bank Transfer                                │
│  Document Type:       Monthly Payroll                              │
│                                                                    │
│  Payment Date *  [10/04/2026 📅]                                   │
│                                                                    │
│                                        [✕ Cancel]  [✅ Mark as Paid]│
└─────────────────────────────────────────────────────────────────┘
```

This is a **status-change confirmation**, not a money-movement action: it records that a
selected, already-generated salary has been paid (by whatever external means), moving its status
`Generated → Paid`. The employee-selection checklist and its running "Total Amount" recompute as
checkboxes are toggled. **Payment Date is a required field** (marked `*`) and defaults to a date
noticeably different from "today" in the reference (`10/04/2026` shown against a header clock of
`Jan 8, 2026` elsewhere in the same mock) — treat the default as a placeholder, not a real default
rule; the real default should be "today" unless told otherwise.

### 9.2 "Transfer to Bank" execution (S11)

Reached from the Individual tab's **"🏦 Transfer to Bank"** button. Larger modal, blue title bar,
genuinely different content.

```text
┌───────────────────────── Transfer Salary to Bank ──────────────────────────┐  ✕
│ ┌───────────┐ ┌────────────┐ ┌──────────────┐ ┌─────────────────┐          │
│ │ Total Emp.│ │ Total Amt. │ │ Already      │ │ Pending Transfer │          │
│ │ 8          │ │ ₹4,31,360  │ │ Transferred  │ │ 8                │          │
│ │ Generated  │ │ To Be      │ │ 0            │ │ Awaiting         │          │
│ │ Salary     │ │ Transferred│ │ This Month   │ │ Transfer         │          │
│ └───────────┘ └────────────┘ └──────────────┘ └─────────────────┘          │
│                                                                              │
│  Transfer Details                                                           │
│  Transfer Date [09/04/2026📅]  Bank Account [SBI-KAPSONS0034▾]              │
│  Transfer Mode [IMPS▾]          Reference No. [TXN-28404375]                │
│                                                                              │
│  [🔍 Search employee...]                          [All Employees ▾]         │
│                                                                              │
│  ☐ Select All Generated Employees                            0 selected     │
│  ☐ Priya Singh   E001 - Manager      [HDFC •••• 4821]  ₹73,650  ●Generated  │
│  ☐ Amit Verma    E002 - Account      [SBI •••• 2034]   ₹55,380  ●Generated  │
│  ☐ Sunita Kapoor E003 - Executive    [HDFC •••• 4821]  ₹89,740  ●Pending    │
│  ☐ Deepak Joshi  E004 - Executive    [SBI •••• 2034]   ₹44,480  ●Pending    │
│  ☐ Neha Gupta    E005 - Supervisor   [HDFC •••• 4821]  ₹52,820  ●Generated  │
│  ☐ Vijay Kumar   E006 - Store Fl.Mgr [SBI •••• 2034]   ₹48,650  ●Generated  │
│                                                                              │
│  Total Transfer Amount: ₹0                              Employees · IMPS   │
│                                                                              │
│                              [✕Cancel] [⬇Download Advice] [⇄Execute Transfer]│
└──────────────────────────────────────────────────────────────────────────────┘
```

This is the real money-movement flow. Distinct from §9.1 in every material way:

- **Four stat tiles**, not two: Total Employees, Total Amount, **Already Transferred (this
  month)**, **Pending Transfer** — tracking a running monthly transfer total the Mark-as-Paid
  modal never mentions.
- **Transfer Details form**: Transfer Date, **Bank Account** (a picker over the company's own
  bank accounts, e.g. `SBI - KAPSONS0034` — this is a **company/tenant bank account master**, not
  an employee one, and does not appear anywhere in the APP Configuration catalogue; it must be
  added), **Transfer Mode** (`IMPS` — implies NEFT/RTGS are siblings), and a **Reference No.**
  field that is pre-filled (`TXN-28404375`) — confirm whether this is generated server-side and
  merely displayed, or genuinely editable; a user-editable transaction reference on a bank transfer
  is a reconciliation risk.
- **Search box and an "All Employees" filter** above the list — absent from the smaller
  Mark-as-Paid modal, because this list is expected to be long (an actual monthly payroll run, not
  a hand-picked 2–4 people).
- **Employee rows show a masked bank account** (`HDFC •••• 4821`) inline — this is the employee's
  own bank details, sourced from wherever bank details are stored (per the employee-app scope
  notes elsewhere in this project, bank details do not have a backend module yet at all — see
  [10-salary-module-dynamic-pages-and-api.md](10-salary-module-dynamic-pages-and-api.md) §5). The
  masking pattern matches the platform's existing sensitive-value convention and must be kept.
- Running **"Total Transfer Amount"** footer bar, dark background, updates live as rows are
  checked — shown as `₹0` because nothing is selected yet in the captured frame.
- Two distinct commit actions: **"⬇ Download Advice"** (produces a bank-upload file / advice
  document without moving anything) and **"⇄ Execute Transfer"** (the actual, presumably
  irreversible action). Having both on the same footer is intentional — banks are commonly paid via
  an uploaded advice file rather than an API call, so "Download Advice" may be the *real* transfer
  mechanism and "Execute Transfer" the system's own bookkeeping of that fact. Confirm which before
  assuming this integrates with a live payment gateway.

## 10. Layout-level open questions for the client

Numbered so they can be answered one at a time, in the style of
[07-data-issues.md](07-data-issues.md):

1. **Two modals share the title "Transfer Salary to Bank."** Confirm distinct titles (§9).
2. **The Designation table's two "Department" columns** — confirm the second is a description
   field, and rename its header (§5).
3. **`Employee` and `Employee Report` tab card subtitles duplicate their titles** — confirm final
   copy (§3.4).
4. **`Loan and Advance` has no reference screen anywhere** — confirm it is in scope for this
   delivery round or a later one, since everything else here has at least one screen.
5. **PF/ESI/TDS vs a single "Deductions" figure** — confirm the single figure is always exactly
   their sum, on every screen that shows only the aggregate (§7.2).
6. **Salary Preview's blue-title-bar chrome vs the other two modals' plain chrome** — confirm one
   modal shell for the whole module (§8).
7. **Reference No. on the Transfer to Bank modal** — confirm whether it is server-generated,
   display-only, or user-editable (§9.2).
8. **"Download Advice" vs "Execute Transfer"** — confirm which one is the system of record for a
   completed transfer, and whether a live bank/payment-gateway integration exists at all (§9.2).
