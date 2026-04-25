---
name: database-review
description: >
  Quét toàn bộ codebase để review chất lượng database layer cho MarketNest (.NET 10, EF Core 10,
  PostgreSQL 16, Redis 7). Sử dụng skill này khi người dùng muốn: kiểm tra EF Core migration,
  tìm N+1 query, audit PostgreSQL index, phân tích query plan với EXPLAIN ANALYZE, kiểm tra
  schema-per-module isolation, review Redis key naming và TTL strategy, tìm missing index,
  kiểm tra AsNoTracking, detect tracking query trong read path, hoặc nói bất kỳ cụm từ nào
  như "review database", "kiểm tra query", "tối ưu EF Core", "N+1", "index audit",
  "Redis TTL", "migration review", "check schema", "slow query".
  Kích hoạt khi người dùng upload file migration, DbContext, entity config, hoặc repository.
compatibility:
  tools: [bash, read_file, write_file, list_files]
  agents: [claude-code, gemini-cli, cursor, continue, aider]
  stack: [.NET 10, EF Core 10, PostgreSQL 16, Redis 7, StackExchange.Redis 2.x]
---

# Database Review Skill — MarketNest

Skill này quét toàn bộ database layer của MarketNest: EF Core entity config, migrations, query handler, repository, và Redis service — sau đó báo cáo phân loại theo mức độ nguy hiểm với fix cụ thể.

---

## Quy trình thực thi (Bắt buộc theo thứ tự)

```
Phase 1: SCAN      → Quét file liên quan đến database trong toàn bộ solution
Phase 2: ANALYZE   → Kiểm tra từng hạng mục theo checklist
Phase 3: REPORT    → Báo cáo phân loại 🔴 / 🟡 / 🟢 với file:line cụ thể
Phase 4: FIX       → Đề xuất fix với code trước/sau (hỏi xác nhận trước khi sửa)
Phase 5: VERIFY    → Kiểm tra lại sau khi sửa
```

---

## Phase 1: SCAN — Xác định file cần review

### 1.1 Quét toàn bộ database-related files

```bash
# Tìm tất cả DbContext
find . -name "*.cs" | xargs grep -l "DbContext\|DbSet<" \
  | grep -v "bin/\|obj/\|.git/" | sort

# Tìm tất cả Entity Configuration (IEntityTypeConfiguration)
find . -name "*Configuration.cs" -o -name "*EntityConfig.cs" \
  | grep -v "bin/\|obj/" | sort

# Tìm tất cả Migration files
find . -path "*/Migrations/*.cs" -not -name "*.Designer.cs" \
  | grep -v "bin/\|obj/" | sort -V

# Tìm tất cả Repository
find . -name "*Repository.cs" | grep -v "bin/\|obj/" | sort

# Tìm tất cả Query Handler (read path)
find . -name "*QueryHandler.cs" | grep -v "bin/\|obj/" | sort

# Tìm tất cả Redis service
find . -name "*.cs" | xargs grep -l "IDatabase\|StackExchange.Redis\|IConnectionMultiplexer\|RedisKey\|KeyExpire" \
  | grep -v "bin/\|obj/" | sort
```

### 1.2 Thu thập thống kê nhanh

```bash
# Đếm số migrations hiện có
find . -path "*/Migrations/*.cs" -not -name "*.Designer.cs" \
  | grep -v "bin/\|obj/" | wc -l

# Tìm migration gần nhất
find . -path "*/Migrations/*.cs" -not -name "*.Designer.cs" \
  | grep -v "bin/\|obj/" | sort -V | tail -5

# Đếm số DbSet
grep -rn "DbSet<" src/ --include="*.cs" | grep -v "bin/\|obj/"

# Tìm nơi dùng .Include() (potential N+1 fix or over-fetch)
grep -rn "\.Include\|\.ThenInclude" src/ --include="*.cs" \
  | grep -v "bin/\|obj/" | wc -l
```

---

## Phase 2: ANALYZE — Checklist chi tiết

---

### 2.1 EF Core Migration Review

