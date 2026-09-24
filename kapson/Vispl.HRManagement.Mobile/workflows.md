# HR Management Mobile — Workflow Design

## 1. Purpose

`screens.md` defines **what** each of the 112 screens is, `widgets.md` defines **what each control is made of**, and `backend.md` defines **what the API exposes**. None of them define the **sequence** that joins them.

This document defines that sequence. Every workflow in this product has the same shape:

```text
someone fills something   ->   HR manages it   ->   the system applies it
```

The employee-facing side always comes first, because the record does not exist until the
employee (or candidate, or manager) creates it. The HR-facing side is always second,
because HR acts on a submitted record, never on an empty one. Every workflow below is
written in that order.

This document is authoritative for **state, sequence, ownership and notification**.
It does not restate field lists, widget internals or DTO shapes; those stay in their
own documents and are cross-referenced by ID.

## 2. Actors

| Actor | Meaning in this document | Typical screens |
|---|---|---|
| Employee | Any signed-in user acting for themselves | `/leave`, `/attendance/me`, `/requests/*`, `/payroll/*` |
| Manager | Reporting manager of the submitter | `/approvals`, `/leave/team/*`, `/attendance/team` |
| HR executive | Operational HR who processes and applies records | `/approvals`, register, verification, onboarding |
| HR administrator | HR who configures policy, masters and workflow | Admin surfaces in `admin.md` |
| Recruiter | HR user owning requisitions, jobs, applicants, interviews | `/recruitment/*` |
| Interviewer | Panel member with scoped access to one interview | MOB-045, MOB-046 |
| Candidate | External applicant; **does not sign in to this app** | Managed by HR through MOB-035–042 |
| System | Scheduler, workers and integrations | No screen; acts through events |

"Employee filling" in this document means the **submitter side**, whoever that is. For
WF-015 the submitter is a candidate on a separate public surface, and the record still
arrives in the same HR queue.

## 3. The Two-Sided Pattern

Every workflow is built from the same seven phases. Nothing in this app should invent a
different pattern.

```text
        SUBMITTER SIDE                 |            HR SIDE
                                       |
  1. Discover                          |
     catalog / balance / dashboard     |
            |                          |
  2. Fill                              |
     draft form, autosave, validate    |
            |                          |
  3. Review                            |
     read-only summary + impact        |
            |                          |
  4. Submit  -----------------------------> 5. Queue
     idempotency key, receipt          |      inbox, SLA clock, routing
            |                          |            |
            |                          |      6. Decide
            |                          |         approve / reject / return
            |                          |            |
            |<---- 7. Notify + Apply <-------------- |
     status, timeline, audit trail     |      post to ledger / master record
```

Phase rules:

1. **Discover** never requires a network round trip to render a permitted entry point.
   The catalog and balances come from versioned cache; only totals refresh live.
2. **Fill** is always resumable. A half-completed form that is killed by the OS must
   return to the same field.
3. **Review** is mandatory for anything that consumes balance, money, or changes a
   master record. It is optional for comments and low-risk metadata edits.
4. **Submit** is the only phase that mutates server state on the submitter side, and it
   is always idempotent.
5. **Queue** is server-owned. The client never computes who the approver is.
6. **Decide** is atomic and single-writer. Two approvers acting simultaneously must
   produce one decision and one conflict error, never two decisions.
7. **Apply** is a server-side consequence of the decision, not a second user action.
   The submitter learns about it through notification and status, never by polling.

## 4. Canonical State Machine

Every submitted record in this product uses these states. Workflow-specific states are
additions, never replacements.

| State | Owner | Meaning | Visible to submitter |
|---|---|---|---|
| `Draft` | Submitter | Exists on device and/or as a server draft; not in any queue | Yes |
| `Submitted` | System | Accepted, validated, routing in progress | Yes |
| `PendingApproval` | Approver | Sitting in one approver's inbox at level *n* | Yes, with approver name |
| `ReturnedForInfo` | Submitter | Approver asked for changes; editable again | Yes, with comment |
| `Approved` | System | All approval levels cleared | Yes |
| `Rejected` | System | Terminal negative decision with reason | Yes, with reason |
| `Applied` | System | Posted to the owning ledger or master record | Yes |
| `Closed` | System | Terminal; no further action possible | Yes |
| `Withdrawn` | Submitter | Cancelled by submitter before decision | Yes |
| `Cancelled` | HR | Cancelled by HR after decision, with reason | Yes |
| `Expired` | System | SLA or validity window elapsed without decision | Yes |

