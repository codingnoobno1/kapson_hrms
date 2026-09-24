using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Mobile.Features.Applicants.Pages;
using Vispl.HRManagement.Mobile.Features.Authentication.Pages;
using Vispl.HRManagement.Mobile.Features.Jobs.Pages;

namespace Vispl.HRManagement.Mobile.Shared.Services;

public sealed class AppNavigator(IServiceProvider services) : IAppNavigator
{
    private Window? window;

    public void Attach(Window targetWindow) => window = targetWindow;

    public Task ShowWorkspaceAsync()
    {
        EnsureWindow().Page = services.GetRequiredService<AppShell>();
        return Task.CompletedTask;
    }

    public Task ShowLoginAsync()
    {
        EnsureWindow().Page = services.GetRequiredService<LoginPage>();
        return Task.CompletedTask;
    }

    public async Task OpenJobAsync(long jobId)
    {
        var page = services.GetRequiredService<JobDetailsPage>();
        page.Load(jobId);
        await EnsureShell().Navigation.PushAsync(page);
    }

    public async Task OpenApplicantAsync(long applicantId)
    {
        var page = services.GetRequiredService<ApplicantDetailsPage>();
        page.Load(applicantId);
        await EnsureShell().Navigation.PushAsync(page);
    }

    private Window EnsureWindow() => window ?? throw new InvalidOperationException("The application window is not attached.");
    private Shell EnsureShell() => EnsureWindow().Page as Shell ?? throw new InvalidOperationException("The workspace shell is not active.");
}
