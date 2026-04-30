---
name: test-quality-check
description: >
  Scan the entire MarketNest test suite to review quality: xUnit convention, FluentAssertions
  usage, test naming (Given_When_Then), Testcontainers setup, NSubstitute mock/stub best practices,
  test isolation, missing edge cases per domain invariants, and NetArchTest architecture rules.
  Use this skill when the user wants to: review test quality, check test convention,
  find missing test cases, check test isolation, review mock setup, audit architecture tests,
  or says anything like "review test", "test quality", "missing test", "test convention",
  "FluentAssertions", "Testcontainers setup", "mock best practice", "architecture test",
  "NetArchTest", "test isolation", "Given When Then".
  Activate when the user uploads *Tests.cs, *Spec.cs, WebAppFactory.cs files or asks about coverage.
compatibility:
  tools: [bash, read_file, write_file, list_files, grep_search, run_in_terminal]
  agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
  stack: [.NET 10, xUnit, FluentAssertions, NSubstitute, Testcontainers, NetArchTest, Respawn]
---

# Test Quality Check Skill — MarketNest

Skill này review toàn bộ 3 test projects của MarketNest:
- `MarketNest.UnitTests` — domain logic + application handlers
- `MarketNest.IntegrationTests` — Testcontainers + WebApplicationFactory
- `MarketNest.ArchitectureTests` — NetArchTest layer rules

Output: báo cáo phân loại **BLOCKER / HIGH / MEDIUM / SUGGESTION** với code fix sẵn sàng.

---

## Test Pyramid của MarketNest

```
         ▲  E2E (Playwright) — Phase 2+
        ▲▲▲ Integration (Testcontainers) — key API flows
       ▲▲▲▲▲ Unit (xUnit + NSubstitute) — domain logic, 80%+ coverage
      ▲▲▲▲▲▲▲ Architecture (NetArchTest) — layer rules, runs in < 5s
```

```
tests/
├── MarketNest.UnitTests/
│   ├── Domain/     ← Aggregate methods, Value Objects, domain rules
│   └── Application/← Command/Query handlers (mocked deps via NSubstitute)
├── MarketNest.IntegrationTests/
│   ├── Fixtures/   ← WebApplicationFactory, Testcontainers, Respawn
│   └── Modules/    ← Per-module API flow tests
└── MarketNest.ArchitectureTests/
    └── *.cs        ← NetArchTest rules
```

---

## Quy trình thực thi

```
Phase 1: SCAN    → Thu thập inventory, metrics cơ bản
Phase 2: ANALYZE → 7 rule groups kiểm tra chất lượng
Phase 3: REPORT  → Phân loại findings + missing coverage map
Phase 4: FIX     → Code fix + test scaffolds sẵn sàng
Phase 5: VERIFY  → Chạy test suite, check green
```

---

## Phase 1: SCAN — Thu thập test inventory

### 1.1 Thống kê tổng quan

```bash
# Đếm test methods theo project
echo "=== Unit Tests ==="
find tests/MarketNest.UnitTests/ -name "*.cs" -not -path "*/bin/*" \
  | xargs grep -c "\[Fact\]\|\[Theory\]" 2>/dev/null | awk -F: '{sum+=$2} END{print "Total: " sum " tests"}'

echo "=== Integration Tests ==="
find tests/MarketNest.IntegrationTests/ -name "*.cs" -not -path "*/bin/*" \
  | xargs grep -c "\[Fact\]\|\[Theory\]" 2>/dev/null | awk -F: '{sum+=$2} END{print "Total: " sum " tests"}'

echo "=== Architecture Tests ==="
find tests/MarketNest.ArchitectureTests/ -name "*.cs" -not -path "*/bin/*" \
  | xargs grep -c "\[Fact\]\|\[Theory\]" 2>/dev/null | awk -F: '{sum+=$2} END{print "Total: " sum " tests"}'

# Top 10 test files lớn nhất
find tests/ -name "*.cs" -not -path "*/bin/*" \
  | xargs wc -l 2>/dev/null | sort -rn | head -10

# Danh sách aggregate được test
find tests/MarketNest.UnitTests/Domain/ -name "*.cs" -not -path "*/bin/*" | sort
```

### 1.2 Coverage gap — so sánh domain vs test

```bash
# Domain aggregates tồn tại
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" \
  | xargs grep -l "AggregateRoot" | xargs basename -s .cs | sort

# Domain aggregates có unit test
find tests/MarketNest.UnitTests/Domain/ -name "*.cs" -not -path "*/bin/*" \
  | xargs basename -s Tests.cs 2>/dev/null | sort

# So sánh: diff của 2 list trên = aggregates chưa có unit test

# Domain invariants cần test (từ domain-design)
# Order: 9 state transitions, 10 invariants
# Cart: max 20 items, max qty 99, cannot-add-to-checkedout
# Review: gate check, 24h immutable, one vote per buyer
# Dispute: max 1 per order, 3-day window, 72h seller deadline

echo "=== Handler coverage ==="
# Commands có handler test chưa
find src/ -name "*CommandHandler.cs" -not -path "*/bin/*" | xargs basename -s .cs | sort > /tmp/handlers.txt
find tests/MarketNest.UnitTests/Application/ -name "*Tests.cs" -not -path "*/bin/*" \
  | xargs basename -s Tests.cs | sort > /tmp/handler_tests.txt
comm -23 /tmp/handlers.txt /tmp/handler_tests.txt
```

