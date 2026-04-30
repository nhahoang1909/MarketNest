# MarketNest — SLA Requirements

> Version: 1.0 | Status: Draft | Date: 2026-04-29
> Consolidated from: `marketnest-docs/business-logic/sla-requirement.md` (external analysis)
> Cross-referenced with: `domain-and-business-rules.md`, `backend-patterns.md`

---

## Table of Contents

1. [Overview — Four SLA Dimensions](#1-overview--four-sla-dimensions)
2. [Availability SLA](#2-availability-sla)
3. [Performance SLA (Latency)](#3-performance-sla-latency)
4. [Business Correctness SLA](#4-business-correctness-sla)
5. [Data Integrity SLA](#5-data-integrity-sla)
6. [Cross-Reference: SLA vs Domain Invariants](#6-cross-reference-sla-vs-domain-invariants)
7. [Alert Severity Matrix](#7-alert-severity-matrix)
8. [Admin SLA Dashboard (Phase 2)](#8-admin-sla-dashboard-phase-2)
9. [Implementation Phases](#9-implementation-phases)
10. [Architecture Decisions](#10-architecture-decisions)

---

## 1. Overview — Four SLA Dimensions

MarketNest's SLA framework covers four dimensions:

| Dimension | Question | Zero-tolerance? |
|---|---|---|
| **Availability** | Is the system up? | No — expressed as monthly uptime % |
| **Performance** | Is the response time acceptable? | No — expressed as P95 latency target |
| **Business Correctness** | Does the business logic run correctly? | **Yes** — several hard invariants |
| **Data Integrity** | Is financial data consistent? | **Yes** — all financial checks are zero-tolerance |

Phase 1 focuses on correctness and integrity; availability/performance targets are relaxed but tracked.

---

## 2. Availability SLA

### Definition

An endpoint is *available* when: HTTP status is not 5xx **AND** response time ≤ the performance threshold (§3).

### Targets

| Service | Phase 1 Target | Phase 2 Target | Phase 1 downtime/month |
|---|---|---|---|
| **Overall system** | 95% | 99% | ≤ 36 hours |
| **Checkout flow** | 97% | 99.5% | ≤ 21.6 hours |
| **Payment capture** | 97% | 99.5% | ≤ 21.6 hours |
| **Seller dashboard** | 93% | 98% | ≤ 50 hours |
| **Admin panel** | 90% | 95% | ≤ 72 hours |

> **Rationale:** Checkout and payment are the revenue path. A buyer who cannot checkout means lost revenue immediately. Seller dashboard downtime is inconvenient but not revenue-critical.

### Planned Maintenance

- **Phase 1**: Deploy any time (solo project, best-effort).
- **Phase 2+**: Announce 24 hours in advance, max 2 hours/week, does not count against downtime.

---

## 3. Performance SLA (Latency)

All latency targets are **P95** (95th percentile). P95 is used instead of average to avoid distortion from outliers.

### API Latency Targets

| Endpoint Category | Phase 1 P95 | Phase 2 P95 | Measurement point |
|---|---|---|---|
| Static pages (home, storefront) | 800 ms | 400 ms | Server TTFB |
| Product listing / search | 1200 ms | 600 ms | Server TTFB |
| Product detail | 800 ms | 400 ms | Server TTFB |
| Cart operations (add / remove) | 500 ms | 300 ms | API response |
| Checkout page load | 1500 ms | 800 ms | Server TTFB |
| **Order placement (POST)** | **3000 ms** | **1500 ms** | End-to-end API |
| **Payment capture** | **5000 ms** | **3000 ms** | End-to-end API |
| Admin queries and reports | 3000 ms | 2000 ms | API response |
| Seller dashboard | 2000 ms | 1000 ms | Server TTFB |

> **Why order placement target is high (3000 ms):** A single PlaceOrder command involves inventory check, reservation, financial calculation, DB write, domain event dispatch, and audit logging — multiple serial steps.
> **Why payment target is highest (5000 ms):** Depends on third-party payment gateway which the platform cannot fully control.

### MediatR Slow-Request Threshold

The `PerformanceBehavior` pipeline logs a warning for any request exceeding:

| Threshold constant | Value |
|---|---|
| `SlaConstants.Performance.SlowRequestMs` | 1000 ms |
| `SlaConstants.Performance.CriticalRequestMs` | 3000 ms |

These thresholds are Phase 1 baselines. Phase 2 will emit them as OpenTelemetry metrics for Prometheus/Grafana dashboards.

### Throughput (Phase 1 target: 100 concurrent users)

| Scenario | Target |
|---|---|
| Browse / search (read-heavy) | 50 req/s sustained |
| Checkout flow | 10 req/s sustained |
| Peak burst (flash sale) | 3× sustained for ≤ 60 seconds |

---

## 4. Business Correctness SLA

### Order Processing

| SLA | Definition | Phase 1 Target |
|---|---|---|
| Order confirmation latency | Buyer completes checkout → `Order.Status = Confirmed` | ≤ 2 minutes (P99) |
| Payment capture success rate | Orders reaching `Confirmed` / Orders reaching `Pending` | ≥ 95% |
| **Inventory accuracy (oversell)** | `StockQuantity < 0` ever occurs | **0 events/month** |
| Order pipeline stuck | Orders stuck in `Pending` > 10 min without reason | 0 orders/day |

> **Oversell = 0 is a hard invariant.** See §6 — this maps directly to domain Invariant I1 (`StockQuantity ≥ 0`), enforced by DB `CHECK` constraint and reservation logic.

### Payout & Commission

| SLA | Definition | Phase 1 Target |
|---|---|---|
| Payout schedule accuracy | `ProcessPayoutBatchJob` runs on time ± 30 min | 99% |
| **Payout calculation correctness** | Commission mismatches vs formula | **0 orders/month** |
| Payout disbursement latency | `Order.Status = Completed` → `Payout.Status = Paid` | ≤ 48 hours |
| Negative payout alert | `SellerNetPayout < 0` detected and alerted | ≤ 5 minutes |

> Payout calculation correctness = 0 errors is mandatory. Errors directly affect seller income and platform trust.
> Commission formula: see `domain-and-business-rules.md` §10.2.

### Dispute Resolution

| SLA | Definition | Phase 1 Target |
|---|---|---|
| Seller response SLA | Seller must respond to a dispute within | 72 hours |
| Admin arbitration SLA | Admin resolves dispute after escalation | 5 business days |
| Auto-escalation accuracy | Disputes not escalated after 72h | 0 missed/month |

> The 72-hour seller SLA and auto-escalation logic are specified in `domain-and-business-rules.md` §3.7.

### Notification Delivery

| SLA | Definition | Phase 1 Target |
|---|---|---|
| Critical notification latency | Order placed/shipped email reaches user | ≤ 5 minutes (P95) |
| Notification delivery rate | Delivered / Attempted | ≥ 98% |
| Digest timing accuracy | Daily digest delivered at 9:00 AM user timezone ± 30 min | ≥ 95% |

### Background Job Reliability

| Job | Key | Schedule | SLA | Consequence if missed |
|---|---|---|---|---|
| `CleanupExpiredReservations` | `cart.cleanup-expired-reservations` | Every 5 min | 0 missed runs/day | Inventory accuracy degraded |
| `ExpireSalesJob` | `catalog.variant.expire-sales` | Every 5 min | 0 missed runs/day | Buyers see expired sale prices |
| `VoucherExpiryJob` | `promotions.voucher.expire` | Every 15 min | 0 missed runs/day | Expired vouchers still usable |
| `AutoCancelUnconfirmedOrders` | `orders.auto-cancel-unconfirmed` | Every 30 min | Miss ≤ 1/week | Buyer funds held too long |
| `AutoCompleteOrders` | `orders.auto-complete` | Daily | Miss ≤ 1/week | Payouts delayed |
| `ProcessPayoutBatch` | `payments.process-payout-batch` | Weekly | **0 missed runs** | Sellers do not receive funds |
| `FinancialReconciliationJob` | `payments.financial-reconciliation` | Nightly | **0 missed runs** | Integrity drift undetected |

---

## 5. Data Integrity SLA

All checks below are **zero-tolerance** hard constraints. Any violation must trigger a P0 alert immediately.

| Check | Formula | Target |
|---|---|---|
| BuyerTotal = Payment.ChargedAmount | `Order.BuyerTotal == Payment.ChargedAmount` | 0 mismatches/month |
| Inventory never negative | `ProductVariant.StockQuantity >= 0` | 0 occurrences |
| Commission calculation accuracy | `|Calculated - Expected| > $0.01` per order | 0 orders/month |
| Duplicate orders | Same `Order.Id` recorded twice | 0 occurrences |
| Orphaned payments | `Payment.Status = Captured` with no linked `Order` | 0/day |
| Voucher overspend | `Voucher.UsageCount > Voucher.UsageLimit` | 0 occurrences |

> `FinancialReconciliationJob` (nightly) checks BuyerTotal vs ChargedAmount.
> DB-level `CHECK` constraint enforces `StockQuantity >= 0` (added in `AddVariantSalePrice` migration).
> `UniqueConstraint` on `Order.Id` enforces no duplicates.

---

## 6. Cross-Reference: SLA vs Domain Invariants

This section maps each SLA requirement to the domain invariant or business rule that already enforces it.

### Inventory / Oversell

| SLA Requirement | Domain Invariant | Enforcement |
|---|---|---|
| `StockQuantity` never < 0 (§5) | **I1** in `domain-and-business-rules.md §7` | DB `CHECK (stock_quantity >= 0)` + reservation pattern |
| Cleanup expired reservations (§4) | Cart module reservation TTL | Background job `CleanupExpiredReservations` |

### Financial Correctness

| SLA Requirement | Domain Rule | Enforcement |
|---|---|---|
| `BuyerTotal = Payment.ChargedAmount` | **§10.2 Canonical Formula** — `BuyerTotal = NetProductAmount + NetShippingFee + PaymentSurcharge` | `FinancialReconciliationJob` (nightly) |
| Commission calculation 0 errors | **§10.4 CommissionBase × CommissionRateSnapshot** | Unit tests in `OrderFinancialCalculator` (Phase 1 checklist) |
| `SellerNetPayout < 0` alert | Payout.NetAmount cannot be negative (business rule) | Domain validation + nightly reconciliation |

### Voucher / Promotions Correctness

| SLA Requirement | Domain Invariant | Enforcement |
|---|---|---|
| `UsageCount ≤ UsageLimit` | Invariant P2 — `VoucherUsage.UsageCount <= Voucher.UsageLimit` | DB constraint + `RedeemVoucher()` domain method guard |
| Expired vouchers unusable | `VoucherExpiryJob` clears stale vouchers | Background job every 15 min |

### Dispute SLA

| SLA Requirement | Domain Rule | Enforcement |
|---|---|---|
| 72-hour seller response | Dispute auto-escalation after 72h | `AutoEscalateDisputesJob` (daily) |
| 5-business-day admin arbitration | Platform policy — manual process | Admin panel workflow |

### Gaps (not yet enforced, Phase 1 backlog)

| Gap | Action Required |
|---|---|
| `Payment.ChargedAmount` field (currently in progress) | Implement `Payment` aggregate + `ChargedAmount` field (§10 checklist) |
| `FinancialReconciliationJob` full logic | Implement after `Order` + `Payment` aggregates complete |
| `AutoEscalateDisputesJob` | Implement with Disputes module (Phase 1) |
| P95 latency tracking via OpenTelemetry | Phase 2 — emit metrics from `PerformanceBehavior` to Prometheus |

---

## 7. Alert Severity Matrix

| Severity | Condition | Response |
|---|---|---|
| **P0 — Critical** | Oversell, payment mismatch, `FinancialReconciliationJob` finds drift, checkout down | Alert immediately, wake on-call |
| **P1 — High** | Monthly uptime below target, `ProcessPayoutBatch` missed, inventory constraint violated | Alert within 15 minutes |
| **P2 — Medium** | P95 latency breach sustained > 15 minutes, notification delivery < 98% | Alert within 1 hour |
| **P3 — Low** | Digest timing drift, slow admin queries, non-critical job delay | Next business day review |

### Phase 1 Alert Implementation

Phase 1 uses structured log events as "alerts":

- P0: `LogLevel.Critical` — triggers immediate attention in Seq
- P1: `LogLevel.Error` — surfaces in Seq error dashboard
- P2: `LogLevel.Warning` — logged by `PerformanceBehavior` for slow requests
- P3: `LogLevel.Information` — no immediate action

Phase 2+ will wire these to real alerting (Grafana alert rules, PagerDuty, Slack/email webhook).

---

## 8. Admin SLA Dashboard (Phase 2)

Route: `/admin/sla`

```
┌─────────────────────────────────────────────────────┐
│  SLA Dashboard                        [24h] [7d] [30d]
├─────────────────────────────────────────────────────┤
│  AVAILABILITY                                        │
│  Overall: 99.2% ✅  |  Checkout: 99.8% ✅           │
│                                                      │
│  PERFORMANCE (P95 Latency — from OpenTelemetry)      │
│  Search: 580ms ✅  |  Order Placement: 2800ms ✅     │
│  Payment: 4200ms ✅                                  │
│                                                      │
│  BUSINESS                                            │
│  Payment capture rate: 96.2% ✅                      │
│  Oversell incidents: 0 ✅                            │
│  Payout missed: 0 ✅                                 │
│                                                      │
│  INTEGRITY                                           │
│  Financial reconciliation: PASSED ✅                 │
│  Last run: 2026-04-29 02:00 UTC                      │
└─────────────────────────────────────────────────────┘
```

### SLA Breach Definitions

- **Availability**: 30-day rolling uptime below phase target.
- **Performance**: P95 latency exceeds threshold for any continuous 15-minute window.
- **Business**: Any single violation of a zero-tolerance SLA (oversell, duplicate order, calculation error).
- **Integrity**: `FinancialReconciliationJob` finds a mismatch.

---

## 9. Implementation Phases

### Phase 1 (Current — Month 1–3)

| Task | Location | Status |
|---|---|---|
| `SlaConstants` — all thresholds as typed constants, no magic numbers | `Base.Common/SlaConstants.cs` | ✅ Done |
| `PerformanceBehavior` — MediatR pipeline timing + slow-request warning | `Auditing/Infrastructure/PerformanceBehavior.cs` | ✅ Done |
| `FinancialReconciliationJob` stub — nightly, checks BuyerTotal vs ChargedAmount | `Payments/Application/Timer/FinancialReconciliation/` | ✅ Stub done |
| DB `CHECK (stock_quantity >= 0)` on `catalog.variants` | Migration `AddVariantSalePrice` | ✅ Done (via `chk_sale_price_positive`) |
| DB `CHECK` constraints on financial fields | Requires Order + Payment aggregates | ⏳ Phase 1 backlog |
| `FinancialReconciliationJob` full logic | Requires Order + Payment domains | ⏳ Phase 1 backlog |
| `AutoEscalateDisputesJob` | Disputes module | ⏳ Phase 1 backlog |

### Phase 2 (Month 4–5)

- OpenTelemetry metrics export → Prometheus + Grafana dashboards
- `/admin/sla` page with live charts (Chart.js)
- Alert rules configured in Grafana (P0–P3 severity)
- P95 tracking computed from OTEL histogram metrics
- Integration tests verifying SLA invariants (Testcontainers)

### Phase 3+ (Month 6–9)

- RabbitMQ dead-letter queue monitoring SLA
- K8s health probes / liveness / readiness mapped to availability SLA
- AKS alert policies for P0/P1 conditions

---

## 10. Architecture Decisions

**ADR-026** — SLA Requirements Formalized as First-Class Project Concern

See `docs/project_notes/decisions.md` — ADR-026.

### Design choices

| Decision | Rationale |
|---|---|
| `SlaConstants` in `Base.Common` | Accessible from all modules without circular dependency |
| `PerformanceBehavior` in `Auditing` | Co-located with `AuditBehavior` — both are cross-cutting MediatR concerns |
| `FinancialReconciliationJob` in `Payments` | Payments module owns financial data and payout logic |
| Phase 1: constants, not DB config | Avoid premature complexity; Phase 2 migrates to `AdminConfig` backing (ADR-021) |
| P95 via OTEL histogram, not in-process map | In-process percentile computation is stateful and memory-intensive; delegate to OTEL collector |

