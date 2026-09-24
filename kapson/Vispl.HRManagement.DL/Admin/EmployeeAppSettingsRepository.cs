using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class EmployeeAppSettingsRepository(HrManagementDbContext dbContext) : IEmployeeAppSettingsRepository
{
    public async Task<EmployeeAppSettingsRecord?> GetAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmployeeAppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertAsync(EmployeeAppSettingsRecord record, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmployeeAppSettings
            .FirstOrDefaultAsync(item => item.TenantKey == record.TenantKey, cancellationToken);

        if (entity is null)
        {
            entity = new EmployeeAppSettingsEntity { TenantKey = record.TenantKey };
            dbContext.EmployeeAppSettings.Add(entity);
        }

        entity.AllowAppPunch = record.AllowAppPunch;
        entity.RequireLocationForPunch = record.RequireLocationForPunch;
        entity.AllowGatePassRequests = record.AllowGatePassRequests;
        entity.AllowAttendanceCorrections = record.AllowAttendanceCorrections;
        entity.ShowSalaryDetails = record.ShowSalaryDetails;
        entity.ShowBankDetails = record.ShowBankDetails;
        entity.ShowOnboarding = record.ShowOnboarding;
        entity.AllowProfileChangeRequests = record.AllowProfileChangeRequests;
        entity.AttendanceTargetPercent = record.AttendanceTargetPercent;
        entity.UpdatedUtc = record.UpdatedUtc;
        entity.UpdatedBy = record.UpdatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static EmployeeAppSettingsRecord ToRecord(EmployeeAppSettingsEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        AllowAppPunch = entity.AllowAppPunch,
        RequireLocationForPunch = entity.RequireLocationForPunch,
        AllowGatePassRequests = entity.AllowGatePassRequests,
        AllowAttendanceCorrections = entity.AllowAttendanceCorrections,
        ShowSalaryDetails = entity.ShowSalaryDetails,
        ShowBankDetails = entity.ShowBankDetails,
        ShowOnboarding = entity.ShowOnboarding,
        AllowProfileChangeRequests = entity.AllowProfileChangeRequests,
        AttendanceTargetPercent = entity.AttendanceTargetPercent,
        UpdatedUtc = entity.UpdatedUtc,
        UpdatedBy = entity.UpdatedBy
    };
}
