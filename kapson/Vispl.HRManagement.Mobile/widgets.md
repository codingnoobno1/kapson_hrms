# HR Management Mobile — Reusable Widget Plan

## 1. Purpose

This document defines the reusable visual and behavioural component system for the .NET MAUI HR Management application. It contains **108 planned widgets** supporting the 112 screens in `screens.md`.

In this plan, a **screen** is a routed page with a ViewModel and use case. A **widget** is a reusable visual component that receives bindable values and commands. Widgets do not navigate by themselves, call APIs, access SQL Server, or own business rules.

## 2. Design Direction from the References

The approved visual language is defined by the nine reference screens in
`docs/screen-references/`. Those images are the authority for colour, spacing and
component anatomy. Where this document and a reference screen disagree, the screen wins.

| File | Screen | Screen ID |
|---|---|---|
| `01-hr-dashboard.png` | HR Dashboard | REF-01 |
| `02-manage-jobs.png` | Manage Jobs — Jobs / Applicants / Shortlisted | REF-02 |
| `03-job-details.png` | View Application Details sheet | REF-03 |
| `04-interview-schedule.png` | Interview Schedule | REF-04 |
| `05-navigation-drawer.png` | Navigation drawer | REF-05 |
| `06-applicant-list.png` | Stall Applicants | REF-06 |
| `07-applicant-details.png` | Full Application Details sheet | REF-07 |
| `08-notifications-general.png` | Notifications — General | REF-08 |
| `09-notifications-interviews.png` | Notifications — Interview Schedule | REF-09 |

The following patterns are established by the references and are mandatory:

- A white app header carrying the HR Management logo on the left, and on the right a circular
  search button plus a circular notification bell with a blue count badge.
- Bottom navigation with exactly four labelled destinations — Home, Manage, Interview Status,
  Menu — and a raised circular action button centred on and overlapping the bar.
- Cards are white, softly rounded, hairline-bordered, and separated by whitespace rather than
  by rules or dividers.
- Status is always a filled pill in the top-right corner of its card, colour-coded by state.
- Interview cards are the only tinted surface: a pale blue plate with a solid blue accent bar
  down the left edge.
- Detail views open as a rounded bottom sheet with a title row and an X close control. They are
  not full-screen pages.
- Sheet content is grouped under emoji-prefixed section headers, with facts shown as
  colon-aligned key/value rows inside bordered cards.
- Segmented pill tabs switch between peer views. Outline filter chips narrow a list.
- Destructive and secondary actions are small coloured icon buttons, not text menus.

**Superseded.** Earlier revisions of this document specified a navy, white and gold enterprise
theme and instructed that it replace "screen-specific hard-coded colours". That direction is
withdrawn. The measured palette in section 4 is now authoritative.

## 3. Widget Engineering Contract

Every widget follows these rules:

1. Expose typed `BindableProperty` values, `ICommand` properties, and optional command parameters.
2. Bind visual state with compiled bindings and a declared `x:DataType`.
3. Raise no domain events directly. A user gesture invokes a ViewModel command supplied by the parent.
4. Never resolve an API client, repository, database, navigation service, or user session.
5. Support loading, empty, error, disabled, selected, focused, and offline visual states where applicable.
6. Use `DynamicResource` theme tokens and semantic colours. Do not bind tenant data directly into styles.
7. Supply `AutomationProperties.Name`, hints, semantic heading levels, predictable focus order, and automation IDs.
8. Maintain at least 44x44 logical-pixel touch targets and support dynamic text without clipping.
9. Avoid nested vertical scrolling. Lists use `CollectionView` and incremental loading.
10. Cancel image/document work when recycled, and never log PII or sensitive values.

Prefer composition (`ContentView` plus standard controls) for application widgets. Use `TemplatedView`, handlers, or platform code only where measurement, native capability, or performance requires it.

## 4. Design Tokens

The values below were sampled from the reference PNGs, not chosen independently. Widgets consume
the semantic name; only this table carries literals.

### 4.1 Brand and surface

| Token | Value | Sampled from |
|---|---|---|
| `Color.BrandPrimary` | `#3375EA` | header actions, drawer active row, primary buttons (all screens) |
| `Color.BrandPrimaryDeep` | `#3469D5` | on-card primary buttons, Inactive pill (REF-02) |
| `Color.BrandTint` | `#EBF1FD` | interview card plate (REF-01, REF-04, REF-09) |
| `Color.AccentCyan` | `#27EBFF` | Invite Interview button (REF-06) |
| `Color.OnBrand` | `#FFFFFF` | label on any brand fill |
| `Color.Background` | `#FFFFFF` | page ground (all screens) |
| `Color.Surface` | `#FFFFFF` | card fill |
| `Color.Border` | `#F2F2F2` | card hairline |
| `Color.Divider` | `#F4F4F4` | section rules inside sheets |
| `Color.TextPrimary` | `#111827` | titles, values |
| `Color.TextSecondary` | `#8C8C8C` | subtitles, meta, placeholders |
| `Color.AvatarPlaceholder` | `#D9D9D9` | applicant avatar fallback (REF-06) |

### 4.2 Status palette

Every status pill draws from this set. No other colours are permitted for state.

| Token | Value | States |
|---|---|---|
| `Color.StatusSuccess` | `#7FD185` | Shortlisted |
| `Color.StatusInfo` | `#3375EA` | Past Interview |
| `Color.StatusInactive` | `#3469D5` | Inactive |
| `Color.StatusActive` | `#00CEFF` | Active |
| `Color.StatusWarning` | `#F6A549` | Draft |
| `Color.StatusPending` | `#EACB2F` | Pending Review |
| `Color.StatusDanger` | `#D33425` | Expired, Rejected |

### 4.3 Action-button palette

| Token | Value | Use |
|---|---|---|
| `Color.ActionView` | `#3469D5` | view / eye icon button |
| `Color.ActionEdit` | `#73BA78` | edit / pencil icon button |
| `Color.ActionDelete` | `#848484` | delete / trash icon button |

### 4.4 KPI gradients

Vertical, top to bottom. Used only by `KpiGradientCard`.

