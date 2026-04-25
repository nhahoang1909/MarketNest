---
name: architecture-guard
description: >
  Quét toàn bộ codebase MarketNest để enforce Clean Architecture rules: Domain không reference
  Infrastructure, no cross-module DB access, module chỉ communicate qua events/interfaces,
  DDD aggregate boundaries đúng chuẩn. Sử dụng skill này khi người dùng muốn: kiểm tra
  architecture, enforce layer rules, tìm dependency vi phạm, check DDD boundary, review
  aggregate design, kiểm tra module isolation, hoặc nói bất kỳ cụm từ nào như
  "architecture review", "check layer", "enforce rules", "DDD boundary", "module isolation",
  "aggregate design", "clean architecture", "dependency violation", "cross-module",
  "kiểm tra kiến trúc", "vi phạm dependency", "tìm layer violation".
  Kích hoạt khi người dùng upload file .cs, .csproj, hoặc hỏi về cấu trúc module.
compatibility:
  tools: [bash, read_file, write_file, list_files]
  agents: [claude-code, gemini-cli, cursor, continue, aider]
  stack: [.NET 10, Clean Architecture, DDD, MediatR 12, NetArchTest]
---

# Architecture Guard Skill — MarketNest

Skill này enforce toàn bộ architectural rules của MarketNest: dependency rules giữa các layer,
module isolation, DDD aggregate integrity, communication contract, và naming convention.
Mọi vi phạm đều được phân loại **BLOCKER / HIGH / MEDIUM** với file:line cụ thể và fix code.

---

## Dependency Map hợp lệ — MarketNest

```
Allowed dependency flow (→ = "may reference"):

MarketNest.Core            ← không reference module nào
MarketNest.{Module}.Domain → Core only
MarketNest.{Module}.Application → Core + Module.Domain
MarketNest.{Module}.Infrastructure → Core + Module.Domain + Module.Application
MarketNest.Web             → tất cả (composition root — DI registration)

Modules (KHÔNG được reference lẫn nhau):
  Identity | Catalog | Cart | Orders | Payments | Reviews | Disputes | Notifications | Admin

Communication rules:
  Sync  → interface trong Core (INotificationService, IPaymentGateway...)
  Async → IDomainEvent / IPublisher (MediatR Phase 1, MassTransit Phase 3+)
  DB    → mỗi module chỉ ToTable() schema của chính mình
```

---

## Quy trình thực thi

```
Phase 1: SCAN       → Thu thập cấu trúc solution, assembly, csproj references
Phase 2: ANALYZE    → 6 nhóm rule, mỗi nhóm có grep + đọc file cụ thể
Phase 3: REPORT     → Phân loại BLOCKER / HIGH / MEDIUM + NetArchTest code snippets
Phase 4: FIX        → Đề xuất refactor cụ thể (hỏi xác nhận trước)
Phase 5: VERIFY     → Chạy lại check + architecture tests
```

---

## Phase 1: SCAN — Thu thập cấu trúc solution

### 1.1 Đọc project references từ .csproj

```bash
# Liệt kê tất cả project files
find . -name "*.csproj" | grep -v "bin/\|obj/" | sort

# Đọc ProjectReference trong từng .csproj (dependency graph thực tế)
find . -name "*.csproj" | grep -v "bin/\|obj/" | while read f; do
    echo "=== $(basename $f) ==="
    grep "ProjectReference" "$f" | grep -oP 'Include="[^"]*"' | sed 's/Include="//;s/"//'
done

# Liệt kê tất cả .cs files theo module
find src/ -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" \
  | sed 's|src/||' | cut -d/ -f1 | sort | uniq -c | sort -rn
```

### 1.2 Kiểm tra nhanh package references