Allowed transitions:

```text
Draft            -> Submitted, (deleted)
Submitted        -> PendingApproval, Rejected, Approved
PendingApproval  -> PendingApproval (next level), Approved, Rejected,
                    ReturnedForInfo, Withdrawn, Expired
ReturnedForInfo  -> Submitted, Withdrawn
Approved         -> Applied, Cancelled
Applied          -> Closed, Cancelled
Rejected         -> Closed
Withdrawn        -> Closed
Expired          -> Closed, PendingApproval (after escalation)
```

Guard rules:

- A transition is rejected if the client-supplied `version` does not match the server
  version. The client shows `ConflictResolutionCard` (WID-104) and reloads.
- `Withdrawn` is only allowed while the record is `Submitted`, `PendingApproval` or
  `ReturnedForInfo`. After `Approved` the submitter must raise a cancellation request.
- `Applied` is never set by a client call. It is set by the owning module after the
  decision commits.
- No state is skipped for convenience. A zero-approver policy still passes through
  `Submitted -> Approved` so the audit trail is uniform.

## 5. Submitter-Side Contract

Applies to every "employee fills" phase in section 8.

| # | Rule |
|---|---|
| S1 | The entry point shows the user's own current position first (balance, last punch, open claims) before the form. |
| S2 | Drafts autosave on field completion and on app background, into encrypted local storage. |
| S3 | Validation runs in three tiers: field-level on blur, form-level on `Review`, server-authoritative on `Submit`. Never rely only on client validation. |
| S4 | Server-computed values — leave days, tax impact, overtime hours, claim eligibility — are requested from the server (`Calculate` command), never computed on device. |
| S5 | Attachments upload before submit, through the protected file flow (MBK-038), and are referenced by ticket ID in the submission body. |
| S6 | Every submit carries a client-generated idempotency key held with the draft. Retrying the same draft never creates a second record. |
| S7 | Offline submit is queued in the explicit outbox (WID-103) only where section 12 permits it. Where it does not, the submit button is disabled with a clear reason, not silently failed. |
| S8 | On success the submitter gets a receipt: reference number, current state, named next approver, and expected SLA. |
| S9 | The submitter can always reach the record's timeline (WID-069) and audit trail (WID-078) from the receipt and from the list. |
| S10 | `ReturnedForInfo` reopens the same record for editing. It never asks the submitter to start a new one. |
| S11 | Sensitive values in a draft (salary, identifiers, medical reason) are masked by default via `SensitiveValueReveal` (WID-108) and excluded from ordinary caches. |
| S12 | Withdrawal is available from the detail screen whenever the state permits it, and requires a confirmation with the consequence stated. |

## 6. HR-Side Contract

Applies to every "HR manages" phase in section 8.

| # | Rule |
|---|---|
| H1 | All pending work reaches HR through one inbox (MOB-097) regardless of workflow, with per-type tabs. Workflow-specific list screens are filtered views over the same data, never a second source of truth. |
| H2 | Routing, approver resolution, delegation and escalation are server-side. The client renders what the server assigned. |
| H3 | Every decision screen shows: what is being asked, what changes if approved, who asked, what evidence exists, and what the remaining chain is. |
| H4 | Reject and Return always require a reason. Approve requires a comment only where policy configuration demands it. |
| H5 | Reason codes come from configured masters (MBK-005), not free text alone. Free text supplements the code. |
| H6 | Decisions are live-only. No decision is ever queued offline. |
| H7 | Decisions are idempotent and version-checked. A duplicate submit returns the original result, not a second decision. |
| H8 | Bulk approval is permitted only for homogeneous, low-risk items and never for anything that changes compensation or master data. |
| H9 | Every action writes an audit entry with actor, tenant, target, before/after summary, correlation ID and outcome. |
| H10 | UI permission checks mirror API authorization; hiding a button is never the only control. |
| H11 | PII on HR screens is field-permission shaped by the API response, not filtered on the device. |
| H12 | After a decision, the item leaves the inbox immediately and the submitter is notified within the notification SLA. |

