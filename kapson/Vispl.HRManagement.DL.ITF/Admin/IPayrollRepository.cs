using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IPayrollRepository
{
    Task<PayrollRunRecord?> GetRunAsync(
        string tenantKey, int year, int month, string salaryType, CancellationToken cancellationToken = default);

    /// <summary>Returns the existing run for the month, creating it first if there is none.</summary>
    Task<PayrollRunRecord> GetOrCreateRunAsync(PayrollRunRecord run, CancellationToken cancellationToken = default);

    Task ApproveRunAsync(
        string tenantKey, long runId, string? approvedBy, DateTimeOffset approvedUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollLineRecord>> GetLinesAsync(
        string tenantKey, long runId, CancellationToken cancellationToken = default);

    /// <summary>Inserts or replaces lines, one per employee per run.</summary>
    Task UpsertLinesAsync(IReadOnlyList<PayrollLineRecord> lines, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a payment against lines that are still <c>Generated</c>, and, for a bank transfer,
    /// the batch in the same save. Returns the lines actually changed.
    /// </summary>
    Task<IReadOnlyList<PayrollLineRecord>> RecordPaymentAsync(
        string tenantKey,
        long runId,
        IReadOnlyCollection<long> employeeIds,
        string newStatus,
        DateOnly paidOn,
        string paymentMode,
        string documentType,
        BankTransferBatchRecord? batch,
        string? updatedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
