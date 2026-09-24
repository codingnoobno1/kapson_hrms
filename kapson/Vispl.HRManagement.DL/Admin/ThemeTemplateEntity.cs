namespace Vispl.HRManagement.DL.Admin;

public sealed class ThemeTemplateEntity
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
    public string SurfaceColor { get; set; } = string.Empty;
    public string FontFamily { get; set; } = string.Empty;
    public string HeadingFontFamily { get; set; } = string.Empty;
    public int BorderRadius { get; set; }
    public string PreviewGradient { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
