# Hiring Pipeline — Apply to Onboarding

How an open position becomes a hired employee: a candidate submits a CV against a published
opening, HR moves the application through screening and interview, an accepted offer opens an
onboarding checklist, and a completed checklist produces the employee record.

## 1. Stages

```
Job published
   │
   ▼
Applied ──────► Screening ──────► Shortlisted ──────► Interviewing
   │                │                   │                  │
   │                ▼                   ▼                  ▼
   └──────────► Rejected  ◄───────────────────────────► Offered
                                                            │
                                                    Offer accepted
                                                            │
                                                            ▼
                                                      Onboarding
                                                            │
                                              all required items complete
                                                            │
                                                            ▼
                                                    Employee created
```

`Withdrawn` is available to the candidate from `Applied` through `Offered`.

Stage transitions are the only way an application moves. Every transition records who made it,
when, and why. Stage is never edited directly.

| From | To | Who | Requires |
|---|---|---|---|
| Applied | Screening | HR | `RECRUITMENT.APPLICATIONS` |
| Screening | Shortlisted / Rejected | HR | `RECRUITMENT.APPLICATIONS`, reason on reject |
| Shortlisted | Interviewing | HR | `RECRUITMENT.APPLICATIONS` |
| Interviewing | Offered / Rejected | HR | `RECRUITMENT.MANAGE` |
| Offered | OfferAccepted / OfferDeclined | Candidate | own application only |
| OfferAccepted | Onboarding | system | automatic on acceptance |
| Onboarding | Hired | system | every required task complete |
| any (≤ Offered) | Withdrawn | Candidate | own application only |

## 2. Applying

The apply endpoint is **public and unauthenticated**. A candidate does not need an account to
apply; the account is created for them and activated later by a one-time link.

### What the candidate submits

Core fields, always required:

| Field | Rule |
|---|---|
| Full name | 2–160 characters |
| Email | valid address, 256 max — this is the candidate's identity key within a tenant |
| Phone | 7–20 characters |
| Total experience | decimal years, 0–60 |
| Current location | up to 180 characters |
| Notice period | days, 0–365 |
| Consent | must be explicitly granted; the submission is refused without it |

Plus a **résumé**, always required, and any documents the opening declares.

### Per-opening requirements

An opening carries `RequiredDocumentCodes` — codes drawn from the existing `DocumentCategory`
master (ID Proof, Educational, Experience, PAN, Aadhaar, …). The apply form renders one upload
slot per code, and the submission is rejected until every one is filled. This reuses the master
data already configured per tenant rather than introducing a second vocabulary.

An opening also carries `ScreeningQuestions` — free-text or single-choice questions stored as
JSON. Answers are stored with the application and shown to HR on the detail sheet.

### File rules

These are enforced in the business layer, not the controller, so every caller gets them:

- **Allowed types only**: PDF, DOC, DOCX, PNG, JPEG. Anything else is refused.
- **Extension and content are both checked.** The declared content type must match the
  allowlist *and* the leading bytes must match the claimed format. A `.pdf` that does not begin
  with `%PDF` is refused.
- **10 MB per file, 25 MB per submission.**
- **The original filename never reaches disk.** Files are stored under a generated GUID name via
  `IMediaStorage`, which already resolves and validates paths against its root. The original name
  is kept in the database as display metadata only.
- **Files are never served from a static path.** Download goes through an authorised endpoint
  that streams the bytes after a permission check.
- Uploads land in state `PendingScan`. A file is not downloadable until a scanner marks it
  `Clean`. Until a scanner is wired, the configured policy decides whether `PendingScan` files
  may be released — the default is no.

### Abuse controls

The endpoint is public, so it needs limits that do not depend on a signed-in identity:

- Rate limit per client address and per tenant.
- One application per candidate email per opening. A second submission updates the first rather
  than creating a duplicate, and only while the application is still in `Applied`.
- The opening must be published and not past its expiry, or the submission is refused.

## 3. Candidate identity

A candidate record is keyed by `(TenantKey, Email)`. The first application creates it.

**No password is collected at apply time.** Submitting an application issues the activation link
and **emails it** — `PublicApplyController` calls `ICandidateActivationService.RequestAsync` and
then `IEmailSender`, so a candidate never has to ask for one. Both are orchestrated in the
controller rather than in the application service, because the recruitment module does not know
about identity or mail and should not learn.

That link is not a convenience. The stage machine stops at `Offered` and only the candidate can
move it further, so without a login the pipeline cannot reach a hire at all. Issuing it never
fails an application: if the activation or the mail cannot be sent, the error is logged and the
CV is still accepted — a lost link can be requested again from `/onboarding`, a lost application
cannot.

The link points at **`/onboarding`**, built in `CandidateLinks` so there is exactly one place
that composes it. Mail goes through the two services described in [mail.md](mail.md); in
Development the link is additionally logged only when the send fails, never otherwise — an
activation link in a log file is a way in.

