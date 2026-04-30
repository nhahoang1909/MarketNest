# PostgreSQL Sequence Service — Running Number Generation

> **ADR-040** | Status: Accepted | Date: 2026-04-30

## Overview

Running number generation (e.g., `ORD202604-00001`) using period-scoped PostgreSQL sequences.
Deadlock-free, race-condition-safe, supports reset by month or year with high concurrent traffic.

## Approach: Period-Scoped Sequence Names

Instead of resetting a single sequence (which has a race window with `ALTER SEQUENCE RESTART`),
we create a **new sequence object for each period**:

```
orders.seq_ord_202604   ← April 2026 orders
orders.seq_ord_202605   ← May 2026 orders (new sequence, starts from 1)
```

- No `ALTER SEQUENCE RESTART` → no race condition
- Each period has an independent, atomic sequence
- Old sequences are cleaned up by a monthly background job

## Format Specification

Format: `{PREFIX}{PERIOD}-{NUMBER}`

| Reset Period | Format | Example |
|---|---|---|
| Monthly | `{PREFIX}{YYYYMM}-{XXXXX}` | `ORD202604-00001` |
| Yearly | `{PREFIX}{YYYY}-{XXXXX}` | `PAY2026-00001` |
| Never | `{PREFIX}-{XXXXX}` | `SKU-00001` |

## Contracts (`MarketNest.Base.Common/Sequences/`)

### `SequenceResetPeriod`

```csharp
public enum SequenceResetPeriod { Never = 0, Monthly = 1, Yearly = 2 }
```

### `SequenceDescriptor`

Immutable record describing a sequence: schema, base name, prefix, pad width, reset period.

```csharp
var desc = new SequenceDescriptor("orders", "ord", "ORD", padWidth: 5, SequenceResetPeriod.Monthly);
desc.GetSequenceName(now);    // → "orders.seq_ord_202604"
desc.Format(42, now);         // → "ORD202604-00042"
```

### `ISequenceService`

```csharp
public interface ISequenceService
{
    Task<string> NextFormattedAsync(SequenceDescriptor descriptor, CancellationToken ct = default);
    Task<long> NextValueAsync(SequenceDescriptor descriptor, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListSequenceNamesAsync(SequenceDescriptor descriptor, string periodKeyPrefix, CancellationToken ct = default);
    Task DropSequenceAsync(string schemaQualifiedName, CancellationToken ct = default);
}
```

## Infrastructure (`MarketNest.Web/Infrastructure/Sequences/`)

### `PostgresSequenceService`

- Registered as **Singleton** (in-process DDL cache shared across requests)
- `EnsureSequenceExistsAsync`: `CREATE SEQUENCE IF NOT EXISTS` — safe under concurrency (PG catalog lock)
- `_provisionedSequences`: `ConcurrentDictionary` — DDL called at most once per period per app instance
- Uses `NpgsqlConnection` directly (same pattern as `NpgsqlJobExecutionStore`)

### `CleanupStaleSequencesJob`

- IBackgroundJob: `common.cleanup-stale-sequences`
- Schedule: `0 2 1 * *` (1st of every month, 02:00 UTC)
- Retention: Monthly sequences → 3 months, Yearly sequences → 2 years
- `SequenceResetPeriod.Never` sequences are never cleaned up

## Module Sequence Descriptors

Each module defines its own `static class` with `SequenceDescriptor` fields:

| Module | Class | Descriptor | Schema | Prefix | PadWidth | Reset | Example |
|---|---|---|---|---|---|---|---|
| Orders | `OrderSequences` | `OrderNumber` | `orders` | `ORD` | 5 | Monthly | `ORD202604-00001` |
| Orders | `OrderSequences` | `InvoiceNumber` | `orders` | `INV` | 6 | Monthly | `INV202604-000001` |
| Payments | `PaymentSequences` | `PayoutNumber` | `payments` | `PAY` | 5 | Yearly | `PAY2026-00001` |
| Catalog | `CatalogSequences` | `SkuNumber` | `catalog` | `SKU` | 5 | Never | `SKU-00001` |

### File Locations

```
src/MarketNest.Orders/Application/Sequences/OrderSequences.cs
src/MarketNest.Payments/Application/Sequences/PaymentSequences.cs
src/MarketNest.Catalog/Application/Sequences/CatalogSequences.cs
```

## Usage in Command Handlers

```csharp
public sealed class PlaceOrderCommandHandler(
    IOrderRepository repository,
    ISequenceService sequenceService) : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<Result<PlaceOrderResult, Error>> Handle(
        PlaceOrderCommand command, CancellationToken ct)
    {
        var orderNumber = await sequenceService.NextFormattedAsync(
            OrderSequences.OrderNumber, ct);
        // → "ORD202604-00001"

        var order = Order.Create(command.BuyerId, orderNumber, ...);
        repository.Add(order);
        // UoW commits via transaction filter

        return Result.Success(new PlaceOrderResult(order.Id, orderNumber));
    }
}
```

## EF Core Migration Strategy

No migration needed per period — sequences are auto-provisioned at runtime by `EnsureSequenceExistsAsync`.
Only requirement: the target schema must exist (handled by existing module migrations like `CREATE SCHEMA IF NOT EXISTS orders;`).

## DI Registration

```csharp
// In Program.cs:
builder.Services.AddSequenceService();
builder.Services.AddScoped<IBackgroundJob, CleanupStaleSequencesJob>();
```

## Testing

- **Unit tests**: Mock `ISequenceService` in command handler tests
- **SequenceDescriptor tests**: Pure logic — `GetSequenceName()`, `Format()`, constructor validation
- **Integration tests**: Use Testcontainers PostgreSQL to verify concurrent NEXTVAL returns unique values

## Key Invariants

| Invariant | Detail |
|---|---|
| Contracts in `Base.Common` | Modules never reference infrastructure |
| Each module owns its descriptors | No cross-module sharing |
| `PostgresSequenceService` = Singleton | In-process DDL cache shared across requests |
| Period rollover = new PG sequence | Never `ALTER SEQUENCE RESTART` |
| `CREATE SEQUENCE IF NOT EXISTS` | PG catalog lock serializes concurrent cold-starts |
| Cleanup retains 3 months / 2 years | Configurable in `CleanupStaleSequencesJob` |
| `Never` descriptors excluded from cleanup | Permanent sequences live forever |

## Capacity Planning

| PadWidth | Max/period | Suitable for |
|---|---|---|
| 5 digits | 99,999 | ~3,200 orders/day (monthly) |
| 6 digits | 999,999 | ~32,000 orders/day |
| 7 digits | 9,999,999 | Hyperscale |