| Token | Stops |
|---|---|
| `Gradient.KpiBlue` | `#3375EA` → `#709EF0` |
| `Gradient.KpiCyan` | `#0FB3EF` → `#57CAF4` |
| `Gradient.KpiAmber` | `#FFC04B` → `#FFD381` |

### 4.5 Shape, spacing and type

```text
Radius.Pill    999      Radius.Card    12      Radius.Sheet   20 (top corners only)
Radius.Control  8       Radius.Chip   999      Radius.Search  24

Spacing.1  4   Spacing.2  8   Spacing.3 12   Spacing.4 16
Spacing.5 20   Spacing.6 24   Spacing.7 32   Spacing.8 40

Type.ScreenTitle   20/600      Type.CardTitle    15/600
Type.CardSubtitle  13/400      Type.Meta         12/400
Type.KpiValue      30/700      Type.PillLabel    11/600
Type.TabLabel      13/600      Type.NavLabel     11/500

Elevation.Card    y1 blur2  rgba(17,24,39,.06)
Elevation.Sheet   y-4 blur24 rgba(17,24,39,.18)
Elevation.Fab     y4 blur12 rgba(51,117,234,.40)

Touch.Minimum 44x44        Icon.Small 16   Icon.Medium 20   Icon.Large 24
```

Tenant branding may override `Color.BrandPrimary`, `Color.BrandPrimaryDeep`, `Color.BrandTint`
and the logo. It must not override the status palette, the action palette, contrast minimums,
spacing, or accessibility states.

## 5. Widget Inventory Summary

| Category | IDs | Count |
|---|---:|---:|
| Reference-verified widgets | RW-01–40 | 40 |
| Foundation primitives | WID-001–016 | 16 |
| Navigation and layout | WID-017–030 | 14 |
| Feedback and loading | WID-031–042 | 12 |
| Forms and selectors | WID-043–060 | 18 |
| Data display | WID-061–078 | 18 |
| HR and recruitment domain widgets | WID-079–098 | 20 |
| Device, offline and security | WID-099–108 | 10 |
| **Total planned** |  | **108** |

Of the 108 planned widgets, **40 now carry a verified visual specification** drawn from the
reference screens (section 6.8). Eleven of those map onto existing WID entries; 29 are
additions or redefinitions this design requires. The remaining 68 stay generic until a
reference screen exists for them.

## 6. Detailed Widget Catalogue

### 6.1 Foundation Primitives — 16 widgets

| ID | Widget | MAUI basis | Principal bindable properties | Commands/events supplied by parent | Primary use |
|---|---|---|---|---|---|
| WID-001 | `AppIcon` | `Image`/font glyph wrapper | `Source`, `SemanticName`, `Size`, `Tint`, `IsDecorative` | None | Consistent accessible icons |
| WID-002 | `AppLabel` | `Label` wrapper/style | `Text`, `TextRole`, `Tone`, `MaxLines`, `IsSensitive` | `RevealCommand` only for sensitive mode | Typography and masked values |
| WID-003 | `AppButton` | `Button`/composed content | `Text`, `Icon`, `Variant`, `IsBusy`, `IsEnabled`, `BadgeText` | `Command`, `CommandParameter` | Primary, secondary, danger and text actions |
| WID-004 | `IconButton` | `ImageButton` | `Icon`, `Label`, `Variant`, `BadgeCount`, `IsToggled` | `Command` | Search, alerts, card actions |
| WID-005 | `AppEntry` | `Entry` | `Text`, `Placeholder`, `Keyboard`, `IsPassword`, `MaxLength`, `ErrorText` | `CompletedCommand`, `TextChangedCommand` | Single-line validated input |
| WID-006 | `AppEditor` | `Editor` | `Text`, `Placeholder`, `MaxLength`, `CharacterCount`, `ErrorText` | `TextChangedCommand` | Notes and descriptions |
| WID-007 | `AppPicker` | `Picker`/sheet selector | `ItemsSource`, `SelectedItem`, `DisplayMemberPath`, `Title`, `ErrorText` | `SelectionChangedCommand` | Small option lists |
| WID-008 | `AppDatePicker` | `DatePicker` | `Date`, `MinimumDate`, `MaximumDate`, `DisplayFormat`, `ErrorText` | `DateChangedCommand` | Effective dates and date filters |
| WID-009 | `AppTimePicker` | `TimePicker` | `Time`, `MinuteInterval`, `DisplayFormat`, `ErrorText` | `TimeChangedCommand` | Interviews, punches and shifts |
| WID-010 | `AppCheckBox` | `CheckBox` + label | `IsChecked`, `Label`, `HelpText`, `IsEnabled` | `CheckedChangedCommand` | Boolean options and consent |
| WID-011 | `AppRadioGroup` | Composed `RadioButton` list | `ItemsSource`, `SelectedValue`, `Orientation`, `ErrorText` | `SelectionChangedCommand` | Exclusive decisions |
| WID-012 | `AvatarView` | `Border` + `Image`/initials | `ImageSource`, `Initials`, `DisplayName`, `Size`, `Status`, `IsSensitive` | `TappedCommand` | Employee and candidate identity |
| WID-013 | `Divider` | `BoxView` | `Orientation`, `Tone`, `Thickness`, `Inset` | None | Visual grouping |
| WID-014 | `InsetHost` | `ContentView` | `Content`, `PaddingToken`, `RespectSafeArea`, `KeyboardInsetMode` | None | Safe area and spacing consistency |
| WID-015 | `FocusRing` | `Border`/visual state wrapper | `Content`, `IsFocused`, `Tone`, `CornerRadius` | None | Keyboard and accessibility focus |
| WID-016 | `PermissionVisibility` | `ContentView` | `RequiredPermissions`, `Mode`, `FallbackContent`, `IsAuthorised` | `DeniedCommand` optional | Permission-shaped composition, never API security |

### 6.2 Navigation and Layout — 14 widgets

