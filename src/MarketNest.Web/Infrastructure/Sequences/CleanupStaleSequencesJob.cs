using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;
using MarketNest.Orders.Application;
using MarketNest.Payments.Application;

namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Timer job: drops period sequences older than the retention window.
/// Runs on the 1st of each month at 02:00 UTC.
/// Safe to skip — old sequences are harmless, just waste catalog space.
/// </summary>
public sealed partial class CleanupStaleSequencesJob(
    ISequenceService sequenceService,
    IAppLogger<CleanupStaleSequencesJob> logger) : IBackgroundJob
{
    /// <summary>
    /// All registered descriptors that use period-scoped sequences.
    /// Add new descriptors here as modules are implemented.
    /// <c>SequenceResetPeriod.Never</c> descriptors are intentionally excluded — they never expire.
    /// </summary>
    private static readonly IReadOnlyList<SequenceDescriptor> AllDescriptors =
    [
        OrderSequences.OrderNumber,
        OrderSequences.InvoiceNumber,
        PaymentSequences.PayoutNumber,
    ];

    private const int MonthlyRetentionMonths = 3;
    private const int YearlyRetentionYears = 2;

    public JobDescriptor Descriptor { get; } = new(
        JobKey: "common.cleanup-stale-sequences",
        DisplayName: "Cleanup Stale Period Sequences",
        OwningModule: "Common",
        Type: JobType.Timer,
        Schedule: "0 2 1 * *",
        IsEnabled: true,
        IsRetryable: true,
        MaxRetryCount: 3,
        Description: "Drops PostgreSQL sequences from expired periods (monthly >3 months, yearly >2 years).");

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var droppedCount = 0;

        foreach (var descriptor in AllDescriptors)
        {
            var cutoff = GetCutoffPeriodKey(descriptor.ResetPeriod, now);
            var staleNames = await FindStaleSequencesAsync(descriptor, cutoff, cancellationToken);

            foreach (var seqName in staleNames)
            {
                Log.DroppingSequence(logger, seqName);
                await sequenceService.DropSequenceAsync(seqName, cancellationToken);
                droppedCount++;
            }
        }

        Log.CleanupComplete(logger, droppedCount);
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<string>> FindStaleSequencesAsync(
        SequenceDescriptor descriptor,
        string cutoffPeriodKey,
        CancellationToken ct)
    {
        var all = await sequenceService.ListSequenceNamesAsync(
            descriptor, periodKeyPrefix: string.Empty, ct);

        return all
            .Where(name =>
            {
                // Extract period key: "orders.seq_ord_202601" → "202601"
                var parts = name.Split('_');
                var periodKey = parts[^1];
                // String comparison is safe: period keys are zero-padded numerics
                return string.Compare(periodKey, cutoffPeriodKey, StringComparison.Ordinal) < 0;
            })
            .ToList();
    }

    private static string GetCutoffPeriodKey(SequenceResetPeriod period, DateTimeOffset now)
        => period switch
        {
            SequenceResetPeriod.Monthly => now.AddMonths(-MonthlyRetentionMonths).ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture),
            SequenceResetPeriod.Yearly => now.AddYears(-YearlyRetentionYears).ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unexpected period: {period}")
        };

    private static partial class Log
    {
        [LoggerMessage(160001, LogLevel.Information,
            "Dropping stale sequence {SequenceName}")]
        public static partial void DroppingSequence(IAppLogger<CleanupStaleSequencesJob> logger, string sequenceName);

        [LoggerMessage(160002, LogLevel.Information,
            "Sequence cleanup complete. Dropped {Count} stale sequences.")]
        public static partial void CleanupComplete(IAppLogger<CleanupStaleSequencesJob> logger, int count);
    }
}