## 7. Workflow Inventory

| ID | Workflow | Submitter | HR/approver side | Screens |
|---|---|---|---|---|
| WF-001 | Leave request and approval | Employee | Manager, then HR | MOB-079–086 |
| WF-002 | Comp-off and encashment | Employee | HR payroll | MOB-088, 097–098 |
| WF-003 | Attendance correction | Employee | Manager or attendance owner | MOB-070, 073–075 |
| WF-004 | Overtime and attendance exception | System-raised, employee-confirmed | Manager, then payroll | MOB-072, 078 |
| WF-005 | Shift swap | Employee pair | Shift owner | MOB-076–077 |
| WF-006 | Reimbursement claim | Employee | Manager, then finance/payroll | MOB-094–095, 097–098 |
| WF-007 | Tax declaration and proof | Employee | Payroll verifier | MOB-091–092 |
| WF-008 | Benefits enrolment | Employee | HR benefits owner | MOB-096 |
| WF-009 | Personal data change | Employee | HR records owner | MOB-051–056 |
| WF-010 | Document submission and verification | Employee | HR verifier | MOB-057, 066 |
| WF-011 | Dynamic HR service request | Employee | Service owner queue | MOB-099–101 |
| WF-012 | New-joiner onboarding | New joiner | HR onboarding owner | MOB-059–066 |
| WF-013 | Probation confirmation | Manager | HR | MOB-067 |
| WF-014 | Resignation and offboarding | Employee | Manager, HR, clearance owners | MOB-068 |
| WF-015 | Recruitment intake to offer | Requisition raiser and candidate | Recruiter, panel, approver | MOB-021–048 |

## 8. Workflow Specifications

Step IDs use `.E` for the submitter side and `.H` for the HR side.

---

### WF-001 — Leave request and approval

**Trigger:** employee needs time off. **Owning module:** Leave. **Controller:** MBK-026.

Employee side:

| Step | Screen | What happens |
|---|---|---|
| E1 | MOB-079 `/leave` | Shows balances (WID-096) and upcoming leave before any form. Entry point is `RequestLeave`. |
| E2 | MOB-080 `/leave/balances` | Optional. Confirms available/pending/used per type and links to the policy (MOB-087). |
| E3 | MOB-081 `/leave/requests/new` | Type, date range, partial-day handling, reason, optional attachment. `Calculate` returns server-computed working days, holiday exclusions and balance impact. |
| E4 | MOB-082 `/leave/requests/review` | Read-only summary: days consumed, resulting balance, overlapping team leave, named approver. |
| E5 | Submit | `POST /leave/requests` with idempotency key. Receipt shows reference, state `PendingApproval`, approver, SLA. |
| E6 | MOB-083 `/leave/requests/{id}` | Timeline, comments, `Withdraw` while the state permits it. |

HR/approver side:

| Step | Screen | What happens |
|---|---|---|
| H1 | MOB-097 `/approvals` | Item lands on the Leave tab with due date and priority. Also visible in MOB-084 as a team-scoped list. |
| H2 | MOB-086 `/leave/team/calendar` | Coverage check before deciding: who else is out in the same window. |
| H3 | MOB-085 `/leave/requests/{id}/decision` | Shows request, balance impact, overlap warnings, prior comments. Actions: `Approve`, `Reject`, `Return`. |
| H4 | — | On approve at final level: balance is debited, calendar entries created, attendance marked, submitter notified. |
| H5 | MOB-083 | HR retains cancellation with reason after approval; cancellation credits the balance back and reverses calendar entries. |

**States:** canonical. **Additional:** `PartiallyApproved` is *not* used; multi-level leave stays in `PendingApproval` with an incrementing level.

