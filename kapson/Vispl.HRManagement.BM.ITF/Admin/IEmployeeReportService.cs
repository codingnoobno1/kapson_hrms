using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>
/// Read models for the Employee tab and the Employee Report tab of the HR portal menu.
/// Directory and hierarchy need EMPLOYEES.VIEW; the monthly attendance report needs REPORTS.VIEW
/// and ATTENDANCE.VIEW; the salary register needs REPORTS.VIEW and PAYROLL.VIEW, because it
/// exposes compensation.
/// </summary>
public interface IEmployeeReportService
{
    Task<IReadOnlyList<EmployeeDirectoryRowDto>> GetDirectoryAsync(
        MobileCallerContext caller, EmployeeReportQuery query, CancellationToken cancellationToken = default);

    Task<OrgHierarchyDto> GetHierarchyAsync(
        MobileCallerContext caller, EmployeeReportQuery query, CancellationToken cancellationToken = default);

    Task<MonthlyAttendanceReportDto> GetMonthlyAttendanceAsync(
        MobileCallerContext caller, EmployeeReportQuery query, CancellationToken cancellationToken = default);

    Task<SalaryRegisterDto> GetSalaryRegisterAsync(
        MobileCallerContext caller, EmployeeReportQuery query, CancellationToken cancellationToken = default);
}
