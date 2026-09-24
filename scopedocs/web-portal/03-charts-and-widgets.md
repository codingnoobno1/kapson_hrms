# Charts and widgets

## Part 1 — Charts

Nine distinct visualisations. Each names the series it draws, the shape that feeds it, and the
rule that keeps it honest.

| ID | Chart | Type | Ref | Series | Feed |
|---|---|---|---|---|---|
| G-01 | Leave Category Wise Distribution | Donut + side legend with values | W04 | One arc per leave policy | `LeaveCategorySliceDto[]` |
| G-02 | Department Wise Leave Usage | Grouped vertical bars, 2 series | W04 | Used Leaves, Available Leaves | `DepartmentLeaveUsageDto[]` |
| G-03 | Monthly Leave Trend | Area/line with markers, 6 points | W05 | Total leave days | `LeaveTrendPointDto[]` |
| G-04 | Leave Status Overview | Pie, 3 slices | W05 | Approved, Pending, Rejected | `LeaveStatusSplitDto` |
| G-05 | Leaves by Leave Type | Horizontal progress bars with `used/entitled` | W06 | One bar per policy | `LeaveTypeUsageDto[]` |
| G-06 | Leave Usage by Date | Stacked vertical bars, one stack per working day | W06 | One series per leave type | `LeaveUsageByDateDto[]` |
| G-07 | Utilisation bar | Inline mini progress bar in a table cell | W04 | Single value 0–100 | `DepartmentLeaveBalanceRowDto.UtilisationPercent` |
| G-08 | Attendance % bar | Inline progress bar with a band dot | W08 | Single value 0–100 | `EmployeeAttendanceRowDto` |
| G-09 | Year-end summary cards | Numeric card cluster (not strictly a chart) | W05 | Count, total days, avg days | `YearEndDepartmentDto[]` |

### Rules these charts must follow

**G-04 is wrong in the prototype and must be fixed, not copied.** The pie is drawn from leave
*status* (Approved / Pending / Rejected) while its legend lists leave *categories* (Casual 480,
Earned 520, Sick 380). Two different dimensions in one widget. Pick one: either a status pie with
status counts in the legend, or drop it because G-01 already covers categories.

**G-01 and G-05 must be generated from active policies, not hard-coded.** The prototype names
four categories in the donut and six in the bars. The platform supports any number of policies
per tenant, so both charts iterate whatever `GetPoliciesAsync` returns.

**Half Day is not a leave type.** G-05 lists it beside Casual and Earned, but the platform models
a half day as a `0.5` portion of *any* policy that permits it. Rendering it as a peer
double-counts the same day. Either report portions separately or exclude it from the type bars.

**G-06 plots working days only.** Its axis in W06 runs Jan 1, 2, 3, 5, 6, 7 … skipping weekends.
The axis is therefore *working days in the month*, which means it depends on the holiday
calendar, not just on weekday arithmetic.

**Colour carries meaning and must be consistent with the mobile apps.** The employee app already
fixes green = present, rose = absent, amber = half day, blue = holiday. Status colours here
follow the same palette: green approved, amber pending, rose rejected.

**Every chart needs an empty state and a "no data yet" state**, and they are different: a
department with zero leave taken is a real answer; a department that has never been configured is
not.

---

## Part 2 — Widget inventory

`◆` visible in a reference · `○` required but not shown in any reference.

### Shell and chrome

| ID | Widget | Ref | Notes |
|---|---|---|---|
| W-01 | Collapsed icon rail | ◆ W01 | ~13 icons, hamburger to expand |
| W-02 | Expanded navigation drawer | ○ | The rail must have a labelled state |
| W-03 | Gradient page header | ◆ all | Title, subtitle, current date, last login, bell, profile |
| W-04 | Current-date chip | ◆ | `CURRENT: Jan 8, 2026` |
| W-05 | Last-login chip | ◆ | `LAST LOGIN: Dec 29, 6:30 PM` — a security signal, keep it |
| W-06 | Notification bell with badge | ◆ | Count 5 |
| W-07 | Profile menu | ◆ | Avatar, name, role, chevron |
| W-08 | Scope filter bar | ◆ W01 | Company select, unit select, date, Apply |
| W-09 | Historical-view banner | ○ | Required when as-of ≠ today |
| W-10 | Breadcrumb | ○ | |

### Data display