| ID | Widget | MAUI basis | Principal bindable properties | Commands/events supplied by parent | Primary use |
|---|---|---|---|---|---|
| WID-017 | `RoleAwareAppShell` | MAUI `Shell` composition | `MenuItems`, `ActiveRole`, `TenantBrand`, `UnreadCount`, `IsOffline` | `RoleChangedCommand`, `SignOutCommand` | Root permission-derived navigation |
| WID-018 | `AppTopBar` | `Grid`/`ContentView` | `Title`, `Subtitle`, `Logo`, `ShowBack`, `Actions`, `UnreadCount` | `BackCommand`, action commands | Consistent page header |
| WID-019 | `BottomTabBar` | Shell tab style/custom view | `Items`, `SelectedRoute`, `CenterAction`, `SafeAreaInset` | `NavigateCommand`, `CenterActionCommand` | Phone primary navigation |
| WID-020 | `FlyoutProfileHeader` | `ContentView` | `Avatar`, `DisplayName`, `RoleName`, `TenantName`, `SyncState` | `OpenProfileCommand`, `ChangeRoleCommand` | Profile flyout header |
| WID-021 | `FlyoutMenuItem` | `Grid` | `Icon`, `Text`, `Description`, `Badge`, `IsSelected`, `IsEnabled` | `Command` | Main and nested flyout actions |
| WID-022 | `PageScaffold` | `ContentView` | `Title`, `TopBar`, `Body`, `Footer`, `BackgroundTone`, `AutomationPrefix` | `RetryCommand` optional | Standard non-scrolling page frame |
| WID-023 | `ScrollPageScaffold` | `ScrollView` + scaffold | `Header`, `Sections`, `Footer`, `IsRefreshing`, `KeyboardMode` | `RefreshCommand` | Detail and form pages |
| WID-024 | `SectionCard` | `Border` | `Title`, `Subtitle`, `Icon`, `Content`, `Tone`, `IsCollapsible`, `IsExpanded` | `ToggleCommand`, `HeaderCommand` | Grouped content sections |
| WID-025 | `ResponsiveGrid` | Adaptive `Grid` | `ItemsSource`, `MinColumnWidth`, `MaxColumns`, `ItemTemplate`, `Gap` | `ItemCommand` | KPI tiles and responsive cards |
| WID-026 | `TwoPaneLayout` | Adaptive `Grid` | `Master`, `Detail`, `Breakpoint`, `IsDetailVisible`, `DetailWidth` | `CloseDetailCommand` | Tablet list/detail layouts |
| WID-027 | `StickyActionBar` | Bottom `Grid` | `PrimaryAction`, `SecondaryActions`, `Summary`, `SafeAreaInset`, `IsBusy` | Bound action commands | Persistent submit/approval actions |
| WID-028 | `SegmentedTabBar` | `CollectionView` | `ItemsSource`, `SelectedItem`, `BadgePath`, `IsScrollable` | `SelectionChangedCommand` | Jobs/applicants and notification tabs |
| WID-029 | `StepHeader` | `ProgressBar` + labels | `Steps`, `CurrentStep`, `Title`, `CanNavigateBack` | `StepSelectedCommand` | Employee/job multi-step forms |
| WID-030 | `ModalSheetHost` | Toolkit popup/native sheet adapter | `Content`, `Title`, `IsOpen`, `SnapPoints`, `CanDismiss` | `CloseCommand`, `DismissedCommand` | Short filters and decisions |

### 6.3 Feedback and Loading — 12 widgets

| ID | Widget | MAUI basis | Principal bindable properties | Commands/events supplied by parent | Primary use |
|---|---|---|---|---|---|
| WID-031 | `BusyOverlay` | Overlay `Grid` | `IsBusy`, `Message`, `IsBlocking`, `Progress` | `CancelCommand` | Blocking or cancellable work |
| WID-032 | `InlineLoader` | `ActivityIndicator` + label | `IsRunning`, `Message`, `Size` | None | Inline asynchronous work |
| WID-033 | `SkeletonBlock` | Animated `BoxView`/shimmer | `WidthRequest`, `HeightRequest`, `CornerRadius`, `IsAnimating` | None | Content placeholder |
| WID-034 | `SkeletonList` | Repeated skeleton layout | `RowCount`, `RowTemplate`, `IsVisible` | None | Initial list loading |
| WID-035 | `EmptyState` | `ContentView` | `Icon`, `Title`, `Message`, `ActionText`, `Tone` | `ActionCommand` | No items or no search results |
| WID-036 | `ErrorState` | `ContentView` | `Title`, `Message`, `CorrelationId`, `CanRetry`, `CanReport` | `RetryCommand`, `ReportCommand` | Recoverable screen errors |
| WID-037 | `OfflineBanner` | Banner `Grid` | `IsOffline`, `LastSyncedAt`, `PendingCount`, `IsLimitedMode` | `OpenSyncCommand` | Global connectivity disclosure |
| WID-038 | `SyncStatusBadge` | Badge | `State`, `PendingCount`, `LastAttemptAt`, `ErrorText` | `TappedCommand` | Draft/outbox state |
| WID-039 | `ToastHost` | Overlay host | `Messages`, `Position`, `Duration`, `MaxVisible` | `DismissCommand`, action command | Non-blocking feedback |
| WID-040 | `ConfirmationSheet` | `ModalSheetHost` | `Title`, `Message`, `ConfirmText`, `CancelText`, `Tone`, `RequireReason` | `ConfirmCommand`, `CancelCommand` | Destructive/approval confirmation |
| WID-041 | `ProgressStepper` | Composed step indicators | `Steps`, `CurrentIndex`, `CompletedIndexes`, `ErrorIndexes` | `StepSelectedCommand` | Workflow progress |
| WID-042 | `PullToRefreshContainer` | `RefreshView` | `Content`, `IsRefreshing`, `LastRefreshedAt`, `CanRefresh` | `RefreshCommand` | Standard refresh behaviour |

### 6.4 Forms and Selectors — 18 widgets

