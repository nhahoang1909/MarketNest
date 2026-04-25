namespace MarketNest.Auditing.Domain;

/// <summary>
/// Standard audit event type suffixes. Combined with entity name to form full event type.
/// Example: "ORDER_CREATED", "PRODUCT_UPDATED", "USER_DELETED".
/// </summary>
public static class AuditEventType
{
    public const string CreatedSuffix = "_CREATED";
    public const string UpdatedSuffix = "_UPDATED";
    public const string DeletedSuffix = "_DELETED";

    // Login events
    public const string LoginSuccess = "LOGIN_SUCCESS";
    public const string LoginFailed = "LOGIN_FAILED";
}

