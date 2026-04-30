namespace MarketNest.Base.Common;

/// <summary>
///     Platform-wide SLA threshold constants.
///     No magic numbers — every value here is referenced by <c>PerformanceBehavior</c>,
///     reconciliation jobs, and (Phase 2) the admin SLA dashboard.
///
///     Phase 1: typed C# constants (no DB backing).
///     Phase 2: migrated to <c>AdminConfig</c> backing so thresholds are admin-configurable.
///     See <c>docs/sla-requirements.md §9</c> for the phased plan.
/// </summary>
public static class SlaConstants
{
    // ── Availability ─────────────────────────────────────────────────────
    /// <summary>Target monthly uptime percentages per service tier (Phase 1).</summary>
    public static class Availability
    {
        /// <summary>Overall system uptime target (Phase 1): 95%.</summary>
        public const double OverallUptimePercent = 95.0;

        /// <summary>Checkout flow uptime target (Phase 1): 97%. Revenue-critical path.</summary>
        public const double CheckoutUptimePercent = 97.0;

        /// <summary>Payment capture uptime target (Phase 1): 97%. Revenue-critical path.</summary>
        public const double PaymentUptimePercent = 97.0;

        /// <summary>Seller dashboard uptime target (Phase 1): 93%.</summary>
        public const double SellerDashboardUptimePercent = 93.0;

        /// <summary>Admin panel uptime target (Phase 1): 90%.</summary>
        public const double AdminPanelUptimePercent = 90.0;
    }

    // ── Performance (P95 Latency in milliseconds) ─────────────────────────
    /// <summary>
    ///     P95 latency thresholds per endpoint category (Phase 1).
    ///     Used by <c>PerformanceBehavior</c> to emit slow-request warnings.
    /// </summary>
    public static class Performance
    {
        /// <summary>General slow-request warning threshold: 1000 ms.</summary>
        public const int SlowRequestMs = 1000;

        /// <summary>Critical slow-request threshold (P0 alert boundary): 3000 ms.</summary>
        public const int CriticalRequestMs = 3000;

        // ── Per-category P95 targets ───────────────────────────────────
        public const int StaticPageMs = 800;
        public const int ProductListingMs = 1200;
        public const int ProductDetailMs = 800;
        public const int CartOperationMs = 500;
        public const int CheckoutPageMs = 1500;

        /// <summary>Order placement P95 target (Phase 1): 3000 ms. Multi-step operation.</summary>
        public const int OrderPlacementMs = 3000;

        /// <summary>
        ///     Payment capture P95 target (Phase 1): 5000 ms.
        ///     Includes third-party gateway round-trip outside platform control.
        /// </summary>
        public const int PaymentCaptureMs = 5000;

        public const int AdminQueryMs = 3000;
        public const int SellerDashboardMs = 2000;
    }

    // ── Business Correctness ─────────────────────────────────────────────
    public static class Business
    {
        /// <summary>Order confirmation latency target (P99): 2 minutes in seconds.</summary>
        public const int OrderConfirmationMaxSeconds = 120;

        /// <summary>Minimum payment capture success rate: 95%.</summary>
        public const double PaymentCaptureSuccessRatePercent = 95.0;

        /// <summary>Seller response window for disputes: 72 hours in seconds.</summary>
        public const int DisputeSellerResponseWindowSeconds = 72 * 60 * 60;

        /// <summary>Admin dispute arbitration deadline: 5 business days in hours.</summary>
        public const int DisputeAdminArbitrationHours = 5 * 8; // 5 business days × 8 hours

        /// <summary>Payout disbursement max latency after order completion: 48 hours.</summary>
        public const int PayoutMaxDisbursementHours = 48;

        /// <summary>Payout schedule tolerance: ± 30 minutes.</summary>
        public const int PayoutScheduleToleranceMinutes = 30;

        /// <summary>Critical notification delivery P95 target: 5 minutes in seconds.</summary>
        public const int CriticalNotificationMaxSeconds = 5 * 60;

        /// <summary>Minimum notification delivery rate: 98%.</summary>
        public const double NotificationDeliveryRatePercent = 98.0;
    }

    // ── Data Integrity ────────────────────────────────────────────────────
    public static class Integrity
    {
        /// <summary>
        ///     Maximum acceptable commission calculation drift per order: $0.01.
        ///     Any difference larger than this triggers a P0 alert.
        /// </summary>
        public const decimal MaxCommissionDriftAmount = 0.01m;

        /// <summary>Job key for the nightly financial reconciliation job.</summary>
        public const string FinancialReconciliationJobKey = "payments.financial-reconciliation";

        /// <summary>Cron schedule for the financial reconciliation job: nightly at 02:00 UTC.</summary>
        public const string FinancialReconciliationJobSchedule = "0 2 * * *";
    }

    // ── Throughput ────────────────────────────────────────────────────────
    public static class Throughput
    {
        /// <summary>Sustained read (browse/search) requests per second target (Phase 1).</summary>
        public const int ReadRequestsPerSecond = 50;

        /// <summary>Sustained checkout flow requests per second target (Phase 1).</summary>
        public const int CheckoutRequestsPerSecond = 10;

        /// <summary>
        ///     Burst multiplier for flash-sale scenarios.
        ///     System must sustain <c>CheckoutRequestsPerSecond × BurstMultiplier</c> for up to
        ///     <see cref="BurstDurationSeconds" /> seconds.
        /// </summary>
        public const int BurstMultiplier = 3;

        /// <summary>Maximum burst duration in seconds before circuit-breaker may activate.</summary>
        public const int BurstDurationSeconds = 60;
    }
}

