# Employee App — Widgets and Screen Mapping

## 1. Purpose

This document defines the reusable component system for the employee app and maps every widget
to the screens that use it.

A **screen** is a routed page or a bottom sheet with a ViewModel and a use case. A **widget** is
a reusable visual component that receives bindable values and commands. Widgets do not navigate,
call APIs, touch SQL Server, or own business rules.

**68 widgets** support the **37 screens** in `employee-app.md`.

This is a separate set from `Vispl.HRManagement.Mobile/widgets.md`. The two apps share design
thinking and token *names*, not token *values* and not controls. The HR app is blue and
card-dense; this app is coral and sheet-driven. Do not import controls across.

## 2. Widget engineering contract

1. Expose typed `BindableProperty` values, `ICommand` properties, and optional command parameters.
2. Bind with compiled bindings and a declared `x:DataType`.
3. Raise no domain events. A gesture invokes a ViewModel command supplied by the parent.
4. Never resolve an API client, repository, database, or session.
5. Render loading, empty, error, disabled, offline and not-ready states where applicable.
6. Consume theme tokens by semantic name. Only §3 carries literals.
7. Supply `AutomationProperties.Name`, hints, heading levels, focus order and automation IDs.
8. Keep touch targets at 44×44 logical pixels minimum and tolerate dynamic text without clipping.
9. Avoid nested vertical scrolling. Lists use `CollectionView` with incremental loading.
10. Cancel image work when recycled. Never log PII.

Prefer composition — `ContentView` over standard controls. Reach for `TemplatedView`, handlers
or platform code only where measurement or native capability requires it.

## 3. Design tokens

Every value below was **sampled from the PNGs in `scopedocs/app2`**, not chosen. Where a widget
needs a colour, it uses the semantic name; only this section carries literals.

### 3.1 Brand and surface

| Token | Value | Sampled from |
|---|---|---|
| `Color.BrandAccent` | `#FF6153` | identity card (5), profile header (2), FAB, primary buttons (6, 7) |
| `Color.BrandAccentSoft` | `#FFB0A9` | pressed and disabled accent states (7) |
| `Color.BrandAccentTint` | `#FFF4F3` | payment details plate (3) |
| `Color.OnAccent` | `#FFFFFF` | any label on an accent fill |
| `Color.Background` | `#FFFFFF` | page ground, all screens |
| `Color.Surface` | `#FFFFFF` | card and sheet fill |
| `Color.SurfaceMuted` | `#F6F6F6` | field plates (2), component rows (3), secondary buttons (7) |
| `Color.CalendarGround` | `#F7F7F7` | calendar grid ground (7) |
| `Color.TextPrimary` | `#111827` | titles and values |
| `Color.TextSecondary` | `#8C8C8C` | subtitles, meta, placeholders |
| `Color.Border` | `#F2F2F2` | card hairline |

### 3.2 Home KPI tile gradients

The home tiles are the only gradient surfaces in the app. Each is a soft diagonal.

| Token | From → To | Tile | In use |
|---|---|---|---|
| `Gradient.KpiRose` | `#FDE9E8` → `#F8EBE6` | Present Days | yes |
| `Gradient.KpiPeriwinkle` | `#C9CFFF` → `#DFE4FA` | Leave Balance | yes |
| `Gradient.KpiMint` | `#C7EFDA` → `#D8F2E4` | third slot | reserved |
| `Gradient.KpiPeach` | `#FCD8AB` → `#F8ECDA` | fourth slot | reserved |

The home grid is **two tiles**, not four — Active Projects and Performance were dropped
(`employee-app.md` §9.1). Mint and Peach stay in the table because they are measured values
from `5.png` and re-deriving them later is waste; they are the palette a third or fourth tile
takes if one is ever added.

Tiles are assigned by **position**, not by key, so a tile that changes meaning keeps the grid's
colour rhythm.

### 3.3 Status palette