| ID | Widget | MAUI basis | Principal bindable properties | Commands/events supplied by parent | Primary use |
|---|---|---|---|---|---|
| WID-043 | `FieldContainer` | `VerticalStackLayout` | `Label`, `HelpText`, `ErrorText`, `IsRequired`, `Content`, `FieldId` | Help action optional | Uniform labels and validation |
| WID-044 | `ValidationSummary` | `CollectionView` | `Errors`, `Title`, `FocusTargetMap`, `IsVisibleWhenEmpty` | `ErrorSelectedCommand` | Form-level validation |
| WID-045 | `SearchBarEx` | `SearchBar` + actions | `Query`, `Placeholder`, `IsSearching`, `DebounceMilliseconds`, `ShowScanner` | `SearchCommand`, `ClearCommand`, `ScanCommand` | Debounced search |
| WID-046 | `FilterChip` | Toggle button | `Text`, `Count`, `IsSelected`, `IsEnabled`, `Icon` | `ToggleCommand` | One filter criterion |
| WID-047 | `FilterChipGroup` | Horizontal `CollectionView` | `ItemsSource`, `SelectedItems`, `SelectionMode`, `ShowClear` | `SelectionChangedCommand`, `ClearCommand` | List filtering |
| WID-048 | `StatusPicker` | Sheet selector | `ItemsSource`, `SelectedStatus`, `AllowedTransitions`, `ReasonRequired` | `ChangeCommand` | Permission-aware workflow states |
| WID-049 | `EmployeeLookup` | Search field + result sheet | `Query`, `SelectedEmployee`, `Scope`, `ExcludeIds`, `IsLoading` | `SearchCommand`, `SelectCommand`, `ClearCommand` | Manager, owner and employee lookup |
| WID-050 | `ManagerLookup` | Specialised employee lookup | `SelectedManager`, `OrganisationUnitId`, `EffectiveDate`, `IncludeVacancies` | `SelectCommand`, `ClearCommand` | Reporting manager assignment |
| WID-051 | `DepartmentPicker` | Searchable picker | `ItemsSource`, `SelectedDepartment`, `CompanyId`, `AllowInactive` | `LoadCommand`, `SelectionChangedCommand` | Department master binding |
| WID-052 | `LocationPicker` | Searchable picker | `ItemsSource`, `SelectedLocation`, `CompanyId`, `CountryFilter` | `LoadCommand`, `SelectionChangedCommand` | Work location master binding |
| WID-053 | `RoleMultiSelect` | Search + selected chips | `AvailableRoles`, `SelectedRoles`, `EffectiveAt`, `Conflicts` | `AddCommand`, `RemoveCommand`, `ValidateCommand` | Account/role assignment |
| WID-054 | `PermissionMatrix` | Grouped `CollectionView` | `Groups`, `SelectedPermissions`, `ReadOnlyInherited`, `SearchQuery` | `TogglePermissionCommand`, `ToggleGroupCommand` | Mobile custom-role administration if enabled |
| WID-055 | `DocumentPickerField` | File picker wrapper | `Files`, `AllowedTypes`, `MaxFiles`, `MaxBytes`, `ErrorText` | `PickCommand`, `RemoveCommand`, `PreviewCommand` | Evidence and document upload |
| WID-056 | `CameraCaptureField` | Media picker/camera wrapper | `Image`, `Purpose`, `Quality`, `RequiresRearCamera`, `ErrorText` | `CaptureCommand`, `RetakeCommand`, `RemoveCommand` | Receipt and document capture |
| WID-057 | `MoneyEntry` | `Entry` + currency selector | `Amount`, `Currency`, `Minimum`, `Maximum`, `Precision`, `IsMasked` | `ValueChangedCommand` | Compensation and claims |
| WID-058 | `PhoneEntry` | `Entry` + country code | `Number`, `CountryCode`, `IsValid`, `ErrorText` | `CountryChangedCommand`, `CompletedCommand` | Contact data |
| WID-059 | `AddressEditor` | Composed fields | `Address`, `CountryOptions`, `StateOptions`, `IsReadOnly`, `Errors` | `CountryChangedCommand`, `ValidateCommand` | Employee/candidate addresses |
| WID-060 | `DynamicFormRenderer` | Schema-driven `ContentView` | `Schema`, `Values`, `Errors`, `ReadOnlyFields`, `VisibilityContext` | `ValueChangedCommand`, `ActionCommand` | HR service requests and report filters |

### 6.5 Data Display — 18 widgets

| ID | Widget | MAUI basis | Principal bindable properties | Commands/events supplied by parent | Primary use |
|---|---|---|---|---|---|
| WID-061 | `KpiTile` | `Border` + labels/icon | `Title`, `Value`, `Icon`, `Trend`, `Tone`, `IsLoading` | `TappedCommand` | Dashboard totals |
| WID-062 | `MetricTrendCard` | Card + chart sparkline | `Title`, `Value`, `Delta`, `Series`, `Period`, `Tone` | `TappedCommand`, `PeriodChangedCommand` | Attendance/recruitment trends |
| WID-063 | `StatusBadge` | `Border` + `Label` | `Text`, `StatusCode`, `Tone`, `Icon`, `Size` | `TappedCommand` optional | Draft/active/rejected states |
| WID-064 | `InfoPairRow` | Two-column `Grid` | `Label`, `Value`, `Icon`, `IsMasked`, `CopyAllowed` | `RevealCommand`, `CopyCommand` | Detail facts |
| WID-065 | `KeyValueGrid` | Adaptive `Grid` | `ItemsSource`, `Columns`, `LabelWidth`, `ValueTemplate` | `ItemCommand` optional | Job/employee summaries |
| WID-066 | `ActionListRow` | `SwipeView`/`Grid` | `Title`, `Subtitle`, `Leading`, `Trailing`, `Actions`, `IsEnabled` | `TappedCommand`, swipe commands | General lists |
| WID-067 | `PaginatedCollection` | `CollectionView` | `ItemsSource`, `ItemTemplate`, `IsLoadingMore`, `HasMore`, `Threshold` | `LoadMoreCommand`, `ItemCommand` | Cursor-paged lists |
| WID-068 | `GroupedCollection` | Grouped `CollectionView` | `Groups`, `ItemTemplate`, `HeaderTemplate`, `IsStickyHeader` | `ItemCommand`, `GroupCommand` | Date/status grouped lists |
| WID-069 | `TimelineView` | `CollectionView` + connector | `ItemsSource`, `Orientation`, `SelectedItem`, `ShowActor` | `ItemCommand` | Audit and workflow timelines |
| WID-070 | `CalendarStrip` | Horizontal date selector | `StartDate`, `SelectedDate`, `MarkedDates`, `RangeLength` | `DateSelectedCommand`, `RangeChangedCommand` | Schedules and attendance |
| WID-071 | `CalendarMonth` | Calendar grid | `VisibleMonth`, `SelectedDate`, `Events`, `SelectionMode`, `MinDate`, `MaxDate` | `DateSelectedCommand`, `MonthChangedCommand` | Leave/team calendars |
| WID-072 | `ChartCard` | Chart abstraction | `Title`, `Series`, `Axes`, `Legend`, `EmptyMessage`, `AccessibleSummary` | `PointSelectedCommand` | Mobile analytics |
| WID-073 | `ProgressBarMetric` | `ProgressBar` + labels | `Current`, `Maximum`, `Label`, `ValueFormat`, `Tone` | `TappedCommand` optional | Completion and pipeline ratios |
| WID-074 | `DonutMetric` | GraphicsView/chart | `Segments`, `CenterValue`, `CenterLabel`, `AccessibleSummary` | `SegmentSelectedCommand` | Distribution summaries |
| WID-075 | `DataTableCard` | Adaptive grid/collection | `Columns`, `Rows`, `Sort`, `CompactMode`, `EmptyText` | `SortCommand`, `RowCommand` | Compact report data |
| WID-076 | `AttachmentTile` | `Border` + icon | `Name`, `Type`, `Size`, `State`, `Thumbnail`, `CanRemove` | `OpenCommand`, `RetryCommand`, `RemoveCommand` | Uploaded/downloaded files |
| WID-077 | `DocumentPreview` | Native/web/PDF adapter | `Source`, `ContentType`, `Title`, `Expiry`, `Watermark`, `IsProtected` | `CloseCommand`, `DownloadCommand` | Résumés, payslips, offers |
| WID-078 | `AuditTrail` | Specialised timeline | `Entries`, `ShowTechnicalDetails`, `CanLoadMore`, `CorrelationId` | `LoadMoreCommand`, `EntryCommand` | Approval and status history |

