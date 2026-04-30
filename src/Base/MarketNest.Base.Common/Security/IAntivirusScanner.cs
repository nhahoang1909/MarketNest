namespace MarketNest.Base.Common;

/// <summary>Result of an antivirus scan on a file stream.</summary>
public record AntivirusScanResult(bool IsClean, string? ThreatName = null)
{
    public static AntivirusScanResult Clean => new(true);
    public static AntivirusScanResult Infected(string threatName) => new(false, threatName);
}

/// <summary>
///     Contract for antivirus file scanning. All file uploads (images, Excel) must pass through this.
///     Phase 1: <c>NoOpAntivirusScanner</c> (always clean — use only in non-production environments).
///     Phase 2/3: Replace with ClamAV or cloud AV integration via DI swap.
/// </summary>
public interface IAntivirusScanner
{
    /// <summary>
    ///     Scan the provided stream for malicious content.
    ///     The stream position is reset to 0 after scanning.
    /// </summary>
    Task<AntivirusScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}