`StatusDto.Tone` and `KpiDto.Tone` are semantic strings from the server. This table is the only
place a tone becomes a colour.

| Tone | Token | Value | Seen as |
|---|---|---|---|
| `success` | `Color.StatusSuccess` | `#84D382` | On Time (6), Approved (4) |
| `warning` | `Color.StatusWarning` | `#F6A549` | Half Day (6) |
| `pending` | `Color.StatusPending` | `#FBDD59` | Leave (6) |
| `danger` | `Color.StatusDanger` | `#FF6153` | Absent (6), Rejected |
| `info` | `Color.StatusInfo` | `#779ADA` | Holiday |
| `muted` | `Color.StatusMuted` | `#B8B8B8` | unavailable tile, disabled action |

`muted` is the tone a KPI tile carries when its module is absent. A widget receiving `muted`,
or a `KpiDto` with a non-null `UnavailableReason`, **must** render the unavailable treatment, never a number.

### 3.4 Attendance calendar cells

| Token | Value | Day state | `StatusDto.Code` |
|---|---|---|---|
| `Color.CalPresent` | `#9DD697` | present | `Present` |
| `Color.CalHalfDay` | `#F6A549` | half day | `Partial` |
| `Color.CalAbsent` | `#EC968F` | absent | `Absent` |
| `Color.CalHoliday` | `#7397D9` | holiday | `Holiday` |
| `Color.CalWeekend` | `#F8E27E` | weekend | `Weekend` |
| `Color.CalLeave` | `#FBDD59` | approved leave | `Leave` |
| `Color.CalFuture` | `#F7F7F7` | not yet occurred | — |

### 3.5 Money accents

| Token | Value | Use |
|---|---|---|
| `Color.MoneyPositive` | `#78D17B` | salary hero card (3) |
| `Color.MoneyOnPositive` | `#FFFFFF` | figures on the hero |

### 3.6 Shape, type and motion

| Token | Value |
|---|---|
| `Radius.Card` | 14 |
| `Radius.Sheet` | 22 (top corners only) |
| `Radius.Pill` | 999 |
| `Radius.Tile` | 18 |
| `Radius.CalendarCell` | 8 |
| `Space.Gutter` | 16 |
| `Space.CardGap` | 12 |
| `Space.SectionGap` | 24 |
| `Font.SectionHeader` | 12, semibold, letter-spaced, `TextSecondary`, uppercase |
| `Font.RowTitle` | 15, semibold, `TextPrimary` |
| `Font.RowSubtitle` | 12, regular, `TextSecondary` |
| `Font.TileValue` | 30, semibold, `TextPrimary` |
| `Font.TileLabel` | 12, regular, `TextSecondary` |
| `Font.SheetTitle` | 16, semibold |
| `Motion.SheetIn` | 240 ms, decelerate |
| `Motion.TilePress` | 90 ms, scale 0.97 |

## 4. Widget catalogue

68 widgets. IDs are `EW-nnn` and are stable.

### 4.1 Shell and chrome — 8

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-001 | `BrandAppBar` | KAPSONS wordmark, circular search, circular bell with count badge | Fixed on every tab root |
| EW-002 | `NotificationBell` | Bell glyph, red count badge, tap command | Badge hidden at zero |
| EW-003 | `CircleIconButton` | 40pt circle, glyph, ripple | Base for EW-002 and search |
| EW-004 | `EmployeeTabBar` | Four labelled destinations, active tint `BrandAccent` | Home, Attendance, Leave, Menu |
| EW-005 | `RaisedActionButton` | 56pt accent circle, overlaps the bar, elevation | The FAB; action per §9.3 of scope |
| EW-006 | `QuickActionSheet` | Grid of labelled actions | Opened by EW-005 |
| EW-007 | `PageHeader` | Back chevron, title | Pushed pages only (EMP-020, 025) |
| EW-008 | `SafeAreaHost` | Insets for notch and home indicator | Wraps every root |

