namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Phase 1 no-op antivirus scanner — always reports file as clean.
///     Replace with a ClamAV or cloud AV binding in Phase 2/3 via DI swap.
///     <para>
///         ⚠️  Do NOT use in public-facing production environments without a real AV implementation.
///         Phase 2 will add ClamAV via the <c>nClam</c> or clamd socket integration.
///     </para>
/// </summary>
internal sealed class NoOpAntivirusScanner : IAntivirusScanner
{
    public Task<AntivirusScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        // Phase 1: trust the file but ensure the stream position is reset for subsequent reads.
        if (fileStream.CanSeek)
            fileStream.Position = 0;
        return Task.FromResult(AntivirusScanResult.Clean);
    }
}