#### Kiểm tra migration naming convention
```bash
# Convention MarketNest: YYYYMMDD_HHmm_DescriptiveTitle
find . -path "*/Migrations/*.cs" -not -name "*.Designer.cs" \
  | grep -v "bin/\|obj/" \
  | xargs -I{} basename {} .cs \
  | grep -vE "^[0-9]{8}_[0-9]{4}_[A-Z][A-Za-z]+"
# Output: migration files vi phạm naming convention
```

**Checklist migration:**
- [ ] Tên migration mô tả rõ: `AddOrderDisputeTable`, `AddIndexProductStoreId` — không phải `Migration1`, `Update2`
- [ ] Mỗi migration có `Down()` đầy đủ (không để trống) — quan trọng khi rollback
- [ ] Không có `MigrationBuilder.Sql()` với raw SQL phức tạp mà không có comment giải thích
- [ ] Không thêm **non-nullable column** vào table có data hiện có mà không có default value
- [ ] Không drop column / rename column trực tiếp (luôn deprecate trước, drop sau)
- [ ] `HasPrecision(18, 2)` cho mọi cột `decimal` liên quan đến tiền tệ

```bash
# Tìm decimal column thiếu precision
grep -rn "\.HasColumnType\|Property.*decimal\|\.IsRequired" src/ --include="*.cs" \
  | grep -i "decimal\|price\|amount\|commission\|payout" \
  | grep -v "HasPrecision\|bin/\|obj/" | head -20

# Tìm migration có Down() rỗng
grep -rn "protected override void Down" . -A3 --include="*.cs" \
  | grep -v "bin/\|obj/" | grep -A2 "Down" | grep "migrationBuilder.}" | head -10

# Tìm migration thêm non-nullable column
grep -rn "IsNullable: false\|\.IsRequired()" . --include="*.cs" \
  | grep -i "Migrations" | grep -v "bin/\|obj/" | head -20
```

#### Kiểm tra schema-per-module isolation

```bash
# Mỗi module phải dùng đúng schema của mình
# identity, catalog, orders, payments, reviews, disputes
grep -rn "\.ToTable(" src/ --include="*.cs" | grep -v "bin/\|obj/" | head -40
```

**Dấu hiệu vi phạm — cần flag ngay:**
```csharp
// ❌ Module Orders reference sang schema catalog
builder.ToTable("products", "catalog");  // trong Orders module

// ✅ Mỗi module chỉ được reference schema của mình
builder.ToTable("order_lines", "orders");
```

```bash
# Kiểm tra cross-schema reference
# Orders module không được ToTable với schema catalog/identity/payments
grep -rn "\.ToTable(" src/MarketNest.Orders/ --include="*.cs" \
  | grep -v '"orders"' | grep -v "bin/\|obj/"

grep -rn "\.ToTable(" src/MarketNest.Catalog/ --include="*.cs" \
  | grep -v '"catalog"' | grep -v "bin/\|obj/"

grep -rn "\.ToTable(" src/MarketNest.Identity/ --include="*.cs" \
  | grep -v '"identity"' | grep -v "bin/\|obj/"

grep -rn "\.ToTable(" src/MarketNest.Payments/ --include="*.cs" \
  | grep -v '"payments"' | grep -v "bin/\|obj/"
```

#### Kiểm tra soft delete query filter

```bash
# Tất cả entity có soft delete phải có HasQueryFilter
grep -rn "IsDeleted\|DeletedAt" src/ --include="*.cs" | grep -v "bin/\|obj/" \
  | grep -v "HasQueryFilter" | grep "Property\|Column" | head -20

# Kiểm tra HasQueryFilter đã được apply
grep -rn "HasQueryFilter" src/ --include="*.cs" | grep -v "bin/\|obj/"
```

---

### 2.2 N+1 Query Detection

N+1 là lỗi phổ biến nhất và nguy hiểm nhất trong EF Core.

