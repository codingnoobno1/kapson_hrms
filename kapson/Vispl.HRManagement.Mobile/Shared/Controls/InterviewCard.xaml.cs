using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Shared.Controls;

public partial class InterviewCard : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item), typeof(InterviewSummary), typeof(InterviewCard));

    public InterviewCard()
    {
        InitializeComponent();
        Loaded += async (_, _) => await this.PlayArrivalAsync(this.StaggerDelayFor());
    }

    public InterviewSummary? Item { get => (InterviewSummary?)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
}
