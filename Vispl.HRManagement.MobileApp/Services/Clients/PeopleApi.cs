using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

/// <summary>The employee directory, and who among them may carry the app.</summary>
public sealed class PeopleApi(HrApiClient client)
{
    private const string BasePath = "api/mobile/v1/employees";

    public Task<ApiResult<CursorPage<EmployeeListItemDto>>> GetAsync(
        string? query = null,
        string? status = null,
        string? cursor = null,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string> { $"pageSize={pageSize}" };
        Append(parameters, "query", query);
        Append(parameters, "status", status);
        Append(parameters, "cursor", cursor);

        return client.GetAsync<CursorPage<EmployeeListItemDto>>(
            $"{BasePath}?{string.Join('&', parameters)}", cancellationToken: cancellationToken);
    }

    public Task<ApiResult<EmployeeOverviewDto>> GetByIdAsync(
        string employeeId, CancellationToken cancellationToken = default) =>
        client.GetAsync<EmployeeOverviewDto>(
            $"{BasePath}/{employeeId}", cancellationToken: cancellationToken);

    public Task<ApiResult<IReadOnlyList<EmployeeMobileAccessDto>>> GetMobileAccessAsync(
        string? state = null, CancellationToken cancellationToken = default) =>
        client.GetAsync<IReadOnlyList<EmployeeMobileAccessDto>>(
            string.IsNullOrWhiteSpace(state)
                ? $"{BasePath}/mobile-access"
                : $"{BasePath}/mobile-access?state={Uri.EscapeDataString(state)}",
            cancellationToken: cancellationToken);

    /// <summary>
    /// Approves app access. The temporary password comes back once and is never recoverable,
    /// so the screen has to show it immediately and the app must not store it.
    /// </summary>
    public Task<ApiResult<GrantMobileAccessResultDto>> GrantMobileAccessAsync(
        string employeeId, CancellationToken cancellationToken = default) =>
        client.PostAsync<GrantMobileAccessResultDto>(
            $"{BasePath}/mobile-access/{employeeId}/grant",
            new GrantMobileAccessRequest(),
            cancellationToken);

    public Task<ApiResult<EmployeeMobileAccessDto>> SetMobileAccessAsync(
        string employeeId, bool isActive, CancellationToken cancellationToken = default) =>
        client.PostAsync<EmployeeMobileAccessDto>(
            $"{BasePath}/mobile-access/{employeeId}/{(isActive ? "restore" : "suspend")}",
            null,
            cancellationToken);

    private static void Append(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}
