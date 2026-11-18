/*
  Kapson HRMS demo data refresh — structural part.

  Turns the original sample dataset into a smaller, non-repetitive one that every module can
  demo end to end. Masters (lookups, grades, salary structures, leave policies, shifts,
  holidays, policies, roles) are kept as they are.

  Runs in one transaction: either all of it applies or none of it does. Take a backup first.
  After this script, run the DemoDataRefresh tool: it creates the logins, re-encrypts bank
  accounts and regenerates payroll through the application's own services.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   -- sqlcmd defaults it off; filtered indexes need it on
SET ANSI_NULLS ON;
BEGIN TRANSACTION;

DECLARE @T nvarchar(20) = N'kapson';
DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();

-- Guard: only run against the original sample data, never twice.
IF NOT EXISTS (SELECT 1 FROM employee.Employees WHERE TenantKey = @T AND EmployeeNumber = 'EMP-1019')
    THROW 50001, 'Demo refresh already applied (EMP-1019 not found). Restore the backup to run again.', 1;

------------------------------------------------------------------------------------------------
-- 1. Remove near-duplicate employees and everything that hangs off them
------------------------------------------------------------------------------------------------
DECLARE @gone TABLE (Id bigint PRIMARY KEY, Number nvarchar(60));
INSERT @gone SELECT Id, EmployeeNumber FROM employee.Employees
WHERE TenantKey = @T AND EmployeeNumber IN ('EMP-1005', 'EMP-1014', 'EMP-1019', 'EMP-1021');

DELETE s FROM workforce.GatePassScans s JOIN workforce.GatePasses g ON g.Id = s.GatePassId WHERE g.EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.GatePasses            WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE c FROM workforce.LeaveRequestComments c JOIN workforce.LeaveRequests r ON r.Id = c.RequestId WHERE r.EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.LeaveRequestDays      WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.LeaveRequests         WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.LeaveBalances         WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.LeaveCredits          WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.LeaveAdjustments      WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.AttendancePunches     WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.AttendanceDays        WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.AttendanceExceptions  WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.AttendanceCorrections WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.MovementRecords       WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.OnDutyRequests        WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM workforce.ShiftAssignments      WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM employee.EmployeeBankAccounts   WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM employee.EmployeeChangeRequests WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM employee.EmployeeTimeline       WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM platform.PolicyAcknowledgements WHERE EmployeeId IN (SELECT Number FROM @gone);   -- keyed by employee number
DELETE r FROM payroll.LoanRepayments r JOIN payroll.Loans l ON l.Id = r.LoanId WHERE l.EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM payroll.Loans                   WHERE EmployeeId IN (SELECT Id FROM @gone);
DELETE FROM employee.Employees              WHERE Id IN (SELECT Id FROM @gone);

------------------------------------------------------------------------------------------------
-- 2. Give everyone a distinct role. Grades stay as they were, so pay history stays coherent.
------------------------------------------------------------------------------------------------
DECLARE @roles TABLE (Num nvarchar(20), Title nvarchar(120), Dept nvarchar(40), Division nvarchar(40), Location nvarchar(40), Manager nvarchar(20));
INSERT @roles VALUES
  ('EMP-1003', 'Senior Financial Analyst',       'FINOPS', NULL,  NULL,         NULL),
  ('EMP-1006', 'Payroll Executive',              'FINOPS', NULL,  NULL,         NULL),
  ('EMP-1010', 'QA Engineer',                    'DIGITAL', NULL, NULL,         NULL),
  ('EMP-1011', 'Data Analyst',                   'DIGITAL', NULL, NULL,         NULL),
  ('EMP-1013', 'Brand Marketing Executive',      'BRAND', NULL,   NULL,         NULL),
  ('EMP-1017', 'Area Manager',                   'RETAIL', NULL,  NULL,         NULL),
  ('EMP-1020', 'Cashier',                        'RETAIL', NULL,  NULL,         NULL),
  ('EMP-1022', 'Customer Relations Executive',   'RETAIL', NULL,  NULL,         NULL),
  ('EMP-1023', 'Talent Acquisition Executive',   'HR',     'HO',  'CHANDIGARH', 'EMP-1001');

UPDATE e SET
    JobTitle = r.Title,
    DepartmentCode = r.Dept,
    DivisionCode = COALESCE(r.Division, e.DivisionCode),
    LocationCode = COALESCE(r.Location, e.LocationCode),
    CostCentre = 'CC-' + r.Dept,
    ManagerEmployeeId = COALESCE((SELECT m.Id FROM employee.Employees m WHERE m.TenantKey = @T AND m.EmployeeNumber = r.Manager), e.ManagerEmployeeId),
    UpdatedUtc = @now
FROM employee.Employees e JOIN @roles r ON r.Num = e.EmployeeNumber
WHERE e.TenantKey = @T;

-- Status follows the joining date: under three months is still probation.
UPDATE e SET EmploymentStatusCode = CASE WHEN e.JoinedOn > DATEADD(month, -3, CAST(@now AS date)) THEN 'PROBATION' ELSE 'ACTIVE' END
FROM employee.Employees e WHERE e.TenantKey = @T;

-- Designations master: one entry per title now in use.
INSERT platform.Designations (TenantKey, Code, Name, GradeCode, DepartmentCode, Description, SortOrder, IsActive, UpdatedUtc)
SELECT @T, v.Code, v.Name, v.Grade, v.Dept, v.Descr, v.Sort, 1, @now
FROM (VALUES
  ('SFA', 'Senior Financial Analyst',     'SPECIALIST', 'FINOPS',  'Leads budgeting, forecasting and month-end financial reporting', 11),
  ('PE',  'Payroll Executive',            'EXECUTIVE',  'FINOPS',  'Runs monthly payroll, statutory deductions and salary disbursement', 12),
  ('QAE', 'QA Engineer',                  'SPECIALIST', 'DIGITAL', 'Tests web and mobile releases and owns the regression suite', 13),
  ('DA',  'Data Analyst',                 'SPECIALIST', 'DIGITAL', 'Builds sales and inventory dashboards for category teams', 14),
  ('BME', 'Brand Marketing Executive',    'SPECIALIST', 'BRAND',   'Plans campaigns and in-store brand activations', 15),
  ('AM',  'Area Manager',                 'MANAGER',    'RETAIL',  'Oversees several stores in a region against sales and service targets', 16),
  ('CSH', 'Cashier',                      'EXECUTIVE',  'RETAIL',  'Handles billing, returns and end-of-day cash reconciliation', 17),
  ('CRE', 'Customer Relations Executive', 'EXECUTIVE',  'RETAIL',  'Handles customer queries, alterations and loyalty enrolment', 18),
  ('TAE', 'Talent Acquisition Executive', 'EXECUTIVE',  'HR',      'Sources and screens candidates and coordinates interviews', 19)
) v(Code, Name, Grade, Dept, Descr, Sort)
WHERE NOT EXISTS (SELECT 1 FROM platform.Designations d WHERE d.TenantKey = @T AND d.Code = v.Code);

-- Timeline: joining titles follow the new roles; confirmations read as confirmations.
UPDATE t SET Title = 'Joined as ' + e.JobTitle,
             Summary = 'Onboarded in ' + e.DepartmentCode + ' (' + e.LocationCode + ') under grade ' + e.GradeCode + '.'
FROM employee.EmployeeTimeline t JOIN employee.Employees e ON e.Id = t.EmployeeId
WHERE t.TenantKey = @T AND t.EventType = 'Hired';

UPDATE t SET Title = 'Probation confirmed — ' + e.JobTitle,
             Summary = CASE e.EmployeeNumber
                 WHEN 'EMP-1001' THEN 'Confirmed after six months; took over payroll and compliance ownership.'
                 WHEN 'EMP-1004' THEN 'Confirmed after clean audit of vendor payments in the first half-year.'
                 WHEN 'EMP-1007' THEN 'Confirmed and given ownership of the online menswear category.'
                 WHEN 'EMP-1011' THEN 'Confirmed; moved from engineering to analytics to build sales dashboards.'
                 WHEN 'EMP-1015' THEN 'Confirmed after the Sector 17 store beat its first-year sales target.'
                 ELSE 'Confirmed after successful probation review.' END
FROM employee.EmployeeTimeline t JOIN employee.Employees e ON e.Id = t.EmployeeId
WHERE t.TenantKey = @T AND t.EventType = 'Promoted';

INSERT employee.EmployeeTimeline (TenantKey, EmployeeId, EventType, Title, Summary, EffectiveOn, ActorId, ActorName, OccurredUtc)
SELECT @T, e.Id, 'Transferred', 'Moved to HR as Talent Acquisition Executive',
       'Moved from the Delhi NCR store team to head office to run campus and store hiring.', '2026-06-01', NULL, 'Simran Kaur', '2026-06-01T09:30:00+05:30'
FROM employee.Employees e WHERE e.TenantKey = @T AND e.EmployeeNumber = 'EMP-1023';

------------------------------------------------------------------------------------------------
-- 3. Emergency contacts and dependants: one real-looking contact each, no copy-paste names
------------------------------------------------------------------------------------------------
DELETE FROM employee.EmployeeContacts   WHERE TenantKey = @T;
DELETE FROM employee.EmployeeDependants WHERE TenantKey = @T;

INSERT employee.EmployeeContacts (TenantKey, EmployeeId, Kind, Name, Relationship, Phone, Email, DateOfBirth, IsPrimary, IsVerified, UpdatedUtc)
SELECT @T, e.Id, 'Emergency', v.Name, v.Rel, v.Phone, NULL, NULL, 1, 1, @now
FROM employee.Employees e JOIN (VALUES
  ('EMP-1001', 'Harjeet Singh',     'Spouse',  '+919814022101'),
  ('EMP-1002', 'Sunil Verma',       'Father',  '+919814022102'),
  ('EMP-1003', 'Manpreet Kaur',     'Spouse',  '+919814022103'),
  ('EMP-1004', 'Amit Sharma',       'Spouse',  '+919814022104'),
  ('EMP-1006', 'Rakesh Bansal',     'Father',  '+919814022106'),
  ('EMP-1007', 'Nimrat Ahluwalia',  'Spouse',  '+919814022107'),
  ('EMP-1008', 'Sushma Gupta',      'Mother',  '+919814022108'),
  ('EMP-1009', 'Vivek Khanna',      'Brother', '+919814022109'),
  ('EMP-1010', 'Nikhil Joshi',      'Spouse',  '+919814022110'),
  ('EMP-1011', 'Anil Vardhan',      'Father',  '+919814022111'),
  ('EMP-1012', 'Kunal Chopra',      'Spouse',  '+919814022112'),
  ('EMP-1013', 'Seema Kapoor',      'Mother',  '+919814022113'),
  ('EMP-1015', 'Rupinder Kaur',     'Spouse',  '+919814022115'),
  ('EMP-1016', 'Gurleen Gill',      'Spouse',  '+919814022116'),
  ('EMP-1017', 'Balwinder Dhillon', 'Spouse',  '+919814022117'),
  ('EMP-1018', 'Jaswant Bains',     'Father',  '+919814022118'),
  ('EMP-1020', 'Kuldeep Brar',      'Father',  '+919814022120'),
  ('EMP-1022', 'Paramjit Bajwa',    'Mother',  '+919814022122'),
  ('EMP-1023', 'Navneet Randhawa',  'Spouse',  '+919814022123'),
  ('EMP-1024', 'Rajiv Sood',        'Father',  '+919814022124'),
  ('EMP-1025', 'Meenu Aggarwal',    'Mother',  '+919814022125')
) v(Num, Name, Rel, Phone) ON v.Num = e.EmployeeNumber
WHERE e.TenantKey = @T;

INSERT employee.EmployeeDependants (TenantKey, EmployeeId, DisplayName, RelationCode, DateOfBirth, IsBenefitEligible, UpdatedUtc)
SELECT @T, e.Id, v.Name, v.Rel, v.Dob, 1, @now
FROM employee.Employees e JOIN (VALUES
  ('EMP-1001', 'Harjeet Singh',    'HUSBAND',  '1990-02-11'),
  ('EMP-1001', 'Gurnoor Kaur',     'DAUGHTER', '2019-07-03'),
  ('EMP-1003', 'Manpreet Kaur',    'WIFE',     '1990-05-21'),
  ('EMP-1003', 'Arjan Singh',      'SON',      '2016-12-09'),
  ('EMP-1004', 'Amit Sharma',      'HUSBAND',  '1995-08-30'),
  ('EMP-1007', 'Nimrat Ahluwalia', 'WIFE',     '1994-04-17'),
  ('EMP-1010', 'Nikhil Joshi',     'HUSBAND',  '1997-01-25'),
  ('EMP-1012', 'Kunal Chopra',     'HUSBAND',  '1993-11-02'),
  ('EMP-1015', 'Rupinder Kaur',    'WIFE',     '1992-06-14'),
  ('EMP-1015', 'Jasleen Kaur',     'DAUGHTER', '2018-03-28'),
  ('EMP-1016', 'Gurleen Gill',     'WIFE',     '1993-09-08'),
  ('EMP-1017', 'Balwinder Dhillon','HUSBAND',  '1991-12-19'),
  ('EMP-1023', 'Navneet Randhawa', 'WIFE',     '1998-03-06')
) v(Num, Name, Rel, Dob) ON v.Num = e.EmployeeNumber
WHERE e.TenantKey = @T;

------------------------------------------------------------------------------------------------
-- 4. Recruitment: coherent pipeline from Applied through to Hired
------------------------------------------------------------------------------------------------
-- Published openings are Active, with a salary band and a closing date.
UPDATE j SET Status = 'Active', StatusChangedUtc = j.PublishedUtc, ExpiresOn = '2026-12-31', SalaryCurrencyCode = 'INR',
    SalaryMinimum = v.MinCtc, SalaryMaximum = v.MaxCtc, UpdatedUtc = @now
FROM recruitment.JobOpenings j JOIN (VALUES
  (1, 840000, 1100000), (2, 480000, 650000), (3, 300000, 420000), (4, 720000, 950000), (5, 420000, 560000)
) v(Id, MinCtc, MaxCtc) ON v.Id = j.Id
WHERE j.TenantKey = @T;

-- The role the company hires most, and the one Diya was hired into.
INSERT recruitment.JobOpenings (TenantKey, Slug, Title, Department, Location, EmploymentType, WorkMode, Experience, Summary,
    ResponsibilitiesJson, RequirementsJson, IsPublished, PublishedUtc, UpdatedUtc, ExpiresOn, RequiredDocumentCodesJson,
    SalaryCurrencyCode, SalaryMaximum, SalaryMinimum, ScreeningQuestionsJson, Status, StatusChangedUtc)
SELECT @T, 'sales-executive-289', 'Sales Executive', 'Retail Operations', 'Chandigarh', 'Full Time', 'On-site', '0–2 years',
    'Help customers find the right outfit, keep the floor guest-ready and grow repeat business at our Sector 17 flagship.',
    N'["Greet and assist walk-in customers","Maintain floor standards and stock replenishment","Enrol customers in the loyalty programme"]',
    N'["Good spoken Hindi, Punjabi and English","Comfortable with weekend and festive-season shifts","Prior retail experience is a plus"]',
    1, '2026-09-15T10:00:00+05:30', @now, '2026-12-31', N'[]', 'INR', 300000, 240000, N'[]', 'Active', '2026-09-15T10:00:00+05:30'
WHERE NOT EXISTS (SELECT 1 FROM recruitment.JobOpenings WHERE TenantKey = @T AND Slug = 'sales-executive-289');
DECLARE @salesJob bigint = (SELECT Id FROM recruitment.JobOpenings WHERE TenantKey = @T AND Slug = 'sales-executive-289');

-- Employees were wrongly linked to unrelated candidates.
UPDATE employee.Employees SET CandidateId = NULL WHERE TenantKey = @T AND EmployeeNumber IN ('EMP-1001', 'EMP-1024');

-- Stages: Nidhi has accepted and is onboarding; Palak holds an offer; Zoya and Raghav match their interviews.
UPDATE recruitment.Applications SET Stage = 'Onboarding',   StageChangedUtc = '2026-10-21T15:00:00+05:30', StageChangedBy = 'system' WHERE TenantKey = @T AND Id = 4;
UPDATE recruitment.Applications SET Stage = 'Offered',      StageChangedUtc = '2026-10-23T12:00:00+05:30', StageChangedBy = 'EMP-1001' WHERE TenantKey = @T AND Id = 10;
UPDATE recruitment.Applications SET Stage = 'Interviewing', StageChangedUtc = '2026-10-20T11:00:00+05:30' WHERE TenantKey = @T AND Id = 13;
UPDATE recruitment.Applications SET Stage = 'Shortlisted',  StageChangedUtc = '2026-10-21T11:00:00+05:30' WHERE TenantKey = @T AND Id = 11;
UPDATE recruitment.Applications SET RejectionReason = 'Portfolio did not show large-format store work.' WHERE TenantKey = @T AND Id = 14;

-- Expected salaries that sit in or near each opening's band (the originals were random).
UPDATE a SET ExpectedCtc = v.Ctc
FROM recruitment.Applications a JOIN (VALUES
  (1, 960000), (2, 900000), (3, 870000), (4, 1020000),   -- Store Manager       8.4–11.0 L
  (5, 610000), (6, 540000), (7, 640000),                 -- Financial Analyst   4.8–6.5 L
  (8, 380000), (9, 320000), (10, 400000),                -- HR Executive        3.0–4.2 L
  (11, 900000), (12, 860000),                            -- E-com Category Lead 7.2–9.5 L
  (13, 520000), (14, 540000)                             -- Visual Merchandiser 4.2–5.6 L
) v(Id, Ctc) ON v.Id = a.Id
WHERE a.TenantKey = @T;

-- Palak's old onboarding belonged to a hack that linked her to the HR manager.
DELETE FROM recruitment.OnboardingTasks WHERE TenantKey = @T AND OnboardingId IN (SELECT Id FROM recruitment.Onboardings WHERE TenantKey = @T AND ApplicationId = 10);
DELETE FROM recruitment.Onboardings     WHERE TenantKey = @T AND ApplicationId = 10;

-- Nidhi joins as Store Manager on 16 Nov; 3 of 5 checklist items approved, 1 waiting for HR.
UPDATE recruitment.Onboardings SET JoiningOn = '2026-11-16', StartedUtc = '2026-10-21T15:00:00+05:30', UpdatedUtc = @now
WHERE TenantKey = @T AND ApplicationId = 4;

-- Diya: applied for Sales Executive, onboarding completed, joins 4 Nov (her employee record exists).
INSERT recruitment.Candidates (TenantKey, Email, FullName, Phone, CurrentLocation, TotalExperienceYears, AccountId, CreatedUtc, UpdatedUtc)
SELECT @T, 'diya.aggarwal@example.com', 'Diya Aggarwal', '+919815100115', 'Panchkula', 1.0, NULL, '2026-09-22T19:10:00+05:30', @now
WHERE NOT EXISTS (SELECT 1 FROM recruitment.Candidates WHERE TenantKey = @T AND Email = 'diya.aggarwal@example.com');
DECLARE @diyaCand bigint = (SELECT Id FROM recruitment.Candidates WHERE TenantKey = @T AND Email = 'diya.aggarwal@example.com');

INSERT recruitment.Applications (TenantKey, JobOpeningId, CandidateId, Stage, Source, NoticePeriodDays, ExpectedCtc, AnswersJson, Rating,
    RejectionReason, ConsentGranted, ConsentPolicyVersion, ConsentUtc, AppliedUtc, StageChangedUtc, StageChangedBy, UpdatedUtc)
VALUES (@T, @salesJob, @diyaCand, 'Hired', 'CareerSite', 15, 264000, N'{}', 4.4, NULL, 1, '1.0',
    '2026-09-22T19:10:00+05:30', '2026-09-22T19:10:00+05:30', '2026-10-20T17:00:00+05:30', 'system', @now);
DECLARE @diyaApp bigint = SCOPE_IDENTITY();

INSERT recruitment.Onboardings (TenantKey, ApplicationId, CandidateId, Stage, JoiningOn, StartedUtc, CompletedUtc, UpdatedUtc)
VALUES (@T, @diyaApp, @diyaCand, 'Completed', '2026-11-04', '2026-10-10T12:00:00+05:30', '2026-10-20T17:00:00+05:30', @now);
DECLARE @diyaOnb bigint = SCOPE_IDENTITY();

INSERT recruitment.OnboardingTasks (TenantKey, OnboardingId, Code, Title, Instructions, TaskType, OwnerKind, IsRequired, SortOrder,
    Status, DocumentId, FormValuesJson, DecisionReason, DecidedBy, DecidedUtc, UpdatedUtc)
SELECT @T, @diyaOnb, t.Code, t.Title, t.Instructions, t.TaskType, t.OwnerKind, t.IsRequired, t.SortOrder, 'Approved', NULL,
    CASE WHEN t.TaskType = 'Form' THEN N'{"bloodGroup":"B+","emergencyContact":"+91 98140 22125"}' END,
    NULL, 'Simran Kaur', DATEADD(day, t.SortOrder, CAST('2026-10-12T12:00:00+05:30' AS datetimeoffset)), @now
FROM recruitment.OnboardingTasks t
WHERE t.OnboardingId = (SELECT Id FROM recruitment.Onboardings WHERE TenantKey = @T AND ApplicationId = 4);

UPDATE employee.Employees SET CandidateId = @diyaCand WHERE TenantKey = @T AND EmployeeNumber = 'EMP-1025';

-- Interviews that match each job and stage.
UPDATE recruitment.Interviews SET ApplicationId = 1, CandidateId = 1, JobOpeningId = 1, RoundCode = 'MANAGERIAL',
    RoundLabel = 'Round 2 - Store Operations Assessment', StartUtc = '2026-10-28T11:00:00+05:30', Mode = 'Onsite',
    LocationOrLink = 'Kapson Flagship Store, Sector 17, Chandigarh', Status = 'Scheduled', UpdatedUtc = @now
WHERE TenantKey = @T AND Id = 1;
UPDATE recruitment.Interviews SET RoundCode = 'SCREENING', RoundLabel = 'Round 1 - HR & Culture Fit',
    StartUtc = '2026-10-12T11:00:00+05:30', Status = 'Completed', UpdatedUtc = @now
WHERE TenantKey = @T AND Id = 2;
UPDATE recruitment.Interviews SET ApplicationId = 5, CandidateId = 5, JobOpeningId = 2, RoundCode = 'TECHNICAL',
    RoundLabel = 'Round 1 - Finance Technical Interview', StartUtc = '2026-10-29T15:00:00+05:30', Mode = 'Online',
    LocationOrLink = 'https://meet.kapson.in/fin-tech-103', Status = 'Scheduled', UpdatedUtc = @now
WHERE TenantKey = @T AND Id = 3;

-- Stage history so each application's detail sheet tells its story.
DELETE FROM recruitment.ApplicationEvents WHERE TenantKey = @T;
DECLARE @path TABLE (Seq int, Stage nvarchar(40));
INSERT @path VALUES (0,'Applied'),(1,'Screening'),(2,'Shortlisted'),(3,'Interviewing'),(4,'Offered'),(5,'OfferAccepted'),(6,'Onboarding'),(7,'Hired');

INSERT recruitment.ApplicationEvents (TenantKey, ApplicationId, FromStage, ToStage, Reason, ActorId, ActorKind, OccurredUtc)
SELECT @T, a.Id, '', 'Applied', NULL, NULL, 'Candidate', a.AppliedUtc
FROM recruitment.Applications a WHERE a.TenantKey = @T;

INSERT recruitment.ApplicationEvents (TenantKey, ApplicationId, FromStage, ToStage, Reason, ActorId, ActorKind, OccurredUtc)
SELECT @T, a.Id, p0.Stage, p1.Stage, NULL,
       CASE WHEN p1.Stage = 'OfferAccepted' THEN NULL WHEN p1.Stage IN ('Onboarding','Hired') THEN 'system' ELSE 'EMP-1001' END,
       CASE WHEN p1.Stage = 'OfferAccepted' THEN 'Candidate' WHEN p1.Stage IN ('Onboarding','Hired') THEN 'System' ELSE 'Hr' END,
       DATEADD(hour, 26 * p1.Seq, a.AppliedUtc)
FROM recruitment.Applications a
JOIN @path cur ON cur.Stage = a.Stage
JOIN @path p1 ON p1.Seq BETWEEN 1 AND cur.Seq
JOIN @path p0 ON p0.Seq = p1.Seq - 1
WHERE a.TenantKey = @T;

INSERT recruitment.ApplicationEvents (TenantKey, ApplicationId, FromStage, ToStage, Reason, ActorId, ActorKind, OccurredUtc)
SELECT @T, a.Id, 'Applied', 'Screening', NULL, 'EMP-1001', 'Hr', DATEADD(hour, 26, a.AppliedUtc) FROM recruitment.Applications a WHERE a.TenantKey = @T AND a.Stage = 'Rejected'
UNION ALL
SELECT @T, a.Id, 'Screening', 'Rejected', a.RejectionReason, 'EMP-1001', 'Hr', a.StageChangedUtc FROM recruitment.Applications a WHERE a.TenantKey = @T AND a.Stage = 'Rejected';

------------------------------------------------------------------------------------------------
-- 5. Clear what the tool regenerates through the application's services
------------------------------------------------------------------------------------------------
-- Payroll history paid everyone the same ₹24,000 gross regardless of grade; it is regenerated.
DELETE FROM payroll.LoanRepayments;
DELETE FROM payroll.PayrollLines;
DELETE FROM payroll.BankTransferBatches;
DELETE FROM payroll.PayrollRuns;

-- Notifications referred to people and events that no longer exist; the tool writes fresh ones.
DELETE FROM platform.Notifications WHERE TenantKey = @T;

-- Old employee logins shared one password from source code and had mismatched names.
DELETE FROM [identity].RefreshTokens WHERE UserId IN (SELECT Id FROM [identity].Users WHERE Portal = 2);
DELETE FROM [identity].Users WHERE Portal = 2;              -- UserRoles cascade
UPDATE employee.Employees SET AccountId = NULL WHERE TenantKey = @T;

-- Development-only HR login: its password is in the repository. Kept for history, switched off.
UPDATE [identity].Users SET IsActive = 0, EmployeeNumber = NULL, MobileAccessEnabled = 0 WHERE UserName = 'hrdev';

COMMIT TRANSACTION;

SELECT 'employees' AS item, COUNT(*) AS n FROM employee.Employees WHERE TenantKey = @T
UNION ALL SELECT 'designations', COUNT(*) FROM platform.Designations WHERE TenantKey = @T
UNION ALL SELECT 'applications', COUNT(*) FROM recruitment.Applications WHERE TenantKey = @T
UNION ALL SELECT 'onboardings', COUNT(*) FROM recruitment.Onboardings WHERE TenantKey = @T
UNION ALL SELECT 'attendance days', COUNT(*) FROM workforce.AttendanceDays WHERE TenantKey = @T
UNION ALL SELECT 'leave requests', COUNT(*) FROM workforce.LeaveRequests WHERE TenantKey = @T;
