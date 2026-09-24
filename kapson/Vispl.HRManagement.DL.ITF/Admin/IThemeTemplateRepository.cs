using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IThemeTemplateRepository
{
    Task<IReadOnlyList<ThemeTemplateRecord>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<ThemeTemplateRecord?> GetByKeyAsync(string templateKey, CancellationToken cancellationToken = default);
}