### 6.6 HR and Recruitment Domain Widgets — 20 widgets

| ID | Widget | MAUI basis | Principal bindable properties | Commands/events supplied by parent | Primary use |
|---|---|---|---|---|---|
| WID-079 | `JobCard` | `SectionCard` composition | `Job`, `ShowCompensation`, `AvailableActions`, `CompactMode` | `OpenCommand`, `EditCommand`, `StatusCommand`, `DeleteCommand` | Job opening lists |
| WID-080 | `RequisitionCard` | Card composition | `Requisition`, `ApprovalState`, `Age`, `AvailableActions` | `OpenCommand`, `SubmitCommand`, `CancelCommand` | Requisition list/dashboard |
| WID-081 | `ApplicantCard` | Card composition | `Applicant`, `Stage`, `Rating`, `ContactVisibility`, `Actions` | `OpenCommand`, `InviteCommand`, `DecisionCommand` | Applicant lists |
| WID-082 | `PipelineStageColumn` | Header + collection | `Stage`, `Applicants`, `Count`, `CanAcceptDrop`, `IsLoading` | `OpenApplicantCommand`, `MoveApplicantCommand`, `LoadMoreCommand` | Tablet recruitment pipeline |
| WID-083 | `CandidateHeader` | Avatar + summary | `Candidate`, `ApplicationStatus`, `Rating`, `MaskedContact`, `Actions` | `CallCommand`, `EmailCommand`, `RateCommand` | Candidate/application detail |
| WID-084 | `ResumeSummaryCard` | Structured card | `Skills`, `Experience`, `Education`, `Highlights`, `Source` | `OpenResumeCommand`, `SearchSkillCommand` | Candidate screening |
| WID-085 | `RatingControl` | Star/scale selector | `Value`, `Maximum`, `IsReadOnly`, `Labels`, `ValidationMessage` | `ValueChangedCommand` | Applicant and interview ratings |
| WID-086 | `ScreeningScorecard` | Criteria collection | `Criteria`, `Total`, `Recommendation`, `IsReadOnly`, `Errors` | `ScoreChangedCommand`, `SaveCommand` | Screening and evaluation |
| WID-087 | `InterviewCard` | Schedule card | `Interview`, `ShowCandidate`, `ShowJoinAction`, `StatusTone` | `OpenCommand`, `JoinCommand`, `EvaluateCommand`, `RescheduleCommand` | Interview lists/dashboard |
| WID-088 | `InterviewSlotPicker` | Calendar + slot grid | `Dates`, `Slots`, `SelectedSlot`, `TimeZone`, `PanelAvailability` | `DateChangedCommand`, `SelectSlotCommand`, `LoadMoreCommand` | Interview creation |
| WID-089 | `InterviewEvaluationForm` | Form composition | `Criteria`, `Recommendation`, `Notes`, `IsSubmitted`, `Errors` | `ScoreChangedCommand`, `SaveDraftCommand`, `SubmitCommand` | Interview feedback |
| WID-090 | `OfferCard` | Secure summary card | `Offer`, `MaskedCompensation`, `ApprovalState`, `AvailableActions` | `OpenCommand`, `ApproveCommand`, `SendCommand` | Offer list and approval |
| WID-091 | `EmployeeCard` | Avatar + assignment card | `Employee`, `ShowContact`, `Presence`, `AvailableActions` | `OpenCommand`, `CallCommand`, `MessageCommand` | Directory/team lists |
| WID-092 | `EmployeeHeader` | Identity summary | `Employee`, `Assignment`, `Status`, `SensitiveFieldsVisible` | `EditCommand`, `MessageCommand`, `RevealCommand` | Employee detail |
| WID-093 | `AttendanceDayCard` | Day summary card | `Date`, `Status`, `Punches`, `Worked`, `Expected`, `Exceptions` | `OpenCommand`, `CorrectionCommand` | My/team attendance |
| WID-094 | `PunchControl` | Stateful primary action | `PunchState`, `CurrentTime`, `LocationState`, `DeviceState`, `IsBusy` | `PunchInCommand`, `PunchOutCommand`, `RetryCommand` | Secure attendance punch |
| WID-095 | `ShiftCard` | Schedule card | `Shift`, `Assignment`, `SwapState`, `AvailableActions` | `OpenCommand`, `SwapCommand`, `AcknowledgeCommand` | Shift roster |
| WID-096 | `LeaveBalanceCard` | Metric card | `LeaveType`, `Available`, `Pending`, `Used`, `Unit`, `Tone` | `OpenPolicyCommand`, `RequestCommand` | Leave dashboard/balances |
| WID-097 | `LeaveRequestCard` | Workflow card | `Request`, `OverlapWarning`, `AvailableActions`, `CompactMode` | `OpenCommand`, `ApproveCommand`, `RejectCommand`, `WithdrawCommand` | Leave lists and approvals |
| WID-098 | `PayslipCard` | Protected document card | `Period`, `NetPayMasked`, `PublishedAt`, `DownloadState`, `CanReveal` | `OpenCommand`, `DownloadCommand`, `RevealCommand` | Payslip list |

