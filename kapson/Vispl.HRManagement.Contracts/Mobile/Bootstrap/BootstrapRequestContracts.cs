using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Mobile.Bootstrap;

public sealed class RenameDeviceRequest
{
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
}
