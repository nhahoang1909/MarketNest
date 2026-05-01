namespace MarketNest.Admin.Domain;

/// <summary>
///     Determines the visual styling and urgency level of an announcement.
/// </summary>
public enum AnnouncementType
{
    /// <summary>General informational announcement.</summary>
    Info = 0,

    /// <summary>Promotional announcement (sales, vouchers, Black Friday).</summary>
    Promotion = 1,

    /// <summary>Warning announcement (maintenance, policy change).</summary>
    Warning = 2,

    /// <summary>Urgent system-wide announcement.</summary>
    Urgent = 3
}