#### Pattern 1 — Lazy loading trong loop
```bash
# Tìm navigation property access trong foreach / Select (potential lazy load)
grep -rn "foreach\|\.Select\|\.ForEach" src/ --include="*.cs" \
  | grep -v "bin/\|obj/\|test\|Test" -A3 | head -30

# Tìm virtual navigation property (lazy loading enabled)
grep -rn "public virtual\|virtual ICollection\|virtual IEnumerable" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -20
```

**Dấu hiệu nguy hiểm:**
```csharp
// ❌ N+1: mỗi order.Lines trigger 1 SELECT thêm
var orders = await db.Orders.ToListAsync();
foreach (var order in orders)
{
    var lineCount = order.Lines.Count; // LAZY LOAD → N queries!
}

// ✅ Eager load
var orders = await db.Orders
    .Include(o => o.Lines)
    .ToListAsync();
```

#### Pattern 2 — Include trong Write path (lãng phí)
```bash
# Command handler không nên Include cho aggregate load
grep -rn "\.Include\|\.ThenInclude" src/ --include="*CommandHandler.cs" \
  | grep -v "bin/\|obj/" | head -20
```

**Rule:** Command handler → dùng `GetByIdAsync()` từ repository (load aggregate). Query handler → dùng `AsNoTracking().Select()` project thẳng ra DTO, chỉ Include khi thực sự cần.

#### Pattern 3 — Missing AsNoTracking trong Query handler
```bash
# Query handler phải dùng AsNoTracking
grep -rn "class.*QueryHandler" src/ --include="*.cs" \
  | grep -v "bin/\|obj/" \
  | awk '{print $0}' | while read line; do
      file=$(echo $line | cut -d: -f1)
      grep -L "AsNoTracking\|SqlQueryRaw\|FromSqlRaw" "$file" 2>/dev/null
    done

# Cách đơn giản hơn: tìm QueryHandler không có AsNoTracking
grep -rn "class.*QueryHandler" src/ --include="*.cs" -l \
  | xargs grep -L "AsNoTracking" | grep -v "bin/\|obj/"
```

**Fix chuẩn:**
```csharp
// ❌ Tracking query trong read path — waste memory + slower
var products = await db.Products
    .Where(p => p.Status == ProductStatus.Active)
    .ToListAsync(ct);

// ✅ No tracking + project ra DTO
var products = await db.Products
    .AsNoTracking()
    .Where(p => p.Status == ProductStatus.Active)
    .Select(p => new ProductListItemDto(p.Id, p.Title, p.Store.Slug, ...))
    .ToListAsync(ct);
```

#### Pattern 4 — Cartesian explosion trong multiple Include
```bash
# Nhiều Include song song (không phải ThenInclude) → Cartesian product
grep -rn "\.Include.*\.Include" src/ --include="*.cs" | grep -v "bin/\|obj/" | head -20
```

```csharp
// ❌ Cartesian explosion: Include 2 collection navigations
var orders = await db.Orders
    .Include(o => o.Lines)     // collection
    .Include(o => o.Shipments) // collection → multiplied rows!
    .ToListAsync();

// ✅ Split query
var orders = await db.Orders
    .Include(o => o.Lines)
    .Include(o => o.Shipments)
    .AsSplitQuery()  // EF Core 5+: tách thành nhiều query thay vì JOIN
    .ToListAsync();
```

---

### 2.3 PostgreSQL Index Audit

#### Scan entity configuration tìm missing index
```bash
# Tìm tất cả HasIndex đã khai báo
grep -rn "HasIndex\|HasForeignKey\|\.HasOne\|\.HasMany" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -40

# Tìm foreign key column không có index (thường bị quên)
grep -rn "HasForeignKey\|ForeignKey\|\.WithOne\|\.WithMany" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -30
```

#### Checklist index bắt buộc cho MarketNest

Đọc từng entity configuration, kiểm tra các cột sau phải có index:

**catalog schema:**
```
products:        store_id, status, created_at (desc), tsv (GIN — full-text)
product_variants: product_id, price
inventory_items: variant_id
```

