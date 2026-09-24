using UIKit;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Platforms.iOS;

/// <summary>iOS Settings → Accessibility → Motion → Reduce Motion.</summary>
public sealed class ReducedMotionService : IReducedMotionService
{
    public bool IsReducedMotionEnabled => UIAccessibility.IsReduceMotionEnabled;
}
