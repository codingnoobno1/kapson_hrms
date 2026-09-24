namespace Vispl.HRManagement.VO.Admin;

public sealed class ThemeTemplateRecord
{
    public required string TemplateKey { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string PrimaryColor { get; set; }
    public required string AccentColor { get; set; }
    public required string SurfaceColor { get; set; }
    public required string FontFamily { get; set; }
    public required string HeadingFontFamily { get; set; }
    public int BorderRadius { get; set; }
    public required string PreviewGradient { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
