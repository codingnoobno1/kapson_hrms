using System.Windows.Input;
using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Shared.Controls;

public partial class ApplicantCard : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item), typeof(ApplicantSummary), typeof(ApplicantCard));
    public static readonly BindableProperty OpenCommandProperty = BindableProperty.Create(
        nameof(OpenCommand), typeof(ICommand), typeof(ApplicantCard));

    public ApplicantCard()
    {
        InitializeComponent();
        Loaded += async (_, _) => await this.PlayArrivalAsync(this.StaggerDelayFor());
    }

    public ApplicantSummary? Item { get => (ApplicantSummary?)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
    public ICommand? OpenCommand { get => (ICommand?)GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }
}
