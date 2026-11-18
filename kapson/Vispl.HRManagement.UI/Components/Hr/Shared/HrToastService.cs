namespace Vispl.HRManagement.UI.Components.Hr.Shared;

/// <summary>
/// The "Saved" message every HR portal form shows after a write. One per signed-in browser tab
/// (scoped to the Blazor circuit); <see cref="HrToastHost"/>, which HrNav renders on every HR
/// page, displays it. A page calls <c>Toast.Success("Designation saved.")</c> and does nothing else.
/// </summary>
public sealed class HrToastService
{
    public sealed record Toast(long Id, string Message, bool IsError, DateTimeOffset ShownUtc);

    private long _next;

    public Toast? Current { get; private set; }

    public event Action? Changed;

    public void Success(string message) => Show(message, isError: false);

    public void Error(string message) => Show(message, isError: true);

    public void Dismiss(long id)
    {
        if (Current?.Id == id)
        {
            Current = null;
            Changed?.Invoke();
        }
    }

    private void Show(string message, bool isError)
    {
        Current = new Toast(Interlocked.Increment(ref _next), message, isError, DateTimeOffset.UtcNow);
        Changed?.Invoke();
    }
}