```bash
# Tìm NuGet packages theo project (phát hiện layer dùng sai package)
find . -name "*.csproj" | grep -v "bin/\|obj/" | while read f; do
    echo "=== $(basename $f) ==="
    grep "PackageReference" "$f" | grep -oP 'Include="[^"]*"'
done

# Tìm Domain project nào đang reference EF Core / Redis / HTTP
find . -name "*.csproj" | grep -i "domain\|Domain" | grep -v "bin/\|obj/" | while read f; do
    result=$(grep -i "EntityFramework\|StackExchange.Redis\|System.Net.Http\|Npgsql" "$f")
    if [ -n "$result" ]; then
        echo "🔴 BLOCKER: Domain project references infrastructure: $f"
        echo "$result"
    fi
done
```

---

## Phase 2: ANALYZE — 6 nhóm rule

---

### Rule Group 1: Layer Dependency — Domain không được reference Infrastructure

**Quy tắc**: `Domain` → chỉ `Core` + BCL (`System.*`). Tuyệt đối không có EF Core, Redis, HTTP, MassTransit.

```bash
# 1A. Tìm using statement sai trong Domain layer
echo "=== Scanning Domain layer for infrastructure using ==="
find src/ -path "*/Domain/*.cs" -not -path "*/bin/*" -not -path "*/obj/*" \
  | xargs grep -ln \
    "Microsoft.EntityFrameworkCore\|StackExchange.Redis\|System.Net.Http\|MassTransit\|Npgsql\|MailKit\|Microsoft.AspNetCore" \
  2>/dev/null

# 1B. Tìm Application layer reference Infrastructure
echo "=== Scanning Application layer for infrastructure using ==="
find src/ -path "*/Application/*.cs" -not -path "*/bin/*" -not -path "*/obj/*" \
  | xargs grep -ln \
    "Microsoft.EntityFrameworkCore\|StackExchange.Redis\|Npgsql\|MailKit" \
  2>/dev/null
# Note: Application có thể dùng MediatR — đây là allowed

# 1C. Tìm Domain class implement interface cần infrastructure
find src/ -path "*/Domain/*.cs" -not -path "*/bin/*" -not -path "*/obj/*" \
  | xargs grep -ln "IDbContext\|DbSet\|IConnectionMultiplexer\|IDatabase\b" \
  2>/dev/null
```

**Dấu hiệu BLOCKER:**
```csharp
// ❌ BLOCKER: Domain/Entities/Order.cs
using Microsoft.EntityFrameworkCore; // EF Core trong Domain!
using StackExchange.Redis;

// ❌ BLOCKER: Domain biết về persistence concern
public class Order : AggregateRoot
{
    [Column("order_status")] // EF annotation trong Domain entity!
    public OrderStatus Status { get; set; }
}

// ✅ Entity config thuộc về Infrastructure/Persistence/
// OrderConfiguration.cs trong Infrastructure mới dùng EF attributes/Fluent API
```

**Fix:**
```csharp
// Domain/Entities/Order.cs — chỉ có pure domain code
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    // Không có [Column], không có [Key], không có EF annotation
}

// Infrastructure/Persistence/Configurations/OrderConfiguration.cs
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "orders");
        builder.Property(o => o.Status)
               .HasConversion<string>();
    }
}
```

---

### Rule Group 2: Module Isolation — Modules không được reference lẫn nhau

**Quy tắc**: `MarketNest.Orders.*` không được `using MarketNest.Catalog.*` hay bất kỳ module nào khác. Chỉ được reference `MarketNest.Core`.

```bash
MODULES="Identity Catalog Cart Orders Payments Reviews Disputes Notifications Admin"

for module in $MODULES; do
    echo "=== Checking $module for cross-module references ==="
    find "src/MarketNest.$module/" -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" \
      2>/dev/null | xargs grep -l "using MarketNest\." 2>/dev/null \
      | xargs grep -h "using MarketNest\." 2>/dev/null \
      | grep -v "using MarketNest\.Core\." \
      | grep -v "using MarketNest\.$module\." \
      | sort -u
done
```

