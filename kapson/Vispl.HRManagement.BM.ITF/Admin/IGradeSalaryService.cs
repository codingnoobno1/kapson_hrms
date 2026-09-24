using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface IGradeSalaryService
{
    // One entry per grade in the Grade master, whether or not a structure has been saved for it.
    Task<IReadOnlyList<GradeSalaryStructureDto>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task SaveAsync(string tenantKey, GradeSalaryStructureDto structure, CancellationToken cancellationToken = default);

    // Calculates an unsaved structure, so an editor previews exactly what payroll will pay.
    Task<SalaryBreakdownDto> CalculateAsync(string tenantKey, GradeSalaryStructureDto structure, CancellationToken cancellationToken = default);
}
