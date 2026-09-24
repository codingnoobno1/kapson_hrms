using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class ThemeTemplateService(IThemeTemplateRepository repository) : IThemeTemplateService
{
    public async Task<IReadOnlyList<ThemeTemplateDto>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetActiveAsync(cancellationToken)).Select(ToDto).ToList();
    public async Task<ThemeTemplateDto?> GetByKeyAsync(string templateKey, CancellationToken cancellationToken = default)
    {
        var record = await repository.GetByKeyAsync(templateKey, cancellationToken);
        return record is null ? null : ToDto(record);
    }
    private static ThemeTemplateDto ToDto(ThemeTemplateRecord item) => new()
    {
        TemplateKey = item.TemplateKey, Name = item.Name, Description = item.Description,
        PrimaryColor = item.PrimaryColor, AccentColor = item.AccentColor, SurfaceColor = item.SurfaceColor,
        FontFamily = item.FontFamily, HeadingFontFamily = item.HeadingFontFamily, BorderRadius = item.BorderRadius,
        PreviewGradient = item.PreviewGradient, SortOrder = item.SortOrder, IsActive = item.IsActive
    };
}
