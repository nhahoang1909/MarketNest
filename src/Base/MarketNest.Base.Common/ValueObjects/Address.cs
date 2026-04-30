namespace MarketNest.Base.Common;

/// <summary>
///     Address value object for shipping/billing. Defined in Base.Common for cross-module contracts.
/// </summary>
public sealed record Address
{
    public Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public string Street { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string PostalCode { get; init; }
    public string Country { get; init; }
}
