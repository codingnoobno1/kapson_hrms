using System.Windows.Input;
using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Services;
using Vispl.HRManagement.Mobile.Shared.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Home.ViewModels;

public sealed class DashboardViewModel : ScreenViewModel
{
    private readonly IHrDataService data;
    private readonly IAppNavigator navigator;

    private IReadOnlyList<KpiMetric> metrics = [];
    private IReadOnlyList<InterviewSummary> interviews = [];
    private IReadOnlyList<JobSummary> spotlightJobs = [];

    public DashboardViewModel(IHrDataService data, IAppNavigator navigator)
    {
        this.data = data;
        this.navigator = navigator;
        OpenJobCommand = new Command<JobSummary>(async job => await OpenJobAsync(job));
    }

    public IReadOnlyList<KpiMetric> Metrics
    {
        get => metrics;
        private set => SetProperty(ref metrics, value);
    }

    public IReadOnlyList<InterviewSummary> Interviews
    {
        get => interviews;
        private set => SetProperty(ref interviews, value);
    }

    public IReadOnlyList<JobSummary> SpotlightJobs
    {
        get => spotlightJobs;
        private set => SetProperty(ref spotlightJobs, value);
    }

    public ICommand OpenJobCommand { get; }

    public override Task LoadAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        Metrics = await data.GetMetricsAsync(cancellationToken);
        Interviews = (await data.GetInterviewsAsync(cancellationToken)).Take(2).ToList();

        SpotlightJobs = (await data.GetJobsAsync(cancellationToken))
            .Where(job => job.Status is "Active" or "Draft")
            .Take(3)
            .ToList();
    });

    private Task OpenJobAsync(JobSummary? job) => job is null ? Task.CompletedTask : navigator.OpenJobAsync(job.Id);
}
