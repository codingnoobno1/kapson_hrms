using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface IEmployeeWorkspaceService
{
    Task<IReadOnlyList<EmployeeExplorerRowDto>> GetExplorerAsync(
        MobileCallerContext caller, EmployeeExplorerQuery query, CancellationToken cancellationToken = default);

    Task<EmployeeWorkspaceDto> GetNewTemplateAsync(
        MobileCallerContext caller, CancellationToken cancellationToken = default);

    Task<EmployeeWorkspaceDto?> GetWorkspaceAsync(
        MobileCallerContext caller, string employeeId, CancellationToken cancellationToken = default);

    Task<string> SaveWorkspaceAsync(
        MobileCallerContext caller,
        EmployeeWorkspaceDto workspace,
        bool asDraft,
        CancellationToken cancellationToken = default);

    Task<UploadedEmployeeMediaDto> UploadMediaAsync(
        MobileCallerContext caller,
        string originalFileName,
        string contentType,
        long length,
        Stream content,
        bool imageOnly,
        CancellationToken cancellationToken = default);
}
