using MarketNest.Base.Common;

namespace MarketNest.Orders.Application;

/// <summary>
/// Sequence descriptors for the Orders module.
/// All sequences use the "orders" PostgreSQL schema.
/// </summary>
public static class OrderSequences
{
    /// <summary>
    /// Monthly-reset order number.
    /// Output: ORD202604-00001 … ORD202604-99999
    /// Capacity: 99,999 orders/month (~3,200/day)
    /// </summary>
    public static readonly SequenceDescriptor OrderNumber = new(
        schema: TableConstants.Schema.Orders,
        baseName: "ord",
        prefix: "ORD",
        padWidth: 5,
        resetPeriod: SequenceResetPeriod.Monthly);

    /// <summary>
    /// Monthly-reset invoice number. PadWidth 6 for higher volume.
    /// Output: INV202604-000001
    /// Capacity: 999,999 invoices/month
    /// </summary>
    public static readonly SequenceDescriptor InvoiceNumber = new(
        schema: TableConstants.Schema.Orders,
        baseName: "inv",
        prefix: "INV",
        padWidth: 6,
        resetPeriod: SequenceResetPeriod.Monthly);
}