---

## Phase 2: ANALYZE — 7 Rule Groups

---

### Rule Group 1: Test Naming Convention — Given_When_Then

**Convention chuẩn MarketNest:**
```
[Fact] void MethodOrScenario_GivenContext_ExpectedOutcome()
[Theory] void MethodOrScenario_GivenContext_ExpectedOutcome(params)

Ví dụ đúng:
  Order_MarkAsShipped_GivenOrderConfirmed_SetsStatusToShipped()
  Order_MarkAsShipped_GivenOrderPending_ReturnsInvalidTransitionError()
  Cart_AddItem_GivenCartHas20Items_ReturnsMaxItemsError()
  PlaceOrderCommand_Handle_GivenValidCart_CreatesOrderAndPublishesEvent()

Ví dụ sai:
  TestShipOrder()
  ShouldShipOrder()
  MarkAsShipped_Works()
  Test1()
```

```bash
# 1A. Tìm test method không theo convention
echo "=== Test methods not following Given_When_Then convention ==="
find tests/ -name "*.cs" -not -path "*/bin/*" | xargs grep -n "public.*void\|public.*Task" \
  | grep "\[Fact\]\|\[Theory\]" | grep -v "bin/\|obj/" | head -5  # warm-up

find tests/ -name "*.cs" -not -path "*/bin/*" | while read f; do
    grep -n "public.*void \|public.*Task " "$f" | while read line; do
        method=$(echo "$line" | grep -oP "(?<=void |Task )\w+")
        # Check: must contain _ separators (Given_When_Then needs at least 2 underscores)
        underscore_count=$(echo "$method" | tr -cd '_' | wc -c)
        if [ "$underscore_count" -lt 2 ] && [ -n "$method" ]; then
            # Exclude non-test helpers: Setup, Dispose, Build, Create, etc.
            if ! echo "$method" | grep -qE "^(Setup|Dispose|Build|Create|Get|Init|Configure|Register)"; then
                echo "⚠️  $f — $method"
            fi
        fi
    done
done | head -30

# 1B. Tìm test class không có suffix Tests hoặc Specs
echo "=== Test classes not ending in 'Tests' ==="
find tests/ -name "*.cs" -not -path "*/bin/*" | xargs grep -l "\[Fact\]\|\[Theory\]" \
  | while read f; do
    classname=$(grep -oP "public class \K\w+" "$f" | head -1)
    if ! echo "$classname" | grep -qE "Tests$|Specs$|Fixtures$|Factory$|Builder$|Base$"; then
        echo "⚠️  $f — class '$classname' should end in 'Tests'"
    fi
done

# 1C. Tìm display name thiếu trên Theory data
echo "=== [Theory] missing descriptive MemberData/InlineData labels ==="
grep -rn "\[Theory\]\|\[InlineData\]" tests/ --include="*.cs" \
  | grep "InlineData(null\|InlineData(0\|InlineData(1\|InlineData(-1" \
  | grep -v "bin/\|obj/" | head -10
```

**Fix pattern:**
```csharp
// ❌ Sai: không rõ context và expected outcome
[Fact]
public void TestShipOrder() { }

[Fact]
public void ShouldWork() { }

// ✅ Đúng: Given_When_Then rõ ràng
[Fact]
public void Order_MarkAsShipped_GivenConfirmedOrder_SetsStatusShippedAndRaisesEvent()
{
    // Arrange
    var order = new OrderBuilder().BuildConfirmed();

    // Act
    var result = order.MarkAsShipped("TRACK-001");

    // Assert
    result.IsSuccess.Should().BeTrue();
    order.Status.Should().Be(OrderStatus.Shipped);
    order.ShippedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    order.DomainEvents.Should().ContainSingle()
         .Which.Should().BeOfType<OrderShippedEvent>()
         .Which.TrackingNumber.Should().Be("TRACK-001");
}

// ✅ Theory với descriptive display name
[Theory]
[InlineData(OrderStatus.Pending,     false, "Cannot ship from Pending")]
[InlineData(OrderStatus.Processing,  false, "Cannot ship from Processing")]
[InlineData(OrderStatus.Shipped,     false, "Cannot ship already Shipped")]
[InlineData(OrderStatus.Cancelled,   false, "Cannot ship Cancelled")]
[InlineData(OrderStatus.Confirmed,   true,  "Can ship from Confirmed")]
public void Order_MarkAsShipped_GivenStatus_ReturnsExpectedResult(
    OrderStatus fromStatus, bool shouldSucceed, string reason)
{
    var order = OrderBuilder.InStatus(fromStatus);
    var result = order.MarkAsShipped("TRACK-001");
    result.IsSuccess.Should().Be(shouldSucceed, reason);
}
```

---

### Rule Group 2: FluentAssertions Usage

**Quy tắc**: Không dùng `Assert.*` của xUnit. Không dùng `.Equals()` hay `==` trong assertion. Dùng FluentAssertions đầy đủ — ngữ nghĩa rõ ràng, error message giải thích được.

