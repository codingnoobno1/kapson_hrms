# Kapsons Employee App

The employee self-service app from `docs/employee-app/`. A separate MAUI head from
`Vispl.HRManagement.Mobile` (the HR/recruiter app): its own app id, icon and navigation.

## What is built

| Screen | ID | State |
|---|---|---|
| Splash and session restore | EMP-001 | Working |
| Sign in | EMP-002 | Working, against the real token endpoint |
| Home — identity card, two KPI tiles | EMP-010 | Working |
| Attendance — four stat tiles, day list | EMP-012 | Working |
| Day detail | EMP-021 | Working, as a sheet |
| Full Month Attendance — calendar, legend | EMP-020 | Working |
| Leave — balances, history, approver strip | EMP-014 | Working |
| Menu | EMP-013 | Working |
| Employee Profile | EMP-040 | Working, as a sheet |
| My Gate Passes | EMP-051 | Working, as a sheet |
| Notifications | EMP-060, 061 | Working |

Rows and buttons whose module answers 501 — Salary, Bank, Onboarding, Analytics, report
download and email, gate pass requests — are present and name the missing feature when tapped.
Nothing is faked, and there is no demo data anywhere in this project.

## Running it

The app talks to `api/mobile/v1` on the host in `MauiProgram.ResolveApiBaseAddress()`, which is
`https://localhost:7060` on Windows and `http://10.0.2.2:7000` on Android (the emulator’s alias for
the host machine). Start `Vispl.HRManagement.Host` first, or change `ResolveApiBaseAddress()`.

```bash
dotnet build Vispl.HRManagement.EmployeeApp/Vispl.HRManagement.EmployeeApp.csproj -f net9.0-windows10.0.19041.0
```

```bash
dotnet build Vispl.HRManagement.EmployeeApp/Vispl.HRManagement.EmployeeApp.csproj -f net9.0-android
```

Sign in with an **Employee-portal** account. HR grants one through the HR portal's Mobile Access
panel (`/hr/dashboard`), which issues a username and a temporary password. An HR or platform
account is refused here, by this app and by the server.

## Syncfusion

Two controls, both earning their place:

- **`SfBottomSheet`** (`Syncfusion.Maui.Toolkit`) — the app's whole detail pattern. MAUI has no
  bottom sheet, and nineteen of the plan's screens are sheets. Wrapped as `Shared/Sheets/AppSheet`.
- **`SfCalendar`** (`Syncfusion.Maui.Calendar`) — the month grid on EMP-020, with a cell template
  that colours each day from its server-assigned status.

Set the licence key before running, or the controls show a trial banner:

```bash
setx SYNCFUSION_LICENSE_KEY "your-key-here"
```

It is read from the environment rather than committed — a key in source is a key in every clone
and every build log.

## Structure

```text
Features/          one folder per area, page + view model together
Shared/Controls/   the widget set from widgets.md, as ContentViews in C#
Shared/Sheets/     AppSheet and the parts a sheet body is built from
Shared/Theme/      Tokens.cs — the only place a literal colour appears
Shared/Services/   secure token storage, device identity
```

Controls are written in C# rather than XAML deliberately: it halves the file count, and binding
explicitly is stricter than a XAML binding that fails silently at runtime.

## Rules this project keeps

1. **No mock data.** A screen with no endpoint shows the not-ready panel.
2. **501 is a designed state.** `MobileFeatureNotReadyException` names the module; the panel
   repeats it. A 501 never surfaces as an error.
3. **Every mutation carries `Idempotency-Key`**, every edit `If-Match`. Handled once, in
   `MobileApiClient`.
4. **The server owns every number.** Leave days, balances, present days and holidays are all
   computed server-side. This app formats; it does not calculate.
5. **No reference to BM, DL, a repository or SQL Server.** Only `api/mobile/v1`, through
   `Vispl.HRManagement.Mobile.Client`.
6. **Six states per screen** — loading, ready, empty, error, offline, not-ready, denied —
   enforced by `ViewModelBase.RunAsync`, so no screen can be written with only a happy path.

## Not built yet

Applying for leave (EMP-030/031) needs the calculate-then-submit handshake: the server issues a
signed, 30-minute token carrying the chargeable day count, and the submit call rejects anything
without it. A form that guessed the number would be wrong in exactly the cases policy exists
for, so it waits until that is wired properly.

Punch (EMP-022) is specified but not scheduled — no approved mockup contains a punch control,
so whether this app records punches or only reads what a biometric device captured is still an
open question (`employee-app.md` §9.2).
