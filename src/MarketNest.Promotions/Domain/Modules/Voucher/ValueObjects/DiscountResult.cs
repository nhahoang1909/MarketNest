namespace MarketNest.Promotions.Domain;

/// <summary>
///     Result envelope for voucher discount calculation.
///     Not a pure Value Object — acts as a discriminated result type.
/// </summary>
public record DiscountResult(
    bool IsValid,
    Money ProductDiscount,
    Money ShippingDiscount,
    string ErrorReason = "")
{
    public static DiscountResult Fail(string reason) =>
        new(false, new Money(0, DomainConstants.Currencies.Default), new Money(0, DomainConstants.Currencies.Default), reason);

    public static DiscountResult Ok(Money productDiscount, Money shippingDiscount) =>
        new(true, productDiscount, shippingDiscount);
}
