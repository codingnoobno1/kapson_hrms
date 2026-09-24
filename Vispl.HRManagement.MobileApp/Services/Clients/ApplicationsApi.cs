using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

public sealed class ApplicationsApi(HrApiClient client)
{
    private const string BasePath = "api/mobile/v1/recruitment/applications";

    public Task<ApiResult<CursorPage<ApplicationListItemDto>>> GetAsync(
        string? stage = null,
        string? query = null,
        string? cursor = null,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string> { $"pageSize={pageSize}" };
        Append(parameters, "stage", stage);
        Append(parameters, "query", query);
        Append(parameters, "cursor", cursor);

        return client.GetAsync<CursorPage<ApplicationListItemDto>>(
            $"{BasePath}?{string.Join('&', parameters)}", cancellationToken: cancellationToken);
    }

    public Task<ApiResult<ApplicationDetailDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        client.GetAsync<ApplicationDetailDto>($"{BasePath}/{id}", cancellationToken: cancellationToken);

    public Task<ApiResult<MutationResultDto>> ChangeStageAsync(
        long id, string toStage, string? reason, CancellationToken cancellationToken = default) =>
        client.PostAsync<MutationResultDto>(
            $"{BasePath}/{id}/stage",
            new ChangeStageRequest { ToStage = toStage, Reason = reason },
            cancellationToken);

    public Task<ApiResult<OnboardingDto>> GetOnboardingAsync(
        long onboardingId, CancellationToken cancellationToken = default) =>
        client.GetAsync<OnboardingDto>(
            $"api/mobile/v1/onboarding/{onboardingId}", cancellationToken: cancellationToken);

    public Task<ApiResult<OnboardingDto>> GetOnboardingByApplicationAsync(
        long applicationId, CancellationToken cancellationToken = default) =>
        client.GetAsync<OnboardingDto>(
            $"api/mobile/v1/onboarding/by-application/{applicationId}", cancellationToken: cancellationToken);

    public Task<ApiResult<MutationResultDto>> DecideTaskAsync(
        long onboardingId, long taskId, string decision, string? reason, CancellationToken cancellationToken = default) =>
        client.PostAsync<MutationResultDto>(
            $"api/mobile/v1/onboarding/{onboardingId}/tasks/{taskId}/decision",
            new OnboardingTaskDecisionRequest { Decision = decision, Reason = reason },
            cancellationToken);

    private static void Append(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}
