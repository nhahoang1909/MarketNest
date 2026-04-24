# Git Conventions

Source of truth: `docs/code-rules.md` Section 9

## Commit Format — Conventional Commits

```
<type>(<scope>): <description>

feat(orders): add dispute window expiry check
fix(cart): release reservation on TTL expiry
refactor(payments): extract commission calculation to value object
test(orders): add integration test for auto-complete job
docs(api): update order state machine diagram
chore(deps): upgrade EF Core to 10.1
perf(catalog): add index on products.status for listing query
```

## Scopes = module names

`orders` `cart` `catalog` `identity` `payments` `reviews` `disputes` `notifications` `admin` `web` `infra`

## Types

| Type | When |
|------|------|
| `feat` | New feature / behaviour |
| `fix` | Bug fix |
| `refactor` | Code change without feature/fix |
| `test` | Adding or fixing tests |
| `perf` | Performance improvement |
| `chore` | Dependencies, tooling, config |
| `docs` | Documentation only |