### 4.2 Sheets — 5

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-010 | `BottomSheetHost` | Scrim, drag handle, snap points, dismiss | The app's primary detail surface |
| EW-011 | `SheetHeader` | Title left, X close right, hairline under | On every sheet |
| EW-012 | `SheetScrollBody` | Single scroll region with bounce | Prevents nested scrolling |
| EW-013 | `SheetActionBar` | Sticky footer, one primary and one secondary | Apply Leave, decisions |
| EW-014 | `SheetHeroPlate` | Full-bleed tinted plate under the header | Profile header (2), salary hero (3) |

### 4.3 Menu list — 4

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-020 | `MenuSectionHeader` | Emoji, uppercase label | MY PROFILE, LEAVE MANAGEMENT, GATE PASS |
| EW-021 | `MenuRow` | Icon tile, title, subtitle, chevron | The Menu tab's only row type |
| EW-022 | `MenuRowIcon` | 32pt rounded tinted square with a glyph | Tint follows section |
| EW-023 | `MenuGroup` | Section header plus its rows, with group spacing | Composes EW-020 and EW-021 |

### 4.4 Identity and KPI — 6

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-030 | `IdentityCard` | Accent plate, avatar, name, title, department, phone, QR | Binds `EmployeeIdentityCardDto` |
| EW-031 | `IdentityQr` | QR render of `QrPayload`, employee number caption | See scope §9.6 before scanning use |
| EW-032 | `Avatar` | Circular image, initials fallback, optional presence dot | |
| EW-033 | `KpiTile` | Gradient ground, icon chip, label, value, optional unit | Binds `KpiDto` |
| EW-034 | `KpiTileGrid` | Two-column responsive grid | Home: 2 tiles |
| EW-035 | `UnavailableTileOverlay` | Em dash, muted tone, description from `KpiDto.UnavailableReason` | **Required** when it is non-null |

### 4.5 Stats and status — 5

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-040 | `StatTile` | White card, coloured circular icon, big value, label | Attendance: 4 tiles |
| EW-041 | `StatTileGrid` | Two-column grid | EMP-012, EMP-020 |
| EW-042 | `StatusPill` | Filled pill, tone-coloured, 11pt label | Binds `StatusDto.Tone` via §3.3 |
| EW-043 | `TonedIconChip` | Circular tinted glyph | Inside EW-040 |
| EW-044 | `TrendBadge` | Arrow and delta | Binds `KpiDto.Trend`; hidden when null |

### 4.6 Attendance — 8

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-050 | `AttendanceDayRow` | Status glyph, date, check-in/out summary, status pill | Binds `AttendanceDayDto` |
| EW-051 | `AttendanceDayList` | Grouped `CollectionView` with incremental load | EMP-012 |
| EW-052 | `MonthStepper` | Back chevron, month label, forward chevron | Forward disabled beyond current month |
| EW-053 | `AttendanceCalendar` | 7-column month grid, coloured cells, today ring | Binds a month of `AttendanceDayDto` |
| EW-054 | `CalendarCell` | Rounded square, day number, state fill | Colours from §3.4 |
| EW-055 | `CalendarLegend` | Swatch and label per state | Required — colour alone is not accessible |
| EW-056 | `PunchButton` | Large accent action, live clock, in/out state | Binds `PunchStateDto`; blocked on scope §9.2 |
| EW-057 | `PunchEvidenceNotice` | Location and device disclosure line | Must appear wherever EW-056 does |

