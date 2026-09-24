using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Shared.Behaviors;

/// <summary>
/// The spec's "buttons/actions: small press scale with colour transition, 120ms" — attached
/// through a Style rather than wired per page, so every button and card gets it once and cannot
/// drift out of sync with the others. The colour half of that pairing is whatever the element's
/// own pressed/normal <see cref="VisualStateManager"/> states already do (unaffected by this);
/// this behavior only owns the scale.
/// </summary>
public sealed class PressScaleBehavior : Behavior<VisualElement>
{
    private PointerGestureRecognizer? recognizer;
    private VisualElement? target;

    protected override void OnAttachedTo(VisualElement bindable)
    {
        base.OnAttachedTo(bindable);

        if (bindable is not View view)
        {
            return;
        }

        target = bindable;
        recognizer = new PointerGestureRecognizer();
        recognizer.PointerPressed += OnPressed;
        recognizer.PointerReleased += OnReleased;
        recognizer.PointerExited += OnReleased;
        view.GestureRecognizers.Add(recognizer);
    }

    protected override void OnDetachingFrom(VisualElement bindable)
    {
        if (recognizer is not null)
        {
            recognizer.PointerPressed -= OnPressed;
            recognizer.PointerReleased -= OnReleased;
            recognizer.PointerExited -= OnReleased;

            if (bindable is View view)
            {
                view.GestureRecognizers.Remove(recognizer);
            }
        }

        recognizer = null;
        target = null;
        base.OnDetachingFrom(bindable);
    }

    private async void OnPressed(object? sender, PointerEventArgs e)
    {
        if (target is not null)
        {
            await target.PressDownAsync();
        }
    }

    private async void OnReleased(object? sender, PointerEventArgs e)
    {
        if (target is not null)
        {
            await target.PressUpAsync();
        }
    }
}