**orders schema:**
```
orders:      buyer_id, status, created_at (desc), seller_id
order_lines: order_id, variant_id
fulfillments: order_id
shipments:   order_id
```

**identity schema:**
```
users:          email (unique), normalized_email (unique)
refresh_tokens: token_hash (unique), user_id, expires_at
```

**payments schema:**
```
payments:    order_id, status
payouts:     seller_id, status
commissions: order_id
```

**reviews schema:**
```
reviews:      product_id, buyer_id, created_at (desc)
review_votes: review_id, user_id (composite unique)
```

**disputes schema:**
```
disputes:         order_id, status
dispute_messages: dispute_id, created_at
```

```bash
# Ví dụ kiểm tra: Order entity có index buyer_id chưa
grep -rn "HasIndex\|buyer_id\|BuyerId" src/MarketNest.Orders/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -20

# Tìm soft delete column thiếu partial index
grep -rn "IsDeleted\|is_deleted" src/ --include="*.cs" \
  | grep "HasIndex" | grep -v "bin/\|obj/" | head -10
```

**Fix pattern — entity configuration:**
```csharp
// ❌ Thiếu index trên buyer_id và status
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "orders");
        builder.HasKey(o => o.Id);
        // ... không có HasIndex
    }
}

// ✅ Đầy đủ index
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Total).HasPrecision(18, 2);

        // Index cho query phổ biến
        builder.HasIndex(o => o.BuyerId);
        builder.HasIndex(o => new { o.BuyerId, o.Status }); // composite
        builder.HasIndex(o => new { o.SellerId, o.Status });
        builder.HasIndex(o => o.CreatedAt);                 // sort by date

        // Partial index: chỉ active orders
        builder.HasIndex(o => o.Status)
               .HasFilter("status NOT IN ('completed', 'cancelled')");
    }
}
```

#### Full-text search index (PostgreSQL tsvector)
```bash
# Kiểm tra Product có tsvector index cho search chưa
grep -rn "tsvector\|GinIndex\|HasAnnotation.*gin\|to_tsvector" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -10
```

```csharp
// ✅ GIN index cho full-text search (PostgreSQL)
builder.HasIndex(p => p.SearchVector)
       .HasMethod("GIN");

// SearchVector được update bằng trigger hoặc computed column
builder.Property(p => p.SearchVector)
       .HasComputedColumnSql(
           "to_tsvector('english', coalesce(title,'') || ' ' || coalesce(description,''))",
           stored: true);
```

---

### 2.4 EXPLAIN ANALYZE — Query Plan Analysis

Dùng khi agent có thể connect vào PostgreSQL (Docker Compose local):

```bash
# Connect vào PostgreSQL trong Docker
docker exec -it $(docker ps -q -f name=postgres) \
  psql -U mn -d marketnest
```

```sql
-- Bật timing và buffers
\timing on

-- Phân tích query phổ biến nhất: browse products
EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
SELECT p.id, p.title, s.slug, MIN(v.price)
FROM catalog.products p
JOIN catalog.storefronts s ON s.id = p.store_id
JOIN catalog.product_variants v ON v.product_id = p.id
WHERE p.status = 'Active'
  AND p.is_deleted = false
GROUP BY p.id, p.title, s.slug
ORDER BY p.created_at DESC
LIMIT 20 OFFSET 0;
-- ⚠️ Tìm: Seq Scan trên bảng lớn → cần index
-- ⚠️ Tìm: Hash Join với rows ước tính quá thấp → cần ANALYZE
-- ✅ Mong muốn: Index Scan, Bitmap Index Scan

-- Orders của buyer
EXPLAIN (ANALYZE, BUFFERS)
SELECT o.id, o.status, o.total, o.created_at
FROM orders.orders o
WHERE o.buyer_id = 'USER_GUID_HERE'
  AND o.status != 'Cancelled'
ORDER BY o.created_at DESC;
-- ⚠️ Seq Scan → cần index (buyer_id, created_at)

-- Refresh token lookup (critical path)
EXPLAIN (ANALYZE, BUFFERS)
SELECT rt.* FROM identity.refresh_tokens rt
WHERE rt.token_hash = 'HASH_HERE'
  AND rt.expires_at > NOW();
-- ✅ Phải là Index Scan (unique index trên token_hash)
```