### 4.7 Leave — 7

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-060 | `LeaveHistoryRow` | Type icon, type name, date range, day count, status | Binds `LeaveRequestListItemDto` |
| EW-061 | `LeaveBalanceCard` | Policy name, available, used, pending, accrual note | Binds `LeaveBalanceDto` |
| EW-062 | `LeaveTypePicker` | Segmented or list picker over policies | Binds `LeavePolicySummaryDto` |
| EW-063 | `DateRangeField` | From and to, half-day toggles, inline validation | Never computes day count |
| EW-064 | `CalculatedDaysPanel` | Server-returned chargeable days plus balance impact | Binds `LeaveCalculationDto`; read-only |
| EW-065 | `ApproverStrip` | Avatar and name of the next approver | Binds `PersonReferenceDto` |
| EW-066 | `DecisionBar` | Approve, Reject, Return, with reason capture | `LEAVE.APPROVE` only |

### 4.8 Record and money — 6

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-070 | `FieldRow` | Label over value on a muted plate | The profile sheet's only row type |
| EW-071 | `FieldCard` | Grouped `FieldRow` set with dividers | Binds a DTO section |
| EW-072 | `MaskedFieldRow` | Masked value with a reveal affordance | Bank and compensation; honours `FieldAccessDto` |
| EW-073 | `MoneyHeroCard` | Green plate, annual figure, monthly subline | Binds `MoneyDto`; salary sheet |
| EW-074 | `PayComponentRow` | Component name and amount, zebra plate | Binds `PayComponentDto` |
| EW-075 | `PaymentTermsCard` | Tinted plate, emoji-led frequency, mode, pay date | Needs the §6.2 contract extension |

### 4.9 Passes and movement — 4

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-080 | `GatePassCard` | Type, window, reason, status pill, actions | Binds `GatePassRowDto` |
| EW-081 | `GatePassCredentialCard` | Large QR, validity window, live countdown | Binds `GatePassCredentialDto`; memory only |
| EW-082 | `MovementRow` | Out time, expected return, overdue flag | Binds `MovementRowDto` |
| EW-083 | `TimeWindowField` | From and to time entry with duration readout | Gate pass and movement |

### 4.10 Forms and feedback — 9

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-090 | `LabeledEntry` | Label, entry, helper, error | |
| EW-091 | `LabeledPicker` | Label, picker over `MasterReferenceDto` | Never shows a raw code |
| EW-092 | `ReasonEditor` | Multiline entry with character counter | Required by `RequiresReason` |
| EW-093 | `AttachmentPicker` | Add, list, remove; size and type guards | |
| EW-094 | `PrimaryButton` | Accent fill, busy spinner, disabled state | Binds `ActionAvailabilityDto` |
| EW-095 | `SecondaryButton` | Muted fill, dark label | Download report, View Full Month |
| EW-096 | `InlineWarningStrip` | Renders `ApiMetaDto.Warnings` | Non-blocking |
| EW-097 | `ConfirmDialog` | Title, body, confirm, cancel | Withdraw, reject, sign out |
| EW-098 | `ToastHost` | Transient confirmation | Never used for errors |

### 4.11 State — 6

| ID | Widget | Anatomy | Notes |
|---|---|---|---|
| EW-100 | `LoadingSkeleton` | Shimmer shaped like the content it replaces | Never a bare spinner on a list |
| EW-101 | `EmptyState` | Glyph, headline, one-line explanation, optional action | |
| EW-102 | `ErrorState` | Explanation, retry, correlation id on long-press | Correlation id from `ApiMetaDto` |
| EW-103 | `FeatureNotReadyPanel` | Calm panel naming the module | Binds the 501 `ProblemDetails` |
| EW-104 | `PermissionDeniedPanel` | Explains the missing permission | 403 |
| EW-105 | `OfflineBanner` | Sticky strip, last-updated time | Shows what is cached and how old |

`EW-103` is not decoration. It is the difference between "Salary is not available yet" and a
crash report, and roughly a quarter of this app's surface depends on it during Phase 1–3.

## 5. Screen-to-widget mapping

Every screen in `employee-app.md` §6, with its widgets and its binding source. `n/r` marks a
screen whose endpoint is not ready.

### 5.1 Bootstrap and session

