# Database comparison: current app schema vs `HrManagemtNewDb29062026`

Compared on 2026-10-26.

* **Current**: the live database `VisplHRManagement`, built by the app's own EF Core migrations
  (structure in `kapson-hrms-schema.sql`).
* **Proposed**: `HrManagemtNewDb29062026_Create.sql` (fixed copy; the untouched original is
  `HrManagemtNewDb29062026_Create.original.sql`).

## At a glance

| | Current (live) | Proposed script |
|---|---|---|
| Tables | 63 in 6 schemas (`employee`, `payroll`, `platform`, `recruitment`, `workforce`, `identity`) | 68 in `dbo`, `audit`, `rpt` |
| Who creates it | 32 EF Core migrations, applied automatically when the app starts | One SQL script run by hand |
| Used by the app today | Yes — 48 entity/repository files, 43 services, ~10,600 lines of data-layer code | **No.** Nothing in the app reads or writes these tables |
| Multi-company (tenant) | Yes — every row carries `TenantKey` | No — single company |
| Keys | `bigint` identities + text codes (`DepartmentCode = 'HR'`) | `int`/`tinyint` numbers (`DepartmentNo = 1`) |
| Lookups (gender, religion, state…) | One table, `platform.MasterOptions` (30 categories, has `ParentCode` for state→city) | 15 separate lookup tables |
| Foreign keys | 7 (login tables only) | 122 |
| CHECK constraints | Few (rules live in C# services) | 114 (PAN, Aadhaar, IFSC, dates, statuses…) |
| Logic in the database | None | 17 triggers, 79 stored procedures, 4 report views |
| Logins & security | ASP.NET Identity: hashed passwords, lockout, permission-based roles, refresh tokens, HR / employee / candidate portals | Own `Users` table with one role per user and a plain `PasswordHash` column; no lockout logic, tokens or portals |
| Sensitive data | Bank numbers encrypted (Data Protection) | Bank number, PAN, Aadhaar stored as plain text |
| Dates | `datetimeoffset` (time zone safe) | `DATETIME2` + `GETDATE()` (server local time) |
| Concurrency | `rowversion` on every table | None |

## What each has that the other lacks

**Only in the current schema** — all of it is used by working screens and the mobile app:
attendance punches, corrections and exceptions · movement (out-of-office) records · gate-pass
scans · leave request days, credits, adjustments and comments · per-user notifications · career
site, themes, mail settings, media files · bank transfer batches and loan repayments · employee
timeline, change requests and dependants · hiring stage history, interview panels and scorecards,
onboarding checklists · candidate accounts and activation · payroll payslip snapshots.

**Only in the proposed script** — genuinely useful additions:
statutory IDs (PAN, Aadhaar, UAN, EPF, ESI) with format checks · PF/ESI details · education history
with CGPA · previous employers · skills · permanent/local addresses · employee documents with
verification · sub-departments · country → state → city with real foreign keys · allowances and
deductions linked per state and grade · loan sureties and three-level loan approval · support
tickets · onboarding wizard drafts · a database-level audit log.

## Recreating the live database with the proposed script

**Pros**
* Strong integrity: bad PAN/Aadhaar/IFSC values, impossible dates and orphaned rows are refused by
  the database itself, whatever program writes to it.
* Richer employee master (statutory, education, previous employment, addresses, documents).
* Audit trail and report views are usable from SSMS, Excel or Power BI without the app.
* Stored procedures give other tools (reports, integrations) a stable way in.

**Cons**
* **The app stops working.** Every screen, the mobile API and payroll read the current tables.
  Moving to the new schema means rewriting the data layer (~10,600 lines) and most of the 43
  services — effectively rebuilding the backend.
* **All current data is lost** — employees, logins, payroll history, attendance, recruitment.
  The script has no migration from the current tables.
* **Security goes backwards**: no lockout, refresh tokens or per-permission roles; Aadhaar, PAN and
  bank numbers in plain text (a compliance risk under India's DPDP Act).
* **Single company only** — the multi-tenant design is gone.
* **Two places for business rules**: leave balance checks, status transitions and approvals would
  exist both in stored procedures and in the C# services, and drift apart.
* Server-local timestamps and no concurrency control.

## Recommendation

**Do not recreate the live database from this script.** Keep the current schema as the single
source of truth and bring the proposed script's good ideas in as normal EF Core migrations, one
area at a time:

1. **Constraints first** — add foreign keys and CHECK constraints (PAN, Aadhaar, IFSC, date order,
   status lists) to the existing tables. Low risk, immediate integrity gain.
2. **Employee master extensions** — new `employee.*` tables for statutory IDs (encrypted like bank
   numbers), PF/ESI, education, previous employment, addresses and documents, with screens added
   alongside.
3. **Geography** — either keep `MasterOptions` with `ParentCode` (already used for state→city) or
   add proper Country/State/City tables keyed by code and tenant.
4. **Payroll configuration** — state + grade component linking, mapped onto the existing
   `GradeSalaryStructures`.
5. **Audit** — extend the existing identity audit with an EF Core interceptor rather than triggers,
   so the who/why is captured from the signed-in user.

## Fixes made to the script

All marked inline with `FIX F<n>`; verified by building it twice in a throwaway database (0 errors
both times) and running constraint tests.

| # | Problem in the original | Fix |
|---|---|---|
| F1 | Not re-runnable: 104 errors on a second run (tables can't be dropped while foreign keys point at them; seed data inserted twice) | Section 1A drops all foreign keys, then all views, procedures and tables, before rebuilding |
| F2 | `usp_Division_Delete` failed to compile — `Employee.Status` doesn't exist | Uses `EmployeeStatus = 'Active'` |
| F3 | Leave approval never saved who approved it (`@ApprovedById = @ApprovedById`) | Sets `ApprovedByID` |
| F4 | Every insert/update wrote two audit rows (the timestamp trigger's own update was audited too) | Timestamp triggers run on UPDATE only; audit triggers skip the timestamp trigger's update |
| F5 | Bank account CHECK accepted letters (`ABCDEFGH1`) | Digits only, 9–18 long |
| F6 | Link Allowance/Deduction sample data always skipped (ran before grades and pay components existed) | Moved after Section 25; linked to Punjab, where GRADE 1 is defined |
| F7 | Loan reference padded to 2 digits, not 3 (`G01-G01-1`) | `G001-G001-1` |
| F8 | Noida Extension filed under Delhi | Uttar Pradesh |

Still worth knowing: the sample `Users` rows have placeholder password hashes (nobody can sign in
with them), and `CK_Emp_DOB` / `CK_EmpPC_Dates` compare against `GETDATE()`, so they are checked
only when a row is written.