```bash
# 2A. Tìm Assert.* của xUnit (nên thay bằng FluentAssertions)
echo "=== Raw xUnit Assert.* usage (replace with FluentAssertions) ==="
grep -rn "Assert\.Equal\|Assert\.True\|Assert\.False\|Assert\.Null\|Assert\.NotNull\|Assert\.Throws\|Assert\.IsType" \
  tests/ --include="*.cs" | grep -v "bin/\|obj/" | head -20

# 2B. Tìm direct equality không dùng Should()
echo "=== Direct equality without Should() ==="
grep -rn "\bvar result\b.*==\|\bactual\b.*==" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/\|//\|Should()" | head -10

# 2C. Tìm Should().Be(true) / Should().Be(false) — quá generic
echo "=== Vague Should().Be(true/false) assertions ==="
grep -rn "\.Should()\.Be(true\|\.Should()\.Be(false" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -20
# Nên dùng: .Should().BeTrue(because: "reason") hoặc .IsSuccess.Should().BeTrue()

# 2D. Tìm exception test không dùng FluentAssertions Invoking pattern
echo "=== Exception tests not using FluentAssertions pattern ==="
grep -rn "\[ExpectedException\]\|Assert\.Throws\b" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -10

# 2E. Tìm null check không descriptive
echo "=== Non-descriptive null assertions ==="
grep -rn "\.Should()\.NotBeNull()\|\.Should()\.BeNull()" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -10
# Tốt hơn nên có: .Should().NotBeNull("because order was just created")

# 2F. Tìm collection assertion thiếu specificity
echo "=== Vague collection assertions ==="
grep -rn "\.Count()\.Should()\.Be\|\.Count\.Should()\.Be" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -10
# Nên dùng: .Should().HaveCount(n) hoặc .Should().ContainSingle()
```

**Fix patterns — FluentAssertions cheat sheet cho MarketNest:**

```csharp
// ─── Result<T, Error> assertions ───────────────────────────────────────────

// ❌ Vague
Assert.True(result.IsSuccess);
result.IsSuccess.Should().Be(true);

// ✅ Descriptive
result.IsSuccess.Should().BeTrue(because: "valid cart should produce an order");
result.IsFailure.Should().BeTrue();
result.Error.Code.Should().Be("ORDER.INVALID_TRANSITION");
result.Error.Type.Should().Be(ErrorType.Conflict);
result.Value.Should().NotBeNull();

// ─── Domain event assertions ────────────────────────────────────────────────

// ❌ Weak
order.DomainEvents.Count.Should().Be(1);

// ✅ Strong
order.DomainEvents.Should().ContainSingle(
    because: "shipping should raise exactly one event")
    .Which.Should().BeOfType<OrderShippedEvent>()
    .Which.Should().Match<OrderShippedEvent>(e =>
        e.OrderId == order.Id &&
        e.TrackingNumber == "TRACK-001");

// ─── Exception assertions ───────────────────────────────────────────────────

// ❌ Old-style
Assert.Throws<DomainException>(() => new Rating(6));

// ✅ FluentAssertions Invoking
Action act = () => new Rating(6);
act.Should().Throw<DomainException>()
   .WithMessage("*between 1 and 5*");

// Or for async
Func<Task> act = () => handler.Handle(invalidCommand, ct);
await act.Should().ThrowAsync<ValidationException>();

// ─── Collection assertions ──────────────────────────────────────────────────

// ❌
items.Count().Should().Be(1);
items.Any(i => i.VariantId == variantId).Should().Be(true);

// ✅
items.Should().HaveCount(1);
items.Should().ContainSingle(i => i.VariantId == variantId);
items.Should().BeInAscendingOrder(i => i.CreatedAt);
items.Should().AllSatisfy(i => i.Status.Should().Be(OrderStatus.Active));

// ─── Money / Value Object assertions ───────────────────────────────────────

// ❌ Comparing wrong
order.Total.Should().Be(Money.Of(100m)); // only works if Equals() overridden

// ✅ Explicit property comparison
order.Total.Amount.Should().Be(100m);
order.Total.Currency.Should().Be("SGD");

// ─── Time assertions ─────────────────────────────────────────────────────────

// ❌ Flaky: exact time comparison
order.ShippedAt.Should().Be(DateTime.UtcNow);

// ✅ Tolerant time assertion
order.ShippedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
order.CompletedAt.Should().BeAfter(order.ShippedAt!.Value);
order.PlacedAt.Should().BeBefore(DateTime.UtcNow);

// ─── HTTP response assertions (Integration) ─────────────────────────────────

// ❌
Assert.Equal(HttpStatusCode.Created, response.StatusCode);

// ✅
response.StatusCode.Should().Be(HttpStatusCode.Created);
response.Headers.Location.Should().NotBeNull();
var body = await response.Content.ReadFromJsonAsync<CartItemCreatedDto>();
body.Should().NotBeNull();
body!.Id.Should().NotBeEmpty();
```

---

### Rule Group 3: NSubstitute Mock/Stub Best Practices

**Quy tắc**: Dùng NSubstitute, không Moq. Interface-based mocking. Không mock what you don't own (BCL types, EF Core). Verify interactions chỉ khi meaningful.

