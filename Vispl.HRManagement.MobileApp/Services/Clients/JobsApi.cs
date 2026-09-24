using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

public sealed class JobsApi(HrApiClient client)
{
    public Task<ApiResult<CursorPage<JobListItemDto>>> GetAsync(
        string? query = null,
        string? department = null,
        string? location = null,
        string? status = null,
        string? cursor = null,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string> { $"pageSize={pageSize}" };
        Append(parameters, "query", query);
        Append(parameters, "department", department);
        Append(parameters, "location", location);
        Append(parameters, "status", status);
        Append(parameters, "cursor", cursor);

        return client.GetAsync<CursorPage<JobListItemDto>>(
            $"{ApiCatalog.JobsPath}?{string.Join('&', parameters)}",
            cancellationToken: cancellationToken);
    }

    public Task<ApiResult<JobDetailDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        client.GetAsync<JobDetailDto>($"{ApiCatalog.JobsPath}/{id}", cancellationToken: cancellationToken);

    private static void Append(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}
