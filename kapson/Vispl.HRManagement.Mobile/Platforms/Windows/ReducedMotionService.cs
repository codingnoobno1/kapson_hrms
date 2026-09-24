using Windows.UI.ViewManagement;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Platforms.Windows;

/// <summary>Windows' "Show animations in Windows" setting, read through UISettings.</summary>
public sealed class ReducedMotionService : IReducedMotionService
{
    public bool IsReducedMotionEnabled
    {
        get
        {
            try
            {
                return !new UISettings().AnimationsEnabled;
            }
            catch
            {
                return false;
            }
        }
    }
}
