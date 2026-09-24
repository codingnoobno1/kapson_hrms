using UIKit;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Platforms.MacCatalyst;

/// <summary>macOS System Settings → Accessibility → Display → Reduce motion, surfaced to Mac Catalyst apps the same way as iOS.</summary>
public sealed class ReducedMotionService : IReducedMotionService
{
    public bool IsReducedMotionEnabled => UIAccessibility.IsReduceMotionEnabled;
}