### 6.7 Device, Offline and Security — 10 widgets

| ID | Widget | MAUI basis | Principal bindable properties | Commands/events supplied by parent | Primary use |
|---|---|---|---|---|---|
| WID-099 | `SecureSessionGuard` | Non-visual behaviour/host | `RequiredPermissions`, `AllowOffline`, `SensitiveLevel`, `SessionState` | `ReauthenticateCommand`, `DeniedCommand` | Route/session gating |
| WID-100 | `BiometricPromptAdapter` | Native service + state view | `Capability`, `Prompt`, `State`, `FallbackAllowed` | `AuthenticateCommand`, `UsePinCommand` | App unlock and sensitive reveal |
| WID-101 | `DeviceTrustCard` | Card | `Device`, `IsCurrent`, `TrustState`, `LastSeenAt`, `CanRevoke` | `RevokeCommand`, `RenameCommand` | Registered device management |
| WID-102 | `ConnectivityMonitorView` | `ContentView` + `IConnectivity` adapter | `NetworkAccess`, `ConnectionProfiles`, `ApiReachable`, `LastCheckedAt` | `RetryCommand` | Diagnostics and sync centre |
| WID-103 | `SyncQueueItem` | Status list row | `Operation`, `State`, `QueuedAt`, `AttemptCount`, `SafeSummary`, `CanDiscard` | `RetryCommand`, `DiscardCommand`, `OpenConflictCommand` | Explicit offline outbox |
| WID-104 | `ConflictResolutionCard` | Comparison card | `LocalSummary`, `ServerSummary`, `ConflictType`, `AllowedChoices` | `UseServerCommand`, `ReviewDraftCommand`, `CancelCommand` | Sync conflict handling |
| WID-105 | `DownloadItem` | Progress list row | `Name`, `Progress`, `State`, `ExpiresAt`, `IsProtected`, `Error` | `PauseCommand`, `RetryCommand`, `OpenCommand`, `RemoveCommand` | Export/download centre |
| WID-106 | `NotificationBell` | `IconButton` specialisation | `UnreadCount`, `HasPriorityAlert`, `IsConnected`, `PulseEnabled` | `OpenNotificationsCommand` | App top bar |
| WID-107 | `QrScannerView` | Camera/native scanner | `IsScanning`, `Purpose`, `OverlayText`, `TorchEnabled`, `PermissionState` | `ResultCommand`, `ToggleTorchCommand`, `RequestPermissionCommand` | Future badge/device/check-in workflows |
| WID-108 | `SensitiveValueReveal` | Secure content wrapper | `MaskedValue`, `RevealedValue`, `IsRevealed`, `RevealDuration`, `Reason` | `RevealCommand`, `HideCommand`, `CopyCommand` if allowed | Pay, tax, contact and identifiers |

### 6.8 Reference-Verified Widgets — 40 widgets

Every widget below is observed in a reference screen. `Ref` names the screens that define it.
`Catalogue` maps it to an existing WID entry, or marks it as an addition this design requires.
These specifications override the generic descriptions in sections 6.1 to 6.7.

#### Chrome and navigation

| # | Widget | Ref | Catalogue | Anatomy and rules |
|---|---|---|---|---|
| RW-01 | `AppHeaderBar` | all | new | White, 56dp. Logo lockup left; `HeaderCircleButton` search then `NotificationBellButton` right. No page title — the title belongs to the content area. Never tinted by status. |
| RW-02 | `HeaderCircleButton` | all | WID-004 variant | 40dp circle, `#F4F4F4` fill, 20dp glyph. |
| RW-03 | `NotificationBellButton` | all | new | `HeaderCircleButton` plus `CountBadge` pinned top-right, offset 2dp outside the circle. |
| RW-04 | `CountBadge` | REF-01, REF-04 | new | Pill, `Color.BrandPrimary` fill, white 11/600 label. Caps display at `99+`. Hidden at zero — never renders an empty ring. |
| RW-05 | `BottomNavBar` | all except REF-05 | WID-017 redefined | Exactly four destinations: Home, Manage, Interview Status, Menu. Icon over 11/500 label. Active item is `Color.BrandPrimary`, inactive `Color.TextSecondary`. Fixed 64dp plus safe-area inset. |
| RW-06 | `CenterActionButton` | all except REF-05 | new | 56dp circle, `Color.BrandPrimary`, white glyph, `Elevation.Fab`. Centred on the nav bar and overlapping it by half its height. It is a nav-bar child, not a free-floating FAB, and must not scroll with content. |
| RW-07 | `NavigationDrawer` | REF-05 | new | Left overlay at ~82% width, scrim over the remainder. Order: brand header, profile block, primary items, nested group, footer items. |
| RW-08 | `DrawerBrandHeader` | REF-05 | new | Logo plus two-line descriptor in `Type.Meta`. Sits above the profile block, separated by a divider. |
| RW-09 | `DrawerProfileBlock` | REF-05 | WID-012 host | 96dp circular avatar, name in 20/700, role in `Color.TextSecondary`. Tapping opens Profile. |
| RW-10 | `DrawerMenuItem` | REF-05 | new | 24dp leading icon, label. Active state fills the full row edge-to-edge with `Color.BrandPrimary` and flips label and icon to white. No rounded inset. |
| RW-11 | `DrawerSubItemCard` | REF-05 | new | Indented card under a parent item: rounded icon tile, title 13/600, description 11/400 in `Color.TextSecondary`. |

#### Structure and containers

