using Microsoft.AspNetCore.DataProtection;
using Vispl.HRManagement.BM.ITF.Shared;

namespace Vispl.HRManagement.Composition.Shared;

/// <summary>
/// <see cref="ISensitiveValueProtector"/> over ASP.NET Core Data Protection, using the host's
/// persisted key ring — so a value protected before a restart still unprotects after it.
/// </summary>
public sealed class DataProtectionSensitiveValueProtector(IDataProtectionProvider provider) : ISensitiveValueProtector
{
    private const string RootPurpose = "Vispl.HRManagement.SensitiveValues.v1";

    public string Protect(string purpose, string value) =>
        provider.CreateProtector(RootPurpose, purpose).Protect(value);

    public string Unprotect(string purpose, string protectedValue) =>
        provider.CreateProtector(RootPurpose, purpose).Unprotect(protectedValue);
}
