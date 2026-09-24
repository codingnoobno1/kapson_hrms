using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class ThemeTemplateRepository(HrManagementDbContext dbContext) : IThemeTemplateRepository
{
    public async Task<IReadOnlyList<ThemeTemplateRecord>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        (await dbContext.ThemeTemplates.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.SortOrder).ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<ThemeTemplateRecord?> GetByKeyAsync(string templateKey, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ThemeTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TemplateKey == templateKey, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    private static ThemeTemplateRecord ToRecord(ThemeTemplateEntity entity) => new()
    {
        TemplateKey = entity.TemplateKey, Name = entity.Name, Description = entity.Description,
        PrimaryColor = entity.PrimaryColor, AccentColor = entity.AccentColor, SurfaceColor = entity.SurfaceColor,
        FontFamily = entity.FontFamily, HeadingFontFamily = entity.HeadingFontFamily,
        BorderRadius = entity.BorderRadius, PreviewGradient = entity.PreviewGradient,
        SortOrder = entity.SortOrder, IsActive = entity.IsActive
    };
}