| # | Widget | Ref | Catalogue | Anatomy and rules |
|---|---|---|---|---|
| RW-12 | `SectionHeader` | REF-01, REF-03, REF-07 | new | Emoji glyph plus title in 14/600, sitting directly on the page ground above its card. Not inside the card. |
| RW-13 | `SegmentedTabBar` | REF-02, REF-08, REF-09 | new | Two or three equal pill segments in a `#F4F4F4` track. Active segment fills `Color.BrandPrimary` with white icon and label. Switches peer views only — never used for filtering. |
| RW-14 | `FilterChip` | REF-06 | WID-046 | Outline pill, `Color.Border` stroke. Selected fills `Color.BrandPrimary`. |
| RW-15 | `FilterChipGroup` | REF-06 | WID-047 | Leading `Filter:` label then a horizontally scrollable chip run. Single-select in the references. |
| RW-16 | `SearchField` | REF-06, REF-07 | WID-045 | Full-bleed rounded field, `Radius.Search`, `#F4F4F4` fill, leading magnifier, placeholder in `Color.TextSecondary`. 16px input text to stop iOS zoom-on-focus. |
| RW-17 | `BottomSheetScaffold` | REF-03, REF-07 | new | Rounded top corners at `Radius.Sheet`, title in 16/600 left, X close right, scrollable body. Detail views use this — not a pushed page. Dismiss on scrim tap and on back gesture. |
| RW-18 | `SectionCard` | REF-03, REF-07 | WID-024 | White, `Radius.Card`, hairline `Color.Border`, 16dp padding. |
| RW-19 | `KeyValueRow` | REF-03, REF-07 | new | Label in `Color.TextSecondary` on a fixed-width left column, a literal `:` separator, then the value. The colons align down the column — this alignment is part of the design, not incidental. |
| RW-20 | `KeyValueCard` | REF-03, REF-07 | WID-065 | `SectionCard` hosting a run of `KeyValueRow`. |
| RW-21 | `BulletDetailCard` | REF-03 | new | Bold lead line, then dash-prefixed items in `Color.TextSecondary`. Used for Key Responsibilities and Required Qualifications. |
| RW-22 | `DescriptionCard` | REF-03 | new | Single paragraph in a bordered card, 13/400, 1.5 line height. |
| RW-23 | `AddressCard` | REF-07 | new | Free-text address line, then a three-column City / State / Pincode strip with labels above values. |

#### Status and data display

| # | Widget | Ref | Catalogue | Anatomy and rules |
|---|---|---|---|---|
| RW-24 | `StatusPill` | REF-01..04, REF-06, REF-09 | WID-063 redefined | Pill, filled, white 11/600 label, 8dp horizontal padding. Anchored top-right of its card. Accepts exactly the eight states in token section 4.2; an unmapped state renders `Color.TextSecondary`, never an arbitrary colour. |
| RW-25 | `KpiGradientCard` | REF-01 | WID-062 redefined | Vertical gradient from section 4.4, white outlined glyph, caption in 12/500, value in `Type.KpiValue`. Equal width in a three-across row. |
| RW-26 | `KpiCardRow` | REF-01 | new | Exactly three `KpiGradientCard`, equal flex, 12dp gutters, cycling blue → cyan → amber. |
| RW-27 | `CollapsibleSectionCard` | REF-01 | new | Header with leading glyph, title, `CountBadge`, and a chevron that rotates on expand. Body holds the collapsed content. Expanded by default on the dashboard. |
| RW-28 | `AnalyticsBarRow` | REF-01 | WID-073 | Label left, rounded track right filling the remaining width, value badge inset at the right end of the track. |
| RW-29 | `AnalyticsCard` | REF-01 | WID-072 | `SectionCard` hosting a titled run of `AnalyticsBarRow`. |
| RW-30 | `RatingStars` | REF-06 | new | Single filled star glyph plus `4.5/5 Rating` text. The references show one star and a numeral, not five glyphs — do not render a five-star strip. |
| RW-31 | `ContactChip` | REF-06 | new | Low-emphasis tinted chip, leading icon, 11/400 text. Email chip and phone chip sit side by side and wrap. Tapping launches mail or dialler. |
| RW-32 | `AvatarView` | REF-05, REF-06 | WID-012 | Circular. Falls back to `Color.AvatarPlaceholder` with a neutral person glyph. 40dp in lists, 96dp in the drawer. |

#### Recruitment domain

| # | Widget | Ref | Catalogue | Anatomy and rules |
|---|---|---|---|---|
| RW-33 | `JobCard` | REF-02 | WID-079 redefined | Title 15/600; department • location in `Color.TextSecondary`; right-aligned expiry date in `Type.Meta`; `JobMetaLine`; `JobCardActionBar`; right-aligned `Last Status Updated:` footer. `StatusPill` top-right. |
| RW-34 | `JobMetaLine` | REF-02 | new | Emoji-prefixed run: employment type • experience band • salary range, dot-separated, wrapping as one block. |
| RW-35 | `JobCardActionBar` | REF-02 | new | Three 28dp rounded-square icon buttons in fixed order — view `Color.ActionView`, edit `Color.ActionEdit`, delete `Color.ActionDelete`. Disabled actions drop to 40% opacity and stay in place; they are never removed, so the row keeps a stable shape. |
| RW-36 | `SalaryRangeText` | REF-02, REF-03 | new | Indian-grouped currency with the `₹` symbol and a `p.a.` suffix, e.g. `₹8,00,000 - ₹11,00,000 p.a.`. Lakh/crore grouping is required. Masked to an em dash when the viewer lacks compensation permission. |
| RW-37 | `InterviewCard` | REF-01, REF-04, REF-09 | WID-087 redefined | `Color.BrandTint` plate, 4dp `Color.BrandPrimary` bar down the left edge, `Radius.Card`. Candidate name 15/600, role in `Color.TextSecondary`, then a date glyph + date and clock glyph + time run, then the round label. `StatusPill` top-right. |
| RW-38 | `ApplicantCard` | REF-06 | WID-081 redefined | Leading `AvatarView`; name 15/600; role and department; two `ContactChip`; `RatingStars`; `ApplicantActionRow`. `StatusPill` top-right. |
| RW-39 | `ApplicantActionRow` | REF-06 | new | Two filled buttons, equal height, left `View Details` in `Color.BrandPrimary`, right `Invite Interview` in `Color.AccentCyan`. Cyan carries dark text to hold contrast. |
| RW-40 | `NotificationCard` | REF-08 | new | Title 14/600 left; clock glyph + time and date right-aligned in `Type.Meta`; two-to-three-line body in `Color.TextSecondary`. Unread state is carried by weight, not by a coloured plate. |