**Dấu hiệu BLOCKER:**
```csharp
// ❌ BLOCKER: Orders/Application/Commands/PlaceOrderCommandHandler.cs
using MarketNest.Catalog.Domain.Entities;    // Orders biết về Catalog entity!
using MarketNest.Identity.Domain.Entities;   // Orders biết về User entity!

// ❌ Orders handler query sang Catalog DbSet trực tiếp
public class PlaceOrderCommandHandler(MarketNestDbContext db)
{
    public async Task Handle(...)
    {
        // BLOCKER: query entity của Catalog module từ Orders handler
        var product = await db.Products.FindAsync(productId);
    }
}
```

**Fix — Dùng interface hoặc domain event:**
```csharp
// ✅ Core/Common/Interfaces/ICatalogService.cs (trong Core — neutral ground)
public interface ICatalogService
{
    Task<ProductSnapshot?> GetProductSnapshotAsync(Guid variantId, CancellationToken ct);
}

// ✅ Catalog/Infrastructure/Services/CatalogService.cs (implementation)
public class CatalogService(MarketNestDbContext db) : ICatalogService
{
    public async Task<ProductSnapshot?> GetProductSnapshotAsync(Guid variantId, CancellationToken ct)
        => await db.ProductVariants
               .AsNoTracking()
               .Where(v => v.Id == variantId)
               .Select(v => new ProductSnapshot(v.Id, v.Product.Title, v.Price, v.Sku))
               .FirstOrDefaultAsync(ct);
}

// ✅ Orders/Application/Commands/PlaceOrderCommandHandler.cs
public class PlaceOrderCommandHandler(ICatalogService catalogService, ...)
{
    // Dùng interface, không dùng Catalog entity trực tiếp
    var snapshot = await catalogService.GetProductSnapshotAsync(variantId, ct);
}
```

**Cross-module DB access:**
```bash
# Tìm handler đọc entity của module khác qua DbSet
MODULES="Identity Catalog Cart Orders Payments Reviews Disputes"
for module in $MODULES; do
    echo "=== $module handlers querying foreign DbSets ==="
    find "src/MarketNest.$module/Application/" -name "*.cs" 2>/dev/null \
      | xargs grep -hn "db\.\|_db\.\|_context\." 2>/dev/null \
      | grep -v "SaveChanges\|Entry\|Set<\|Database\." \
      | grep -v "bin/\|obj/"
    # Manual review: kiểm tra DbSet được query có thuộc module này không
done
```

---

### Rule Group 3: DDD Aggregate Integrity

**Quy tắc**: Aggregate root có state private, mutations qua domain method, raise events từ bên trong aggregate, không anemic model.

```bash
# 3A. Tìm public setter trên Aggregate Root (anemic model)
echo "=== Anemic Domain Model: public setters on aggregate entities ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    # Tìm { get; set; } — public setter
    matches=$(grep -n "{ get; set; }" "$f")
    if [ -n "$matches" ]; then
        echo "🔴 $f"
        echo "$matches"
    fi
done

# 3B. Tìm domain event được raise từ Application layer (nên raise từ Aggregate)
echo "=== Domain events raised from Application layer (should be in Aggregate) ==="
find src/ -path "*/Application/*.cs" -not -path "*/bin/*" \
  | xargs grep -ln "AddDomainEvent\|new.*Event(" 2>/dev/null \
  | grep -v "bin/\|obj/"

# 3C. Tìm Aggregate không inherit AggregateRoot / Entity
echo "=== Aggregates not inheriting base classes ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    classname=$(grep -oP "public class \K\w+" "$f" | head -1)
    if ! grep -q "AggregateRoot\|: Entity\|: ValueObject" "$f"; then
        echo "⚠️  $f — $classname không inherit AggregateRoot hay Entity"
    fi
done

# 3D. Tìm Value Object có public setter hoặc không có equality
echo "=== Value Objects with mutable state ==="
find src/ -path "*/Domain/ValueObjects/*.cs" -not -path "*/bin/*" \
  | xargs grep -ln "{ get; set; }" 2>/dev/null

# 3E. Tìm direct property assignment trên aggregate từ bên ngoài
# (thường thấy trong handler: order.Status = OrderStatus.Shipped — sai!)
echo "=== Direct property assignment on aggregates from handlers ==="
find src/ -path "*/Application/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "\.(Status|Total|BuyerId|SellerId|Amount)\s*=" 2>/dev/null \
  | grep -v "bin/\|obj/" | head -20
```