| Screen | Widgets | Binds |
|---|---|---|
| EMP-001 Splash | EW-008, EW-100 | `session/bootstrap` |
| EMP-002 Sign in | EW-008, EW-090, EW-094, EW-102 | `auth/token` |
| EMP-003 Tenant resolve | EW-090, EW-094 | `discovery/resolve-tenant` |
| EMP-004 Biometric unlock | EW-094, EW-097 | local, then `auth/refresh` |
| EMP-005 Forced update | EW-101, EW-094 | `discovery/metadata` |

### 5.2 Tab roots

| Screen | Widgets | Binds |
|---|---|---|
| EMP-010 Home | EW-001, EW-002, EW-004, EW-005, EW-030, EW-031, EW-032, EW-033, EW-034, EW-035, EW-044, EW-100, EW-105 | `me/summary` |
| EMP-012 Attendance | EW-001, EW-004, EW-040, EW-041, EW-042, EW-043, EW-050, EW-051, EW-095, EW-100, EW-105 | `me/kpis/attendance`, `attendance/me` |
| EMP-013 Menu | EW-001, EW-004, EW-020, EW-021, EW-022, EW-023 | permissions from session |
| EMP-014 Leave | EW-001, EW-004, EW-061, EW-060, EW-042, EW-094, EW-100, EW-101 | `me/kpis/leave`, `leave/balances`, `leave/requests` |

### 5.3 Attendance

| Screen | Widgets | Binds |
|---|---|---|
| EMP-020 Full Month | EW-007, EW-052, EW-040, EW-041, EW-053, EW-054, EW-055, EW-095, EW-094 | `me/kpis/attendance`, `attendance/me` |
| EMP-021 Day detail | EW-010, EW-011, EW-070, EW-071, EW-042, EW-094 | one `AttendanceDayDto` |
| EMP-022 Punch | EW-010, EW-011, EW-056, EW-057, EW-094, EW-098 | `attendance/punch` |
| EMP-023 Correction | EW-010, EW-011, EW-083, EW-092, EW-093, EW-013, EW-094 | `attendance/corrections` POST |
| EMP-024 My corrections | EW-010, EW-011, EW-042, EW-101, EW-097 | `attendance/corrections` GET |
| EMP-025 Analytics `n/r` | EW-007, EW-103 | undefined — scope §9.4 |

### 5.4 Leave

| Screen | Widgets | Binds |
|---|---|---|
| EMP-030 Apply Leave | EW-010, EW-011, EW-062, EW-063, EW-092, EW-093, EW-064, EW-013, EW-094 | `leave/policies`, `leave/requests/calculate` |
| EMP-031 Review | EW-010, EW-011, EW-064, EW-065, EW-071, EW-013, EW-094 | `leave/requests` POST |
| EMP-032 Leave History | EW-010, EW-011, EW-060, EW-042, EW-101, EW-100 | `leave/requests` |
| EMP-033 Request detail | EW-010, EW-011, EW-071, EW-042, EW-065, EW-092, EW-094, EW-097 | `leave/requests/{id}` |
| EMP-035 Pending approval | EW-010, EW-011, EW-060, EW-042, EW-101, EW-104 | `leave/team/requests` |
| EMP-036 Decision | EW-010, EW-011, EW-071, EW-066, EW-092, EW-097 | `leave/requests/{id}/decision` |

### 5.5 Profile and record

| Screen | Widgets | Binds |
|---|---|---|
| EMP-040 Profile | EW-010, EW-011, EW-014, EW-032, EW-070, EW-071, EW-094 | `employees/{id}` + `/personal` + `/organisation` |
| EMP-041 Update Profile | EW-010, EW-011, EW-090, EW-091, EW-013, EW-094, EW-096 | `employees/{id}/change-requests` |
| EMP-042 Onboarding `n/r` | EW-010, EW-011, EW-042, EW-103 | `me/onboarding` — see backend §6.3 |
| EMP-043 Salary `n/r` | EW-010, EW-011, EW-073, EW-074, EW-075, EW-072, EW-103 | `payroll/salary-structure` |
| EMP-044 Bank `n/r` | EW-010, EW-011, EW-072, EW-071, EW-103 | new module — backend §6.1 |

