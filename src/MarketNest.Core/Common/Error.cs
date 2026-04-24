namespace MarketNest.Core.Common;

/// <summary>
///     Structured error record with domain error code, message, and type.
///     Error codes use DOMAIN.ENTITY_ERROR format.
/// </summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Validation)
{
    public static Error NotFound(string entity, string id)
        => new($"{entity.ToUpperInvariant()}.NOT_FOUND", $"{entity} '{id}' not found", ErrorType.NotFound);

    public static Error Unauthorized(string? detail = null)
        => new("UNAUTHORIZED", detail ?? "Authentication required", ErrorType.Unauthorized);

    public static Error Forbidden(string? detail = null)
        => new("FORBIDDEN", detail ?? "Insufficient permissions", ErrorType.Forbidden);

    public static Error Conflict(string code, string message)
        => new(code, message, ErrorType.Conflict);

    public static Error Unexpected(string? detail = null)
        => new("UNEXPECTED_ERROR", detail ?? "An unexpected error occurred", ErrorType.Unexpected);
}

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unexpected
}