```bash
# 3A. Tìm Moq usage (MarketNest dùng NSubstitute)
echo "=== Moq usage (should use NSubstitute instead) ==="
grep -rn "using Moq\|new Mock<\|\.Setup(\|\.Verify(" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -10

# 3B. Tìm mock concrete class (nên mock interface)
echo "=== Mocking concrete classes ==="
grep -rn "Substitute\.For<[A-Z][a-z]" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" \
  | grep -v "Substitute\.For<I" | head -10  # Filter out interfaces (start with I)

# 3C. Tìm over-verification (verify tất cả calls — test brittleness)
echo "=== Over-verification: Received() on everything ==="
grep -rn "\.Received()\." tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | wc -l
# Nếu > 50% test methods đều có Received() → over-specified

# 3D. Tìm test setup quá phức tạp (mock nhiều hơn 3 deps trong 1 test)
echo "=== Tests with too many substitutes (> 3 = test smell) ==="
find tests/MarketNest.UnitTests/ -name "*.cs" -not -path "*/bin/*" | while read f; do
    count=$(grep -c "Substitute\.For\b" "$f" 2>/dev/null || echo 0)
    if [ "$count" -gt 6 ]; then
        echo "⚠️  $f has $count substitutes — too many dependencies?"
    fi
done

# 3E. Tìm test không configure substitute nhưng dùng return value
echo "=== Unconfigured substitute return values ==="
grep -rn "\.Returns\b\|\.ReturnsNull\b\|\.Throws\b" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -20
```

**Fix patterns — NSubstitute chuẩn:**

```csharp
// ─── Setup chuẩn cho Command Handler unit test ──────────────────────────────

public class PlaceOrderCommandHandlerTests
{
    // ✅ Fields: private readonly substitutes
    private readonly IOrderRepository     _orderRepo     = Substitute.For<IOrderRepository>();
    private readonly ICartReservationService _reservation = Substitute.For<ICartReservationService>();
    private readonly IInventoryService    _inventory     = Substitute.For<IInventoryService>();
    private readonly PlaceOrderCommandHandler _sut;

    public PlaceOrderCommandHandlerTests()
    {
        // ✅ SUT created once, substitutes configured per test
        _sut = new PlaceOrderCommandHandler(_orderRepo, _reservation, _inventory);
    }

    [Fact]
    public async Task Handle_GivenValidCart_CreatesOrderAndSavesChanges()
    {
        // Arrange — configure substitutes for this specific test
        var command = new PlaceOrderCommandBuilder().Build();
        _reservation.IsReservedAsync(command.CartId).Returns(true);
        _inventory.ReserveAsync(Arg.Any<IEnumerable<CartItemSnapshot>>())
                  .Returns(Result.Success());

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — Result
        result.IsSuccess.Should().BeTrue();

        // Assert — side effects (only verify what MATTERS for this test)
        _orderRepo.Received(1).Add(Arg.Is<Order>(o =>
            o.BuyerId == command.BuyerId &&
            o.Status == OrderStatus.Pending));
        await _orderRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GivenReservationExpired_ReturnsCartReservationExpiredError()
    {
        // Arrange
        var command = new PlaceOrderCommandBuilder().Build();
        _reservation.IsReservedAsync(command.CartId).Returns(false); // expired!

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — failure path
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CART.RESERVATION_EXPIRED");

        // Assert — no order created
        _orderRepo.DidNotReceive().Add(Arg.Any<Order>());
        await _orderRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

// ❌ Anti-pattern: test constructor doing real work
public class BadHandlerTests
{
    private readonly Mock<IOrderRepository> _mockRepo = new(); // Moq!
    private readonly PlaceOrderCommandHandler _sut;

    public BadHandlerTests()
    {
        // ❌ Complex setup in constructor → hard to see what each test needs
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Order?)null);
        _sut = new PlaceOrderCommandHandler(_mockRepo.Object);
    }
}
```

---

### Rule Group 4: Test Isolation

**Quy tắc**: Tests phải independent — bất kỳ thứ tự chạy nào đều phải pass. Không share mutable state. Integration tests phải reset DB sau mỗi test.

```bash
# 4A. Tìm static mutable state trong test class
echo "=== Static mutable state in test classes ==="
grep -rn "private static\|public static" tests/ --include="*.cs" \
  | grep -v "readonly\|const\|bin/\|obj/" | head -20

# 4B. Tìm test không reset DB (integration test phải dùng Respawn)
echo "=== Integration tests potentially not resetting DB ==="
find tests/MarketNest.IntegrationTests/ -name "*.cs" -not -path "*/bin/*" \
  | xargs grep -L "Respawn\|ResetAsync\|respawner\|Checkpoint" 2>/dev/null \
  | grep -v "Factory\|Builder\|Base\|bin/"

# 4C. Tìm test dùng DateTime.Now (flaky — phải dùng IDateTimeService)
echo "=== Tests using DateTime.Now/UtcNow directly (use IDateTimeService) ==="
grep -rn "DateTime\.Now\|DateTime\.UtcNow\|DateTimeOffset\.Now" \
  tests/ --include="*.cs" | grep -v "bin/\|obj/\|FakeDateTimeService" | head -15

# 4D. Tìm test share HttpClient state
echo "=== Integration tests modifying shared HttpClient headers ==="
grep -rn "DefaultRequestHeaders\." tests/MarketNest.IntegrationTests/ --include="*.cs" \
  | grep -v "bin/\|obj/\|Factory\|CreateClient" | head -10

# 4E. Tìm xUnit Collection fixture dùng sai cách
echo "=== Potentially misconfigured collection fixtures ==="
grep -rn "\[Collection\]\|\[CollectionDefinition\]" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -10
```

**Fix pattern — DB isolation với Respawn:**

