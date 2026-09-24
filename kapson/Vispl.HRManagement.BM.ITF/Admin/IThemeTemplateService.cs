using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface IThemeTemplateService
{
    Task<IReadOnlyList<ThemeTemplateDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<ThemeTemplateDto?> GetByKeyAsync(string templateKey, CancellationToken cancellationToken = default);
}