**Edge cases:** retrospective dates beyond the configured window require HR rather than manager approval; negative-balance requests are blocked at E3 unless policy allows LOP conversion, which must be shown explicitly at E4; a cancelled approved leave that overlaps a locked payroll period is rejected with a period-locked error.

---

### WF-002 — Comp-off and encashment

**Trigger:** employee has eligible credits. **Controller:** MBK-026.

Employee side: E1 MOB-088 shows eligible credits with their source and expiry; E2 the employee selects credits and requests comp-off or encashment; E3 the server returns the calculated value or leave-day equivalent; E4 submit.

HR side: H1 the request lands in MOB-097 under the payroll tab; H2 MOB-098 shows the credit origin, expiry and monetary impact; H3 approval credits leave balance or queues a payroll input for the next open period.

**Edge cases:** credits expiring inside the approval SLA are flagged at H1 and escalate immediately; encashment is blocked when the payroll period is locked, and the request is held rather than rejected.

---

### WF-003 — Attendance correction

**Trigger:** missing or wrong punch. **Controller:** MBK-024.

Employee side:

| Step | Screen | What happens |
|---|---|---|
| E1 | MOB-070 `/attendance/me` | Anomalous days are visibly flagged on `AttendanceDayCard` (WID-093) with a `Correction` action. |
| E2 | MOB-074 `/attendance/corrections/new` | Prefilled with the recorded punches. Employee supplies requested times and a reason code plus optional evidence. |
| E3 | Review + submit | Server recomputes worked hours and shows the delta before submit. |

HR/approver side: H1 the item appears in MOB-097 and in the daily register (MOB-072) as an open exception; H2 MOB-075 shows a before/after comparison, the raw device evidence from the original punch, and the reason; H3 approval rewrites the attendance day and recomputes downstream overtime and leave deductions.

**Edge cases:** corrections into a locked attendance period are rejected with the period lock date; repeated corrections by the same employee within a configured window raise a flag on H2 rather than blocking; a correction that would remove an already-approved overtime claim must reject with a dependency error and name the dependent record.

---

### WF-004 — Overtime and attendance exception

**Trigger:** system-detected exception from punch data. This workflow inverts the usual order — the system creates the record, the employee confirms it, HR approves.

Employee side: E1 the exception appears on MOB-070 and as a task (MOB-019); E2 the employee confirms or disputes the calculated duration and supplies justification.

HR side: H1 MOB-078 shows exception type, evidence, calculated duration and the applicable overtime policy; H2 `Resolve` or `ApproveOvertime`; H3 approved overtime becomes a payroll input for the open period and appears on the register (MOB-072).

**Edge cases:** an unconfirmed exception past its window auto-expires and is recorded as unpaid, with the expiry visible to both sides; overtime above the statutory cap is blocked at H2 with the cap value shown.

---

### WF-005 — Shift swap

Employee side: E1 MOB-076 roster; E2 MOB-077 assignment detail, `Swap`; E3 the counterparty employee is selected via `EmployeeLookup` (WID-049) restricted to eligible, qualified, rested employees; E4 submit sends an acceptance request to the counterparty.

Counterparty: accepts or declines from MOB-019 or the notification. A declined swap closes without reaching HR.

HR side: H1 an accepted swap reaches the shift owner in MOB-097; H2 the decision screen shows both rosters, coverage after swap, and any rest-period or qualification violation; H3 approval rewrites both assignments atomically.

**Edge cases:** a swap is void if either assignment changes before approval — the version check fails and both parties are notified; swaps crossing a payroll or attendance lock are blocked.

---

### WF-006 — Reimbursement claim

**Controller:** MBK-029.

Employee side:

| Step | Screen | What happens |
|---|---|---|
| E1 | MOB-094 `/payroll/reimbursements` | Status tabs, running totals, remaining entitlement per type. |
| E2 | MOB-095 `/payroll/reimbursements/new` | Type, amount (WID-057), date, merchant, receipts captured with `CameraCaptureField` (WID-056). |
| E3 | — | Receipts upload first and are referenced by ticket. Eligibility and cap checks are server-computed. |
| E4 | Review + submit | Shows claimable versus claimed amount when a cap applies. |

