# Functional Specifications

> Version: 1.0 | Date: 2026-05-01 | Phase: 1 (Modular Monolith)

This folder contains functional specification documents for each module in the MarketNest platform. Each document is structured as a collection of **User Stories** that can be directly translated into GitHub Issues for sprint planning.

## Document Index

| File | Module | Stories |
|------|--------|---------|
| [identity.md](./identity.md) | Identity | US-IDENT-001 → 012 |
| [catalog.md](./catalog.md) | Catalog | US-CATALOG-001 → 015 |
| [cart.md](./cart.md) | Cart | US-CART-001 → 008 |
| [orders.md](./orders.md) | Orders | US-ORDER-001 → 014 |
| [payments.md](./payments.md) | Payments | US-PAY-001 → 009 |
| [reviews.md](./reviews.md) | Reviews | US-REVIEW-001 → 007 |
| [disputes.md](./disputes.md) | Disputes | US-DISPUTE-001 → 007 |
| [notifications.md](./notifications.md) | Notifications | US-NOTIF-001 → 006 |
| [admin.md](./admin.md) | Admin | US-ADMIN-001 → 011 |
| [promotions.md](./promotions.md) | Promotions | US-PROMO-001 → 010 |

## Story Format

Each user story follows this structure:

```markdown
## US-{MODULE}-{NNN}: {Title}

**As a** {actor}, **I want to** {goal}, **so that** {benefit}.

### Acceptance Criteria

- [ ] Given ... When ... Then ...
- [ ] Given ... When ... Then ...

### Business Rules

- Rule 1
- Rule 2

### Technical Notes

- Domain events raised
- Related invariants
- Dependencies on other modules

### Priority

Phase 1 | Phase 2
```

## How to Use

1. **Sprint Planning**: Each `US-*` story maps to one GitHub Issue
2. **Estimation**: Stories are sized for 1–3 day implementation cycles
3. **Dependencies**: Check "Technical Notes" for cross-module dependencies
4. **Traceability**: Business rules reference `docs/domain-and-business-rules.md` sections

## Source Documents

- `docs/domain-and-business-rules.md` — DDD aggregates, business rules, invariants
- `docs/backend-patterns.md` — CQRS patterns, base classes
- `docs/frontend-guide.md` — UI patterns, page inventory
- `docs/architecture.md` — Module boundaries, ADRs