```csharp
// tests/MarketNest.IntegrationTests/Fixtures/MarketNestWebAppFactory.cs
public class MarketNestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // ✅ One container per test collection — không start/stop mỗi test
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("marketnest_test")
        .WithUsername("mn")
        .WithPassword("mn_secret")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    // ✅ Respawn resets data without recreating schema (much faster than truncate)
    private Respawner _respawner = null!;
    private NpgsqlConnection _dbConnection = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        // Apply migrations once
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketNestDbContext>();
        await db.Database.MigrateAsync();

        // Configure Respawn — exclude migration history table
        _dbConnection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _dbConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["identity", "catalog", "orders", "payments", "reviews", "disputes"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    // ✅ Public method for each test to call in InitializeAsync
    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);
        // Also flush Redis test keys
        var redis = Services.GetRequiredService<IConnectionMultiplexer>();
        await redis.GetServer(redis.GetEndPoints().First()).FlushAllDatabasesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // ✅ Replace real services with test doubles
            services.RemoveAll<DbContextOptions<MarketNestDbContext>>();
            services.AddDbContext<MarketNestDbContext>(opt =>
                opt.UseNpgsql(_postgres.GetConnectionString())
                   .UseSnakeCaseNamingConvention());

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

            // ✅ Deterministic time
            services.RemoveAll<IDateTimeService>();
            services.AddSingleton<IDateTimeService>(
                new FakeDateTimeService(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)));

            // ✅ No-op email sender
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, NoOpEmailSender>();
        });
    }

    public async Task DisposeAsync()
    {
        await _dbConnection.DisposeAsync();
        await _postgres.StopAsync();
        await _redis.StopAsync();
    }
}

// ✅ Integration test base — ResetDatabase in IAsyncLifetime
public abstract class IntegrationTestBase(MarketNestWebAppFactory factory)
    : IClassFixture<MarketNestWebAppFactory>, IAsyncLifetime
{
    protected readonly HttpClient Http = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync(); // ✅ clean slate each test
        await SeedAsync();                  // ✅ each test seeds what it needs
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Override to seed test-specific data
    protected virtual Task SeedAsync() => Task.CompletedTask;

    protected async Task<HttpClient> AsAuthenticatedBuyerAsync(Guid? userId = null)
    {
        var client = factory.CreateClient();
        var token  = await GetTestJwtAsync("buyer@test.com", "Buyer", userId);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
```

---

### Rule Group 5: Missing Edge Cases — Domain-Specific

Đọc `domain-design.md` và `business-logic-requirements.md`, sau đó so sánh với tests hiện có.
Các invariant **bắt buộc phải có test** cho từng aggregate:

```bash
# 5A. Kiểm tra Order state machine — tất cả transition invalid phải được test
echo "=== Order state machine test coverage ==="
# Valid transitions: Pending→Confirmed, Confirmed→Processing, Processing→Shipped
#                   Shipped→Delivered, Delivered→Completed, Delivered→Disputed
# Invalid transitions: ALL other combinations
grep -rn "OrderStatus\.\|InvalidTransition\|MarkAsShipped\|MarkAsDelivered\|Complete\|Cancel" \
  tests/MarketNest.UnitTests/Domain/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -30

# 5B. Kiểm tra Cart business rules đã được test chưa
echo "=== Cart business rules test coverage ==="
grep -rn "MaxItems\|20.*items\|AddItem.*CheckedOut\|MergeQuantity\|PriceDrift" \
  tests/MarketNest.UnitTests/ --include="*.cs" | grep -v "bin/\|obj/"

# 5C. Kiểm tra Review gate check
echo "=== Review gate check tests ==="
grep -rn "ReviewGate\|COMPLETED.*order\|NotCompletedOrder\|duplicate.*review" \
  tests/MarketNest.UnitTests/ --include="*.cs" | grep -v "bin/\|obj/"

# 5D. Kiểm tra Dispute invariants
echo "=== Dispute invariant tests ==="
grep -rn "DisputeWindow\|3.*day\|72h\|MaxDispute\|one.*dispute\|dispute.*window" \
  tests/MarketNest.UnitTests/ --include="*.cs" | grep -v "bin/\|obj/"

# 5E. Kiểm tra Money/ValueObject boundary tests
echo "=== Value object boundary tests ==="
grep -rn "Money\.\|Rating.*[0-6]\|Sku.*empty\|StorefrontSlug" \
  tests/MarketNest.UnitTests/ --include="*.cs" | grep -v "bin/\|obj/"
```

**Edge case checklist bắt buộc — tạo test nếu thiếu:**

