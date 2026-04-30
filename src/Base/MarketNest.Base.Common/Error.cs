namespace MarketNest.Base.Common;

#pragma warning disable CA1716 // 'Error' is an intentional DDD type name
/// <summary>
///     Structured error record with domain error code, message, and type.
///     Error codes use DOMAIN.ENTITY_ERROR format.
/// </summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Validation)
{
    public static Error NotFound(string entity, string id)
        => new($"{entity.ToUpperInvariant()}.{DomainConstants.ErrorCodes.NotFoundSuffix}", $"{entity} '{id}' not found",
            ErrorType.NotFound);

    public static Error Unauthorized(string? detail = null)
        => new(DomainConstants.ErrorCodes.Unauthorized, detail ?? DomainConstants.ErrorMessages.AuthenticationRequired,
            ErrorType.Unauthorized);

    public static Error Forbidden(string? detail = null)
        => new(DomainConstants.ErrorCodes.Forbidden, detail ?? DomainConstants.ErrorMessages.InsufficientPermissions,
            ErrorType.Forbidden);

    public static Error Conflict(string code, string message)
        => new(code, message, ErrorType.Conflict);

    /// <summary>
    ///     Creates a concurrency conflict error for a single entity update.
    ///     Indicates the entity was modified by another user between read and write.
    /// </summary>
    public static Error ConcurrencyConflict(string entity, string id)
        => new(DomainConstants.ErrorCodes.ConcurrencyConflict,
            $"{entity} '{id}' has been modified by another user. Please reload and try again.",
            ErrorType.Conflict);

    /// <summary>
    ///     Creates a concurrency conflict error for bulk operations where some records are stale.
    ///     The <paramref name="staleIds"/> identifies which items failed the concurrency check.
    /// </summary>
    public static Error BulkConcurrencyConflict(string entity, IEnumerable<string> staleIds)
        => new(DomainConstants.ErrorCodes.ConcurrencyConflict,
            $"{entity} records [{string.Join(", ", staleIds)}] have been modified by another user. Please reload and try again.",
            ErrorType.Conflict);

    public static Error Unexpected(string? detail = null)
        => new(DomainConstants.ErrorCodes.UnexpectedError, detail ?? DomainConstants.ErrorMessages.UnexpectedError,
            ErrorType.Unexpected);
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
