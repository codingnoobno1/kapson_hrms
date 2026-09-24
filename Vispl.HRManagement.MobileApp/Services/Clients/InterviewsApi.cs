using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

public sealed class InterviewsApi(HrApiClient client)
{
    private const string BasePath = "api/mobile/v1/recruitment/interviews";

    public Task<ApiResult<CursorPage<InterviewListItemDto>>> GetAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        string? status = null,
        bool assignedToMeOnly = false,
        string? query = null,
        string? cursor = null,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string> { $"pageSize={pageSize}" };

        if (from.HasValue)
        {
            parameters.Add($"from={from.Value:yyyy-MM-dd}");
        }

        if (to.HasValue)
        {
            parameters.Add($"to={to.Value:yyyy-MM-dd}");
        }

        if (assignedToMeOnly)
        {
            parameters.Add("assignedToMeOnly=true");
        }

        Append(parameters, "status", status);
        Append(parameters, "query", query);
        Append(parameters, "cursor", cursor);

        return client.GetAsync<CursorPage<InterviewListItemDto>>(
            $"{BasePath}?{string.Join('&', parameters)}", cancellationToken: cancellationToken);
    }

    public Task<ApiResult<InterviewDetailDto>> GetByIdAsync(
        string interviewId, CancellationToken cancellationToken = default) =>
        client.GetAsync<InterviewDetailDto>($"{BasePath}/{interviewId}", cancellationToken: cancellationToken);

    public Task<ApiResult<MutationResultDto>> CancelAsync(
        string interviewId, string reasonCode, string? comment, CancellationToken cancellationToken = default) =>
        client.PostAsync<MutationResultDto>(
            $"{BasePath}/{interviewId}/cancel",
            new CancelInterviewRequest { ReasonCode = reasonCode, Comment = comment },
            cancellationToken);

    private static void Append(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}
