using System.Reflection;

namespace MarketNest.Promotions;

/// <summary>Marker class for assembly scanning (MediatR, FluentValidation, DatabaseInitializer).</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
