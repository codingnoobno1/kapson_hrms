using System.Windows.Input;
using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Services;
using Vispl.HRManagement.Mobile.Shared.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Applicants.ViewModels;

public sealed class ApplicantsViewModel : ScreenViewModel
{
    private readonly IHrDataService data;
    private readonly IAppNavigator navigator;
    private IReadOnlyList<ApplicantSummary> allApplicants = [];
    private string query = string.Empty;
    private string selectedStatus = "All";

    public ApplicantsViewModel(IHrDataService data, IAppNavigator navigator)
    {
        this.data = data;
        this.navigator = navigator;
        OpenApplicantCommand = new Command<ApplicantSummary>(async applicant => await OpenApplicantAsync(applicant));
        SelectStatusCommand = new Command<string>(status => SelectedStatus = status ?? "All");
    }

    public string Query
    {
        get => query;
        set { if (SetProperty(ref query, value)) OnPropertyChanged(nameof(FilteredApplicants)); }
    }

    public string SelectedStatus
    {
        get => selectedStatus;
        set { if (SetProperty(ref selectedStatus, value)) OnPropertyChanged(nameof(FilteredApplicants)); }
    }

    public IReadOnlyList<string> Statuses { get; } = ["All", "Shortlisted", "Pending Review", "Interview", "Rejected"];

    public IEnumerable<ApplicantSummary> FilteredApplicants => allApplicants.Where(applicant =>
        (SelectedStatus == "All" || applicant.Status == SelectedStatus) &&
        (string.IsNullOrWhiteSpace(Query) || applicant.Name.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
         applicant.JobTitle.Contains(Query, StringComparison.OrdinalIgnoreCase)));

    public ICommand OpenApplicantCommand { get; }
    public ICommand SelectStatusCommand { get; }

    public override Task LoadAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        allApplicants = await data.GetApplicantsAsync(cancellationToken);
        OnPropertyChanged(nameof(FilteredApplicants));
    });

    private Task OpenApplicantAsync(ApplicantSummary? applicant) =>
        applicant is null ? Task.CompletedTask : navigator.OpenApplicantAsync(applicant.Id);
}
