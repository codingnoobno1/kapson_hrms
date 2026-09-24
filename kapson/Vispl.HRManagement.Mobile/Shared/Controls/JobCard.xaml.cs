using System.Windows.Input;
using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Shared.Controls;

public partial class JobCard : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item), typeof(JobSummary), typeof(JobCard));
    public static readonly BindableProperty OpenCommandProperty = BindableProperty.Create(
        nameof(OpenCommand), typeof(ICommand), typeof(JobCard));

    public JobCard()
    {
        InitializeComponent();
        Loaded += async (_, _) => await this.PlayArrivalAsync(this.StaggerDelayFor());
    }

    public JobSummary? Item { get => (JobSummary?)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
    public ICommand? OpenCommand { get => (ICommand?)GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }
}