HR side: H1 manager approval in MOB-098 with receipt preview (WID-077); H2 finance or payroll verification of amount, tax treatment and duplicate detection; H3 approved claims become a payroll input or a direct payment instruction depending on configuration; H4 the payment reference is written back to MOB-094.

**Edge cases:** duplicate receipt detection is server-side and surfaces at H1 with a link to the matching claim; claims for a closed financial year are blocked at E2 with the cutoff date; partial approval is permitted for this workflow only, and must record both the claimed and approved amounts.

---

### WF-007 — Tax declaration and proof

Employee side: E1 MOB-091 shows the current regime and projection; E2 MOB-092 declaration sections with per-section save; E3 proof documents attach per declaration line; E4 submit locks the declaration for the window.

HR side: H1 payroll verifier receives per-line verification work; H2 each line is accepted, reduced, or rejected with a reason, against the attached proof; H3 the verified total feeds the tax computation and the employee sees the revised projection.

**Edge cases:** declaration windows are configuration-driven and both sides show the window dates; a rejected line returns only that line for correction, never the whole declaration; proof documents are never cached to disk unprotected on either side.

---

### WF-008 — Benefits enrolment

Employee side: E1 MOB-096 shows the enrolment window, current coverage and plan comparison; E2 dependants are selected or added, which may itself trigger WF-010 for dependant proof; E3 submit within the window.

HR side: H1 the benefits owner reviews eligibility, dependant proof and premium impact; H2 approval writes coverage effective dates and a payroll deduction input; H3 the enrolment confirmation is issued as a document to the employee.

**Edge cases:** enrolment outside the window requires a qualifying-life-event request, which routes through WF-011 rather than this workflow.

---

### WF-009 — Personal data change

Employee side: E1 MOB-051 overview shows which sections are self-editable; E2 the employee edits MOB-052 (personal), MOB-056 (contacts and dependants) or their own address; E3 fields configured as controlled do not save directly — they raise a change request with before/after values.

HR side: H1 the records owner receives the change request with a field-level before/after comparison; H2 supporting proof is required for controlled fields (name, identifiers, bank details); H3 approval writes the master record and adds an entry to the employment timeline (MOB-058).

**Edge cases:** bank and statutory identifier changes always require proof and always notify the employee on the previously registered channel as well as the new one; a change request pending against a field blocks a second concurrent request on the same field.

---

### WF-010 — Document submission and verification

Employee side: E1 MOB-057 lists required, submitted, verified, rejected and expiring documents; E2 upload or capture; E3 the document enters `Submitted` and is not usable until verified.

HR side: H1 verification work appears in the verification queue and, for new joiners, in MOB-066; H2 the verifier previews the document (WID-077), checks extracted data against the master record, and chooses `Verify`, `Reject` or `RequestAgain`; H3 verified documents set expiry-driven reminders where the type requires renewal.

**Edge cases:** rejection must name the defect so the employee is not left guessing; expiring documents raise a reminder to the employee at the configured lead time and to HR at expiry; a document rejected twice escalates to the HR owner rather than looping.

---

### WF-011 — Dynamic HR service request

This is the catch-all workflow. Anything not covered by WF-001–010 must be modelled here
rather than as a new bespoke screen.

Employee side: E1 MOB-099 catalog, filtered to services the user is eligible for; E2 MOB-100 renders the service's schema through `DynamicFormRenderer` (WID-060) with server-supplied validation and attachments; E3 submit against the schema version that was rendered.

HR side: H1 the request routes to the service owner queue defined in configuration; H2 MOB-101 shows fields, SLA state, timeline and comments; H3 the owner comments, requests information, fulfils or cancels; H4 fulfilment may attach an output document.

**Edge cases:** if the schema version changed between render and submit, the server rejects and the client re-renders with the values it can safely carry forward; SLA breach escalates per section 11 without changing state.

---

### WF-012 — New-joiner onboarding

This is the longest chain and the clearest example of the two-sided pattern.

HR preparation (before the joiner exists): MOB-059 identity with duplicate check, MOB-060 employment, MOB-061 organisation, MOB-062 account and role assignment. `CreateEmployee` provisions the account and issues the invite.

