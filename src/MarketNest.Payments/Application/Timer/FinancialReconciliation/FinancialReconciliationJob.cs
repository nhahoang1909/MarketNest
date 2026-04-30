
namespace MarketNest.Payments.Application;

/// <summary>
///     Nightly background job that reconciles financial data to detect integrity drift.
///
///     Checks performed (Phase 1 foundation — full logic requires Order + Payment aggregates):
///       1. <c>Order.BuyerTotal == Payment.ChargedAmount</c> for all completed orders.
///       2. <c>SellerNetPayout ≥ 0</c> for all payouts.
///       3. <c>Payment.Status = Captured</c> with no linked <c>Order</c> (orphaned payments).
///
///     Breach: any discrepancy is logged as <c>LogLevel.Critical</c> (P0 alert in Phase 2).
///
///     Phase 1 status: stub — full implementation deferred until <c>Order</c> and <c>Payment</c>
///     aggregates are complete. The job skeleton, registration, and logging are in place.
///
///     See <c>docs/sla-requirements.md §5</c> and <c>SlaConstants.Integrity</c>.
/// </summary>
public partial class FinancialReconciliationJob(
    IAppLogger<FinancialReconciliationJob> logger) : IBackgroundJob
{
    private const string OwningModule = "Payments";

    public JobDescriptor Descriptor { get; } = new(
        SlaConstants.Integrity.FinancialReconciliationJobKey,
        "Nightly financial reconciliation — BuyerTotal vs ChargedAmount",
        OwningModule,
        JobType.Timer,
        SlaConstants.Integrity.FinancialReconciliationJobSchedule,
        true,
        true,
        MaxRetryCount: 3,
        "Nightly at 02:00 UTC. Detects BuyerTotal/ChargedAmount drift, orphaned payments, and negative payouts. " +
        "Any mismatch is logged at Critical severity (P0 SLA breach). Idempotent — safe to retry.");

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        Log.InfoStart(logger, context.ExecutionId);

        // ── Phase 1 stub ──────────────────────────────────────────────────
        // Full reconciliation logic is deferred until Order and Payment aggregates are implemented.
        //
        // When Order + Payment domains are complete, implement:
        //
        //   1. Check BuyerTotal == Payment.ChargedAmount:
        //      var mismatches = await orderQuery
        //          .Where(o => o.Status == OrderStatus.Completed)
        //          .Join(paymentQuery, o => o.Id, p => p.OrderId,
        //              (o, p) => new { o.BuyerTotal, p.ChargedAmount, o.Id })
        //          .Where(x => Math.Abs(x.BuyerTotal - x.ChargedAmount) > SlaConstants.Integrity.MaxCommissionDriftAmount)
        //          .ToListAsync(cancellationToken);
        //
        //   2. Detect orphaned payments (Captured status, no linked order):
        //      var orphaned = await paymentQuery
        //          .Where(p => p.Status == PaymentStatus.Captured && p.OrderId == null)
        //          .ToListAsync(cancellationToken);
        //
        //   3. Detect negative payouts:
        //      var negativePayout = await payoutQuery
        //          .Where(p => p.NetAmount < 0)
        //          .ToListAsync(cancellationToken);
        //
        //   Report each violation via Log.CriticalMismatch / Log.CriticalOrphanedPayment /
        //   Log.CriticalNegativePayout (all LogLevel.Critical for P0 alerting in Phase 2).

        Log.InfoCompleted(logger, context.ExecutionId, mismatchCount: 0, orphanCount: 0);
        await Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.PaymentsReconciliationJobStart, LogLevel.Information,
            "FinancialReconciliationJob Start — ExecutionId={ExecutionId}")]
        public static partial void InfoStart(ILogger logger, Guid executionId);

        [LoggerMessage((int)LogEventId.PaymentsReconciliationJobMismatch, LogLevel.Critical,
            "SLA BREACH — BuyerTotal/ChargedAmount mismatch for OrderId={OrderId}: " +
            "BuyerTotal={BuyerTotal}, ChargedAmount={ChargedAmount}, Drift={Drift}")]
        public static partial void CriticalMismatch(
            ILogger logger, Guid orderId, decimal buyerTotal, decimal chargedAmount, decimal drift);

        [LoggerMessage((int)LogEventId.PaymentsReconciliationJobOrphan, LogLevel.Critical,
            "SLA BREACH — Orphaned payment detected: PaymentId={PaymentId} is Captured but has no linked Order")]
        public static partial void CriticalOrphanedPayment(ILogger logger, Guid paymentId);

        [LoggerMessage((int)LogEventId.PaymentsReconciliationJobNegativePayout, LogLevel.Critical,
            "SLA BREACH — Negative payout detected: PayoutId={PayoutId}, NetAmount={NetAmount}")]
        public static partial void CriticalNegativePayout(ILogger logger, Guid payoutId, decimal netAmount);

        [LoggerMessage((int)LogEventId.PaymentsReconciliationJobCompleted, LogLevel.Information,
            "FinancialReconciliationJob Completed — ExecutionId={ExecutionId}, " +
            "Mismatches={MismatchCount}, Orphans={OrphanCount}")]
        public static partial void InfoCompleted(
            ILogger logger, Guid executionId, int mismatchCount, int orphanCount);
    }
}