**Đọc kết quả EXPLAIN:**

| Ký hiệu | Ý nghĩa | Hành động |
|---|---|---|
| `Seq Scan` trên bảng > 1000 rows | Quét toàn bảng | Thêm index trên cột WHERE |
| `cost=xxx..yyy` yyy lớn | Query expensive | Xem lại JOIN, index, SELECT * |
| `rows=1 actual rows=5000` | Cardinality estimate sai lớn | Chạy `ANALYZE table_name` |
| `Hash Join` | OK cho medium size | Xem execution time |
| `Nested Loop` với nhiều rows | Nguy hiểm nếu outer lớn | Kiểm tra index inner table |
| `Buffers: shared hit=xxx read=yyy` | `read` cao → cache miss | Xem xét caching |

```sql
-- Cập nhật statistics sau khi thêm index
ANALYZE catalog.products;
ANALYZE orders.orders;

-- Kiểm tra index usage sau khi tạo
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan,        -- số lần index được dùng
    idx_tup_read,
    idx_tup_fetch
FROM pg_stat_user_indexes
WHERE schemaname IN ('catalog', 'orders', 'identity', 'payments')
ORDER BY idx_scan DESC;

-- Tìm index không bao giờ được dùng (có thể xóa)
SELECT indexrelid::regclass AS index_name,
       relid::regclass AS table_name,
       idx_scan
FROM pg_stat_user_indexes
WHERE idx_scan = 0
  AND schemaname NOT IN ('pg_catalog', 'information_schema')
ORDER BY table_name;
```

---

### 2.5 Redis Key Naming & TTL Strategy

#### Scan toàn bộ Redis service code

```bash
# Tìm tất cả nơi tạo Redis key
grep -rn "marketnest:\|\"cart:\|\"session:\|\"ratelimit:\|\"refresh:\|\"blacklist:" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -30

# Tìm tất cả KeyExpireAsync / StringSetAsync với TTL
grep -rn "KeyExpireAsync\|StringSetAsync\|HashSetAsync\|SetAsync" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -30

# Tìm Redis operation KHÔNG có TTL (potential memory leak)
grep -rn "StringSetAsync\|HashSetAsync\|SetAsync" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" \
  | grep -v "TimeSpan\|expiry\|Expiry\|ttl\|TTL\|KeyExpire" | head -20
```

#### Convention chuẩn MarketNest — so sánh với implementation

```
# Convention đã định nghĩa trong architecture-requirements.md:
marketnest:cart:{userId}:reservation:{productVariantId}   TTL: 15min  (900s)
marketnest:session:{sessionId}                            TTL: 24h    (86400s)
marketnest:ratelimit:{userId}:{endpoint}                  TTL: 1min   (60s)
marketnest:refresh:{tokenId}                              TTL: 7d     (604800s)
marketnest:blacklist:{tokenId}                            TTL: 7d     (604800s)
```

**Checklist Redis review:**

```bash
# 1. Key format đúng chưa (dùng : phân cách, không dùng / hay .)
grep -rn "\"marketnest" src/ --include="*.cs" | grep -v "bin/\|obj/" | head -20

# 2. Hardcoded TTL hay dùng constant
grep -rn "TimeSpan\|900\|86400\|604800\|ReservationTtl\|SessionTtl" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -20

# 3. Lua script cho atomic operations
grep -rn "ScriptEvaluateAsync\|LuaScript\|lua" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -10
```