**Dấu hiệu BLOCKER:**
```csharp
// ❌ BLOCKER: Anemic domain model
public class Order : AggregateRoot
{
    public OrderStatus Status { get; set; }  // public setter!
    public decimal Total { get; set; }        // ai cũng set được
}

// ❌ BLOCKER: Handler thay đổi state trực tiếp thay vì gọi domain method
public class ShipOrderCommandHandler
{
    public async Task Handle(ShipOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(cmd.OrderId);
        order.Status = OrderStatus.Shipped;           // bypass invariant!
        order.ShippedAt = DateTime.UtcNow;
        // Domain event không được raise → Notifications không nhận được!
    }
}

// ❌ BLOCKER: Domain event raise từ Application, không phải từ Aggregate
public class PlaceOrderCommandHandler
{
    public async Task Handle(...)
    {
        // ...
        await publisher.Publish(new OrderPlacedEvent(...)); // sai chỗ!
    }
}
```

**Fix chuẩn:**
```csharp
// ✅ Aggregate: private setter + domain method + raise event từ bên trong
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    public DateTime? ShippedAt { get; private set; }

    public Result<Unit, Error> MarkAsShipped(string trackingNumber)
    {
        if (Status != OrderStatus.Confirmed)
            return Error.InvalidOperation("ORDER_INVALID_TRANSITION",
                $"Cannot ship order in status {Status}");

        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;

        // ✅ Event raised từ aggregate method
        AddDomainEvent(new OrderShippedEvent(Id, BuyerId, SellerId, trackingNumber));
        return Result.Success();
    }
}

// ✅ Handler: chỉ orchestrate, không mutate state trực tiếp
public class ShipOrderCommandHandler(IOrderRepository repo) : CommandHandler<ShipOrderCommand, Unit>
{
    public override async Task<Result<Unit, Error>> Handle(ShipOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdOrThrowAsync(cmd.OrderId, ct);
        var result = order.MarkAsShipped(cmd.TrackingNumber); // domain method
        if (result.IsFailure) return result;
        await repo.SaveChangesAsync(ct); // events dispatched by UoW interceptor
        return Result.Success();
    }
}
```

---

### Rule Group 4: Module Communication — Events & Interfaces Only

**Quy tắc**: Module A muốn notify Module B → publish `IDomainEvent`. Module A muốn query data từ Module B → dùng interface được định nghĩa trong `Core`.

```bash
# 4A. Tìm direct service injection cross-module
# (Orders inject CatalogService implementation trực tiếp thay vì ICatalogService)
echo "=== Cross-module direct concrete service injection ==="
MODULES="Identity Catalog Cart Orders Payments Reviews Disputes Notifications"
for module in $MODULES; do
    find "src/MarketNest.$module/Application/" -name "*.cs" 2>/dev/null \
      | xargs grep -hn "new MarketNest\.\|= new " 2>/dev/null \
      | grep -v "$module\|Core\|bin/\|obj/" | head -5
done

# 4B. Tìm module consume event của module khác trực tiếp qua type reference
echo "=== Cross-module event handler coupling ==="
find src/ -name "*.cs" -not -path "*/bin/*" | xargs grep -hn "INotificationHandler<\|IConsumer<" \
  2>/dev/null | grep -v "bin/\|obj/" | head -20
# Manual review: event type đến từ module nào?

# 4C. Tìm HTTP client call giữa modules (không được phép trong monolith)
echo "=== Illegal HTTP calls between modules ==="
find src/ -path "*/Application/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "HttpClient\|IHttpClientFactory\|localhost:\|127\.0\.0\.1" \
  2>/dev/null | grep -v "bin/\|obj/" | head -10

# 4D. Domain event naming convention check
echo "=== Domain events not following naming convention ==="
find src/ -path "*/Domain/Events/*.cs" -not -path "*/bin/*" | while read f; do
    name=$(basename "$f" .cs)
    if ! echo "$name" | grep -qE "Event$|DomainEvent$"; then
        echo "⚠️  $f — event file should end in 'Event'"
    fi
done
```

