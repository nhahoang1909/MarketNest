namespace MarketNest.Promotions.Domain;

public record DiscountResult(
    bool IsValid,
    Money ProductDiscount,
    Money ShippingDiscount,
    string? ErrorReason = null)
{
    public static DiscountResult Fail(string reason) =>
        new(false, new Money(0, DomainConstants.Currencies.Default), new Money(0, DomainConstants.Currencies.Default), reason);

    public static DiscountResult Ok(Money productDiscount, Money shippingDiscount) =>
        new(true, productDiscount, shippingDiscount);
}
