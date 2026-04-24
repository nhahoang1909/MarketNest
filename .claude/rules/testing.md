# Testing Rules

Source of truth: `docs/architecture-requirements.md` Section 9, `docs/code-rules.md` Section 10

## Test Layers & Tools

| Layer | Tool | Target |
|-------|------|--------|
| Unit | xUnit + FluentAssertions | Domain logic: 80%+ |
| Integration | Testcontainers + WebApplicationFactory | APIs + DB: key happy paths |
| Architecture | NetArchTest | Layer rules — must stay green |
| Contract (Phase 3+) | Pact.io | Service boundaries |
| Load (Phase 2) | k6 | Baseline benchmarks |
| E2E (Phase 2) | Playwright | Critical user flows |

## Unit Tests — Domain & Handlers

Test domain logic and command handlers in isolation. No containers, no HTTP.

```csharp
// Test aggregates directly
var order = Order.Create(buyerId, cartSnapshot);
var result = order.MarkAsShipped("TRACK123");
result.IsSuccess.Should().BeTrue();
order.Status.Should().Be(OrderStatus.Shipped);

// Test domain events raised
order.DomainEvents.Should().ContainSingle()
    .Which.Should().BeOfType<OrderShippedEvent>();
```

## Integration Tests — Testcontainers

```csharp
// Spin up real PostgreSQL + Redis per test collection
public class OrderIntegrationTests : IClassFixture<MarketNestWebApplicationFactory>
{
    // Tests hit real DB — no mocking of repositories or DbContext
    // Reset DB state between tests (Respawn or transaction rollback)
}
```

## Architecture Tests — NetArchTest

These must pass on every commit. Examples:

```csharp
// Domain has no infrastructure references
Types.InAssembly(domainAssembly)
     .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
     .GetResult().IsSuccessful.Should().BeTrue();

// Web layer never calls repositories directly
Types.InAssembly(webAssembly)
     .That().ResideInNamespace("MarketNest.Web.Pages")
     .ShouldNot().HaveDependencyOn("MarketNest.*.Infrastructure")
     .GetResult().IsSuccessful.Should().BeTrue();
```

## PR Gate Checklist

Before merging, verify:
- [ ] Architecture tests pass (`dotnet test --filter Category=Architecture`)
- [ ] All commands validated by FluentValidation
- [ ] No raw SQL strings (Dapper uses parameters)
- [ ] No public setters on aggregates
- [ ] No `async void` (except event handlers)
- [ ] All `CancellationToken` parameters propagated
- [ ] New business rules have unit tests
- [ ] New API endpoints have integration tests
- [ ] No secrets in code or config files
- [ ] Logging uses structured templates (no interpolation)