Joiner side:

| Step | Screen | What happens |
|---|---|---|
| E1 | MOB-001–007 | First sign-in, device registration, biometric setup. |
| E2 | MOB-064 `/onboarding/{employeeId}` | The joiner sees only their own checklist, with stage progress. |
| E3 | MOB-065 | Each task: instructions, due date, evidence upload, completion. |
| E4 | MOB-052/056 | Personal data and contacts completed by the joiner (routes through WF-009 rules for controlled fields). |
| E5 | MOB-057 | Required documents uploaded (routes into WF-010). |

HR side:

| Step | Screen | What happens |
|---|---|---|
| H1 | MOB-063 `/onboarding` | Cohort view: progress, overdue tasks, owners. This is the HR management surface for the whole intake. |
| H2 | MOB-066 | Document verification per joiner. |
| H3 | MOB-064 | Reassign tasks, chase overdue items, mark HR-owned tasks complete. |
| H4 | — | When all mandatory tasks are verified, the employee record moves to active and the probation clock starts (WF-013). |

**Edge cases:** a joiner who does not start before the joining date raises an HR alert on H1; onboarding cannot complete while any mandatory document is unverified, and the blocking item must be named on both sides.

---

### WF-013 — Probation confirmation

Manager side: E1 the probation review task appears ahead of the due date; E2 MOB-067 captures goals, feedback and a recommendation (confirm, extend, or separate).

HR side: H1 HR reviews the recommendation with the full probation history; H2 confirm writes the employment status change and timeline entry; extend sets a new review date and re-arms the reminder; H3 a separation recommendation hands over to WF-014.

**Edge cases:** an overdue probation review escalates to HR and, past a configured grace period, auto-confirms or holds according to policy — the policy choice must be visible on the screen, not implicit.

---

### WF-014 — Resignation and offboarding

Employee side: E1 the employee raises a resignation through WF-011 or a dedicated entry; E2 intended last working date, reason, and notice-period calculation shown by the server.

HR/manager side:

| Step | Screen | What happens |
|---|---|---|
| H1 | MOB-097 | Manager acknowledgement and comment. |
| H2 | MOB-068 `/employees/{id}/offboarding` | HR sets the agreed last working date, notice adjustment and reason category. |
| H3 | MOB-068 | Clearance checklist fans out to owners: IT, finance, admin, manager. Each owner sees only their own tasks. |
| H4 | — | Final settlement input is raised to payroll once all clearance tasks close. |
| H5 | — | On the last working date: account deactivation, device revocation (MOB-109 equivalent server-side), access removal, timeline entry. |

**Edge cases:** open leave, pending claims and unreturned assets must be listed on H2 before the date is agreed; account revocation is time-triggered by the last working date, never by the checklist completing early.

---

### WF-015 — Recruitment intake to offer

This is the workflow the nine reference images in `docs/reference-screens.md` depict. The
submitter side splits in two: an internal requisition raiser, and an external candidate on
a separate public surface.

**Stage 1 — internal fill (requisition):** E1 MOB-024 create requisition with department, headcount, budget and justification; E2 submit; H1 the approval chain runs through MOB-025 and MOB-097; H2 an approved requisition unlocks job creation.

**Stage 2 — recruiter fill (job):** H3 MOB-029 basics, MOB-030 description, MOB-031 compensation, MOB-032 review and publish. REF-MOB-003 is the read view of the result (MOB-028). REF-MOB-002 is the managed list (MOB-026) with its segmented status views and quick actions.

**Stage 3 — candidate fill (application):** the candidate applies on the public careers surface. No candidate signs into this app. The application arrives as a record in the recruiter's pipeline.

**Stage 4 — HR manages:**