A candidate sees only their own applications, their own documents, and published openings. This
is enforced in the service layer by candidate id from the token, never by a client-supplied id.

### The candidate account becomes the employee account

An email address belongs to exactly one account. Somebody who applied already holds a login on
that address, so when HR grants them mobile access the existing account is **moved** to the
Employee portal (`IAccessAccountService.TransferPortalAsync`) rather than duplicated:

* the username changes to the employee-number form, because the portals do not share a shape;
* the password is replaced, so the credential that reached the candidate portal cannot reach the
  employee one;
* the candidate roles are dropped and the Employee role attached;
* `CandidateId` is cleared, so the token stops carrying `candidate_id` — the link from person to
  application survives on the employee record, which is where it belongs;
* `LastLoginUtc` is cleared, because a sign-in on the old portal is not use of the new one, and
  leaving it would tell HR the employee is already using an app they have never opened.

If the address is held by an account on any *other* portal, the grant is refused with an
explanation instead of silently rewiring an HR or platform login.

## 4. Onboarding

Accepting an offer creates an `Onboarding` record and seeds it from a per-tenant template — a
checklist of tasks, each either a document the candidate must upload or a form section they must
complete.

Default seeded tasks:

| Task | Type | Owner |
|---|---|---|
| Personal details | Form | Candidate |
| Address and emergency contact | Form | Candidate |
| Bank account for salary | Form | Candidate |
| PAN card | Document | Candidate |
| Aadhaar | Document | Candidate |
| Educational certificates | Document | Candidate |
| Previous experience letters | Document | Candidate |
| Signed offer letter | Document | Candidate |
| Background verification | Form | HR |
| Asset allocation | Form | HR |

Each task is `Pending`, `Submitted`, `Approved`, or `Rejected`. Candidate tasks become
`Submitted` on upload; HR approves or rejects with a reason. Onboarding completes only when every
task marked required is `Approved`.

Completion raises `OnboardingCompleted`, which the employee module consumes to create the
employee record. The recruitment module does not write employee rows itself — that boundary is
the one the modular monolith depends on.

## 4a. Mobile access is granted, never implied

Being hired creates an employee record. It does not create a login. Somebody has to decide that
this person may carry the company's attendance, leave and salary data on a phone, and take it
back when they leave — so the grant is an explicit HR action with an audit trail, not a side
effect of provisioning.

`GET /api/mobile/v1/employees/mobile-access` lists every employee with their standing, those
awaiting a decision first:

| State | Meaning |
|---|---|
| `NotGranted` | Hired, no account. The row HR acts on. |
| `AwaitingFirstSignIn` | Granted; the temporary password has not been used yet. |
| `Active` | In use. |
| `Suspended` | The account exists and is switched off. |
| `Blocked` | No email address on the employee record, so no account can be made. |

Granting also writes the new account's id onto the employee record. Without that link the login
works and reaches nothing: every self-service read resolves the caller's own employee by account
id, so the person would sign in and find no attendance, no leave and no payslip of their own.
Restoring suspended access re-checks the link, which repairs accounts granted before it existed.

Granting returns the temporary password **once**. It is never stored in readable form and cannot
be recovered — a lost one is replaced by a reset, not by looking it up. The account is created
against the Employee portal with the `EMPLOYEE` role (self-service only) unless a wider role is
named; a manager who approves their team's leave is a deliberate choice, not something the grant
should infer.

Both of these are on the HR portal dashboard as *Applicants by open position* and
*Employee app access*.

## 4b. Where the candidate does their part

> **Two callers, one behaviour.** Applying happens two ways — the public API and the career
> site's own apply page, which calls `IApplicationService` directly. Twice now that has meant
> one of them silently missing something the other had:
>
> - The activation email was orchestrated in `PublicApplyController`, so applications from the
>   website created an account and sent nothing, while the page told the candidate an email was
>   on its way. It now lives behind `ICandidateInvitation`, which both callers use.
> - Identity validation came from `[ApiController]` model binding, which the page does not get.
>   A submission reached the database with no name, no email and no phone — a candidate nobody
>   could ever contact or activate. Those guards are now in `ApplicationService.SubmitAsync`,
>   where both callers get them.
>
> The rule this keeps proving: anything both entry points must do belongs below both of them.

**`/onboarding`** — public, and the only candidate-facing screen in the product. It is where the
activation mail points and where somebody returns to afterwards. It is reachable from the career
site's header and footer on every page, and the apply page's confirmation links straight to it —
that confirmation is the one moment a candidate is certainly still reading, and leaving the link
to be discovered in an inbox is how people fall out of the process.

