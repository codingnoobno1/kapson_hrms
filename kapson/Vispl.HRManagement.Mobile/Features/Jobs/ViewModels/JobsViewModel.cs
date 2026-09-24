using System.Windows.Input;
using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Services;
using Vispl.HRManagement.Mobile.Shared.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Jobs.ViewModels;

public sealed class JobsViewModel : ScreenViewModel
{
    private readonly IHrDataService data;
    private readonly IAppNavigator navigator;
    private IReadOnlyList<JobSummary> allJobs = [];
    private string query = string.Empty;
    private string selectedStatus = "All";

    public JobsViewModel(IHrDataService data, IAppNavigator navigator)
    {
        this.data = data;
        this.navigator = navigator;
        OpenJobCommand = new Command<JobSummary>(async job => await OpenJobAsync(job));
        SelectStatusCommand = new Command<string>(status => SelectedStatus = status ?? "All");
    }

    public string Query
    {
        get => query;
        set { if (SetProperty(ref query, value)) OnPropertyChanged(nameof(FilteredJobs)); }
    }

    public string SelectedStatus
    {
        get => selectedStatus;
        set { if (SetProperty(ref selectedStatus, value)) OnPropertyChanged(nameof(FilteredJobs)); }
    }

    public IReadOnlyList<string> Statuses { get; } = ["All", "Active", "Draft", "Inactive", "Expired"];

    // Filtering stays client-side over the loaded page, as it was. Pushing it to the server is
    // a separate change: the endpoint takes a query, but paging would have to come with it.
    public IEnumerable<JobSummary> FilteredJobs => allJobs.Where(job =>
        (SelectedStatus == "All" || job.Status == SelectedStatus) &&
        (string.IsNullOrWhiteSpace(Query) || job.Title.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
         job.Department.Contains(Query, StringComparison.OrdinalIgnoreCase)));

    public ICommand OpenJobCommand { get; }
    public ICommand SelectStatusCommand { get; }

    public override Task LoadAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        allJobs = await data.GetJobsAsync(cancellationToken);
        OnPropertyChanged(nameof(FilteredJobs));
    });

    private Task OpenJobAsync(JobSummary? job) => job is null ? Task.CompletedTask : navigator.OpenJobAsync(job.Id);
}
