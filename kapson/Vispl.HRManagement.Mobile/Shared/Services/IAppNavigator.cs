namespace Vispl.HRManagement.Mobile.Shared.Services;

public interface IAppNavigator
{
    void Attach(Window window);
    Task ShowWorkspaceAsync();
    Task ShowLoginAsync();
    Task OpenJobAsync(long jobId);
    Task OpenApplicantAsync(long applicantId);
}
