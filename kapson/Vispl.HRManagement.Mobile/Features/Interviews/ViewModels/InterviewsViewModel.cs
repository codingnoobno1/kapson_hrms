using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Services;
using Vispl.HRManagement.Mobile.Shared.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Interviews.ViewModels;

public sealed class InterviewsViewModel(IHrDataService data) : ScreenViewModel
{
    private IReadOnlyList<InterviewSummary> interviews = [];

    public IReadOnlyList<InterviewSummary> Interviews
    {
        get => interviews;
        private set => SetProperty(ref interviews, value);
    }

    public override Task LoadAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async () => Interviews = await data.GetInterviewsAsync(cancellationToken));
}