**Dấu hiệu HIGH:**
```csharp
// ❌ HIGH: Orders inject concrete Notification service thay vì interface
public class PlaceOrderCommandHandler(
    NotificationService notificationService)  // concrete class!
{ ... }

// ❌ HIGH: Event handler tham chiếu event type của module khác trực tiếp
// Trong Payments module:
public class OrderPlacedEventHandler : INotificationHandler<OrderPlacedEvent>
// OrderPlacedEvent từ Orders.Domain — buộc Payments phải biết Orders.Domain
```

**Fix:**
```csharp
// ✅ Core định nghĩa shared event contract (không thuộc module nào)
// Core/Common/Events/IOrderPlacedEvent.cs
public interface IOrderPlacedIntegrationEvent : IDomainEvent
{
    Guid OrderId { get; }
    Guid BuyerId { get; }
    decimal Total { get; }
}

// ✅ Orders publish event implement interface đó
// Orders/Domain/Events/OrderPlacedEvent.cs
public record OrderPlacedEvent(Guid OrderId, Guid BuyerId, decimal Total)
    : IOrderPlacedIntegrationEvent;

// ✅ Payments handler bind với interface, không bind với Orders.Domain type
// Payments/Application/EventHandlers/OrderPlacedHandler.cs
public class OrderPlacedHandler : INotificationHandler<IOrderPlacedIntegrationEvent>
{ ... }
```

---

### Rule Group 5: Application Layer Contracts

**Quy tắc**: Command trả về `Result<T, Error>`, không trả về `void`. Query không thay đổi state. Handler không validate thủ công (dùng FluentValidation pipeline). Web layer không gọi Repository trực tiếp.

```bash
# 5A. Command handler trả về void (sai — phải trả về Result)
echo "=== Command handlers returning void ==="
find src/ -path "*/Application/Commands/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "Task Handle\|async Task " 2>/dev/null \
  | grep -v "Result<\|Task<Result\|bin/\|obj/" | head -20

# 5B. Query handler thay đổi state (gọi SaveChanges)
echo "=== Query handlers calling SaveChanges ==="
find src/ -path "*/Application/Queries/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "SaveChanges\|\.Add(\|\.Remove(\|\.Update(" 2>/dev/null \
  | grep -v "bin/\|obj/" | head -10

# 5C. Handler validate thủ công thay vì dùng FluentValidation pipeline
echo "=== Manual validation in handlers (should use FluentValidation pipeline) ==="
find src/ -path "*/Application/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "if.*==.*null\|if.*string.IsNullOrEmpty\|throw new ArgumentException\|throw new ValidationException" \
  2>/dev/null | grep -v "bin/\|obj/" | head -20

# 5D. Web/Pages layer inject Repository hoặc DbContext trực tiếp
echo "=== Web layer bypassing Application (direct Repository/DbContext injection) ==="
find src/MarketNest.Web/ -name "*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "IOrderRepository\|ICatalogRepository\|MarketNestDbContext\|DbSet<" \
  2>/dev/null | grep -v "bin/\|obj/" | head -10

# 5E. Mỗi Command phải có Validator tương ứng
echo "=== Commands missing FluentValidation ==="
find src/ -path "*/Application/Commands/*Command.cs" -not -path "*/bin/*" \
  | while read f; do
    cmdname=$(basename "$f" .cs)
    validator="${cmdname}Validator.cs"
    dir=$(dirname "$f")
    if ! find "$dir/../Validators" -name "$validator" 2>/dev/null | grep -q .; then
        echo "⚠️  Missing validator for: $cmdname"
    fi
done
```

