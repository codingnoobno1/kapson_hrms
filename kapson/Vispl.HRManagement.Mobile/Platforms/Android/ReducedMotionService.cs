using Android.Provider;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Platforms.Android;

/// <summary>
/// Android expresses "reduce motion" as the system animator scale, not a single switch — a
/// developer who has turned off animations for testing gets the same treatment as somebody who
/// asked for less motion, which is the right call either way.
/// </summary>
public sealed class ReducedMotionService : IReducedMotionService
{
    public bool IsReducedMotionEnabled
    {
        get
        {
            try
            {
                var resolver = global::Android.App.Application.Context.ContentResolver;
                var scale = Settings.Global.GetFloat(resolver, Settings.Global.AnimatorDurationScale, 1f);
                return scale <= 0f;
            }
            catch
            {
                // A setting that cannot be read is not evidence the user wants less motion.
                return false;
            }
        }
    }
}
