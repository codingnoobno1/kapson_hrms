namespace Vispl.HRManagement.Mobile.Shared.Motion;

/// <summary>
/// Whether the OS has been told to minimise motion — iOS "Reduce Motion", Android's animator
/// duration scale set to 0, or Windows' "Show animations" switched off. One property per
/// platform under <c>Platforms/*</c>; there is no MAUI-wide API for this today.
/// </summary>
public interface IReducedMotionService
{
    bool IsReducedMotionEnabled { get; }
}
