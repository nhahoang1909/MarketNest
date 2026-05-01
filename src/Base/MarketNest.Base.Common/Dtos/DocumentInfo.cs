namespace MarketNest.Base.Common;

/// <summary>
///     Value object representing a stored document/file reference.
///     Used across modules for images, attachments, and uploaded files.
/// </summary>
public sealed record DocumentInfo
{
    public DocumentInfo(Guid id, string fileName, string fileType, long fileSizeBytes, string url)
    {
        if (id == Guid.Empty) throw new ArgumentException("Document id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(fileType)) throw new ArgumentException("File type is required.", nameof(fileType));
        if (fileSizeBytes < 0) throw new ArgumentException("File size cannot be negative.", nameof(fileSizeBytes));
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL is required.", nameof(url));

        Id = id;
        FileName = fileName;
        FileType = fileType;
        FileSizeBytes = fileSizeBytes;
        Url = url;
    }

    /// <summary>Unique identifier for the document.</summary>
    public Guid Id { get; init; }

    /// <summary>Original file name (e.g., "invoice-2024.pdf").</summary>
    public string FileName { get; init; }

    /// <summary>MIME type or file extension (e.g., "application/pdf", "image/png").</summary>
    public string FileType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>Accessible URL (relative or absolute) for downloading/viewing.</summary>
    public string Url { get; init; }

    /// <summary>Optional descriptive title (differs from file name when user provides a label).</summary>
    public string? Title { get; init; }

    /// <summary>Upload timestamp (UTC).</summary>
    public DateTimeOffset? UploadedAt { get; init; }

    /// <summary>Friendly file size string (e.g., "1.2 MB").</summary>
    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };
}

