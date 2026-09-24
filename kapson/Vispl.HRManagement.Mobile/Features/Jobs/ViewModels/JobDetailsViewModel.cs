using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Services;
using Vispl.HRManagement.Mobile.Shared.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Jobs.ViewModels;

public sealed class JobDetailsViewModel(IHrDataService data) : ScreenViewModel
{
    private long jobId;
    private JobSummary? job;

    public JobSummary? Job
    {
        get => job;
        private set => SetProperty(ref job, value);
    }

    /// <summary>
    /// Records which job to show. The read itself happens in <see cref="LoadAsync"/>, because
    /// it is now a network call and a navigation must not block on one.
    /// </summary>
    public void For(long id) => jobId = id;

    public override Task LoadAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async () => Job = await data.GetJobAsync(jobId, cancellationToken));
}