`/career/{tenant}/onboarding` is the same page. Given a tenant, it wears that client's own
career-site header, footer and theme, and stops asking which company: somebody who followed a
link from a company's own site should not have to know our tenant key, and an unbranded form
makes a legitimate link look like a phishing page.

It handles four states from one route: setting a password from a mail link, signing in,
requesting a fresh link, and the signed-in view of their own applications and checklist. It
talks to the public and candidate APIs over HTTP rather than reaching into the business
services, so it is held to exactly the same rules a phone is — a candidate sees their own
applications because the token says so, not because the page remembered to filter. Being
server-side Blazor, the token stays in the circuit and never reaches the browser.

`GET /api/candidate/v1/me` exists for it: everything one candidate may see about themselves,
resolved from their own token. Without it the only candidate-facing read was "fetch onboarding
15", which nobody arriving from an email could possibly supply.

Two things the page deliberately does differently from the HR view:

- **Progress is theirs, not ours.** `CompletionPercent` counts what HR has *approved*, which is
  the right number for HR and a demoralising lie to somebody who has just uploaded everything —
  it reads 0%. The page counts the candidate's own outstanding items instead, and says "we are
  checking what you sent" once nothing is left on them.
- **Stages are explained in words they would use.** "Interviewing" is our name for a column on a
  board; "we are arranging interviews" is what it means to the person it is about.

## 5. Permissions

| Action | Permission |
|---|---|
| Submit an application | none — public |
| View applications | `RECRUITMENT.APPLICATIONS` |
| Move an application stage | `RECRUITMENT.APPLICATIONS` |
| Make or withdraw an offer | `RECRUITMENT.MANAGE` |
| Download a candidate document | `RECRUITMENT.APPLICATIONS` |
| View or approve onboarding | `EMPLOYEES.VIEW` / `EMPLOYEES.CREATE` |
| See the openings board | `RECRUITMENT.VIEW` |
| See who has mobile access | `EMPLOYEES.VIEW` |
| Grant, suspend or restore mobile access | `ACCOUNTS.MANAGE` |

Candidate-facing routes are authorised by candidate identity, not by these permissions.

## 6. Data protection

- Candidate records carry personal data. They are tenant-scoped by `TenantKey` on every query;
  there is no cross-tenant read path.
- Documents are private by default and released only through a permission-checked stream.
- Rejected applications retain their documents for a configurable window, then the files are
  deleted while the application record and its audit trail are kept.
- Consent is recorded with a timestamp and the policy version shown at the time.

## 7. Endpoints

Public:

```
GET  /api/public/v1/openings                      published openings
GET  /api/public/v1/openings/{slug}               opening detail plus requirements
POST /api/public/v1/openings/{slug}/apply         multipart submission
```

HR, bearer plus permission:

```
GET   /api/mobile/v1/recruitment/applications
GET   /api/mobile/v1/recruitment/applications/{id}
POST  /api/mobile/v1/recruitment/applications/{id}/stage
GET   /api/mobile/v1/recruitment/applications/{id}/documents/{documentId}
GET   /api/mobile/v1/onboarding/{id}
POST  /api/mobile/v1/onboarding/{id}/tasks/{taskId}/complete
POST  /api/mobile/v1/onboarding/{id}/tasks/{taskId}/decision
GET   /api/mobile/v1/recruitment/openings/pipeline    applicants per vacancy
GET   /api/mobile/v1/employees/mobile-access
POST  /api/mobile/v1/employees/mobile-access/{employeeId}/grant
POST  /api/mobile/v1/employees/mobile-access/{employeeId}/suspend
POST  /api/mobile/v1/employees/mobile-access/{employeeId}/restore
```

Candidate, bearer:

```
GET   /api/candidate/v1/applications
POST  /api/candidate/v1/applications/{id}/offer      accept or decline
GET   /api/candidate/v1/onboarding/{id}
POST  /api/candidate/v1/onboarding/{id}/tasks/{taskId}
```

## 8. Build order

1. Entities, DbContext configuration, migration.
2. Contracts for application and onboarding.
3. Repositories, then services carrying the validation and file rules.
4. Public apply endpoint and the career-site apply form.
5. HR review endpoints, then the mobile applicant screens against real data.
6. Onboarding checklist and its candidate-facing screens.
7. `OnboardingCompleted` handler in the employee module.
8. Mobile access: the grant, and the HR dashboard panels that drive it.

## 9. The chain, verified end to end

Everything above is exercised as one arc against a running host by
`scripts/verify-hiring-chain.py`: apply on the tenant → the board shows the applicant against the position →
HR walks it to `Offered` → the candidate accepts from their own login → both sides finish the
checklist → the employee is provisioned → HR grants mobile access → the employee signs in, is
forced to replace the password, and lands on the Employee portal. It also checks the negatives:
an HR token cannot accept somebody's offer, a second grant is refused, suspending stops the
login, and the person ends up with exactly one account.
