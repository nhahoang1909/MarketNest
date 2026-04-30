namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Tier 3 system configuration: upload and input validation limits.
///     Bound from <c>appsettings.json</c> section <c>Validation</c>.
/// </summary>
public record ValidationOptions
{
    public const string Section = "Validation";

    public int PasswordMinLength { get; init; } = 8;
    public int PasswordMaxLength { get; init; } = 128;
    public int UsernameMinLength { get; init; } = 3;
    public int UsernameMaxLength { get; init; } = 50;

    public string[] AllowedImageMimeTypes { get; init; } =
        ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Maximum image upload size in bytes. Default: 5 MB.</summary>
    public long MaxImageSizeBytes { get; init; } = 5_242_880;

    public int MaxProductImagesPerUpload { get; init; } = 5;

    public string[] AllowedDocumentMimeTypes { get; init; } =
        ["application/pdf", "image/jpeg", "image/png"];

    /// <summary>Maximum document upload size in bytes. Default: 10 MB.</summary>
    public long MaxDocumentSizeBytes { get; init; } = 10_485_760;
}

