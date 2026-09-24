using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Shared.Controls;

/// <summary>
/// The spec's "Loading: skeleton shimmer, 1.4s" — a muted rounded block that pulses opacity
/// while it's on screen. Size and position it like any other View (WidthRequest, HeightRequest,
/// Grid.Row, ...); place a stack of these where the real content will be, wired to
/// <c>IsVisible="{Binding IsBusy}"</c>.
/// </summary>
public partial class SkeletonView : ContentView
{
    private static readonly TimeSpan HalfCycle = TimeSpan.FromMilliseconds(MotionTokens.DurationShimmer / 2.0);

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(double), typeof(SkeletonView), 12d,
        propertyChanged: (bindable, _, value) => ((SkeletonView)bindable).ApplyCornerRadius((double)value));

    private CancellationTokenSource? shimmerCancellation;

    public SkeletonView()
    {
        InitializeComponent();
        Loaded += (_, _) => StartShimmer();
        Unloaded += (_, _) => StopShimmer();
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private void ApplyCornerRadius(double radius)
    {
        if (PlateShape is RoundRectangle shape)
        {
            shape.CornerRadius = radius;
        }
    }

    private async void StartShimmer()
    {
        if (MotionSettings.ReducedMotionEnabled)
        {
            Plate.Opacity = 0.6;
            return;
        }

        shimmerCancellation?.Cancel();
        var cancellation = shimmerCancellation = new CancellationTokenSource();

        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await Plate.FadeTo(0.85, (uint)HalfCycle.TotalMilliseconds, Easing.SinInOut);
                await Plate.FadeTo(0.35, (uint)HalfCycle.TotalMilliseconds, Easing.SinInOut);
            }
        }
        catch (ObjectDisposedException)
        {
            // The page navigated away mid-fade; nothing left to animate.
        }
    }

    private void StopShimmer()
    {
        shimmerCancellation?.Cancel();
        shimmerCancellation = null;
    }
}