```csharp
// ════════════════════════════════════════════════════════════════
// ORDER AGGREGATE — Required Tests
// ════════════════════════════════════════════════════════════════

public class OrderStateMachineTests
{
    // ── Valid transitions (must succeed) ─────────────────────────
    [Fact] void Confirm_GivenPendingOrder_Succeeds() { }
    [Fact] void MarkAsShipped_GivenConfirmedOrder_Succeeds() { }
    [Fact] void MarkAsDelivered_GivenShippedOrder_Succeeds() { }
    [Fact] void Complete_GivenDeliveredOrder_Succeeds() { }
    [Fact] void Cancel_GivenConfirmedOrder_Succeeds() { }
    [Fact] void OpenDispute_GivenDeliveredOrderWithin3Days_Succeeds() { }

    // ── Invalid transitions (must return error, NOT throw) ───────
    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    void MarkAsShipped_GivenNonConfirmedStatus_ReturnsInvalidTransition(OrderStatus from) { }

    [Fact] void OpenDispute_GivenDeliveredOrderAfter3Days_ReturnsDisputeWindowClosed() { }
    [Fact] void OpenDispute_GivenAlreadyDisputedOrder_ReturnsMaxOneDisputeError() { }

    // ── Domain events ─────────────────────────────────────────────
    [Fact] void MarkAsShipped_GivenConfirmedOrder_RaisesOrderShippedEvent() { }
    [Fact] void Complete_GivenDeliveredOrder_RaisesOrderCompletedEvent() { }
    [Fact] void Cancel_GivenConfirmedOrder_RaisesOrderCancelledEvent() { }

    // ── Invariants ────────────────────────────────────────────────
    [Fact] void Create_GivenEmptyCart_ReturnsError() { }
    [Fact] void Total_AfterConfirmed_IsImmutable() { }
}

// ════════════════════════════════════════════════════════════════
// CART AGGREGATE — Required Tests
// ════════════════════════════════════════════════════════════════

public class CartTests
{
    [Fact] void AddItem_GivenNewVariant_AddsToCart() { }
    [Fact] void AddItem_GivenExistingVariant_MergesQuantity() { }
    [Fact] void AddItem_Given20ItemsAlready_ReturnsMaxItemsError() { }
    [Fact] void AddItem_GivenQuantityOver99_ReturnsMaxQtyError() { }
    [Fact] void AddItem_GivenCheckedOutCart_ReturnsError() { }
    [Fact] void AddItem_GivenAbandonedCart_ReturnsError() { }
    [Fact] void RemoveItem_GivenNonExistentItem_ReturnsNotFoundError() { }
    [Fact] void Checkout_GivenActiveCart_SetsStatusCheckedOutAndRaisesEvent() { }
    [Fact] void AddItem_SnapshotPriceIsSetAtTimeOfAdd_NotCurrentPrice() { }
}

// ════════════════════════════════════════════════════════════════
// VALUE OBJECTS — Required Tests
// ════════════════════════════════════════════════════════════════

public class MoneyTests
{
    [Fact] void Money_GivenNegativeAmount_ThrowsDomainException() { }
    [Fact] void Money_GivenZeroAmount_ThrowsDomainException() { }
    [Fact] void Money_Add_GivenDifferentCurrencies_ThrowsDomainException() { }
    [Fact] void Money_Equality_GivenSameAmountAndCurrency_AreEqual() { }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    void Rating_GivenOutOfRange_ThrowsDomainException(int value) { }
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    void Rating_GivenValidValue_CreatesSuccessfully(int value) { }
}

// ════════════════════════════════════════════════════════════════
// REVIEW AGGREGATE — Required Tests
// ════════════════════════════════════════════════════════════════

public class ReviewTests
{
    [Fact] void Submit_GivenBuyerWithCompletedOrder_Succeeds() { }
    [Fact] void Submit_GivenBuyerWithPendingOrder_ReturnsGateError() { }
    [Fact] void Submit_GivenDuplicateReview_ReturnsError() { }
    [Fact] void Edit_GivenWithin24Hours_Succeeds() { }
    [Fact] void Edit_GivenAfter24Hours_ReturnsImmutableError() { }
    [Fact] void AddSellerReply_GivenNoExistingReply_Succeeds() { }
    [Fact] void AddSellerReply_GivenExistingReply_ReturnsError() { }
    [Fact] void Vote_GivenSameBuyerVotingTwice_ReturnsError() { }
}
```

---

### Rule Group 6: Testcontainers Setup Quality

```bash
# 6A. Kiểm tra container image version pinned chưa
echo "=== Testcontainers without pinned image versions ==="
grep -rn "WithImage(" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" \
  | grep -v ":16\|:7\|:3\|:latest-alpine\|alpine\|[0-9]\.[0-9]" | head -10
# latest không chấp nhận được — phải pin version

# 6B. Tìm container khởi động trong constructor (slow — phải IAsyncLifetime)
echo "=== Containers started in constructor (use IAsyncLifetime) ==="
find tests/ -name "*.cs" -not -path "*/bin/*" | while read f; do
    if grep -q "PostgreSqlContainer\|RedisContainer" "$f"; then
        if ! grep -q "IAsyncLifetime\|InitializeAsync" "$f"; then
            echo "⚠️  $f — Container might not use IAsyncLifetime"
        fi
    fi
done

# 6C. Kiểm tra Respawn có được configure đúng không
echo "=== Respawn configuration ==="
grep -rn "Respawner\|RespawnerOptions\|ResetAsync" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -10

# 6D. Tìm test tạo nhiều container (nên share qua IClassFixture)
echo "=== Multiple container instances per test ==="
find tests/MarketNest.IntegrationTests/ -name "*.cs" -not -path "*/bin/*" | while read f; do
    count=$(grep -c "new PostgreSqlBuilder\|new RedisBuilder" "$f" 2>/dev/null || echo 0)
    if [ "$count" -gt 0 ] && ! grep -q "IClassFixture\|WebApplicationFactory" "$f"; then
        echo "⚠️  $f — Creating containers directly (use IClassFixture<MarketNestWebAppFactory>)"
    fi
done

# 6E. Kiểm tra migration được apply trong test setup
echo "=== Migration applied in test setup ==="
grep -rn "MigrateAsync\|EnsureCreated\|Database\.Migrate" tests/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -5
```

