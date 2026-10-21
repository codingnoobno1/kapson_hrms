using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.DL.Admin;
using Vispl.HRManagement.DL.Employee;
using Vispl.HRManagement.DL.Recruitment;
using Vispl.HRManagement.DL.Workforce;

namespace Vispl.HRManagement.DL.Shared;

/// <summary>
/// Seeds a realistic, internally consistent operational dataset for the "kapson" tenant when
/// the workforce tables are empty. Every KPI, chart, and drill-down table on the HR dashboard
/// derives its figures from these rows at read time.
/// </summary>
internal static class WorkforceSampleSeeder
{
    private const string Tenant = "kapson";

    private sealed record EmployeeSpec(
        string Number,
        string FirstName,
        string LastName,
        string Gender,
        string MaritalStatus,
        DateOnly BirthDate,
        DateOnly JoinedOn,
        string CompanyCode,
        string DivisionCode,
        string DepartmentCode,
        string LocationCode,
        string GradeCode,
        string JobTitle,
        string ShiftCode,
        string? ManagerNumber);

    public static async Task SeedIfEmptyAsync(
        HrManagementDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.Employees.AnyAsync(item => item.TenantKey == Tenant, cancellationToken))
        {
            return;
        }

        logger.LogInformation("Seeding realistic operational workforce dataset for tenant '{Tenant}'.", Tenant);

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var shifts = await dbContext.ShiftDefinitions
            .Where(item => item.TenantKey == Tenant)
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var leavePolicies = await dbContext.LeavePolicies
            .Where(item => item.TenantKey == Tenant)
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var jobOpenings = await dbContext.JobOpenings
            .Where(item => item.TenantKey == Tenant)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var holidays = (await dbContext.LeaveHolidays
                .Where(item => item.TenantKey == Tenant)
                .Select(item => item.Date)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // Mid-month join date for partial-month attendance verification (falls in current month <= today)
        var midMonthJoinDate = today.Day > 4
            ? new DateOnly(today.Year, today.Month, Math.Min(8, today.Day - 2))
            : today;

        // 24 active employees employed as of today + 1 future joiner (EMP-1025)
        var specs = new List<EmployeeSpec>
        {
            // HR (Head Office)
            new("EMP-1001", "Simran", "Kaur", "FEMALE", "MARRIED",
                 SafeDate(today.Year - 34, today.Month, today.Day), SafeDate(today.Year - 5, today.Month, today.Day),
                "KAPSON-RETAIL", "HO", "HR", "CHANDIGARH", "MANAGER", "HR Manager", "GENERAL", null),
            new("EMP-1002", "Rohan", "Verma", "MALE", "SINGLE",
                SafeDate(today.Year - 27, 4, 14), today.AddDays(-3).AddYears(-2),
                "KAPSON-RETAIL", "HO", "HR", "CHANDIGARH", "EXECUTIVE", "HR Executive", "GENERAL", "EMP-1001"),

            // FINOPS (Head Office & Regional)
            new("EMP-1003", "Vikramjit", "Singh", "MALE", "MARRIED",
                SafeDate(today.Year - 38, 11, 22), SafeDate(today.Year - 6, 3, 10),
                "KAPSON-RETAIL", "HO", "FINOPS", "CHANDIGARH", "SPECIALIST", "Financial Analyst", "GENERAL", "EMP-1001"),
            new("EMP-1004", "Neha", "Sharma", "FEMALE", "MARRIED",
                today.AddDays(2).AddYears(-29), SafeDate(today.Year - 3, 7, 1),
                "KAPSON-RETAIL", "HO", "FINOPS", "MOHALI", "EXECUTIVE", "Accounts Executive", "GENERAL", "EMP-1003"),
            new("EMP-1005", "Arjun", "Malhotra", "MALE", "SINGLE",
                SafeDate(today.Year - 30, 8, 9), SafeDate(today.Year - 2, 1, 15),
                "KAPSON-LUXE", "REGIONAL", "FINOPS", "CHANDIGARH", "SPECIALIST", "Financial Analyst", "GENERAL", "EMP-1003"),
            new("EMP-1006", "Pooja", "Bansal", "FEMALE", "SINGLE",
                today.AddDays(-2).AddYears(-26), SafeDate(today.Year - 1, 6, 12),
                "KAPSON-DIGITAL", "HO", "FINOPS", "MOHALI", "EXECUTIVE", "Accounts Executive", "GENERAL", "EMP-1003"),

            // DIGITAL (Head Office & Warehouse)
            new("EMP-1007", "Kabir", "Ahluwalia", "MALE", "MARRIED",
                SafeDate(today.Year - 33, 2, 18), today.AddDays(5).AddYears(-6),
                "KAPSON-DIGITAL", "HO", "DIGITAL", "MOHALI", "LEAD", "E-commerce Category Lead", "GENERAL", "EMP-1001"),
            new("EMP-1008", "Ananya", "Gupta", "FEMALE", "SINGLE",
                SafeDate(today.Year - 29, 9, 5), SafeDate(today.Year - 3, 2, 20),
                "KAPSON-DIGITAL", "HO", "DIGITAL", "MOHALI", "SENIOR", "Senior Software Engineer", "GENERAL", "EMP-1007"),
            new("EMP-1009", "Devansh", "Khanna", "MALE", "SINGLE",
                today.AddDays(4).AddYears(-25), SafeDate(today.Year - 2, 5, 11),
                "KAPSON-DIGITAL", "HO", "DIGITAL", "MOHALI", "SPECIALIST", "Software Engineer", "GENERAL", "EMP-1008"),
            new("EMP-1010", "Kritika", "Joshi", "FEMALE", "MARRIED",
                SafeDate(today.Year - 28, today.Month, today.Day), SafeDate(today.Year - 2, 8, 19),
                "KAPSON-DIGITAL", "WAREHOUSE", "DIGITAL", "CHD_MOH", "SPECIALIST", "Software Engineer", "GENERAL", "EMP-1008"),
            new("EMP-1011", "Harsh", "Vardhan", "MALE", "SINGLE",
                SafeDate(today.Year - 26, 12, 3), SafeDate(today.Year - 1, 9, 1),
                "KAPSON-DIGITAL", "WAREHOUSE", "DIGITAL", "MOHALI", "SPECIALIST", "Software Engineer", "GENERAL", "EMP-1007"),

            // BRAND (Regional & Store)
            new("EMP-1012", "Meera", "Chopra", "FEMALE", "MARRIED",
                today.AddDays(-5).AddYears(-31), today.AddDays(-6).AddYears(-4),
                "KAPSON-LUXE", "REGIONAL", "BRAND", "CHANDIGARH", "SPECIALIST", "Visual Merchandiser", "STORE-OPEN", "EMP-1001"),
            new("EMP-1013", "Rishi", "Kapoor", "MALE", "SINGLE",
                SafeDate(today.Year - 27, 6, 25), SafeDate(today.Year - 2, 4, 1),
                "KAPSON-LUXE", "STORE", "BRAND", "DELHI_NCR", "SPECIALIST", "Visual Merchandiser", "STORE-OPEN", "EMP-1012"),
            new("EMP-1014", "Tanya", "Bhatia", "FEMALE", "SINGLE",
                today.AddDays(6).AddYears(-24), SafeDate(today.Year - 1, 11, 10),
                "KAPSON-RETAIL", "STORE", "BRAND", "CHANDIGARH", "SPECIALIST", "Visual Merchandiser", "STORE-OPEN", "EMP-1012"),

            // RETAIL (Store & Regional)
            new("EMP-1015", "Gurpreet", "Sandhu", "MALE", "MARRIED",
                SafeDate(today.Year - 36, 3, 30), SafeDate(today.Year - 3, today.Month, today.Day),
                "KAPSON-RETAIL", "STORE", "RETAIL", "CHANDIGARH", "MANAGER", "Store Manager", "GENERAL", "EMP-1001"),
            new("EMP-1016", "Navjot", "Gill", "MALE", "MARRIED",
                SafeDate(today.Year - 35, 7, 19), SafeDate(today.Year - 4, 2, 14),
                "KAPSON-LUXE", "STORE", "RETAIL", "CHD_MOH", "MANAGER", "Store Manager", "STORE-OPEN", "EMP-1001"),
            new("EMP-1017", "Jaspreet", "Dhillon", "FEMALE", "MARRIED",
                SafeDate(today.Year - 32, 5, 8), today.AddDays(3).AddYears(-1),
                "KAPSON-RETAIL", "REGIONAL", "RETAIL", "DELHI_NCR", "MANAGER", "Store Manager", "STORE-OPEN", "EMP-1001"),
            new("EMP-1018", "Manpreet", "Bains", "MALE", "SINGLE",
                SafeDate(today.Year - 25, 1, 17), SafeDate(today.Year - 2, 3, 5),
                "KAPSON-RETAIL", "STORE", "RETAIL", "CHANDIGARH", "EXECUTIVE", "Sales Executive", "STORE-OPEN", "EMP-1015"),
            new("EMP-1019", "Simranjeet", "Mann", "FEMALE", "SINGLE",
                SafeDate(today.Year - 24, 10, 28), SafeDate(today.Year - 1, 4, 18),
                "KAPSON-RETAIL", "STORE", "RETAIL", "CHANDIGARH", "EXECUTIVE", "Sales Executive", "STORE-OPEN", "EMP-1015"),
            new("EMP-1020", "Karanveer", "Brar", "MALE", "SINGLE",
                SafeDate(today.Year - 26, 8, 12), SafeDate(today.Year - 2, 7, 22),
                "KAPSON-LUXE", "STORE", "RETAIL", "CHD_MOH", "EXECUTIVE", "Sales Executive", "STORE-OPEN", "EMP-1016"),
            new("EMP-1021", "Sukhman", "Sidhu", "MALE", "SINGLE",
                SafeDate(today.Year - 25, 2, 9), SafeDate(today.Year - 1, 5, 15),
                "KAPSON-LUXE", "STORE", "RETAIL", "CHD_MOH", "EXECUTIVE", "Sales Executive", "STORE-OPEN", "EMP-1016"),
            new("EMP-1022", "Harleen", "Bajwa", "FEMALE", "SINGLE",
                SafeDate(today.Year - 27, 4, 3), SafeDate(today.Year - 1, 8, 9),
                "KAPSON-RETAIL", "STORE", "RETAIL", "DELHI_NCR", "EXECUTIVE", "Sales Executive", "STORE-OPEN", "EMP-1017"),
            new("EMP-1023", "Rajवीर".Replace("वीर", "vir"), "Randhawa", "MALE", "MARRIED",
                SafeDate(today.Year - 29, 11, 14), SafeDate(today.Year - 2, 10, 1),
                "KAPSON-LUXE", "REGIONAL", "RETAIL", "DELHI_NCR", "EXECUTIVE", "Sales Executive", "GENERAL", "EMP-1017"),
            new("EMP-1024", "Aarav", "Sood", "MALE", "SINGLE",
                SafeDate(today.Year - 23, 6, 11), midMonthJoinDate,
                "KAPSON-RETAIL", "STORE", "RETAIL", "CHANDIGARH", "EXECUTIVE", "Sales Executive", "STORE-OPEN", "EMP-1015"),

            // Future joiner (excluded from today's active scope, verifies future-joiner rules)
            new("EMP-1025", "Diya", "Aggarwal", "FEMALE", "SINGLE",
                SafeDate(today.Year - 24, 9, 19), today.AddDays(10),
                "KAPSON-RETAIL", "STORE", "RETAIL", "CHANDIGARH", "EXECUTIVE", "Sales Executive", "STORE-OPEN", "EMP-1015")
        };

        var employeeEntities = new Dictionary<string, EmployeeEntity>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var entity = new EmployeeEntity
            {
                TenantKey = Tenant,
                EmployeeNumber = spec.Number,
                LegalFirstName = spec.FirstName,
                LegalLastName = spec.LastName,
                PreferredName = $"{spec.FirstName} {spec.LastName}",
                DateOfBirth = spec.BirthDate,
                GenderCode = spec.Gender,
                MaritalStatusCode = spec.MaritalStatus,
                NationalityCode = "INDIAN",
                Email = $"{spec.FirstName.ToLowerInvariant()}.{spec.LastName.ToLowerInvariant()}@kapson.in",
                Phone = $"+9198765{43210 + i:00000}",
                JoinedOn = spec.JoinedOn,
                EmploymentTypeCode = "FULL_TIME",
                EmploymentStatusCode = spec.JoinedOn > today.AddMonths(-3) ? "PROBATION" : "ACTIVE",
                GradeCode = spec.GradeCode,
                JobTitle = spec.JobTitle,
                ProbationEndsOn = spec.JoinedOn.AddMonths(6),
                CompanyCode = spec.CompanyCode,
                DivisionCode = spec.DivisionCode,
                DepartmentCode = spec.DepartmentCode,
                LocationCode = spec.LocationCode,
                CostCentre = $"CC-{spec.DepartmentCode}",
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            };

            dbContext.Employees.Add(entity);
            employeeEntities[spec.Number] = entity;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Wire reporting manager hierarchy
        foreach (var spec in specs.Where(item => item.ManagerNumber is not null))
        {
            if (employeeEntities.TryGetValue(spec.Number, out var emp) &&
                employeeEntities.TryGetValue(spec.ManagerNumber!, out var mgr))
            {
                emp.ManagerEmployeeId = mgr.Id;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Shift Assignments ------------------------------------------------
        foreach (var spec in specs)
        {
            var emp = employeeEntities[spec.Number];
            if (!shifts.TryGetValue(spec.ShiftCode, out var shift))
            {
                shift = shifts["GENERAL"];
            }

            dbContext.ShiftAssignments.Add(new ShiftAssignmentEntity
            {
                TenantKey = Tenant,
                EmployeeId = emp.Id,
                ShiftDefinitionId = shift.Id,
                EffectiveFrom = new DateOnly(today.Year - 1, 1, 1),
                EffectiveTo = null,
                AssignedUtc = now.AddMonths(-6),
                AssignedBy = "EMP-1001",
                Note = "Standard roster assignment"
            });
        }

        // ---- Emergency Contacts & Dependants ----------------------------------
        var activeSpecs = specs.Where(item => item.JoinedOn <= today).ToList();
        foreach (var spec in activeSpecs.Take(12))
        {
            var emp = employeeEntities[spec.Number];
            dbContext.EmployeeContacts.Add(new EmployeeContactEntity
            {
                TenantKey = Tenant,
                EmployeeId = emp.Id,
                Kind = "Emergency",
                Name = $"Rajesh {spec.LastName}",
                Relationship = spec.MaritalStatus == "MARRIED" ? "Spouse" : "Father",
                Phone = $"+9198140{emp.Id:00000}",
                Email = $"family.{spec.LastName.ToLowerInvariant()}@example.com",
                IsPrimary = true,
                IsVerified = true,
                UpdatedUtc = now
            });

            if (spec.MaritalStatus == "MARRIED")
            {
                dbContext.EmployeeDependants.Add(new EmployeeDependantEntity
                {
                    TenantKey = Tenant,
                    EmployeeId = emp.Id,
                    DisplayName = $"Kiran {spec.LastName}",
                    RelationCode = spec.Gender == "MALE" ? "WIFE" : "HUSBAND",
                    DateOfBirth = spec.BirthDate.AddYears(1),
                    IsBenefitEligible = true,
                    UpdatedUtc = now
                });
            }
        }

        // ---- Leave Balances (Current Year) ------------------------------------
        var alPolicy = leavePolicies["AL"];
        var slPolicy = leavePolicies["SL"];
        var clPolicy = leavePolicies["CL"];
        var plPolicy = leavePolicies["PL"];

        for (var i = 0; i < activeSpecs.Count; i++)
        {
            var emp = employeeEntities[activeSpecs[i].Number];
            var carryForward = (i % 4) * 2m; // 0, 2, 4, 6 days carried forward

            dbContext.LeaveBalances.AddRange(
                new LeaveBalanceEntity
                {
                    TenantKey = Tenant,
                    EmployeeId = emp.Id,
                    PolicyId = alPolicy.Id,
                    Year = today.Year,
                    Entitled = alPolicy.AnnualEntitlement,
                    Accrued = alPolicy.AnnualEntitlement,
                    CarriedForward = carryForward,
                    AdjustmentDays = 0m,
                    UpdatedUtc = now
                },
                new LeaveBalanceEntity
                {
                    TenantKey = Tenant,
                    EmployeeId = emp.Id,
                    PolicyId = slPolicy.Id,
                    Year = today.Year,
                    Entitled = slPolicy.AnnualEntitlement,
                    Accrued = slPolicy.AnnualEntitlement,
                    CarriedForward = 0m,
                    AdjustmentDays = 0m,
                    UpdatedUtc = now
                },
                new LeaveBalanceEntity
                {
                    TenantKey = Tenant,
                    EmployeeId = emp.Id,
                    PolicyId = clPolicy.Id,
                    Year = today.Year,
                    Entitled = clPolicy.AnnualEntitlement,
                    Accrued = clPolicy.AnnualEntitlement,
                    CarriedForward = 0m,
                    AdjustmentDays = 0m,
                    UpdatedUtc = now
                });

            if (i % 5 == 0)
            {
                dbContext.LeaveBalances.Add(new LeaveBalanceEntity
                {
                    TenantKey = Tenant,
                    EmployeeId = emp.Id,
                    PolicyId = plPolicy.Id,
                    Year = today.Year,
                    Entitled = plPolicy.AnnualEntitlement,
                    Accrued = plPolicy.AnnualEntitlement,
                    CarriedForward = 0m,
                    AdjustmentDays = 0m,
                    UpdatedUtc = now
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Leave Requests & Leave Request Days ------------------------------
        var leaveRequestsToCreate = new List<(EmployeeEntity Emp, LeavePolicyEntity Policy, DateOnly From, DateOnly To, decimal Portion, string State, string Reason)>();

        // 1. Two employees ON LEAVE TODAY (EMP-1019, EMP-1020)
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1019"], clPolicy, today, today, 1m, "Approved",
            "Family function in Ludhiana"));
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1020"], alPolicy, today.AddDays(-1), today.AddDays(1), 1m, "Approved",
            "Annual family trip to Shimla"));

        // 2. Approved leaves earlier in the current month (for Monthly Leave by Type & Date charts)
        var currentMonthWorkingDays = GetWorkingDaysInMonth(today.Year, today.Month, holidays)
            .Where(d => d != today)
            .ToList();

        if (currentMonthWorkingDays.Count >= 6)
        {
            leaveRequestsToCreate.Add((
                employeeEntities["EMP-1002"], clPolicy, currentMonthWorkingDays[0], currentMonthWorkingDays[0], 1m, "Approved",
                "Personal bank and property documentation"));
            leaveRequestsToCreate.Add((
                employeeEntities["EMP-1004"], slPolicy, currentMonthWorkingDays[1], currentMonthWorkingDays[2], 1m, "Approved",
                "Viral fever and medical rest"));
            leaveRequestsToCreate.Add((
                employeeEntities["EMP-1008"], alPolicy, currentMonthWorkingDays[2], currentMonthWorkingDays[4], 1m, "Approved",
                "Planned autumn break"));
            leaveRequestsToCreate.Add((
                employeeEntities["EMP-1012"], clPolicy, currentMonthWorkingDays[3], currentMonthWorkingDays[3], 0.5m, "Approved",
                "Half-day dental appointment"));
            leaveRequestsToCreate.Add((
                employeeEntities["EMP-1015"], slPolicy, currentMonthWorkingDays[4], currentMonthWorkingDays[5], 1m, "Approved",
                "Seasonal flu recovery"));
            leaveRequestsToCreate.Add((
                employeeEntities["EMP-1016"], alPolicy, currentMonthWorkingDays[5], currentMonthWorkingDays[5], 1m, "Approved",
                "Attending cousin's wedding"));
        }

        // 3. Approved leaves across the previous 5 months (for 6-month trend & category distribution)
        var historicalSpec = new (string EmpNo, LeavePolicyEntity Policy, int MonthsBack, int StartDay, int DaysCount, string Reason)[]
        {
            ("EMP-1001", alPolicy, 5, 11, 3, "Summer family vacation"),
            ("EMP-1003", clPolicy, 5, 18, 2, "Home renovation work"),
            ("EMP-1007", slPolicy, 4, 8, 2, "Medical checkup and rest"),
            ("EMP-1009", alPolicy, 4, 15, 4, "Family visit to Jaipur"),
            ("EMP-1013", clPolicy, 4, 22, 1, "Personal work"),
            ("EMP-1015", alPolicy, 3, 6, 3, "Out-of-station trip"),
            ("EMP-1017", slPolicy, 3, 14, 2, "Migraine and medical rest"),
            ("EMP-1018", clPolicy, 3, 24, 2, "Brother's engagement ceremony"),
            ("EMP-1004", alPolicy, 2, 5, 3, "Festival travel with family"),
            ("EMP-1006", clPolicy, 2, 12, 2, "Personal errand"),
            ("EMP-1010", slPolicy, 2, 19, 3, "Recovery from throat infection"),
            ("EMP-1011", plPolicy, 2, 25, 5, "Paternity leave for newborn"),
            ("EMP-1005", alPolicy, 1, 7, 4, "Extended family holiday"),
            ("EMP-1014", slPolicy, 1, 15, 2, "Fever and rest"),
            ("EMP-1021", clPolicy, 1, 21, 2, "Hometown visit"),
            ("EMP-1022", alPolicy, 1, 25, 3, "Personal travel")
        };

        foreach (var item in historicalSpec)
        {
            var monthDate = today.AddMonths(-item.MonthsBack);
            var start = SafeWorkingDate(monthDate.Year, monthDate.Month, item.StartDay);
            var end = start.AddDays(item.DaysCount - 1);
            leaveRequestsToCreate.Add((
                employeeEntities[item.EmpNo], item.Policy, start, end, 1m, "Approved", item.Reason));
        }

        // 4. Six PENDING Leave Requests across all 5 departments (populates Pending Approval KPI, Queue & Year-End cards)
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1018"], alPolicy, today.AddDays(2), today.AddDays(4), 1m, "Pending",
            "Attending sister's wedding in Amritsar"));
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1022"], clPolicy, today.AddDays(1), today.AddDays(2), 1m, "Pending",
            "Urgent family visit to Jalandhar"));
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1009"], alPolicy, today.AddDays(3), today.AddDays(6), 1m, "Pending",
            "Diwali family travel booking"));
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1005"], slPolicy, today.AddDays(1), today.AddDays(2), 1m, "Pending",
            "Scheduled minor dental procedure"));
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1013"], clPolicy, today.AddDays(4), today.AddDays(5), 1m, "Pending",
            "House shifting and utility setup"));
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1002"], alPolicy, today.AddDays(5), today.AddDays(7), 1m, "Pending",
            "Family pilgrimage trip"));

        // 5. Rejected & Withdrawn requests (so Leave Status Overview pie chart has all statuses)
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1018"], clPolicy, today.AddDays(-20), today.AddDays(-19), 1m, "Rejected",
            "Peak weekend store sale — staffing required"));
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1011"], alPolicy, today.AddDays(-35), today.AddDays(-33), 1m, "Rejected",
            "Release freeze window"));
        leaveRequestsToCreate.Add((
            employeeEntities["EMP-1014"], clPolicy, today.AddDays(-15), today.AddDays(-15), 1m, "Withdrawn",
            "Appointment rescheduled to weekend"));

        foreach (var req in leaveRequestsToCreate)
        {
            var dayDates = new List<DateOnly>();
            for (var d = req.From; d <= req.To; d = d.AddDays(1))
            {
                dayDates.Add(d);
            }

            var totalDays = dayDates.Count * req.Portion;
            var isDecided = req.State is "Approved" or "Rejected";

            var entity = new LeaveRequestEntity
            {
                TenantKey = Tenant,
                EmployeeId = req.Emp.Id,
                PolicyId = req.Policy.Id,
                LeaveTypeCode = req.Policy.Code,
                FromDate = req.From,
                ToDate = req.To,
                IsFirstDayPartial = req.Portion < 1m,
                IsLastDayPartial = false,
                Days = totalDays,
                Reason = req.Reason,
                State = req.State,
                AttachmentsJson = "[]",
                SubmittedUtc = now.AddDays(-2),
                DecidedByUserId = isDecided ? "EMP-1001" : null,
                DecidedByName = isDecided ? "Simran Kaur" : null,
                DecidedUtc = isDecided ? now.AddDays(-1) : null,
                DecisionComment = req.State == "Rejected" ? "Declined due to floor roster coverage." : null,
                UpdatedUtc = now
            };

            dbContext.LeaveRequests.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var d in dayDates)
            {
                dbContext.LeaveRequestDays.Add(new LeaveRequestDayEntity
                {
                    TenantKey = Tenant,
                    RequestId = entity.Id,
                    EmployeeId = req.Emp.Id,
                    Date = d,
                    Portion = req.Portion
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Attendance Days, Punches & Missing Punch Exceptions --------------
        // Today's breakdown across the 24 active employees:
        // - EMP-1019, EMP-1020: On Leave today (2)
        // - EMP-1021, EMP-1022, EMP-1023: Absent / Not In Yet today (3)
        // - Remaining 19 active employees: Present today (19), of which 5 arrived late:
        //   EMP-1005 (09:42 vs 09:00), EMP-1008 (09:34 vs 09:00), EMP-1012 (10:28 vs 10:00),
        //   EMP-1015 (09:24 vs 09:00), EMP-1018 (10:19 vs 10:00)

        var onLeaveToday = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EMP-1019", "EMP-1020" };
        var absentToday = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EMP-1021", "EMP-1022", "EMP-1023" };
        var lateTodayMinutes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["EMP-1005"] = 42,
            ["EMP-1008"] = 34,
            ["EMP-1012"] = 28,
            ["EMP-1015"] = 24,
            ["EMP-1018"] = 19
        };

        // Seed past 18 working days + today
        for (var offset = 18; offset >= 0; offset--)
        {
            var date = today.AddDays(-offset);
            if (holidays.Contains(date))
            {
                continue;
            }

            foreach (var spec in activeSpecs)
            {
                if (spec.JoinedOn > date)
                {
                    continue;
                }

                var emp = employeeEntities[spec.Number];
                var shift = shifts.GetValueOrDefault(spec.ShiftCode) ?? shifts["GENERAL"];
                var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                // General shift works Mon-Fri, Store shifts work Mon-Sun
                if (isWeekend && shift.WorkingDays == "NYYYYYN" && offset > 0)
                {
                    continue;
                }

                if (offset == 0)
                {
                    if (onLeaveToday.Contains(spec.Number))
                    {
                        dbContext.AttendanceDays.Add(new AttendanceDayEntity
                        {
                            TenantKey = Tenant,
                            EmployeeId = emp.Id,
                            WorkDate = date,
                            ShiftCode = shift.Code,
                            Status = "Leave",
                            WorkedMinutes = 0,
                            ExpectedMinutes = 480,
                            ExceptionsJson = "[]",
                            UpdatedUtc = now
                        });
                        continue;
                    }

                    if (absentToday.Contains(spec.Number))
                    {
                        continue;
                    }

                    var delay = lateTodayMinutes.GetValueOrDefault(spec.Number, 0);
                    var inTime = delay > 0
                        ? shift.StartTime.AddMinutes(delay)
                        : shift.StartTime.AddMinutes(-6 + (int)(emp.Id % 10));

                    var inUtc = new DateTimeOffset(date.ToDateTime(inTime), TimeSpan.Zero);

                    dbContext.AttendancePunches.Add(new AttendancePunchEntity
                    {
                        TenantKey = Tenant,
                        EmployeeId = emp.Id,
                        WorkDate = date,
                        Type = "In",
                        OccurredUtc = inUtc,
                        Source = "Device",
                        DeviceId = $"BIO-{spec.LocationCode}-01",
                        LocationLabel = spec.LocationCode,
                        ClientEventId = $"seed-{emp.Id}-{date:yyyyMMdd}-in",
                        CreatedUtc = inUtc
                    });

                    dbContext.AttendanceDays.Add(new AttendanceDayEntity
                    {
                        TenantKey = Tenant,
                        EmployeeId = emp.Id,
                        WorkDate = date,
                        ShiftCode = shift.Code,
                        Status = "Present",
                        WorkedMinutes = 480,
                        ExpectedMinutes = 480,
                        ExceptionsJson = delay > 0 ? "[\"LateIn\"]" : "[]",
                        UpdatedUtc = now
                    });
                }
                else
                {
                    // Historical working days: create realistic missing punches on 4 specific (employee, offset) pairs
                    var isMissPunchOut = (spec.Number == "EMP-1004" && offset == 2) ||
                                         (spec.Number == "EMP-1016" && offset == 4) ||
                                         (spec.Number == "EMP-1009" && offset == 7);
                    var isMissPunchIn = spec.Number == "EMP-1013" && offset == 5;

                    // Give the 5 latecomers extra historical lateness earlier this week and month
                    var histDelay = lateTodayMinutes.ContainsKey(spec.Number) && (offset is 1 or 3 or 6 or 11)
                        ? 22 + (offset * 2)
                        : 0;

                    var inTime = histDelay > 0
                        ? shift.StartTime.AddMinutes(histDelay)
                        : shift.StartTime.AddMinutes(-5 + (int)((emp.Id + offset) % 8));
                    var outTime = shift.EndTime.AddMinutes((int)((emp.Id + offset) % 12));

                    var inUtc = new DateTimeOffset(date.ToDateTime(inTime), TimeSpan.Zero);
                    var outUtc = new DateTimeOffset(date.ToDateTime(outTime), TimeSpan.Zero);

                    if (!isMissPunchIn)
                    {
                        dbContext.AttendancePunches.Add(new AttendancePunchEntity
                        {
                            TenantKey = Tenant,
                            EmployeeId = emp.Id,
                            WorkDate = date,
                            Type = "In",
                            OccurredUtc = inUtc,
                            Source = "Device",
                            DeviceId = $"BIO-{spec.LocationCode}-01",
                            LocationLabel = spec.LocationCode,
                            ClientEventId = $"seed-{emp.Id}-{date:yyyyMMdd}-in",
                            CreatedUtc = inUtc
                        });
                    }

                    if (!isMissPunchOut)
                    {
                        dbContext.AttendancePunches.Add(new AttendancePunchEntity
                        {
                            TenantKey = Tenant,
                            EmployeeId = emp.Id,
                            WorkDate = date,
                            Type = "Out",
                            OccurredUtc = outUtc,
                            Source = "Device",
                            DeviceId = $"BIO-{spec.LocationCode}-01",
                            LocationLabel = spec.LocationCode,
                            ClientEventId = $"seed-{emp.Id}-{date:yyyyMMdd}-out",
                            CreatedUtc = outUtc
                        });
                    }

                    if (isMissPunchOut || isMissPunchIn)
                    {
                        dbContext.AttendanceExceptions.Add(new AttendanceExceptionEntity
                        {
                            TenantKey = Tenant,
                            EmployeeId = emp.Id,
                            WorkDate = date,
                            ExceptionType = "MissingPunch",
                            DurationMinutes = 0,
                            State = offset <= 5 ? "Open" : "Confirmed",
                            EmployeeJustification = isMissPunchOut
                                ? "Forgot to tap out while leaving for courier dispatch"
                                : "Biometric terminal was restarting at morning entry",
                            DetectedUtc = outUtc,
                            UpdatedUtc = outUtc
                        });
                    }

                    var isRareAbsent = (spec.Number == "EMP-1021" && offset == 8) ||
                                       (spec.Number == "EMP-1022" && offset == 10);

                    dbContext.AttendanceDays.Add(new AttendanceDayEntity
                    {
                        TenantKey = Tenant,
                        EmployeeId = emp.Id,
                        WorkDate = date,
                        ShiftCode = shift.Code,
                        Status = isRareAbsent ? "Absent" : (isMissPunchIn || isMissPunchOut ? "Partial" : "Present"),
                        WorkedMinutes = isRareAbsent ? 0 : (isMissPunchIn || isMissPunchOut ? 240 : 480),
                        ExpectedMinutes = 480,
                        ExceptionsJson = isMissPunchIn || isMissPunchOut ? "[\"MissingPunch\"]" : "[]",
                        UpdatedUtc = outUtc
                    });
                }
            }
        }

        // ---- Out of Office Movements (Today) ----------------------------------
        dbContext.MovementRecords.AddRange(
            // Personal work
            new MovementRecordEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1004"].Id,
                Kind = "Personal",
                WorkDate = today,
                GoingOutUtc = now.AddMinutes(-95),
                ExpectedReturnUtc = now.AddMinutes(-20), // Overdue
                ActualReturnUtc = null,
                Detail = "Doctor Appointment — Sector 17 Clinic",
                DestinationLabel = "Sector 17, Chandigarh",
                State = "Open",
                RecordedBy = "EMP-1004",
                CreatedUtc = now.AddMinutes(-95),
                UpdatedUtc = now.AddMinutes(-95)
            },
            new MovementRecordEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1009"].Id,
                Kind = "Personal",
                WorkDate = today,
                GoingOutUtc = now.AddMinutes(-35),
                ExpectedReturnUtc = now.AddMinutes(25),
                ActualReturnUtc = null,
                Detail = "Bank KYC Verification — HDFC Sector 8",
                DestinationLabel = "Sector 8, Chandigarh",
                State = "Open",
                RecordedBy = "EMP-1009",
                CreatedUtc = now.AddMinutes(-35),
                UpdatedUtc = now.AddMinutes(-35)
            },
            new MovementRecordEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1014"].Id,
                Kind = "Personal",
                WorkDate = today,
                GoingOutUtc = now.AddMinutes(-150),
                ExpectedReturnUtc = now.AddMinutes(-90),
                ActualReturnUtc = now.AddMinutes(-95),
                Detail = "Vehicle Service Drop-off",
                DestinationLabel = "Industrial Area Phase 1",
                State = "Returned",
                RecordedBy = "EMP-1014",
                CreatedUtc = now.AddMinutes(-150),
                UpdatedUtc = now.AddMinutes(-95)
            },
            // Official work
            new MovementRecordEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1012"].Id,
                Kind = "Official",
                WorkDate = today,
                GoingOutUtc = now.AddMinutes(-70),
                ExpectedReturnUtc = now.AddMinutes(50),
                ActualReturnUtc = null,
                Detail = "Seasonal Window Display Audit — Elante Mall Store",
                DestinationLabel = "Elante Mall, Chandigarh",
                State = "Open",
                RecordedBy = "EMP-1012",
                CreatedUtc = now.AddMinutes(-70),
                UpdatedUtc = now.AddMinutes(-70)
            },
            new MovementRecordEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1003"].Id,
                Kind = "Official",
                WorkDate = today,
                GoingOutUtc = now.AddMinutes(-110),
                ExpectedReturnUtc = now.AddMinutes(-15), // Overdue
                ActualReturnUtc = null,
                Detail = "Statutory Audit & GST Reconciliation with External CA",
                DestinationLabel = "Sector 34, Chandigarh",
                State = "Open",
                RecordedBy = "EMP-1003",
                CreatedUtc = now.AddMinutes(-110),
                UpdatedUtc = now.AddMinutes(-110)
            },
            new MovementRecordEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1007"].Id,
                Kind = "Official",
                WorkDate = today,
                GoingOutUtc = now.AddMinutes(-180),
                ExpectedReturnUtc = now.AddMinutes(-60),
                ActualReturnUtc = now.AddMinutes(-65),
                Detail = "Marketplace Fulfilment Partner Review",
                DestinationLabel = "Mohali Phase 8B",
                State = "Returned",
                RecordedBy = "EMP-1007",
                CreatedUtc = now.AddMinutes(-180),
                UpdatedUtc = now.AddMinutes(-65)
            });

        // ---- Gate Passes & Gate Pass Scans ------------------------------------
        var approvedPass1 = new GatePassEntity
        {
            TenantKey = Tenant,
            EmployeeId = employeeEntities["EMP-1016"].Id,
            PassType = "DayPass",
            FromDate = today,
            ToDate = today,
            FromTime = new TimeOnly(11, 0),
            ToTime = new TimeOnly(15, 0),
            Purpose = "Inter-store stock transfer supervision",
            DestinationLabel = "Mohali Flagship Store",
            State = "Approved",
            VerificationToken = "A1B2C3D4E5F60718293A4B5C6D7E8F90",
            DecidedByUserId = "EMP-1001",
            DecidedByName = "Simran Kaur",
            DecidedUtc = now.AddHours(-3),
            RequestedUtc = now.AddHours(-5),
            UpdatedUtc = now.AddHours(-3)
        };

        var approvedPass2 = new GatePassEntity
        {
            TenantKey = Tenant,
            EmployeeId = employeeEntities["EMP-1013"].Id,
            PassType = "MultiDayPass",
            FromDate = today,
            ToDate = today.AddDays(2),
            Purpose = "Festive visual merchandising rollout across NCR stores",
            DestinationLabel = "Delhi NCR Stores",
            State = "Approved",
            VerificationToken = "F0E1D2C3B4A5968778695A4B3C2D1E0F",
            DecidedByUserId = "EMP-1001",
            DecidedByName = "Simran Kaur",
            DecidedUtc = now.AddDays(-1),
            RequestedUtc = now.AddDays(-2),
            UpdatedUtc = now.AddDays(-1)
        };

        dbContext.GatePasses.AddRange(
            approvedPass1,
            approvedPass2,
            new GatePassEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1011"].Id,
                PassType = "DayPass",
                FromDate = today.AddDays(1),
                ToDate = today.AddDays(1),
                FromTime = new TimeOnly(14, 0),
                ToTime = new TimeOnly(17, 30),
                Purpose = "Warehouse barcode scanner hardware replacement",
                DestinationLabel = "Zirakpur Logistics Hub",
                State = "Pending",
                RequestedUtc = now.AddHours(-2),
                UpdatedUtc = now.AddHours(-2)
            },
            new GatePassEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1006"].Id,
                PassType = "DayPass",
                FromDate = today.AddDays(1),
                ToDate = today.AddDays(1),
                FromTime = new TimeOnly(10, 30),
                ToTime = new TimeOnly(13, 0),
                Purpose = "Bank signatory mandate submission",
                DestinationLabel = "ICICI Bank, Sector 9",
                State = "Pending",
                RequestedUtc = now.AddHours(-4),
                UpdatedUtc = now.AddHours(-4)
            },
            new GatePassEntity
            {
                TenantKey = Tenant,
                EmployeeId = employeeEntities["EMP-1017"].Id,
                PassType = "MultiDayPass",
                FromDate = today.AddDays(3),
                ToDate = today.AddDays(5),
                Purpose = "Regional winter collection buyer showcase",
                DestinationLabel = "Gurugram Ambience Mall",
                State = "Pending",
                RequestedUtc = now.AddHours(-6),
                UpdatedUtc = now.AddHours(-6)
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.GatePassScans.AddRange(
            new GatePassScanEntity
            {
                TenantKey = Tenant,
                GatePassId = approvedPass1.Id,
                Direction = "Exit",
                ScannedUtc = now.AddHours(-2),
                ScannedBy = "GATE-SEC-01",
                GateLabel = "Main Gate — Sector 17"
            },
            new GatePassScanEntity
            {
                TenantKey = Tenant,
                GatePassId = approvedPass1.Id,
                Direction = "Entry",
                ScannedUtc = now.AddMinutes(-30),
                ScannedBy = "GATE-SEC-01",
                GateLabel = "Main Gate — Sector 17"
            },
            new GatePassScanEntity
            {
                TenantKey = Tenant,
                GatePassId = approvedPass2.Id,
                Direction = "Exit",
                ScannedUtc = now.AddHours(-4),
                ScannedBy = "GATE-SEC-02",
                GateLabel = "Dispatch Gate — Mohali"
            });

        // ---- Recruitment Candidates & Applications ----------------------------
        if (jobOpenings.Count > 0)
        {
            var candidateSpecs = new (string Name, string Email, string Phone, string Location, decimal Exp, int OpeningIdx, string Stage, decimal Rating)[]
            {
                ("Amanpreet Singh", "amanpreet.singh@example.com", "+919815100101", "Chandigarh", 6.5m, 0, "Interviewing", 4.5m),
                ("Riya Mehra", "riya.mehra@example.com", "+919815100102", "Mohali", 5.0m, 0, "Shortlisted", 4.2m),
                ("Sahil Thakur", "sahil.thakur@example.com", "+919815100103", "Panchkula", 4.0m, 0, "Applied", 3.8m),
                ("Nidhi Chawla", "nidhi.chawla@example.com", "+919815100104", "Chandigarh", 7.0m, 0, "Offered", 4.8m),

                ("Pranav Kulkarni", "pranav.kulkarni@example.com", "+919815100105", "Mohali", 3.5m, 1, "Interviewing", 4.3m),
                ("Ishita Bedi", "ishita.bedi@example.com", "+919815100106", "Chandigarh", 2.5m, 1, "Screening", 3.9m),
                ("Kunal Garg", "kunal.garg@example.com", "+919815100107", "Ludhiana", 4.2m, 1, "Applied", 4.0m),

                ("divya.nair", "divya.nair@example.com", "+919815100108", "Chandigarh", 2.0m, 2, "Shortlisted", 4.1m),
                ("Tarun Gill", "tarun.gill@example.com", "+919815100109", "Mohali", 1.5m, 2, "Applied", 3.7m),
                ("Palak Arora", "palak.arora@example.com", "+919815100110", "Chandigarh", 3.0m, 2, "Hired", 4.7m),

                ("Raghav Sethi", "raghav.sethi@example.com", "+919815100111", "Mohali", 6.0m, 3, "Interviewing", 4.6m),
                ("Sneha Oberoi", "sneha.oberoi@example.com", "+919815100112", "Delhi NCR", 5.5m, 3, "Applied", 4.0m),

                ("Zoya Khan", "zoya.khan@example.com", "+919815100113", "Delhi NCR", 3.5m, 4, "Shortlisted", 4.4m),
                ("Yashvardhan Rao", "yash.rao@example.com", "+919815100114", "Gurugram", 2.8m, 4, "Rejected", 2.9m)
            };

            for (var idx = 0; idx < candidateSpecs.Length; idx++)
            {
                var cSpec = candidateSpecs[idx];
                var candidate = new CandidateEntity
                {
                    TenantKey = Tenant,
                    Email = cSpec.Email,
                    FullName = cSpec.Name == "divya.nair" ? "Divya Nair" : cSpec.Name,
                    Phone = cSpec.Phone,
                    CurrentLocation = cSpec.Location,
                    TotalExperienceYears = cSpec.Exp,
                    CreatedUtc = now.AddDays(-(14 - idx)),
                    UpdatedUtc = now.AddDays(-(7 - (idx % 5)))
                };

                dbContext.Candidates.Add(candidate);
                await dbContext.SaveChangesAsync(cancellationToken);

                var opening = jobOpenings[cSpec.OpeningIdx % jobOpenings.Count];
                var appliedUtc = now.AddDays(-(12 - (idx % 10)));

                var app = new ApplicationEntity
                {
                    TenantKey = Tenant,
                    JobOpeningId = opening.Id,
                    CandidateId = candidate.Id,
                    Stage = cSpec.Stage,
                    Source = idx % 2 == 0 ? "CareerSite" : "Referral",
                    NoticePeriodDays = 30,
                    ExpectedCtc = 650000m + (idx * 45000m),
                    AnswersJson = "{}",
                    Rating = cSpec.Rating,
                    RejectionReason = cSpec.Stage == "Rejected" ? "Portfolio did not match luxury retail guidelines." : null,
                    ConsentGranted = true,
                    ConsentPolicyVersion = "1.0",
                    ConsentUtc = appliedUtc,
                    AppliedUtc = appliedUtc,
                    StageChangedUtc = appliedUtc.AddDays(1),
                    StageChangedBy = "EMP-1001",
                    UpdatedUtc = appliedUtc.AddDays(1)
                };

                dbContext.Applications.Add(app);
            }
        }

        // ---- Sample Loans & Salary Advances -----------------------------------
        dbContext.Loans.AddRange(
            new LoanEntity
            {
                TenantKey = Tenant,
                VoucherNo = "LN-2026-001",
                EmployeeId = employeeEntities["EMP-1015"].Id,
                Type = "Loan",
                Principal = 120000m,
                InterestPercent = 6.0m,
                Installments = 12,
                TotalRepayable = 127200m,
                MonthlyInstallment = 10600m,
                StartYear = today.Year,
                StartMonth = Math.Max(1, today.Month - 2),
                Purpose = "Two-wheeler purchase & family medical expenses",
                Status = "Active",
                RequestedOn = today.AddMonths(-2),
                DisbursedOn = today.AddMonths(-2).AddDays(3),
                RequestedBy = "EMP-1015",
                DecidedBy = "EMP-1001",
                DecidedUtc = now.AddMonths(-2),
                CreatedUtc = now.AddMonths(-2),
                UpdatedUtc = now
            },
            new LoanEntity
            {
                TenantKey = Tenant,
                VoucherNo = "ADV-2026-002",
                EmployeeId = employeeEntities["EMP-1018"].Id,
                Type = "Advance",
                AdvanceTypeCode = "FESTIVAL",
                Principal = 15000m,
                InterestPercent = 0m,
                Installments = 3,
                TotalRepayable = 15000m,
                MonthlyInstallment = 5000m,
                StartYear = today.Year,
                StartMonth = today.Month,
                Purpose = "Diwali festival advance",
                Status = "Active",
                RequestedOn = today.AddDays(-10),
                DisbursedOn = today.AddDays(-7),
                RequestedBy = "EMP-1018",
                DecidedBy = "EMP-1001",
                DecidedUtc = now.AddDays(-8),
                CreatedUtc = now.AddDays(-10),
                UpdatedUtc = now
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Finished seeding operational workforce dataset for '{Tenant}'.", Tenant);
    }

    private static DateOnly SafeDate(int year, int month, int day)
    {
        var maxDay = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Clamp(day, 1, maxDay));
    }

    private static DateOnly SafeWorkingDate(int year, int month, int preferredDay)
    {
        var date = SafeDate(year, month, preferredDay);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
            if (date.Month != month)
            {
                date = date.AddDays(-3);
                break;
            }
        }
        return date;
    }

    private static List<DateOnly> GetWorkingDaysInMonth(int year, int month, HashSet<DateOnly> holidays)
    {
        var result = new List<DateOnly>();
        var daysInMonth = DateTime.DaysInMonth(year, month);
        for (var d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(year, month, d);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(date))
            {
                result.Add(date);
            }
        }
        return result;
    }
}