**Dấu hiệu HIGH:**
```csharp
// ❌ HIGH: Command handler trả về void — caller không biết thành công hay thất bại
public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand cmd, CancellationToken ct)
    { ... } // void!
}

// ❌ HIGH: Query handler gọi SaveChanges — side effect trong read path!
public class GetOrderDetailQueryHandler
{
    public async Task<OrderDetailDto> Handle(GetOrderDetailQuery q, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync(q.OrderId);
        order.LastViewedAt = DateTime.UtcNow; // mutation trong query!
        await db.SaveChangesAsync(ct);
        return MapToDto(order);
    }
}

// ❌ MEDIUM: Web Page inject repository trực tiếp, bypass Application layer
public class OrderDetailModel(IOrderRepository repo) : PageModel
{
    public async Task OnGet(Guid id)
        => Order = await repo.GetByIdAsync(id); // bypass CQRS!
}
```

**Fix:**
```csharp
// ✅ Command handler luôn trả về Result
public class CancelOrderCommandHandler
    : ICommandHandler<CancelOrderCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdOrThrowAsync(cmd.OrderId, ct);
        var result = order.Cancel(cmd.Reason);
        if (result.IsFailure) return result;
        await repo.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ✅ Web Page inject ISender (MediatR), không inject repository
public class OrderDetailModel(ISender sender) : PageModel
{
    public async Task OnGet(Guid id)
        => Order = await sender.Send(new GetOrderDetailQuery(id, User.GetUserId()));
}
```

---

### Rule Group 6: Naming & Structure Conventions

**Quy tắc**: Convention được enforce qua code, không phải memory.

```bash
# 6A. Domain Event naming — phải kết thúc bằng "Event"
echo "=== Domain events not ending in 'Event' ==="
find src/ -path "*/Domain/Events/*.cs" -not -path "*/bin/*" | while read f; do
    grep -oP "public record \K\w+" "$f" | grep -v "Event$" && echo "  in $f"
done

# 6B. Command naming — phải kết thúc bằng "Command"
echo "=== Commands not ending in 'Command' ==="
find src/ -path "*/Application/Commands/*.cs" -not -path "*/bin/*" | while read f; do
    grep -oP "public record \K\w+" "$f" | grep -v "Command$" && echo "  in $f"
done

# 6C. Query naming — phải kết thúc bằng "Query"
echo "=== Queries not ending in 'Query' ==="
find src/ -path "*/Application/Queries/*.cs" -not -path "*/bin/*" | while read f; do
    grep -oP "public record \K\w+" "$f" | grep -v "Query$\|QueryHandler$" && echo "  in $f"
done

# 6D. Private fields naming — phải dùng _camelCase
echo "=== Private fields not following _camelCase convention ==="
find src/ -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" \
  | xargs grep -hn "private.*readonly\|private.*[A-Za-z]" 2>/dev/null \
  | grep -v "_\|bin/\|obj/" | grep "private " | head -20

# 6E. Repository trong Infrastructure, không trong Application
echo "=== Repository implementation misplaced in Application layer ==="
find src/ -path "*/Application/*Repository.cs" -not -path "*/bin/*" 2>/dev/null
# Repository interface: Application OK. Repository implementation: phải ở Infrastructure

# 6F. Aggregate Root phải có static factory method Create()
echo "=== Aggregates missing static Create() factory ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    if grep -q "AggregateRoot" "$f"; then
        if ! grep -q "public static.*Create\b" "$f"; then
            echo "⚠️  $(basename $f) — AggregateRoot thiếu static Create() factory"
        fi
    fi
done
```

---

## Phase 3: REPORT — Báo cáo Architecture Violations

