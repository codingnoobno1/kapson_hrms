using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DemoDataRefresh;

/// <summary>
/// Builds the demo data pack from what is actually in the database after seeding, so the PDF
/// can never describe data that is not there.
/// </summary>
sealed class ReportBuilder(ToolOptions options)
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private readonly StringBuilder html = new();

    public async Task<string> BuildAsync(IReadOnlyList<Credential> credentials)
    {
        var t = options.Tenant;
        var names = (await Q(@"SELECT Category, Code, Name FROM platform.MasterOptions WHERE TenantKey = @t", r => (r.GetString(0), r.GetString(1), r.GetString(2)), ("@t", t)))
            .GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.GroupBy(x => x.Item2, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First().Item3, StringComparer.OrdinalIgnoreCase));
        string Name(string category, string? code) =>
            code is not null && names.TryGetValue(category, out var map) && map.TryGetValue(code, out var n) ? n : code ?? "—";

        var people = await Q(@"
            SELECT e.EmployeeNumber, e.LegalFirstName + ' ' + e.LegalLastName, e.JobTitle, e.DepartmentCode, e.LocationCode, e.GradeCode,
                   e.JoinedOn, e.EmploymentStatusCode, m.LegalFirstName + ' ' + m.LegalLastName, e.Email, e.DivisionCode,
                   (SELECT COUNT(*) FROM employee.Employees r WHERE r.ManagerEmployeeId = e.Id)
            FROM employee.Employees e LEFT JOIN employee.Employees m ON m.Id = e.ManagerEmployeeId
            WHERE e.TenantKey = @t AND e.IsActive = 1 ORDER BY e.EmployeeNumber",
            r => new
            {
                Number = r.GetString(0), Name = r.GetString(1), Title = r.IsDBNull(2) ? "" : r.GetString(2), Dept = r.GetString(3),
                Location = r.GetString(4), Grade = r.IsDBNull(5) ? "" : r.GetString(5), Joined = DateOnly.FromDateTime(r.GetDateTime(6)),
                Status = r.GetString(7), Manager = r.IsDBNull(8) ? "—" : r.GetString(8), Email = r.IsDBNull(9) ? "" : r.GetString(9),
                Division = r.IsDBNull(10) ? "" : r.GetString(10), Reports = r.GetInt32(11)
            }, ("@t", t));

        var grades = await Q("SELECT GradeCode, BasicMonthly, ComponentsJson FROM platform.GradeSalaryStructures WHERE TenantKey = @t ORDER BY BasicMonthly",
            r => (Grade: r.GetString(0), Basic: r.GetDecimal(1), Json: r.GetString(2)), ("@t", t));

        var september = await Q(@"
            SELECT l.EmployeeNumber, l.DisplayName, l.Designation, l.GradeCode, l.PayableDays, l.DaysInMonth, l.Basic, l.Hra, l.Conveyance,
                   l.OtherAllowances, l.Gross, l.Pf, l.Esi, l.Tds, l.OtherDeductions, l.Deductions, l.NetPayable, l.Status, l.PaidOn
            FROM payroll.PayrollLines l JOIN payroll.PayrollRuns r ON r.Id = l.RunId
            WHERE r.TenantKey = @t AND r.Year = 2026 AND r.Month = 9 ORDER BY l.EmployeeNumber",
            r => new
            {
                Number = r.GetString(0), Name = r.GetString(1), Title = r.IsDBNull(2) ? "" : r.GetString(2), Grade = r.IsDBNull(3) ? "" : r.GetString(3),
                Basic = r.GetDecimal(6), Hra = r.GetDecimal(7), Conv = r.GetDecimal(8), Other = r.GetDecimal(9), Gross = r.GetDecimal(10),
                Pf = r.GetDecimal(11), Esi = r.GetDecimal(12), OtherDed = r.GetDecimal(14), Ded = r.GetDecimal(15), Net = r.GetDecimal(16), Status = r.GetString(17)
            }, ("@t", t));

        var runs = await Q(@"
            SELECT r.Year, r.Month, COUNT(l.Id), SUM(l.Gross), SUM(l.Deductions), SUM(l.NetPayable), MAX(b.ReferenceNo), MAX(b.TransferDate)
            FROM payroll.PayrollRuns r JOIN payroll.PayrollLines l ON l.RunId = r.Id LEFT JOIN payroll.BankTransferBatches b ON b.RunId = r.Id
            WHERE r.TenantKey = @t GROUP BY r.Year, r.Month ORDER BY r.Year, r.Month",
            r => (Month: new DateOnly(r.GetInt32(0), r.GetInt32(1), 1), Count: r.GetInt32(2), Gross: r.GetDecimal(3), Ded: r.GetDecimal(4), Net: r.GetDecimal(5),
                  Ref: r.IsDBNull(6) ? "—" : r.GetString(6), Date: r.IsDBNull(7) ? (DateOnly?)null : DateOnly.FromDateTime(r.GetDateTime(7)))
            , ("@t", t));

        var loans = await Q(@"
            SELECT l.VoucherNo, e.LegalFirstName + ' ' + e.LegalLastName, l.Type, l.Principal, l.MonthlyInstallment, l.Installments, l.StartYear, l.StartMonth, l.Purpose,
                   (SELECT COUNT(*) FROM payroll.LoanRepayments p WHERE p.LoanId = l.Id)
            FROM payroll.Loans l JOIN employee.Employees e ON e.Id = l.EmployeeId WHERE l.TenantKey = @t ORDER BY l.VoucherNo",
            r => (Voucher: r.GetString(0), Name: r.GetString(1), Type: r.GetString(2), Principal: r.GetDecimal(3), Emi: r.GetDecimal(4), Count: r.GetInt32(5),
                  Start: new DateOnly(r.GetInt32(6), r.GetInt32(7), 1), Purpose: r.IsDBNull(8) ? "" : r.GetString(8), Paid: r.GetInt32(9)), ("@t", t));

        var leave = await Q(@"
            SELECT e.LegalFirstName + ' ' + e.LegalLastName, ISNULL(p.Name, r.LeaveTypeCode), r.FromDate, r.ToDate, r.Days, r.Reason, r.State
            FROM workforce.LeaveRequests r JOIN employee.Employees e ON e.Id = r.EmployeeId
            LEFT JOIN workforce.LeavePolicies p ON p.Id = r.PolicyId
            WHERE r.TenantKey = @t AND r.State = 'Pending' ORDER BY r.FromDate",
            r => (Name: r.GetString(0), Type: r.GetString(1), From: DateOnly.FromDateTime(r.GetDateTime(2)), To: DateOnly.FromDateTime(r.GetDateTime(3)),
                  Days: r.GetDecimal(4), Reason: r.IsDBNull(5) ? "" : r.GetString(5)), ("@t", t));

        var leaveTotals = await Q(@"SELECT State, COUNT(*) FROM workforce.LeaveRequests WHERE TenantKey = @t GROUP BY State ORDER BY COUNT(*) DESC",
            r => (State: r.GetString(0), Count: r.GetInt32(1)), ("@t", t));

        var attendance = await Q(@"SELECT Status, COUNT(*) FROM workforce.AttendanceDays WHERE TenantKey = @t AND WorkDate >= '2026-10-01' GROUP BY Status ORDER BY COUNT(*) DESC",
            r => (Status: r.GetString(0), Count: r.GetInt32(1)), ("@t", t));

        var jobs = await Q(@"
            SELECT j.Title, j.Location, j.WorkMode, j.SalaryMinimum, j.SalaryMaximum, j.Status, (SELECT COUNT(*) FROM recruitment.Applications a WHERE a.JobOpeningId = j.Id)
            FROM recruitment.JobOpenings j WHERE j.TenantKey = @t ORDER BY j.Id",
            r => (Title: r.GetString(0), Location: r.GetString(1), Mode: r.GetString(2), Min: r.IsDBNull(3) ? 0 : r.GetDecimal(3), Max: r.IsDBNull(4) ? 0 : r.GetDecimal(4),
                  Status: r.GetString(5), Applicants: r.GetInt32(6)), ("@t", t));

        var pipeline = await Q(@"
            SELECT c.FullName, j.Title, a.Stage, a.AppliedUtc, a.Source, c.TotalExperienceYears, a.ExpectedCtc, c.Email
            FROM recruitment.Applications a JOIN recruitment.Candidates c ON c.Id = a.CandidateId JOIN recruitment.JobOpenings j ON j.Id = a.JobOpeningId
            WHERE a.TenantKey = @t ORDER BY CASE a.Stage WHEN 'Hired' THEN 0 WHEN 'Onboarding' THEN 1 WHEN 'Offered' THEN 2 WHEN 'Interviewing' THEN 3
                WHEN 'Shortlisted' THEN 4 WHEN 'Screening' THEN 5 WHEN 'Applied' THEN 6 ELSE 7 END, c.FullName",
            r => (Name: r.GetString(0), Job: r.GetString(1), Stage: r.GetString(2), Applied: r.GetDateTimeOffset(3), Source: r.GetString(4),
                  Exp: r.GetDecimal(5), Ctc: r.IsDBNull(6) ? 0 : r.GetDecimal(6), Email: r.GetString(7)), ("@t", t));

        var interviews = await Q(@"
            SELECT c.FullName, j.Title, i.RoundLabel, i.StartUtc, i.Mode, i.Status,
                   (SELECT STRING_AGG(p.DisplayName, ', ') FROM recruitment.InterviewPanelists p WHERE p.InterviewId = i.Id)
            FROM recruitment.Interviews i JOIN recruitment.Candidates c ON c.Id = i.CandidateId JOIN recruitment.JobOpenings j ON j.Id = i.JobOpeningId
            WHERE i.TenantKey = @t ORDER BY i.StartUtc",
            r => (Name: r.GetString(0), Job: r.GetString(1), Round: r.GetString(2), When: r.GetDateTimeOffset(3), Mode: r.GetString(4), Status: r.GetString(5),
                  Panel: r.IsDBNull(6) ? "—" : r.GetString(6)), ("@t", t));

        var onboarding = await Q(@"
            SELECT c.FullName, j.Title, o.Stage, o.JoiningOn, t.Title, t.Status, t.SortOrder
            FROM recruitment.Onboardings o JOIN recruitment.Candidates c ON c.Id = o.CandidateId
            JOIN recruitment.Applications a ON a.Id = o.ApplicationId JOIN recruitment.JobOpenings j ON j.Id = a.JobOpeningId
            JOIN recruitment.OnboardingTasks t ON t.OnboardingId = o.Id WHERE o.TenantKey = @t ORDER BY o.Id, t.SortOrder",
            r => (Name: r.GetString(0), Job: r.GetString(1), Stage: r.GetString(2), Joining: r.IsDBNull(3) ? (DateOnly?)null : DateOnly.FromDateTime(r.GetDateTime(3)),
                  Task: r.GetString(4), Status: r.GetString(5)), ("@t", t));

        // ---------------------------------------------------------------- document
        Open();
        var active = people.Where(p => p.Joined <= DateOnly.FromDateTime(DateTime.Today)).ToList();

        html.Append($"""
            <section class="cover">
              <div class="brand">KAPSON <span>HRMS</span></div>
              <h1>Demo Data Pack</h1>
              <p class="lead">Every person, login, salary and hiring record loaded into the Kapson HRMS platform, and what to try with each.</p>
              <div class="facts">
                <div><b>{active.Count}</b><span>employees working</span></div>
                <div><b>{people.Count - active.Count}</b><span>joining soon</span></div>
                <div><b>{credentials.Count(c => c.Portal == "HR portal")}</b><span>HR logins</span></div>
                <div><b>{pipeline.Count}</b><span>job applications</span></div>
                <div><b>{runs.Count}</b><span>payroll months paid</span></div>
              </div>
              <table class="kv">
                <tr><th>Site</th><td>{E(options.SiteUrl)}</td></tr>
                <tr><th>HR portal</th><td>{E(options.SiteUrl)}/hr</td></tr>
                <tr><th>Candidate portal</th><td>{E(options.SiteUrl)}/onboarding</td></tr>
                <tr><th>Careers site</th><td>{E(options.SiteUrl)}/kapson/careers</td></tr>
                <tr><th>Employees</th><td>Kapsons mobile app (server address = the site above)</td></tr>
                <tr><th>Prepared</th><td>{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5)).ToString("dd MMM yyyy, HH:mm", India)} IST</td></tr>
              </table>
              <div class="alert"><b>Confidential.</b> This document contains sign-in passwords. Share it only with the people testing the platform,
              and delete it once everyone has signed in and changed their password.</div>
            </section>
            """);

        // 1. Logins
        Section("1", "Sign-in details", "Employee and HR passwords are temporary: the app asks for a new one at first sign-in.");
        foreach (var group in credentials.GroupBy(c => c.Portal))
        {
            html.Append($"<h3>{E(group.Key)} <small>— sign in at {E(group.First().SignInAt)}</small></h3>");
            Table(["Emp no.", "Name", "Access", "Username", "Temporary password"],
                group.Select(c => new[] { c.EmployeeNumber, c.Name, c.Role, Code(c.UserName), Code(c.Password) }), raw: [3, 4]);
        }
        html.Append("""<p class="note">The platform administrator login (<code>admin</code>, at /admin) was not changed. The old developer login <code>hrdev</code> has been switched off.</p>""");

        // 2. Organisation
        Section("2", "Organisation", "Kapson's retail business across Chandigarh, Mohali and Delhi NCR.");
        var depts = people.GroupBy(p => p.Dept).OrderByDescending(g => g.Count());
        Table(["Department", "People", "Head", "Locations"],
            depts.Select(g => new[]
            {
                Name("Department", g.Key), g.Count().ToString(),
                g.OrderByDescending(p => p.Reports).ThenBy(p => p.Joined).First().Name,
                string.Join(", ", g.SelectMany(p => Name("Location", p.Location).Split(',', StringSplitOptions.TrimEntries)).Distinct())
            }));

        html.Append("<h3>Employee directory</h3>");
        Table(["Emp no.", "Name", "Designation", "Department", "Location", "Grade", "Joined", "Status", "Reports to"],
            people.Select(p => new[]
            {
                p.Number, E(p.Name) + (p.Reports > 0 ? $" <span class=\"tag\">{p.Reports} report{(p.Reports > 1 ? "s" : "")}</span>" : ""),
                E(p.Title), E(Name("Department", p.Dept)), E(Name("Location", p.Location)), E(Name("Grade", p.Grade)),
                p.Joined.ToString("dd MMM yyyy", India), StatusTag(p.Status, p.Joined), E(p.Manager)
            }), raw: [1, 7], encodeAll: false, small: true);

        // 3. Salary
        Section("3", "Salary structure", "Monthly pay by grade, as configured in the grade salary masters. Payroll calculates every payslip from these.");
        var gradeRows = new List<string[]>();
        foreach (var g in grades)
        {
            var s = SalaryFor(g.Basic, g.Json);
            var count = people.Count(p => p.Grade.Equals(g.Grade, StringComparison.OrdinalIgnoreCase));
            gradeRows.Add([E(Name("Grade", g.Grade)), count == 0 ? "—" : count.ToString(), M(g.Basic), M(s.Hra), M(s.Fixed), M(s.Special), $"<b>{M(s.Gross)}</b>",
                M(s.Pf), s.Esi == 0 ? "—" : M(s.Esi), M(s.Pt), $"<b>{M(s.Gross - s.Pf - s.Esi - s.Pt)}</b>", M(s.Gross * 12)]);
        }
        Table(["Grade", "Staff", "Basic", "HRA 40%", "Transport + Medical", "Special 10%", "Gross", "PF 12%*", "ESI 0.75%†", "Prof. tax", "Net", "Annual gross"],
            gradeRows, raw: Enumerable.Range(0, 12).ToArray(), encodeAll: false, small: true, numeric: true);
        html.Append("""<p class="note">* PF is capped at ₹1,800 a month. † ESI applies only when gross is ₹21,000 or less. TDS is 0% in the masters. Salaries are pro-rated for absence and for joining mid-month.</p>""");

        html.Append("<h3>Payroll history</h3>");
        Table(["Month", "Payslips", "Gross", "Deductions", "Net paid", "Bank batch", "Credited"],
            runs.Select(r => new[] { r.Month.ToString("MMMM yyyy", India), r.Count.ToString(), M(r.Gross), M(r.Ded), M(r.Net), r.Ref, r.Date?.ToString("dd MMM", India) ?? "—" }),
            numeric: true);
        html.Append("""<p class="note"><b>October 2026 is open</b> — HR can generate it live from HR portal → Payroll → Generate. It will include Aarav Sood's part month (joined 8 Oct), absences in October attendance, and the first instalment of Manpreet Bains's festival advance.</p>""");

        html.Append("<h3>September 2026 payslips</h3>");
        Table(["Emp no.", "Name", "Grade", "Basic", "HRA", "Allowances", "Gross", "PF", "ESI", "Other ded.", "Net pay"],
            september.Select(s => new[] { s.Number, E(s.Name), E(Name("Grade", s.Grade)), M(s.Basic), M(s.Hra), M(s.Conv + s.Other), M(s.Gross), M(s.Pf), s.Esi == 0 ? "—" : M(s.Esi), M(s.OtherDed), $"<b>{M(s.Net)}</b>" }),
            raw: [10], encodeAll: false, small: true, numeric: true);
        html.Append("""<p class="note">"Other ded." is professional tax plus any loan instalment recovered that month.</p>""");

        if (loans.Count > 0)
        {
            html.Append("<h3>Loans and salary advances</h3>");
            Table(["Voucher", "Employee", "Type", "Amount", "Monthly", "Instalments", "Starts", "Recovered", "Purpose"],
                loans.Select(l => new[] { l.Voucher, l.Name, l.Type, M(l.Principal), M(l.Emi), l.Count.ToString(), l.Start.ToString("MMM yyyy", India), $"{l.Paid} of {l.Count}", l.Purpose }));
        }

        // 4. Recruitment
        Section("4", "Recruitment and onboarding", "Six open roles on the careers site, with applicants at every stage of the pipeline.");
        Table(["Opening", "Location", "Work mode", "Salary band (CTC / year)", "Applicants", "Status"],
            jobs.Select(j => new[] { j.Title, j.Location, j.Mode, j.Min == 0 ? "—" : $"{M(j.Min)} – {M(j.Max)}", j.Applicants.ToString(), j.Status }));

        html.Append("<h3>Candidates</h3>");
        Table(["Candidate", "Applied for", "Stage", "Applied", "Source", "Experience", "Expected CTC"],
            pipeline.Select(p => new[] { E(p.Name), E(p.Job), StageTag(p.Stage), p.Applied.ToString("dd MMM", India), E(p.Source), $"{p.Exp:0.#} yr{(p.Exp == 1 ? "" : "s")}", p.Ctc == 0 ? "—" : M(p.Ctc) }),
            raw: [2], encodeAll: false);

        html.Append("<h3>Interviews</h3>");
        Table(["Candidate", "Opening", "Round", "When (IST)", "Mode", "Panel", "Status"],
            interviews.Select(i => new[] { i.Name, i.Job, i.Round, i.When.ToOffset(TimeSpan.FromHours(5.5)).ToString("dd MMM, HH:mm", India), i.Mode, i.Panel, i.Status }));

        html.Append("<h3>Onboarding checklists</h3>");
        foreach (var o in onboarding.GroupBy(o => o.Name))
        {
            var first = o.First();
            html.Append($"<p class=\"onb\"><b>{E(first.Name)}</b> — {E(first.Job)}, joining {first.Joining?.ToString("dd MMM yyyy", India) ?? "—"} · onboarding <b>{E(first.Stage)}</b></p>");
            Table(["Checklist item", "Status"], o.Select(x => new[] { E(x.Task), TaskTag(x.Status) }), raw: [1], encodeAll: false);
        }

        // 5. Workforce
        Section("5", "Attendance and leave", "Attendance covers the last few weeks; leave covers the year so far.");
        html.Append("<div class=\"cols\"><div>");
        html.Append("<h3>October attendance (days)</h3>");
        Table(["Status", "Days"], attendance.Select(a => new[] { a.Status, a.Count.ToString() }), numeric: true);
        html.Append("</div><div>");
        html.Append("<h3>Leave requests by status</h3>");
        Table(["Status", "Requests"], leaveTotals.Select(l => new[] { l.State, l.Count.ToString() }), numeric: true);
        html.Append("</div></div>");
        if (leave.Count > 0)
        {
            html.Append("<h3>Waiting for approval</h3>");
            Table(["Employee", "Type", "From", "To", "Days", "Reason"],
                leave.Select(l => new[] { l.Name, l.Type, l.From.ToString("dd MMM", India), l.To.ToString("dd MMM", India), l.Days.ToString("0.#"), l.Reason }));
        }

        // 6. Walkthrough
        Section("6", "What to try", "A short script that touches every module.");
        html.Append($"""
            <ol class="steps">
              <li><b>HR dashboard</b> — sign in as <code>simran.kaur</code> at /hr. Headcount, attendance, pending leave and hiring KPIs all come from this data.</li>
              <li><b>Payroll</b> — Payroll → Generate → October 2026 → Generate all. Review the lines, approve, then record the bank transfer from the HDFC payroll account and download the bank advice.</li>
              <li><b>Leave</b> — approve or reject the {leave.Count} pending requests. Each manager also sees their own team's requests in the mobile app.</li>
              <li><b>Recruitment</b> — move Riya Mehra (Shortlisted) to Interviewing and schedule a round; make an offer to Raghav Sethi.</li>
              <li><b>Offer acceptance</b> — sign in at /onboarding as Palak Arora and accept the HR Executive offer. Her onboarding checklist opens straight away.</li>
              <li><b>Onboarding</b> — as HR, approve Nidhi Chawla's emergency-contact form; as Nidhi, upload the remaining relieving letter.</li>
              <li><b>Employee app</b> — sign in as <code>emp.1018</code> (Manpreet Bains): payslips, leave balance, festival advance, gate pass, attendance.</li>
              <li><b>Manager view</b> — sign in as <code>emp.1015</code> (Gurpreet Sandhu, Store Manager) to see the store team and approve their requests.</li>
            </ol>
            <p class="note">Before employees can sign in on their phones, the Kapsons mobile app must point at {E(options.SiteUrl)} — the current build still points at an old ngrok address.</p>
            """);

        html.Append("</body></html>");
        return html.ToString();
    }

    // ---------------------------------------------------------------- salary maths (mirrors the grade masters)

    private sealed record Salary(decimal Hra, decimal Fixed, decimal Special, decimal Gross, decimal Pf, decimal Esi, decimal Pt);

    private static Salary SalaryFor(decimal basic, string json)
    {
        decimal hra = 0, fixedAllowances = 0, special = 0, otherPercent = 0;
        var components = JsonSerializer.Deserialize<List<Component>>(json, Json.Options) ?? [];
        foreach (var c in components.Where(c => c.Kind == "Allowance"))
        {
            var amount = c.CalculationType == "PercentOfBasic" ? basic * c.Value / 100 : c.Value;
            if (c.ComponentCode == "HRA") hra = amount;
            else if (c.ComponentCode == "SPECIAL") special = amount;
            else if (c.CalculationType == "Fixed") fixedAllowances += amount;
            else otherPercent += amount;
        }

        var gross = basic + hra + fixedAllowances + special + otherPercent;
        decimal pf = 0, esi = 0, pt = 0;
        foreach (var c in components.Where(c => c.Kind == "Deduction"))
        {
            var amount = c.CalculationType switch
            {
                "PercentOfBasic" => basic * c.Value / 100,
                "PercentOfGross" => c.GrossCeiling is { } ceiling && gross > ceiling ? 0 : gross * c.Value / 100,
                _ => c.Value
            };
            if (c.CapAmount is { } cap) amount = Math.Min(amount, cap);
            if (c.ComponentCode == "PF") pf = amount;
            else if (c.ComponentCode == "ESI") esi = amount;
            else pt += amount;
        }

        return new Salary(hra, fixedAllowances, special + otherPercent, Math.Round(gross), Math.Round(pf), Math.Round(esi, MidpointRounding.AwayFromZero), pt);
    }

    private sealed record Component(string Kind, string ComponentCode, string CalculationType, decimal Value, decimal? CapAmount, decimal? GrossCeiling);

    // ---------------------------------------------------------------- html helpers

    private void Open() => html.Append("""
        <!doctype html><html lang="en"><head><meta charset="utf-8"><title>Kapson HRMS — Demo Data Pack</title>
        <style>
          @page { size: A4; margin: 14mm 12mm; }
          :root { --ink:#1d2433; --muted:#5d6678; --line:#dfe3ea; --brand:#7a1f2b; --soft:#f6f2ee; --ok:#1f7a4d; --warn:#9a5b00; --bad:#a3242b; --info:#245d9a; }
          * { box-sizing: border-box; }
          body { font-family: "Segoe UI", Arial, sans-serif; color: var(--ink); font-size: 10.5px; line-height: 1.45; margin: 0; background:#fff; }
          .cover { page-break-after: always; padding-top: 30mm; }
          .brand { font-weight: 800; letter-spacing: .18em; color: var(--brand); font-size: 15px; }
          .brand span { color: var(--ink); font-weight: 400; }
          h1 { font-size: 34px; margin: 6px 0 8px; letter-spacing: -.01em; }
          .lead { font-size: 14px; color: var(--muted); max-width: 135mm; margin: 0 0 20px; }
          .facts { display: flex; gap: 10px; margin: 18px 0 22px; }
          .facts div { flex: 1; background: var(--soft); border-radius: 8px; padding: 12px; }
          .facts b { display: block; font-size: 24px; color: var(--brand); }
          .facts span { color: var(--muted); font-size: 10px; text-transform: uppercase; letter-spacing: .06em; }
          .kv { width: 100%; border-collapse: collapse; margin-bottom: 18px; }
          .kv th { text-align: left; width: 34mm; color: var(--muted); font-weight: 600; padding: 5px 0; border-bottom: 1px solid var(--line); }
          .kv td { padding: 5px 0; border-bottom: 1px solid var(--line); font-family: Consolas, monospace; }
          .alert { border-left: 4px solid var(--bad); background: #fbeff0; padding: 10px 12px; border-radius: 4px; font-size: 11px; }
          section.part { page-break-before: always; }
          h2 { font-size: 18px; margin: 0 0 2px; display: flex; gap: 10px; align-items: baseline; }
          h2 .n { color: var(--brand); font-weight: 800; }
          .sub { color: var(--muted); margin: 0 0 12px; font-size: 11px; }
          h3 { font-size: 12.5px; margin: 16px 0 6px; }
          h3 small { font-weight: 400; color: var(--muted); }
          table.t { width: 100%; border-collapse: collapse; margin-bottom: 6px; page-break-inside: auto; }
          table.t tr { page-break-inside: avoid; }
          table.t th { text-align: left; font-size: 9px; text-transform: uppercase; letter-spacing: .05em; color: var(--muted); background: var(--soft); padding: 5px 6px; border-bottom: 1px solid var(--line); }
          table.t td { padding: 5px 6px; border-bottom: 1px solid var(--line); vertical-align: top; }
          table.t.small td, table.t.small th { padding: 4px 5px; font-size: 9.5px; }
          table.t.num td:not(:first-child):not(:nth-child(2)), table.t.num th:not(:first-child):not(:nth-child(2)) { text-align: right; font-variant-numeric: tabular-nums; }
          code { font-family: Consolas, monospace; background: #eef1f6; padding: 1px 4px; border-radius: 3px; font-size: 10px; }
          .tag { display: inline-block; font-size: 8.5px; padding: 1px 6px; border-radius: 10px; background: #eef1f6; color: var(--muted); white-space: nowrap; }
          .tag.ok { background: #e3f3ea; color: var(--ok); } .tag.warn { background: #fbf0dc; color: var(--warn); }
          .tag.bad { background: #fbe6e7; color: var(--bad); } .tag.info { background: #e4eef9; color: var(--info); }
          .note { color: var(--muted); font-size: 9.5px; margin: 4px 0 10px; }
          .onb { margin: 10px 0 4px; }
          .cols { display: flex; gap: 16px; } .cols > div { flex: 1; }
          ol.steps { padding-left: 18px; } ol.steps li { margin-bottom: 7px; }
        </style></head><body>
        """);

    private void Section(string number, string title, string subtitle) =>
        html.Append($"<section class=\"part\"><h2><span class=\"n\">{number}</span>{E(title)}</h2><p class=\"sub\">{E(subtitle)}</p></section>");

    private void Table(string[] headers, IEnumerable<string[]> rows, int[]? raw = null, bool encodeAll = true, bool small = false, bool numeric = false)
    {
        raw ??= [];
        html.Append($"<table class=\"t{(small ? " small" : "")}{(numeric ? " num" : "")}\"><thead><tr>");
        foreach (var h in headers) html.Append($"<th>{E(h)}</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            html.Append("<tr>");
            for (var i = 0; i < row.Length; i++)
            {
                var cell = raw.Contains(i) || !encodeAll ? row[i] : E(row[i]);
                html.Append($"<td>{cell}</td>");
            }
            html.Append("</tr>");
        }
        html.Append("</tbody></table>");
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string Code(string value) => $"<code>{E(value)}</code>";
    private static string M(decimal amount) => "₹" + Math.Round(amount).ToString("#,##,##0", India);

    private static string StatusTag(string status, DateOnly joined) =>
        joined > DateOnly.FromDateTime(DateTime.Today) ? "<span class=\"tag info\">Joining</span>"
        : status == "PROBATION" ? "<span class=\"tag warn\">Probation</span>" : "<span class=\"tag ok\">Permanent</span>";

    private static string StageTag(string stage) => stage switch
    {
        "Hired" or "Onboarding" or "OfferAccepted" => $"<span class=\"tag ok\">{E(stage)}</span>",
        "Offered" or "Interviewing" => $"<span class=\"tag info\">{E(stage)}</span>",
        "Rejected" or "Withdrawn" or "OfferDeclined" => $"<span class=\"tag bad\">{E(stage)}</span>",
        _ => $"<span class=\"tag warn\">{E(stage)}</span>"
    };

    private static string TaskTag(string status) => status switch
    {
        "Approved" => "<span class=\"tag ok\">Approved</span>",
        "Submitted" => "<span class=\"tag info\">Submitted — waiting for HR</span>",
        "Rejected" => "<span class=\"tag bad\">Rejected</span>",
        _ => "<span class=\"tag warn\">Pending — candidate to upload</span>"
    };

    private Task<List<T>> Q<T>(string sql, Func<Microsoft.Data.SqlClient.SqlDataReader, T> map, params (string, object)[] p) =>
        Db.QueryAsync(options.ConnectionString, sql, map, p);
}
