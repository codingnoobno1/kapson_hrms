# Reference mockups, round 2 (2026-09-18)

Four screenshots supplied in chat on 2026-09-18, from an external Android Studio mockup project
("MyApplication11" — visible in the status bar of one of the four, importing its Gradle project).
No image files came with them; only the rendered screenshots were visible in the conversation, and
there is no tool available in this environment that extracts a pasted image's bytes onto disk. This
file transcribes what each screen showed instead of storing the pictures — the same "describe every
value" fallback `../../Vispl.HRManagement.Mobile/docs/employee-app-references/README.md` calls for
when the images themselves might not survive.

If the source PNGs become available later (e.g. exported from that Android Studio project), they
belong at `docs/reference-mockups/` alongside this file, named to match the headings below.

## 1. Home

Coral identity card: avatar, "John Doe", "Store Manager", a location pin with "Operations", a
phone number, a QR code and "EMP-001" in the corner — this is the same card
`Shared/Controls/IdentityCard` already renders here, populated from the caller's own employee
record rather than a fixed sample name.

Below it, two interview cards ("Amit Patel — Sales Executive", "Round 1 — Screening" /
"Round 2 — Technical", one marked "Past Interview") — the Interview Schedule card
(`InterviewScheduleCard`), which this app already shows to an interviewer and hides for anyone
else.

Below that, two labelled groups of two cards each:

- **HR & PAYROLL** — Salary Slip, Attendance
- **WORK & PROJECTS** — My Projects ("View assigned project and tasks"), Tasks ("Manage your
  daily tasks and deadlines")
- **HR PAYROLL** (as labelled in the mockup, evidently meant as a third, unrelated group) — Help
  & Support, Company Information

Bottom tab bar: Home, Projects, Attendance, Menu, with a raised coral (+) between Projects and
Attendance.

**Status**: identity card and interview card were already built. Salary Slip and Attendance were
already built. My Projects, Tasks, Help & Support and Company Information were added to Home in
this pass — Projects and Tasks open the same not-ready panel every other unbuilt feature in this
app uses (there is still no projects/tasks module, a decision recorded elsewhere in this codebase
before this pass), Help & Support and Company Information are static content sheets. The bottom
bar in this app carries Home / Leave / Attendance / Menu, not Home / Projects / Attendance / Menu
— that is an existing, deliberate difference (see the main README's "four destinations" note), not
something this pass changed.

## 2. Menu (partial — scrolled to the lower half)

Visible sections, top to bottom:

- **(cut off above)** "Leave Pending for Approval — Approve pending leave requests"
- **GATE PASS** — Apply Gate Pass, Gate Pass History, Gate Pass Pending for Approval
- **MISS PUNCH** — Apply Miss Punch ("Report missed check-in/out"), Check Miss Punch Approval
- **ON DUTY** — Apply On Duty ("Request on-duty status"), Check On Duty Approval ("View and
  approve on-duty requests")

**Status**: Leave Pending for Approval, Apply Gate Pass, and Passes Pending for Approval already
existed. "Gate Pass History" in the mockup is this app's existing "My Gate Passes" row — same
screen, different label; not renamed, to avoid drifting from the HR portal's own wording for the
same feature. "Miss Punch" in the mockup is this app's existing "Attendance Correction" flow
(wrong or missed punches, fixed the same way); also not renamed for the same reason. **On Duty
did not exist at all** — no table, no service, no endpoint, no menu row — and was built end to end
in this pass (see below).

## 3. Apply On Duty (modal)

A form titled "Apply On Duty" with fields, in order: **On Duty Purpose*** (placeholder "e.g.,
Client Meeting, Conference, etc."), **From Date*** and **To Date*** (both `dd/mm/yyyy`),
**Location/Destination*** (placeholder "Enter Location"), **Reason*** (multi-line, placeholder
"Provide reason for leave..."), and a "Submit Application" button.

**Status**: built. `Features/Presence/PresenceFlows.PresentApplyOnDutyAsync` reproduces this exact
field set and order.

## 4. Check On Duty Approval (modal)

A list titled "Check On Duty Approval" of already-decided requests, each a card: purpose
("Client Meeting", "Annual Conference"), a date range, a location, and a green check with
"Approved".

**Status**: built as the *pending* queue instead of a decided-item history, to match how every
other approval screen in this app works (Leave Pending for Approval, Passes Pending for Approval,
Corrections Pending for Approval all show what is waiting on a decision, not what was already
decided). The mockup's static image happens to show two already-approved examples; that is a
property of the screenshot, not a spec for showing history here.

## What "On Duty" actually is

Business travel that still counts as working time — a client meeting, a conference, a site visit,
spanning one or more days. Deliberately **not** the same thing as a gate pass (permission to be off
the premises, checked at a physical gate — `GatePassService`) and **not** leave (time not worked).
Built by mirroring the gate-pass module's shape end to end:

- `Vispl.HRManagement.VO.Workforce.OnDutyRequestRecord` / `OnDutyFilter`
- `Vispl.HRManagement.DL.Workforce.OnDutyRequestEntity` + `OnDutyRepository` (table
  `workforce.OnDutyRequests`, migration `AddOnDutyModule`)
- `Vispl.HRManagement.BM.Mobile.OnDutyService` (`IOnDutyService`)
- `Vispl.HRManagement.Api.Employee.Controllers.OnDutyController`
  (`api/mobile/v1/on-duty/...`)
- `Vispl.HRManagement.Mobile.Client.Api.OnDutyApiClient`
- Permissions: `ONDUTY.TEAM` / `ONDUTY.APPROVE` (HR portal), `SELF.ONDUTY.REQUEST` /
  `TEAM.ONDUTY.APPROVE` (Employee portal) — added to `HrPermissionCatalog` only. The system
  roles (`EMPLOYEE`, `REPORTING_MANAGER`, `HR_ADMINISTRATOR`, `HR_EXECUTIVE`) pick these up
  automatically the next time `AccessRoleService` reconciles them; nothing was hand-seeded.

`Decide` carries no `[RequirePermission]` attribute on its controller action, unlike gate pass's
decision endpoint. The service checks for *either* `ONDUTY.APPROVE` (HR portal) or
`TEAM.ONDUTY.APPROVE` (a reporting manager inside this app) — one endpoint, two names for the same
decision, chosen because gate pass's own decision endpoint is gated on `GATEPASS.APPROVE` alone,
which no Employee-portal role can ever hold (only `TEAM.LEAVE.APPROVE`-style keys are granted
there), so a manager's "Passes Pending for Approval" row in this app calls an endpoint that would
answer 403 if actually used. That mismatch was not introduced by this pass and was not fixed here —
it affects gate passes and attendance corrections, not on duty — but it is real and worth a look.