```markdown
# Architecture Guard Report — MarketNest
**Date**: <ngày>
**Scope**: Clean Architecture, DDD, Module Isolation, Communication Contracts

---

## Tổng quan

| Rule Group | Violations | Severity |
|---|---|---|
| Layer Dependency | X | BLOCKER |
| Module Isolation | X | BLOCKER |
| DDD Aggregate Integrity | X | BLOCKER / HIGH |
| Module Communication | X | HIGH |
| Application Contracts | X | HIGH / MEDIUM |
| Naming & Structure | X | MEDIUM |

---

## 🔴 BLOCKER — Phải fix trước khi merge

### [B-001] Domain references Infrastructure
- **File**: `src/MarketNest.Orders/Domain/Entities/Order.cs:3`
- **Vi phạm**: `using Microsoft.EntityFrameworkCore;`
- **Rule**: Domain layer không được reference bất kỳ infrastructure package nào
- **Fix**: Xóa using, chuyển EF annotation sang `OrderConfiguration.cs`

---

## 🟠 HIGH — Fix trong sprint hiện tại

### [H-001] Cross-module entity access
- **File**: `src/MarketNest.Orders/Application/Commands/PlaceOrder/PlaceOrderCommandHandler.cs:45`
- **Vi phạm**: `await db.Products.FindAsync(productId)` — Orders query Catalog entity
- **Fix**: Tạo `ICatalogService` trong Core, implement trong Catalog.Infrastructure

---

## 🟡 MEDIUM — Backlog

...

---

## NetArchTest — Code để thêm vào ArchitectureTests project

(Xem Phase 4 bên dưới)
```

---

## Phase 4: FIX — NetArchTest Code Snippets

Sau khi phát hiện vi phạm, agent tạo test case cụ thể để thêm vào
`tests/MarketNest.ArchitectureTests/`. Test chạy trong CI và sẽ fail nếu vi phạm tái diễn.

```csharp
// tests/MarketNest.ArchitectureTests/LayerDependencyTests.cs

public class LayerDependencyTests
{
    // ── Assemblies ────────────────────────────────────────────────────────
    private static readonly Assembly CoreAssembly         = typeof(AggregateRoot).Assembly;
    private static readonly Assembly OrdersDomainAssembly = typeof(Order).Assembly;
    private static readonly Assembly CatalogDomainAssembly= typeof(Product).Assembly;
    // Add remaining modules as needed

    // ── Rule 1: Domain không reference Infrastructure ─────────────────────
    [Fact]
    public void Domain_ShouldNotDependOn_EntityFrameworkCore()
    {
        var result = Types.InAssembly(OrdersDomainAssembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer must not know about EF Core. Violations: " +
                     string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Redis()
    {
        var result = Types.InAssembly(OrdersDomainAssembly)
            .Should()
            .NotHaveDependencyOn("StackExchange.Redis")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotDependOn_MassTransit()
    {
        var result = Types.InAssembly(OrdersDomainAssembly)
            .Should()
            .NotHaveDependencyOn("MassTransit")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    // ── Rule 2: Application không reference Infrastructure concrete ────────
    [Fact]
    public void Application_ShouldNotDependOn_EntityFrameworkCore()
    {
        // Application có thể dùng EF Core interfaces nhưng không phải concrete DbContext
        // Adjust nếu project structure của bạn tách Application ra assembly riêng
        var ordersAppAssembly = typeof(PlaceOrderCommand).Assembly;

        var result = Types.InAssembly(ordersAppAssembly)
            .That()
            .ResideInNamespace("MarketNest.Orders.Application")
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    // ── Rule 3: Module isolation — Orders không reference Catalog ──────────
    [Fact]
    public void Orders_ShouldNotDependOn_CatalogDomain()
    {
        var result = Types.InAssembly(OrdersDomainAssembly)
            .Should()
            .NotHaveDependencyOn("MarketNest.Catalog")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Orders module must not reference Catalog module. Use ICatalogService interface.");
    }

    [Fact]
    public void Orders_ShouldNotDependOn_IdentityDomain()
    {
        var result = Types.InAssembly(OrdersDomainAssembly)
            .Should()
            .NotHaveDependencyOn("MarketNest.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    // ── Rule 4: DDD — Aggregate root properties phải private setter ────────
    [Fact]
    public void AggregateRoots_ShouldNotHave_PublicPropertySetters()
    {
        var result = Types.InAssemblies([OrdersDomainAssembly, CatalogDomainAssembly])
            .That()
            .Inherit(typeof(AggregateRoot))
            .Should()
            .NotHavePublicSetterOnProperty()  // custom rule — xem helper bên dưới
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Aggregate roots must protect their invariants via domain methods, not public setters.");
    }

    // ── Rule 5: Web không inject Repository trực tiếp ─────────────────────
    [Fact]
    public void WebLayer_ShouldNotDependOn_Repositories()
    {
        var webAssembly = typeof(Program).Assembly;

        var result = Types.InAssembly(webAssembly)
            .That()
            .ResideInNamespace("MarketNest.Web.Pages")
            .Should()
            .NotHaveDependencyOn("IOrderRepository")
            .And()
            .NotHaveDependencyOn("ICatalogRepository")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Razor Pages must use ISender (MediatR), not repositories directly.");
    }

    // ── Rule 6: Command handler return type phải là Result ─────────────────
    [Fact]
    public void CommandHandlers_ShouldReturn_ResultType()
    {
        var result = Types.InAssemblies([OrdersDomainAssembly])
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Should()
            .HaveDependencyOn("MarketNest.Core.Common.Result") // Result<T,E> type
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    // ── Rule 7: Domain Event naming ────────────────────────────────────────
    [Fact]
    public void DomainEvents_ShouldEndWith_Event()
    {
        var result = Types.InAssemblies([OrdersDomainAssembly, CatalogDomainAssembly])
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .HaveNameEndingWith("Event")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
```

