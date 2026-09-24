using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IGradeSalaryRepository
{
    Task<IReadOnlyList<GradeSalaryRecord>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task<GradeSalaryRecord?> GetAsync(string tenantKey, string gradeCode, CancellationToken cancellationToken = default);

    Task UpsertAsync(GradeSalaryRecord structure, CancellationToken cancellationToken = default);
}
