# Vispl HR — mobile app

A .NET MAUI Blazor Hybrid client for the HR API. It is an **API client only**: it holds no
DbContext, no repositories and no business rules, and it never reaches SQL Server. Every number
it shows was computed on the server.

It sits outside the layered solution on purpose. The layering rule (UI → UIVM → BM.ITF ← BM →
DL.ITF ← DL) governs the server; a phone client that obeyed it would have to reference BM, which
is exactly what must not happen. The one project reference is to `Vispl.HRManagement.Contracts`.

## Why that reference matters

The app deserialises into the server's own DTOs. A contract change that would break a screen
breaks the **build** instead of failing on somebody's phone. Response shapes therefore cannot
drift; what can drift is the URL, and that is what
`kapson/scripts/verify-mobile-app-wiring.py` exists to catch.

## Screens

| Screen | Route | Reads | Writes |
|---|---|---|---|
| Sign in | `/signin` | tenant discovery, then `auth/token` | — |
| Home | `/` | HR home KPIs, next interviews, job analytics | — |
| Manage Jobs | `/jobs` | job list and detail | — |
| Applicants | `/applicants` | application list, detail, onboarding checklist | **move stage, approve / reject onboarding tasks** |
| Interviews | `/interviews` | interviews, grouped by day | **cancel** |
| **People** | `/people` | employee directory, record, app-access list | **grant / suspend / restore app access** |
| **Leave Approvals** | `/leave` | the client's approval queue | **approve / reject** |
| **Attendance** | `/attendance` | today's counts, late arrivals, missing punches, off-site | — |
| Notifications | `/notifications` | notification list | mark all read |
| Master Data | `/masters` | the master-data bundle | — |
| Modules | `/modules`, `/modules/{key}` | probes each catalogued endpoint | — |
| Settings | `/settings` | session, tenant, sample-data switch | sign out |

The bottom bar carries four destinations and that is a design decision, not a limit of the
implementation — everything else is reached from the drawer.

## Sample mode

`Settings` has a switch that feeds the screens from `SampleContent` instead of the API, for
reviewing layout without a server. While it is on the shell shows a persistent banner, so
sample data can never be mistaken for real data. It defaults to **off**.

## The writes

Each one is something somebody actually opens the app to do, rather than a form that happens
to exist:

- **Move an applicant's stage.** The buttons come from the server's own transition table
  (`ApplicationStages.IsHrTransitionAllowed`), so a move is never offered that would be
  refused. A stage with no moves says *why* — waiting on the candidate and being finished are
  different states and must not look alike.
- **Approve or reject an onboarding task.** Approving the last required one is what completes
  onboarding and hires the candidate, so the whole sheet is re-read afterwards rather than the
  single row.
- **Approve or reject leave.** The queue marks the reader's own requests, because nobody may
  decide their own and saying so beats a refusal after the tap.
- **Grant, suspend or restore app access.** The temporary password comes back once, is never
  stored in readable form, and cannot be fetched again — the screen shows it immediately and
  says so.
- **Cancel an interview.** Offered only on a live one; a completed or already-cancelled
  interview is history.
- **Mark notifications read.**

A rule runs through all of these: an action is shown only where the server would accept it.
Buttons that are always visible and sometimes refused teach people to distrust the app.

## Talking to a local server

`MobileAppOptions` defaults to `http://10.0.2.2:7000/` on Android — the address an emulator
reaches the host machine on, which `localhost` does not — and `http://localhost:7000/`
elsewhere. Settings can override it at runtime.

## Verifying

With the host running:

```
SMOKE_BASE_URL=http://localhost:7000 SMOKE_USERNAME=... SMOKE_PASSWORD=... \
  python ../kapson/scripts/verify-mobile-app-wiring.py
```

It exercises every call every screen makes, checks each answers inside the `ApiEnvelope` the
client unwraps, performs each write, confirms an illegal stage jump is refused, and asserts
that no entry in `ApiCatalog` points at a route that does not exist.

Two of the write checks need a database in the right state — an application sitting at
`Applied`, and an onboarding checklist with tasks awaiting a decision. The script says so and
skips rather than passing vacuously when neither exists.