**Custom NetArchTest rule — `NotHavePublicSetterOnProperty()`:**

```csharp
// tests/MarketNest.ArchitectureTests/Helpers/CustomArchRules.cs
public static class CustomArchRules
{
    public static PredicateList NotHavePublicSetterOnProperty(this ShouldList should)
    {
        // NetArchTest custom condition
        return should.MeetCustomRule(new NoPublicSetterRule());
    }
}

public class NoPublicSetterRule : ICustomRule
{
    public bool MeetsRule(TypeDefinition type)
    {
        return !type.Properties.Any(p =>
            p.SetMethod is { IsPublic: true });
    }
}
```

---

## Phase 5: VERIFY — Chạy sau khi fix

```bash
# Build để kiểm tra compiler errors
dotnet build --no-incremental 2>&1 | grep -E "error|warning" | head -30

# Chạy Architecture Tests (nhanh nhất, không cần Docker)
dotnet test tests/MarketNest.ArchitectureTests -v normal

# Chạy Unit Tests (kiểm tra domain logic không bị break)
dotnet test tests/MarketNest.UnitTests -v normal

# Kiểm tra lại grep patterns từ Phase 2 — expect 0 kết quả
echo "=== Re-checking cross-module references ==="
find src/ -name "*.cs" -not -path "*/bin/*" | xargs grep -l "using MarketNest\." \
  | xargs grep -h "using MarketNest\." | grep -v "Core\." | sort -u

echo "=== Re-checking public setters on aggregates ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" \
  | xargs grep -n "{ get; set; }" 2>/dev/null | grep -v "bin/\|obj/"
```

---

## Quick Reference — Architecture Rules

| Rule | Allowed | NOT Allowed |
|---|---|---|
| Domain references | `Core`, BCL (`System.*`) | EF Core, Redis, HTTP, MassTransit |
| Application references | `Core`, `Domain`, MediatR | EF Core concrete, Redis concrete |
| Infrastructure references | Tất cả trong module | Module khác |
| Module → Module | Via `Core` interface | Direct namespace reference |
| DB access cross-module | Không bao giờ | `db.Products` từ Orders handler |
| State mutation | `order.MarkAsShipped()` | `order.Status = OrderStatus.Shipped` |
| Event raise location | Bên trong aggregate method | Application handler |
| Command return type | `Result<T, Error>` | `void`, `Task` |
| Web layer | `ISender.Send()` | Repository, DbContext |
| Communication async | `IDomainEvent` + `IPublisher` | Direct method call cross-module |
