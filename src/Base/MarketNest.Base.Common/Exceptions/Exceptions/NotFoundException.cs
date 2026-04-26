namespace MarketNest.Base.Common;

/// <summary>
///     Thrown when an entity is not found. Infrastructure-level exception.
/// </summary>
public class NotFoundException(string entityName, string id)
    : Exception($"{entityName} with id '{id}' was not found.")
{
    public string EntityName { get; } = entityName;
    public string EntityId { get; } = id;
}
