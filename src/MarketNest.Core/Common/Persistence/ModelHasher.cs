using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MarketNest.Core.Common.Persistence;
/// <summary>
/// Computes a deterministic SHA-256 hash of an EF Core model snapshot.
/// Used by <c>DatabaseInitializer</c> to detect schema changes between startups.
/// </summary>
public static class ModelHasher
{
    /// <summary>
    /// Produces a hex-encoded SHA-256 hash of the model's debug string representation.
    /// This captures all entity types, properties, indexes, relationships, and constraints.
    /// </summary>
    public static string ComputeHash(IModel model)
    {
        var modelSnapshot = model.ToDebugString(MetadataDebugStringOptions.LongDefault);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(modelSnapshot));
        return Convert.ToHexStringLower(bytes);
    }
}
