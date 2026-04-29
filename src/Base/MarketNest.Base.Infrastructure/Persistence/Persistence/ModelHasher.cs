using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Computes a deterministic SHA-512 hash of an EF Core model snapshot.
///     Used by <c>DatabaseInitializer</c> to detect schema changes between startups.
///     SHA-512 produces 128 hex characters and is stored in a VARCHAR(128) column.
/// </summary>
public static class ModelHasher
{
    /// <summary>
    ///     Produces a hex-encoded SHA-512 hash of the model's debug string representation.
    ///     This captures all entity types, properties, indexes, relationships, and constraints.
    ///     Returns 128 hex characters (512 bits / 4 bits per character).
    /// </summary>
    public static string ComputeHash(IModel model)
    {
        string modelSnapshot = model.ToDebugString(MetadataDebugStringOptions.LongDefault);
        byte[] bytes = SHA512.HashData(Encoding.UTF8.GetBytes(modelSnapshot));
        return Convert.ToHexStringLower(bytes);
    }
}
