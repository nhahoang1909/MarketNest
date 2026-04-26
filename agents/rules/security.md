# Security Rules

Source of truth: `docs/architecture-requirements.md` Section 8, `docs/code-rules.md`

## Defense-in-Depth Layers

| Layer | Implementation |
|-------|---------------|
| Transport | HTTPS-only, HSTS, TLS 1.3 |
| Auth | JWT (short-lived) + Refresh Token (Redis-backed, revocable) |
| Authorization | RBAC via `IAuthorizationHandler` + Policy-based |
| Input | EF Core parameterized queries, Razor auto-escaping, CSP headers |
| Rate limiting | ASP.NET Core `RateLimiter` middleware |
| Secrets | User Secrets (dev) → Azure Key Vault / HashiCorp Vault (prod) |

## Auth Pattern

```csharp
// JWT: short-lived access token
// Refresh token: stored in Redis with TTL 7d, revocable via blacklist

// Redis key namespaces:
marketnest:refresh:{tokenId}     TTL: 7d
marketnest:blacklist:{tokenId}   TTL: 7d  // revoked tokens
marketnest:ratelimit:{userId}:{endpoint}  TTL: 1min
```

On logout or password change: add token to `blacklist:*` key to invalidate before expiry.

## SQL Injection Prevention

```csharp
// ✅ Always EF Core parameterized queries or Dapper with parameters
var orders = await db.Orders.Where(o => o.BuyerId == buyerId).ToListAsync(ct);

// Dapper — explicit parameters only
var result = await conn.QueryAsync<OrderDto>(
    "SELECT * FROM orders.orders WHERE buyer_id = @BuyerId",
    new { BuyerId = buyerId });

// ❌ Never string-concatenated SQL
$"SELECT * FROM orders WHERE buyer_id = '{buyerId}'"
```

## XSS Prevention

Razor auto-escapes all `@variable` output — never use `@Html.Raw()` on user-supplied content.

CSP headers configured in middleware — block inline scripts except Alpine.js and HTMX bootstrap.

## Secrets Management

```
Dev:  dotnet user-secrets (never appsettings.json)
Prod: Azure Key Vault or HashiCorp Vault
CI:   GitHub Actions secrets (never hardcoded in workflow YAML)

❌ Never commit: connection strings, JWT secret keys, API keys, SMTP passwords
❌ Never log: passwords, tokens, credit card numbers, full PII (user ID is OK)
```

## Rate Limiting

```csharp
// Applied at middleware level via ASP.NET Core RateLimiter
// Key per: userId + endpoint
// Redis key: marketnest:ratelimit:{userId}:{endpoint}   TTL: 1min
```

Endpoints requiring rate limiting at minimum: login, register, checkout, review submission.

## Input Validation

Validation happens at the API boundary only — FluentValidation pipeline behavior runs before every command handler. Domain layer trusts that input has already been validated.

```csharp
// ❌ Don't re-validate inside domain methods what FluentValidation already checks
// ✅ Domain throws DomainException only for invariant violations (not for user input errors)
```
