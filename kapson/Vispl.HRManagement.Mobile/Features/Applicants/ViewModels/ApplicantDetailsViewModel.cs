using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Services;
using Vispl.HRManagement.Mobile.Shared.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Applicants.ViewModels;

public sealed class ApplicantDetailsViewModel(IHrDataService data) : ScreenViewModel
{
    private long applicantId;
    private ApplicantSummary? applicant;

    public ApplicantSummary? Applicant
    {
        get => applicant;
        private set => SetProperty(ref applicant, value);
    }

    /// <summary>
    /// Records which applicant to show. The read happens in <see cref="LoadAsync"/>, because it
    /// is now a network call and a navigation must not block on one.
    /// </summary>
    public void For(long id) => applicantId = id;

    public override Task LoadAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async () => Applicant = await data.GetApplicantAsync(applicantId, cancellationToken));
}
