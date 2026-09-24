# Vispl HR Management Mobile

The mobile project is a runnable .NET MAUI application for Android and Windows development. iOS and Mac Catalyst platform files are included and build on macOS.

## Implemented design slice

- Responsive HR login
- Role-aware Shell with flyout and primary navigation
- HR dashboard with KPI, interview and hiring widgets
- Job list and job detail
- Applicant list and applicant detail
- Interview schedule
- General/interview notification centre
- Shared brand header, KPI tile, status pill, job card, applicant card and interview card

The UI follows the images preserved in [`docs/reference-screens.md`](docs/reference-screens.md), while using reusable controls and MVVM bindings instead of copying fixed screenshot pixels.

## Motion

The timings in [scopedocs/screen-references/hr-mobile-visual-map.md](../../scopedocs/screen-references/hr-mobile-visual-map.md) are implemented as `Shared/Motion/MotionTokens.cs` (durations/easing) and `Shared/Motion/MotionExtensions.cs` (`PlayArrivalAsync`, `PressDownAsync`/`PressUpAsync`), collapsing to instant under `Shared/Motion/IReducedMotionService` (one implementation per platform under `Platforms/*`, reading the OS's own reduce-motion/animator-scale setting). Built: page/card fade+rise arrival with stagger, button/card press-scale (via `PressScaleBehavior`, attached through `Styles.xaml` so it reaches every button without per-page wiring), a `SkeletonView` shimmer loading state on every list page. Two spec items were **not** built as specified, since building them meant more than adding motion to something that already exists:
- **Bottom sheet** (job/applicant details): these are pushed full pages today, not an overlay-over-content sheet — approximated with a larger slide-up-on-arrival instead of restructuring navigation.
- **Drawer**: the nav flyout is the platform-native Shell flyout, whose transition MAUI does not expose for customisation; left as-is rather than replacing it with a custom drawer control.
- **Center FAB** and the **analytics/progress-fill** widget don't exist as features yet, so there was nothing to animate.

## Plan documents

| Document | Defines |
|---|---|
| [`screens.md`](screens.md) | The 112 routed screens, ViewModels, bindings and caching |
| [`widgets.md`](widgets.md) | The 108 reusable widgets and design tokens |
| [`workflows.md`](workflows.md) | The 15 end-to-end workflows: what the employee fills, how HR manages it, and the states between |
| [`backend.md`](backend.md) | The 38 mobile controllers and their DTO contract |
| [`admin.md`](admin.md) | Tenant configuration that drives the runtime app |
| [`docs/reference-screens.md`](docs/reference-screens.md) | The nine approved visual references |

## Run on Windows

```powershell
dotnet run --project .\Vispl.HRManagement.Mobile.csproj -f net9.0-windows10.0.19041.0
```

Sign in with a real HR-portal account — this now talks to the actual host over `/api/mobile/v1` (see `MauiProgram.ResolveApiBaseAddress`), not the old preview/demo login.

## Run on Android

Start an Android emulator or connect a developer-enabled device, then run:

```powershell
dotnet build .\Vispl.HRManagement.Mobile.csproj -f net9.0-android
```

Select the device through Visual Studio or use the appropriate Android deployment target.

## Integration boundary

`PreviewAuthenticationService` and `DemoDataService` are isolated development adapters. They must be replaced by OIDC Authorization Code + PKCE and typed `/api/mobile/v1` clients before production use. The UI project must never reference BM, DL, repositories, `DbContext`, or SQL Server directly.