| Step | Screen | Reference | What happens |
|---|---|---|---|
| H4 | MOB-021 `/recruitment` | REF-MOB-001 | Recruiter's daily view: job KPIs, pipeline funnel, upcoming interviews, ageing. |
| H5 | MOB-035 `/recruitment/applicants` | REF-MOB-006 | Search, filter chips, applicant cards with stage and rating. |
| H6 | MOB-036 / MOB-037 | REF-MOB-007 | Application summary and permission-shaped candidate profile. |
| H7 | MOB-040 | — | Screening scorecard; draft is encrypted, submit is live. |
| H8 | MOB-041 / MOB-042 | — | Stage change or shortlist/reject decision with reason and templated notification. |
| H9 | MOB-044 | — | Interview scheduling against panel availability. |
| H10 | MOB-043 / MOB-045 | REF-MOB-004, REF-MOB-009 | Interview schedule and detail. The same `InterviewCard` (WID-087) serves the dashboard, the schedule and the interview notification tab. |
| H11 | MOB-046 | — | Panel evaluation; only the assigned interviewer can submit. |
| H12 | MOB-048 | — | Offer generation, approval chain, and send. |
| H13 | — | — | Offer acceptance raises `CandidateHired`, which starts WF-012. |

**Notifications:** REF-MOB-008 and REF-MOB-009 are the two tabs of MOB-016. Interview
notifications must reuse the schedule card and its route contract rather than a
notification-only variant.

**Edge cases:** a job cannot be published without an approved requisition with remaining headcount; moving an applicant to a stage that requires a completed scorecard is blocked with the missing artefact named; compensation is never cached on device after submit; offer approval and send are separate permissions.

---

## 9. Notification Events

Every workflow raises events on both sides. Event keys are configured in `admin.md` §16;
this table fixes which workflow raises which class of event.

| Event class | Raised at | Recipient | Deep link |
|---|---|---|---|
| `submitted` | Phase 4 | Approver | Decision screen |
| `pending.reminder` | SLA lead time | Approver | Decision screen |
| `returned` | Return decision | Submitter | The editable record |
| `approved` | Final approval | Submitter | Record detail |
| `rejected` | Rejection | Submitter | Record detail with reason |
| `applied` | Phase 7 | Submitter | Ledger or master view |
| `escalated` | SLA breach | Escalation target | Decision screen |
| `expired` | Window elapsed | Both | Record detail |
| `cancelled` | Post-approval cancellation | Submitter | Record detail |

Rules: every notification carries the record reference, the workflow ID and a deep link
that resolves to a permitted screen; a recipient who has lost permission sees a
"no longer available" state rather than an error; notification text never contains
salary, identifiers or medical reasons.

## 10. SLA, Delegation and Escalation

- Each workflow type has a configured decision SLA. The clock starts when the item enters
  an approver's inbox, not at submit, and pauses in `ReturnedForInfo`.
- Delegation (MOB-102) is time-bounded and scoped by workflow type. A delegated decision
  records both the delegate and the original approver in the audit trail.
- Escalation on SLA breach re-routes to the configured target and raises `escalated`. The
  record stays `PendingApproval`; escalation is a routing change, not a state change.
- Approval chains are resolved at submit and re-resolved on escalation. A chain is never
  recomputed silently mid-flight, because that would move an item out of an inbox someone
  is already looking at.
- An approver may never approve their own submission. Where the chain resolves to the
  submitter, it skips to the next level and records the skip.

## 11. Offline Behaviour

Section S7 permits queued submission only where the row below says so.

| Workflow | Draft offline | Submit offline | Decide offline | Reason |
|---|---|---|---|---|
| WF-001 Leave | Yes | No | No | Balance must be validated server-side at submit |
| WF-002 Comp-off | Yes | No | No | Credit eligibility is server-computed |
| WF-003 Correction | Yes | Yes, queued | No | Low risk, deterministic, dated |
| WF-004 Overtime | Yes | Yes, queued | No | Confirmation only |
| WF-005 Shift swap | Yes | No | No | Requires live counterparty availability |
| WF-006 Reimbursement | Yes | No | No | Receipts must upload first |
| WF-007 Tax | Yes, encrypted | No | No | Window and proof validation |
| WF-008 Benefits | No | No | No | Window-bound, sensitive |
| WF-009 Personal data | Yes, encrypted | No | No | Controlled fields need proof |
| WF-010 Documents | Capture only | No | No | Upload requires network |
| WF-011 HR request | Yes, encrypted | No | No | Schema version must be validated |
| WF-012 Onboarding | Narrow task drafts | No | No | Evidence upload required |
| WF-013 Probation | Notes only | No | No | Status change is sensitive |
| WF-014 Offboarding | No | No | No | Sensitive throughout |
| WF-015 Recruitment | Screening and evaluation drafts, encrypted | No | No | Decisions and PII are live-only |

