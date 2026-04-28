namespace MarketNest.Admin.Infrastructure;

/// <summary>
/// Execution order constants for Admin module data seeders.
/// Lower values run first. Grouped by dependency: reference data (50–59), then dependent data (60+).
/// </summary>
public static class SeederOrder
{
    public const int Country = 50;
    public const int Nationality = 51;
    public const int PhoneCountryCode = 52;
    public const int Gender = 53;
    public const int ProductCategory = 54;
}