### 5.6 Gate pass and movement

| Screen | Widgets | Binds |
|---|---|---|
| EMP-050 Apply Gate Pass | EW-010, EW-011, EW-091, EW-083, EW-092, EW-013, EW-094 | `gate-passes` POST |
| EMP-051 My gate passes | EW-010, EW-011, EW-080, EW-042, EW-101, EW-097 | `gate-passes/me` |
| EMP-052 Credential | EW-010, EW-011, EW-081 | `gate-passes/{id}/credential` |
| EMP-053 Movement | EW-010, EW-011, EW-082, EW-083, EW-094 | `movement/me`, `movement/out` |
| EMP-054 Pending passes | EW-010, EW-011, EW-080, EW-066, EW-104 | `gate-passes/pending` |

### 5.7 Shell services

| Screen | Widgets | Binds |
|---|---|---|
| EMP-060 Notifications | EW-007, EW-042, EW-101, EW-100 | `notifications` |
| EMP-061 Notification detail | EW-010, EW-011, EW-071, EW-094 | `notifications/{id}` |
| EMP-062 Search `n/r` | EW-090, EW-103 | `search` |
| EMP-063 Quick actions | EW-005, EW-006 | local |
| EMP-064 Settings `n/r` | EW-070, EW-103 | `profile/preferences` |
| EMP-065 Sign out / devices | EW-071, EW-097, EW-103 | `auth/revoke`, `devices` |

## 6. Widget reuse

| Widget | Screens |
|---|---:|
| EW-010 `BottomSheetHost`, EW-011 `SheetHeader` | 19 |
| EW-094 `PrimaryButton` | 15 |
| EW-042 `StatusPill` | 11 |
| EW-071 `FieldCard`, EW-070 `FieldRow` | 9 |
| EW-103 `FeatureNotReadyPanel` | 6 |
| EW-101 `EmptyState` | 6 |

The sheet host is the highest-leverage control in the app and the one MAUI does not give you.
Build it first and build it properly: snap points, drag-to-dismiss, scrim tap, keyboard
avoidance, and a single scroll region. Every other widget assumes it works.

## 7. Build order

1. **EW-010 to EW-014** — the sheet system. Nineteen screens are blocked until it exists.
2. **EW-001 to EW-008** — chrome and tabs. Makes the app navigable.
3. **EW-100 to EW-105** — the six states. Build these before any content widget, so no screen
   is ever written with only a happy path.
4. **EW-030 to EW-035, EW-040 to EW-044** — identity and tiles. Completes EMP-010 and EMP-012.
   The home grid is two tiles; EW-034 lays out two columns and one row.
5. **EW-050 to EW-055** — attendance list and calendar. Completes EMP-020.
6. **EW-020 to EW-023** — the menu. Cheap, and it makes the app feel whole.
7. **EW-060 to EW-066, EW-090 to EW-098** — leave and forms. Phase 2.
8. **EW-080 to EW-083** — passes. Phase 3.
9. **EW-070 to EW-075** — record and money. Phase 4, behind the payroll module.

## 8. Accessibility floor

- Colour never carries meaning alone. Every calendar state and every status pill pairs its
  colour with a glyph or a label; `EW-055` exists for this reason.
- `StatusDto.Name` is the accessible label for `EW-042`; do not announce the tone.
- The identity card announces name, title and department as one label, not four.
- KPI tiles announce as "label, value, unit". An unavailable tile announces `KpiDto.UnavailableReason`.
- Every sheet traps focus, announces its title as a heading, and closes on the platform's back
  gesture as well as the X control.
- Text scales to 200% without clipping. Tiles grow; they do not truncate their value.
