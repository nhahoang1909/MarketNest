using System.Reflection;

namespace MarketNest.Admin;

/// <summary>Marker class for assembly scanning (MediatR, FluentValidation).</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
