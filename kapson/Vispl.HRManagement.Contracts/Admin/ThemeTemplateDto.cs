using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

public sealed class ThemeTemplateDto
{
    [Required, RegularExpression("^[a-z0-9-]+$")] public string TemplateKey { get; set; } = string.Empty;
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(400)] public string Description { get; set; } = string.Empty;
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] public string PrimaryColor { get; set; } = string.Empty;
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] public string AccentColor { get; set; } = string.Empty;
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] public string SurfaceColor { get; set; } = string.Empty;
    [Required, StringLength(160)] public string FontFamily { get; set; } = string.Empty;
    [Required, StringLength(160)] public string HeadingFontFamily { get; set; } = string.Empty;
    public int BorderRadius { get; set; }
    [Required, StringLength(240)] public string PreviewGradient { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