#### Contrast note

`Color.AccentCyan` (`#27EBFF`) and `Color.StatusActive` (`#00CEFF`) fail WCAG AA against white
text. The references pair cyan with dark text; implementations must do the same. Do not place
white labels on either cyan token.

## 7. Widget Data-Binding Pattern

Widgets accept immutable display models plus commands. A parent screen owns loading, permission, validation, and API interaction.

```xml
<widgets:ApplicantCard
    x:DataType="models:ApplicantListItem"
    Applicant="{Binding .}"
    OpenCommand="{Binding Source={RelativeSource AncestorType={x:Type vm:ApplicantSearchViewModel}},
                          Path=OpenApplicantCommand}"
    DecisionCommand="{Binding Source={RelativeSource AncestorType={x:Type vm:ApplicantSearchViewModel}},
                              Path=OpenDecisionCommand}"
    AutomationId="{Binding AutomationId}" />
```

Simplified bindable property contract:

```csharp
public sealed class ApplicantCard : ContentView
{
    public static readonly BindableProperty ApplicantProperty =
        BindableProperty.Create(
            nameof(Applicant),
            typeof(ApplicantListItem),
            typeof(ApplicantCard));

    public static readonly BindableProperty OpenCommandProperty =
        BindableProperty.Create(
            nameof(OpenCommand),
            typeof(ICommand),
            typeof(ApplicantCard));

    public ApplicantListItem? Applicant
    {
        get => (ApplicantListItem?)GetValue(ApplicantProperty);
        set => SetValue(ApplicantProperty, value);
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }
}
```

For commands inside item templates, prefer a typed parent binding or an explicit command passed to the widget. Do not make widgets search the dependency injection container for their parent ViewModel.

## 8. State Ownership Matrix

| State | Owner | Widget responsibility |
|---|---|---|
| API loading and cancellation | Screen ViewModel/use-case service | Render supplied `IsBusy`/skeleton state |
| Validation rules | ViewModel/domain/API | Render supplied error and focus target |
| Permission decision | API and session permission service | Render permitted action state; never authorise |
| Navigation | Screen ViewModel/navigation service | Invoke supplied command only |
| Filter selection | Screen/filter ViewModel | Bind selection and invoke change command |
| Theme | Resource dictionaries/theme service | Consume semantic `DynamicResource` tokens |
| Connectivity and sync | Connectivity/sync services | Display supplied state and retry/discard commands |
| Sensitive reveal | Session/security service | Mask by default and render timed authorised reveal |

## 9. Resource Dictionary Layout

```text
Shared/Styles
├── Colors.xaml
├── Typography.xaml
├── Spacing.xaml
├── Shapes.xaml
├── Elevation.xaml
├── Controls.xaml
├── Status.xaml
├── LightTheme.xaml
├── DarkTheme.xaml
└── Tenants
    └── TenantOverrides.xaml
```

Use `VisualStateManager` for `Normal`, `PointerOver`, `Focused`, `Pressed`, `Selected`, `Disabled`, `Loading`, `Error`, and `Offline` states. State changes should not depend on colour alone; use labels, icons, outlines, or patterns as well.

## 10. Accessibility and Localisation Checklist

Every widget must be checked for:

- screen-reader name, role, value, hint, and state
- logical focus order and visible keyboard focus
- dynamic text at the largest supported accessibility size
- no clipped status, salary, names, or translated labels
- right-to-left layout readiness
- locale-aware dates, times, numbers, currencies, and pluralisation
- adequate contrast in light/dark themes and tenant themes
- a non-colour signal for success, warning, error, and selection
- reduced-motion mode for shimmer, chart animation, and transitions
- redaction from screenshots/app-switcher when rendering sensitive values

## 11. Performance Rules

- Use `CollectionView` recycling and incremental loading for large directories, applicants, jobs, notifications, and audit trails.
- Do not nest a same-direction `ScrollView` around a `CollectionView`.
- Prefer fixed/aspect-ratio image sizes and thumbnails; cancel recycled image requests.
- Use compiled bindings on pages, widgets, and each `DataTemplate`.
- Keep card view trees shallow and avoid per-row heavy effects.
- Aggregate dashboard values on the server; do not download full datasets to calculate KPIs on the device.
- Page large report data and render an accessible summary before charts.
- Profile startup and scrolling on representative low/mid-range Android devices, not only emulators.

## 12. Testing Contract

Each widget requires:

1. Bindable-property and command tests where logic exists.
2. Snapshot or screenshot coverage for light, dark, loading, error, disabled, selected, and large-text states.
3. Accessibility assertions for names and touch target size.
4. Automation IDs in the form `Feature.Widget.Action`, with row identifiers appended safely.
5. Tests proving sensitive values are masked by default.
6. Phone and tablet layout checks for composite widgets.

Critical widgets also need device tests: `PunchControl`, `DocumentPickerField`, `CameraCaptureField`, `DocumentPreview`, `BiometricPromptAdapter`, `QrScannerView`, `DownloadItem`, and `SecureSessionGuard`.

## 13. Build Order

### Foundation pack

Build WID-001–078 first: primitives, navigation, scaffolds, loading/error states, forms, pagination, timelines, calendars, attachments, and document preview.

### MVP domain pack

Then build WID-079–098 for recruitment, employee, attendance, leave, and payroll surfaces used by Phase 1 screens.

### Native/security pack

Build WID-099–108 alongside the authentication, secure storage, sync, protected download, and device-permission infrastructure.

Do not attempt to implement all 108 widgets as one large library before real screens exist. Establish the contracts, then build and harden widgets in vertical feature slices while retaining the IDs and semantics defined here.

## 14. Platform References

- [.NET MAUI compiled bindings](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings)
- [.NET MAUI data binding](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/)
- [.NET Community Toolkit MVVM](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [.NET MAUI Shell navigation](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/navigation)
- [.NET MAUI secure storage](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage)
- [.NET MAUI connectivity](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/communication/networking)