**Dấu hiệu cần fix:**
```csharp
// ❌ Key không theo convention (dùng / thay vì :)
var key = $"marketnest/cart/{userId}/reservation/{variantId}";

// ❌ Magic number TTL
await db.StringSetAsync(key, value, TimeSpan.FromSeconds(900));

// ❌ Không có TTL → memory leak
await db.StringSetAsync(key, value);

// ❌ Non-atomic check-then-set (race condition)
var existing = await db.StringGetAsync(key);
if (existing.IsNullOrEmpty)
    await db.StringSetAsync(key, value, TimeSpan.FromSeconds(900));

// ✅ Đúng: constant + đúng key format + NX flag (atomic)
private const int ReservationTtlSeconds = 900;
var key = new RedisKey($"marketnest:cart:{userId}:reservation:{variantId}");
await db.StringSetAsync(key, value,
    expiry: TimeSpan.FromSeconds(ReservationTtlSeconds),
    when: When.NotExists); // atomic NX
```

**Luôn dùng Lua script cho check-then-set:**
```csharp
// ✅ Atomic reservation (đã implement đúng trong architecture-requirements.md)
var script = LuaScript.Prepare(@"
    local current = redis.call('GET', @key)
    if current then
        redis.call('SET', @key, @value, 'EX', @ttl)
        return 1
    else
        return redis.call('SET', @key, @value, 'EX', @ttl, 'NX')
    end");
await db.ScriptEvaluateAsync(script, new { key, value, ttl = ReservationTtlSeconds });
```

#### Kiểm tra Redis key scan (nếu connect được)
```bash
docker exec -it $(docker ps -q -f name=redis) redis-cli

# Kiểm tra các key có TTL không
redis-cli --scan --pattern "marketnest:*" | head -20

# Kiểm tra TTL của từng loại key
redis-cli TTL "marketnest:cart:USER_ID:reservation:VARIANT_ID"
redis-cli TTL "marketnest:refresh:TOKEN_ID"

# Tìm key không có TTL (-1 = persist, -2 = not exist)
redis-cli --scan --pattern "marketnest:*" | while read key; do
    ttl=$(redis-cli TTL "$key")
    if [ "$ttl" = "-1" ]; then echo "NO TTL: $key"; fi
done
```

---

### 2.6 EF Core Best Practices — Nâng cao

#### Kiểm tra SaveChanges interceptor cho domain events
```bash
grep -rn "ISaveChangesInterceptor\|SaveChangesInterceptor\|SavingChangesAsync" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -10
```

#### Kiểm tra Connection Resiliency
```bash
grep -rn "EnableRetryOnFailure\|UseNpgsql" src/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -10
```

```csharp
// ✅ Phải có EnableRetryOnFailure
builder.Services.AddDbContext<MarketNestDbContext>(opt =>
    opt.UseNpgsql(connectionString, npgsql =>
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null))
       .UseSnakeCaseNamingConvention());  // ✅ snake_case cho PostgreSQL
```

#### Kiểm tra snake_case naming convention
```bash
# PostgreSQL convention: snake_case column names
grep -rn "UseSnakeCaseNamingConvention\|EFCore.NamingConventions" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -5
```

#### Kiểm tra compiled queries cho hot path
```bash
# Các query chạy nhiều lần nên dùng EF.CompileAsyncQuery
grep -rn "EF\.CompileAsyncQuery\|EF\.CompileQuery" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -10
```

```csharp
// ✅ Compiled query cho product detail (hot path)
private static readonly Func<MarketNestDbContext, Guid, Task<ProductDetailDto?>> GetProductByIdQuery =
    EF.CompileAsyncQuery((MarketNestDbContext db, Guid id) =>
        db.Products
          .AsNoTracking()
          .Where(p => p.Id == id && !p.IsDeleted)
          .Select(p => new ProductDetailDto(...))
          .FirstOrDefault());
```

---

## Phase 3: REPORT — Báo cáo database review

Tạo báo cáo theo format sau:

```markdown
# Database Review Report — MarketNest
**Date**: <ngày>
**Scope**: EF Core migrations, PostgreSQL index, N+1 queries, Redis TTL, schema isolation

---

## Tổng quan

| Hạng mục | Điểm (1–10) | Vấn đề |
|---|---|---|
| Migration quality | X/10 | X findings |
| Schema-per-module isolation | X/10 | X findings |
| N+1 detection | X/10 | X findings |
| Index coverage | X/10 | X missing indexes |
| Redis key/TTL | X/10 | X findings |
| EF Core best practices | X/10 | X findings |

---

## 🔴 CRITICAL

1. **[Tên vấn đề]** — `path/to/file.cs:line`
   - Mô tả: ...
   - Impact: Mỗi request tạo N query thêm / full table scan / memory leak
   - Fix: ...

---

## 🟡 HIGH

...

---

## 🟢 MEDIUM / Quick wins

...

---

## Migration Index cần tạo

```csharp
// Migration: YYYYMMDD_HHmm_AddMissingIndexes.cs
migrationBuilder.CreateIndex(
    name: "IX_orders_buyer_id_status",
    schema: "orders",
    table: "orders",
    columns: new[] { "buyer_id", "status" });
```

---

## Redis key audit

| Key pattern | TTL hiện tại | TTL đúng | Vấn đề |
|---|---|---|---|
| marketnest:cart:* | persist | 900s | CRITICAL: memory leak |
```

---

## Phase 4: FIX — Thực thi thay đổi

**Quy tắc bắt buộc:**
1. Hỏi xác nhận trước khi sửa entity configuration hoặc tạo migration mới
2. Tạo migration mới cho mỗi thay đổi index/schema — không sửa migration cũ
3. Không sửa file `*.Designer.cs`
4. Sau khi thêm index, migration phải có `Down()` tương ứng (`DropIndex`)
5. Một migration = một thay đổi có chủ đề rõ ràng

### Template tạo migration mới (thêm index)

```bash
# Tạo migration thêm missing indexes
cd src/MarketNest.Web
dotnet ef migrations add 20260425_1430_AddMissingIndexOrdersBuyer \
  --project ../MarketNest.Infrastructure \
  --startup-project . \
  --output-dir ../MarketNest.Infrastructure/Migrations

# Kiểm tra migration trước khi apply
dotnet ef migrations script --idempotent
```

---

## Phase 5: VERIFY — Kiểm tra sau sửa

```bash
# Build sạch
dotnet build --no-incremental

# Chạy migration trên test DB
dotnet ef database update --connection "Host=localhost;..."

# Chạy architecture tests (kiểm tra layer rules)
dotnet test tests/MarketNest.ArchitectureTests --no-build

# Chạy integration tests (Testcontainers sẽ apply migration tự động)
dotnet test tests/MarketNest.IntegrationTests --no-build

# Verify index trong PostgreSQL
docker exec -it $(docker ps -q -f name=postgres) \
  psql -U mn -d marketnest -c "
    SELECT schemaname, tablename, indexname, indexdef
    FROM pg_indexes
    WHERE schemaname IN ('catalog','orders','identity','payments','reviews','disputes')
    ORDER BY schemaname, tablename, indexname;"
```

---

## Tài liệu tham khảo nhanh

| Vấn đề | EF Core API |
|---|---|
| No tracking read | `.AsNoTracking()` |
| Cartesian explosion | `.AsSplitQuery()` |
| Hot path query | `EF.CompileAsyncQuery()` |
| Soft delete filter | `.HasQueryFilter(e => !e.IsDeleted)` |
| Money column | `.HasPrecision(18, 2)` |
| snake_case PG | `UseSnakeCaseNamingConvention()` |
| Retry on fail | `EnableRetryOnFailure(3, 5s, null)` |
| Partial index | `.HasFilter("condition")` |
| GIN full-text | `.HasMethod("GIN")` |
| Composite index | `HasIndex(e => new { e.A, e.B })` |

| Redis pattern | StackExchange.Redis API |
|---|---|
| Atomic set-if-not-exists | `StringSetAsync(key, val, ttl, When.NotExists)` |
| Atomic check-then-set | `ScriptEvaluateAsync(luaScript, ...)` |
| Renew TTL | `KeyExpireAsync(key, ttl)` |
| Key exists check | `KeyExistsAsync(key)` |
| Scan keys | `server.Keys(pattern: "marketnest:cart:*")` |
