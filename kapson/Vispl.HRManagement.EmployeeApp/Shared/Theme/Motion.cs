namespace Vispl.HRManagement.EmployeeApp.Shared.Theme;

/// <summary>Short, GPU-friendly motion shared by the native app. No layout properties animate.</summary>
public static class Motion
{
    public static async Task EnterAsync(VisualElement view, uint delay = 0)
    {
        if (delay > 0) await Task.Delay((int)delay);
        view.Opacity = 0;
        view.TranslationY = 10;
        await Task.WhenAll(
            view.FadeTo(1, 180, Easing.CubicOut),
            view.TranslateTo(0, 0, 220, Easing.CubicOut));
    }

    public static async Task PulseAsync(VisualElement view)
    {
        await view.ScaleTo(.975, 70, Easing.CubicOut);
        await view.ScaleTo(1, 110, Easing.CubicOut);
    }

    public static void AddDepth(Border card, float opacity = .07f) => card.Shadow = new Shadow
    {
        Brush = Colors.Black,
        Opacity = opacity,
        Radius = 10,
        Offset = new Point(0, 3)
    };
}
