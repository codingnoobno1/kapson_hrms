namespace Vispl.HRManagement.BM.ITF.Shared;

/// <summary>
/// Encrypts values that must be stored but never shown in full — bank account numbers today.
/// <paramref name="purpose"/> isolates one kind of value from another: a value protected for one
/// purpose cannot be unprotected under a different one.
/// </summary>
public interface ISensitiveValueProtector
{
    string Protect(string purpose, string value);

    string Unprotect(string purpose, string protectedValue);
}
