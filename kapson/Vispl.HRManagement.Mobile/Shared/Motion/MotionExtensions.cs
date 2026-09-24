namespace Vispl.HRManagement.Mobile.Shared.Motion;

/// <summary>
/// The handful of animations named in the motion spec, as extension methods over
/// <see cref="VisualElement"/> so a page or control reaches for <c>this.PlayArrivalAsync()</c>
/// rather than hand-rolling fade/translate pairs. Every one of them checks
/// <see cref="MotionSettings.ReducedMotionEnabled"/> first and snaps straight to the end state
/// when it's on, so a caller never needs its own reduced-motion branch.
/// </summary>
public static class MotionExtensions
{
    /// <summary>
    /// Fade plus rise — the page/list/card arrival named throughout the spec. Call it from
    /// <c>OnAppearing</c> for a page, or from a control's <c>Loaded</c> event for a card.
    /// </summary>
    public static async Task PlayArrivalAsync(
        this VisualElement element, uint delay = 0, double? riseDistance = null, uint? duration = null)
    {
        if (MotionSettings.ReducedMotionEnabled)
        {
            element.Opacity = 1;
            element.TranslationY = 0;
            return;
        }

        element.Opacity = 0;
        element.TranslationY = riseDistance ?? MotionTokens.RiseDistance;

        if (delay > 0)
        {
            await Task.Delay((int)delay);
        }

        await Task.WhenAll(
            element.FadeTo(1, duration ?? MotionTokens.DurationBase, MotionTokens.EntranceEasing),
            element.TranslateTo(0, 0, duration ?? MotionTokens.DurationBase, MotionTokens.EntranceEasing));
    }

    /// <summary>
    /// This element's position among its parent's children — only meaningful for a
    /// non-virtualised list (a <c>BindableLayout</c>-driven <c>VerticalStackLayout</c>, which
    /// materialises every item at once and so needs an explicit stagger). A virtualised
    /// <c>CollectionView</c> cell has no such parent and gets -1: its items already arrive
    /// visibly staggered because the platform materialises them across several frames.
    /// </summary>
    public static int IndexInParent(this VisualElement element) =>
        element.Parent is Layout layout ? layout.Children.IndexOf(element) : -1;

    /// <summary>The stagger delay for <see cref="PlayArrivalAsync"/>, capped so a long list settles quickly.</summary>
    public static uint StaggerDelayFor(this VisualElement element)
    {
        var index = element.IndexInParent();
        return index < 0 ? 0 : (uint)(Math.Min(index, MotionTokens.StaggerCap) * MotionTokens.StaggerStep);
    }

    /// <summary>Press-down half of the small scale used on buttons, cards and tiles.</summary>
    public static Task PressDownAsync(this VisualElement element) =>
        MotionSettings.ReducedMotionEnabled
            ? Task.CompletedTask
            : element.ScaleTo(MotionTokens.PressScale, MotionTokens.DurationPress, MotionTokens.PressEasing);

    /// <summary>Release half — returns the element to full size.</summary>
    public static Task PressUpAsync(this VisualElement element) =>
        MotionSettings.ReducedMotionEnabled
            ? Task.CompletedTask
            : element.ScaleTo(1, MotionTokens.DurationPress, MotionTokens.PressEasing);
}