No decision is ever taken offline in any workflow. Queued submissions surface in the sync
centre (MOB-110) with their state, and a failed queued submission must reopen the draft
rather than being discarded.

## 12. Screen Coverage

Screens reached by workflow. Screens not listed here are navigation, dashboard, settings
or reporting surfaces that do not carry workflow state.

| Workflow | Screens |
|---|---|
| WF-001 | MOB-079, 080, 081, 082, 083, 084, 085, 086, 087, 097, 098 |
| WF-002 | MOB-088, 097, 098 |
| WF-003 | MOB-070, 072, 073, 074, 075, 097 |
| WF-004 | MOB-019, 070, 072, 078, 097 |
| WF-005 | MOB-019, 076, 077, 097 |
| WF-006 | MOB-094, 095, 097, 098, 106 |
| WF-007 | MOB-091, 092, 097 |
| WF-008 | MOB-096, 097 |
| WF-009 | MOB-051, 052, 053, 054, 056, 058, 097, 098 |
| WF-010 | MOB-057, 066, 097 |
| WF-011 | MOB-099, 100, 101, 097, 098 |
| WF-012 | MOB-059–066, 019, 052, 056, 057 |
| WF-013 | MOB-019, 067, 097 |
| WF-014 | MOB-068, 097, 098, 109 |
| WF-015 | MOB-021–048, 016, 097 |

## 13. Backend Implications

The controller inventory in `backend.md` §8 covers these workflows, with three
clarifications this design makes explicit:

1. `ApprovalsController` (MBK-031) is the single decision surface. Workflow-specific
   controllers expose reads and submissions; they must not each implement their own
   approve/reject endpoint with a different contract.
2. Every submission endpoint accepts an idempotency key header and returns the original
   result on replay, per `backend.md` §12.
3. Every decision endpoint requires the record version and returns `409` with the current
   server state on mismatch, so the client can render WID-104.

Workflow definitions — approval chains, SLA values, reason-code sets, escalation targets,
enrolment and declaration windows — are tenant configuration owned by the admin plan, not
constants in mobile or server code.

## 14. Build Order

| Phase | Workflows | Rationale |
|---|---|---|
| 1 | WF-015 stages 2 and 4 | The reference screens are already implemented as a design slice; wiring them to real state proves the pattern on the most-specified surface. |
| 2 | WF-001, WF-003 | Highest daily volume, simplest state, exercises the full submit/approve/apply chain end to end. |
| 3 | WF-011, WF-010 | The generic request and document engines; once these exist, most remaining HR asks need configuration, not code. |
| 4 | WF-006, WF-002, WF-004, WF-005 | Payroll-adjacent and roster workflows, once the ledger integration is in place. |
| 5 | WF-012, WF-013, WF-009 | Lifecycle workflows, which depend on documents and change-request approval already working. |
| 6 | WF-007, WF-008, WF-014 | Window-bound and terminal workflows. |

## 15. Definition of Done

A workflow is complete when all of the following hold:

- both sides are implemented — a submitter can create it and an approver can decide it;
- every canonical state in section 4 is reachable and correctly rendered on both sides;
- the submit path is idempotent and the decision path is version-checked;
- reason codes come from configuration, not hard-coded lists;
- SLA, escalation and delegation behave per section 10;
- offline behaviour matches its row in section 11 exactly, including the disabled-with-reason case;
- all nine notification classes that apply are raised, deep-linked and permission-checked;
- the audit trail shows actor, before/after, correlation ID and outcome for every transition;
- UI permission checks are mirrored by API authorization, verified by a test that calls the API without the permission;
- the workflow is exercised by an end-to-end test that runs fill → submit → decide → apply → notify.
