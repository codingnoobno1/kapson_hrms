# HR mobile visual map

This file maps the approved HR mobile screenshots (`01`–`09`) to the shared visual tokens and motion used by `Vispl.HRManagement.MobileApp`.

## Colour roles

| Role | Token | Value | Applied to |
|---|---|---:|---|
| Primary interaction | `--color-brand-primary` | `#3478ED` | Active tabs, links, view actions, FAB, selected navigation |
| Deep primary | `--color-brand-primary-deep` | `#2F68D5` | Strong blue states and depth |
| Interview tint | `--color-brand-tint` | `#EAF1FE` | Interview schedule cards only |
| Cyan action | `--color-accent-cyan` | `#26D9EA` | Invite Interview and cyan brand accents |
| Amber accent | `--color-accent-amber` | `#FFC34E` | Third KPI tile |
| App background | `--color-background` | `#FCFCFC` | Page ground |
| Card surface | `--color-surface` | `#FFFFFF` | Header, cards, sheets, drawer and navigation |
| Muted control | `--color-surface-muted` | `#F6F6F6` | Header action circles and inactive controls |
| Border | `--color-border` | `#E2E2E2` | Cards, fields and dividers |
| Main text | `--color-text-primary` | `#3F3F3F` | Body copy and labels |
| Strong text | `--color-text-strong` | `#171717` | Brand and primary headings |
| Secondary text | `--color-text-secondary` | `#8A8A8A` | Metadata, dates and helper copy |
| Shortlisted/success | `--color-status-success` | `#78CE82` | Shortlisted pills and positive state |
| Active | `--color-status-active` | `#12BBDD` | Active job status |
| Draft | `--color-status-warning` | `#F5A444` | Draft job status |
| Pending | `--color-status-pending` | `#F0CC21` | Pending Review status |
| Rejected/expired | `--color-status-danger` | `#D9342B` | Rejected and expired states |
| Contact tint | `--color-contact-tint` | `#FFF4F4` | Applicant email and phone chips |
| Rating | `--color-rating` | `#F2BE00` | Rating star |

KPI cards use blue, cyan and amber vertical gradients plus the translucent lower wave visible in reference `01`. The fallback HR logo uses the same blue/cyan gradient as four interlocking leaves.

## Reference-to-component mapping

| Reference | Shared implementation |
|---|---|
| `01-hr-dashboard.png` | `AppHeader`, `KpiCardRow`, `CollapsibleSectionCard`, `InterviewCard`, `AnalyticsCard`, `TabBar` |
| `02-manage-jobs.png` | `SegmentedTabBar`, `JobCard`, status pills and job action buttons |
| `03-job-details.png` | `BottomSheet`, `KeyValueCard`, `DescriptionCard`, `BulletDetailCard` |
| `04-interview-schedule.png` | `InterviewCard`, pale-blue interview tint and blue left accent |
| `05-navigation-drawer.png` | `NavDrawer`, drawer scrim, active full-width blue row and nested cards |
| `06-applicant-list.png` | `SearchField`, `FilterChipGroup`, `ApplicantCard`, contact chips and rating |
| `07-applicant-details.png` | `BottomSheet`, summary block, key/value and address cards |
| `08-notifications-general.png` | `SegmentedTabBar`, `NotificationCard` |
| `09-notifications-interviews.png` | `SegmentedTabBar`, `InterviewCard` |

## Motion mapping

| Interaction | Motion | Timing |
|---|---|---:|
| Page/list arrival | Fade plus 10px rise; list rows stagger top-to-bottom | 180ms |
| KPI tiles | Staggered rise; translucent wave settles upward | 180–260ms |
| Buttons/actions | Small press scale with colour transition | 120ms |
| Center FAB | Scale-in on shell load; press dip preserves centering | 260ms |
| Bottom sheet | Slides from bottom while scrim fades | 180–260ms |
| Drawer | Slides from left while scrim fades | 180–260ms |
| Segmented selection | Short scale-settle on selected segment | 180ms |
| Analytics/progress | Horizontal fill grows from the origin | 260ms |
| Loading | Skeleton shimmer | 1.4s |

All animation and transition durations collapse under `prefers-reduced-motion`.