| ID | Widget | Ref | Notes |
|---|---|---|---|
| W-11 | Headline KPI card | ◆ W01 | Left colour rule, icon, label, big number, sub-caption |
| W-12 | KPI strip | ◆ W01 | Six across, must wrap |
| W-13 | Policy card | ◆ W01 | Icon, title, updated date, excerpt, read/unread chip |
| W-14 | Read/Unread chip | ◆ W01 | |
| W-15 | Policy modal | ◆ W02 | Headed sheet, sections, acknowledgement footer |
| W-16 | Bulleted policy section | ◆ W02 | Heading + points |
| W-17 | Acknowledgement footer | ◆ W02 | Confirmation text + Cancel / Already Read |
| W-18 | Tabbed list card | ◆ W01 | Today / Belated / Upcoming |
| W-19 | Person moment row | ◆ W01 | Name, dept • designation • turning/years, date chip |
| W-20 | "View More" link | ◆ W01 | |
| W-21 | Data table | ◆ W03,W04,W07,W08 | Sticky blue header, zebra-free, dense |
| W-22 | Sortable column header | ○ | Nine-column tables are unusable without it |
| W-23 | Table pagination | ○ | |
| W-24 | Column chooser | ○ | |
| W-25 | Inline progress bar cell | ◆ W04,W08 | |
| W-26 | Status dot + label | ◆ W08 | 🟢 Good / 🟡 Fair |
| W-27 | Type chip | ◆ W03,W08 | HOLIDAY, CHECK-IN, CHECK-OUT, DAY PASS, MULTI-DAY PASS |
| W-28 | Duration formatter | ◆ W03 | `8 hrs 45 min`, `32 min` |
| W-29 | Time formatter | ◆ | `09:32 AM` |
| W-30 | Ratio label | ◆ W06 | `48/74` |
| W-31 | Department summary card | ◆ W05 | Name, count badge, metrics, View Details |
| W-32 | Month pill selector | ◆ W06 | 12 pills with ← → paging |
| W-33 | Card section heading with emoji | ◆ all | |

### Charts

| ID | Widget | Ref | Notes |
|---|---|---|---|
| W-34 | Donut chart with value legend | ◆ W04 | |
| W-35 | Grouped bar chart | ◆ W04 | |
| W-36 | Area/line trend chart | ◆ W05 | |
| W-37 | Pie chart with legend | ◆ W05 | |
| W-38 | Horizontal progress bar chart | ◆ W06 | Value label inside the bar |
| W-39 | Stacked bar chart | ◆ W06 | |
| W-40 | Chart legend (interactive) | ◆ | Clicking a series should toggle it |
| W-41 | Chart empty state | ○ | |
| W-42 | Chart tooltip | ○ | |

### Interaction and feedback

| ID | Widget | Ref | Notes |
|---|---|---|---|
| W-43 | Primary / secondary button | ◆ | |
| W-44 | Modal dialog | ◆ W02 | |
| W-45 | Confirmation dialog | ○ | For approve/reject |
| W-46 | Approve / Reject action pair | ○ | Rejection requires a reason |
| W-47 | Toast / snackbar | ○ | |
| W-48 | Inline validation message | ○ | |
| W-49 | Loading skeleton — card | ○ | |
| W-50 | Loading skeleton — table | ○ | |
| W-51 | Loading skeleton — chart | ○ | |
| W-52 | Error + retry panel | ○ | Per widget, not per page: one failing chart must not blank the dashboard |
| W-53 | Empty state | ○ | |
| W-54 | Permission-gated section | ○ | Renders nothing when the section is `null` |
| W-55 | Export menu | ○ | CSV / Excel / PDF on every table |
| W-56 | Date range picker | ○ | |
| W-57 | Multi-select filter | ○ | Department, designation |
| W-58 | Search box | ○ | |
| W-59 | Drill-through link | ◆ W01 | "View All" on Pending Approval |
| W-60 | Row detail drawer | ○ | |
| W-61 | Bulk selection toolbar | ○ | Approving 11 requests one at a time is the wrong shape |
| W-62 | Print stylesheet | ○ | |

**62 widgets: 33 evidenced, 29 required but not shown.**

The gap is concentrated in exactly the places prototypes usually skip — loading, empty, error,
permissions, sorting, paging, export, and bulk actions. Those are most of the real build.
