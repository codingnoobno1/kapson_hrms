using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

/// <summary>
/// Calls a catalogued endpoint that has no client model yet, so the shell can report the exact
/// server-side readiness state rather than guessing.
/// </summary>
public sealed class FeatureApi(HrApiClient client)
{
    public Task<ApiResult<MobileFeatureStatusDto>> ProbeAsync(
        ApiModule module,
        CancellationToken cancellationToken = default) =>
        client.GetAsync<MobileFeatureStatusDto>(module.Path, cancellationToken: cancellationToken);
}