**Testcontainers setup chuẩn:**
```csharp
// ✅ Pinned versions, shared fixture, IAsyncLifetime
private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")    // ✅ pinned major version
    .WithDatabase("marketnest_test")
    .WithUsername("mn")
    .WithPassword("mn_secret")
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilCommandIsCompleted("pg_isready", "-U", "mn")) // ✅ health check
    .Build();

// ❌ Sai: latest tag, no health wait
private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
    .WithImage("postgres:latest")  // unpinned!
    .Build();                       // no wait strategy
```

---

### Rule Group 7: NetArchTest — Architecture Test Coverage

```bash
# 7A. Đọc toàn bộ ArchitectureTests project
find tests/MarketNest.ArchitectureTests/ -name "*.cs" -not -path "*/bin/*" | sort

# 7B. Kiểm tra các rule cơ bản đã có chưa
echo "=== Architecture rules coverage ==="
grep -rn "NotHaveDependencyOn\|HaveDependencyOn\|ResideInNamespace\|HaveNameEndingWith\|BeSealed\|BeAbstract" \
  tests/MarketNest.ArchitectureTests/ --include="*.cs" | grep -v "bin/\|obj/" | head -30

# 7C. Tìm test Assembly không được load
echo "=== Assemblies referenced in arch tests ==="
grep -rn "typeof(.*Assembly\|InAssembly\|Types\.In" \
  tests/MarketNest.ArchitectureTests/ --include="*.cs" | grep -v "bin/\|obj/" | head -20

# 7D. Kiểm tra test kết quả đúng cách (GetResult().IsSuccessful)
echo "=== Arch tests missing proper result assertion ==="
grep -rn "GetResult()" tests/MarketNest.ArchitectureTests/ --include="*.cs" \
  | grep -v "IsSuccessful\|bin/\|obj/" | head -10
```

**Full NetArchTest suite — tất cả rules cần có:**

```csharp
// tests/MarketNest.ArchitectureTests/LayerRulesTests.cs

public class LayerRulesTests
{
    // ── Assembly references ──────────────────────────────────────────────────
    private static readonly Assembly Core    = typeof(AggregateRoot).Assembly;
    private static readonly Assembly OrdersDomain = typeof(Order).Assembly;
    private static readonly Assembly CatalogDomain= typeof(Product).Assembly;
    private static readonly Assembly CartDomain   = typeof(Cart).Assembly;
    private static readonly Assembly Web          = typeof(Program).Assembly;

    // Helper: assert and surface failing type names
    private static void AssertRule(TestResult result, string ruleName)
    {
        result.IsSuccessful.Should().BeTrue(
            $"{ruleName} violated by: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // ── Rule 1: Domain → only Core ──────────────────────────────────────────
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore", "Domain must not reference EF Core")]
    [InlineData("StackExchange.Redis",           "Domain must not reference Redis")]
    [InlineData("MassTransit",                   "Domain must not reference MassTransit")]
    [InlineData("System.Net.Http",               "Domain must not reference HttpClient")]
    [InlineData("Microsoft.AspNetCore",          "Domain must not reference ASP.NET Core")]
    public void DomainLayer_MustNotDependOn_Infrastructure(string ns, string reason)
    {
        AssertRule(
            Types.InAssembly(OrdersDomain)
                 .Should().NotHaveDependencyOn(ns)
                 .GetResult(), reason);
    }

    // ── Rule 2: No cross-module references ───────────────────────────────────
    [Theory]
    [InlineData("MarketNest.Catalog", "Orders must not know about Catalog module")]
    [InlineData("MarketNest.Payments","Orders must not know about Payments module")]
    [InlineData("MarketNest.Identity","Orders must not know about Identity module")]
    [InlineData("MarketNest.Reviews", "Orders must not know about Reviews module")]
    public void OrdersModule_MustNotDependOn_OtherModules(string module, string reason)
    {
        AssertRule(
            Types.InAssembly(OrdersDomain)
                 .Should().NotHaveDependencyOn(module)
                 .GetResult(), reason);
    }

    // ── Rule 3: Domain events naming ─────────────────────────────────────────
    [Fact]
    public void DomainEvents_ShouldEndWith_Event()
    {
        AssertRule(
            Types.InAssemblies([OrdersDomain, CatalogDomain, CartDomain])
                 .That().ImplementInterface(typeof(IDomainEvent))
                 .Should().HaveNameEndingWith("Event")
                 .GetResult(),
            "Domain events must end with 'Event'");
    }

    // ── Rule 4: Aggregate roots naming ───────────────────────────────────────
    [Fact]
    public void CommandHandlers_ShouldEndWith_CommandHandler()
    {
        AssertRule(
            Types.InAssembly(OrdersDomain)
                 .That().ImplementInterface(typeof(ICommandHandler<,>))
                 .Should().HaveNameEndingWith("CommandHandler")
                 .GetResult(),
            "Command handlers must end with 'CommandHandler'");
    }

    // ── Rule 5: Web layer must not access Repositories directly ──────────────
    [Fact]
    public void WebLayer_MustNotDependOn_RepositoryImplementations()
    {
        AssertRule(
            Types.InAssembly(Web)
                 .That().ResideInNamespace("MarketNest.Web.Pages")
                 .Should().NotHaveDependencyOn("MarketNest.Infrastructure")
                 .GetResult(),
            "Razor Pages must use ISender, not repositories");
    }

    // ── Rule 6: Validators must be in Validators namespace ───────────────────
    [Fact]
    public void FluentValidators_ShouldResideIn_ValidatorsNamespace()
    {
        AssertRule(
            Types.InAssemblies([OrdersDomain])
                 .That().Inherit(typeof(AbstractValidator<>))
                 .Should().ResideInNamespaceContaining("Validators")
                 .GetResult(),
            "Validators must be in Validators namespace");
    }

    // ── Rule 7: Queries must not call SaveChanges ─────────────────────────────
    // (This is a code-smell check — can't fully enforce via NetArchTest,
    // but we can at least verify QueryHandlers don't depend on UoW)
    [Fact]
    public void QueryHandlers_ShouldNotDependOn_IUnitOfWork()
    {
        AssertRule(
            Types.InAssembly(OrdersDomain)
                 .That().HaveNameEndingWith("QueryHandler")
                 .Should().NotHaveDependencyOn("IUnitOfWork")
                 .GetResult(),
            "Query handlers must not depend on IUnitOfWork (read-only)");
    }

    // ── Rule 8: Value objects must be records (immutable) ────────────────────
    [Fact]
    public void ValueObjects_ShouldBeRecords_OrInheritValueObject()
    {
        AssertRule(
            Types.InAssemblies([OrdersDomain, CatalogDomain])
                 .That().ResideInNamespaceContaining("ValueObjects")
                 .Should().Inherit(typeof(ValueObject))
                 .GetResult(),
            "All types in ValueObjects namespace must inherit ValueObject base");
    }
}
```

