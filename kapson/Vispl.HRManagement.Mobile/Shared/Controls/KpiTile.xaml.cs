using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Shared.Controls;

public partial class KpiTile : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item), typeof(KpiMetric), typeof(KpiTile));

    public KpiTile()
    {
        InitializeComponent();

        // Emphasis timing (260ms), not the base 180ms — the spec calls these out separately
        // from plain list arrival ("KPI tiles: staggered rise ... 180-260ms").
        Loaded += async (_, _) => await this.PlayArrivalAsync(this.StaggerDelayFor(), duration: MotionTokens.DurationEmphasis);
    }

    public KpiMetric? Item { get => (KpiMetric?)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
}