---

## Phase 3: REPORT — Báo cáo chất lượng test

```markdown
# Test Quality Report — MarketNest
**Date**: <ngày>
**Test counts**: Unit: X | Integration: Y | Architecture: Z

---

## Dashboard

| Metric | Value | Target | Status |
|---|---|---|---|
| Unit test count | X | > 100 | ✅/⚠️ |
| Domain coverage | X% | > 80% | ✅/⚠️ |
| Arch test rules | X | > 12 | ✅/⚠️ |
| Naming violations | X | 0 | ✅/⚠️ |
| Isolation violations | X | 0 | ✅/⚠️ |
| Missing edge cases | X | 0 | ✅/⚠️ |

---

## 🔴 BLOCKER

### [B-001] Integration tests share DB state (no Respawn)
- `tests/MarketNest.IntegrationTests/Orders/PlaceOrderTests.cs`
- Fix: Add `IAsyncLifetime` + call `factory.ResetDatabaseAsync()` in `InitializeAsync()`

---

## 🟠 HIGH

### [H-001] Order state machine missing invalid transition tests
- All 9 invalid transitions (Pending→Shipped, Completed→Confirmed...) untested
- Fix: Add `OrderStateMachineTests` với `[Theory]` across all invalid combos

---

## 🟡 MEDIUM

### [M-001] 12 test methods using raw Assert.* instead of FluentAssertions
...

---

## 💡 SUGGESTION — Missing edge cases

### Review aggregate (0/7 edge cases covered)
- [ ] Review gate: buyer with pending order
- [ ] Review gate: duplicate review same product
- [ ] Edit: after 24h window
- [ ] SellerReply: second reply attempt
...
```

---

## Phase 5: VERIFY

```bash
# Chạy toàn bộ test suite
dotnet test --no-build --configuration Release \
  --logger "trx;LogFileName=test-results.trx" \
  -- RunConfiguration.MaxCpuCount=4

# Riêng từng project
dotnet test tests/MarketNest.ArchitectureTests --no-build  # nhanh nhất, chạy trước
dotnet test tests/MarketNest.UnitTests --no-build
dotnet test tests/MarketNest.IntegrationTests --no-build   # chậm nhất, cần Docker

# Coverage report (nếu có coverlet)
dotnet test tests/MarketNest.UnitTests \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

# Kiểm tra không còn Assert.* trong tests
grep -rn "Assert\.Equal\|Assert\.True\|Assert\.False" tests/ \
  --include="*.cs" | grep -v "bin/\|obj/" | wc -l
# Expect: 0

# Kiểm tra naming convention
find tests/ -name "*.cs" -not -path "*/bin/*" | xargs grep -c "public void\|public Task" \
  | awk -F: '{sum+=$2} END{print "Total test methods: " sum}'
```

---

## Quick Reference

| Anti-pattern | Fix |
|---|---|
| `Assert.Equal(a, b)` | `b.Should().Be(a)` |
| `Assert.True(x)` | `x.Should().BeTrue(because: "...")` |
| `Assert.Throws<Ex>(act)` | `act.Should().Throw<Ex>().WithMessage("...")` |
| `.Count.Should().Be(1)` | `.Should().ContainSingle()` |
| `.Count.Should().Be(0)` | `.Should().BeEmpty()` |
| `!= null assertion` | `.Should().NotBeNull(because: "...")` |
| `DateTime.Now` in test | `FakeDateTimeService` injected |
| No DB reset | `await factory.ResetDatabaseAsync()` in `InitializeAsync()` |
| `new Mock<T>()` | `Substitute.For<T>()` |
| `mock.Verify(...)` | `sub.Received(n).Method(...)` |
| `mock.Setup(...)` | `sub.Method().Returns(...)` |
| Unpinned image tag | `postgres:16-alpine` not `postgres:latest` |
| Test name `TestSomething()` | `Something_Given_When_Then()` |
