# Payments Module — Functional Specification

> Module: `MarketNest.Payments` | Schema: `payments` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Payments module handles payment capture from buyers, refund processing, and seller payout disbursement. It manages two separate financial flows: Buyer → Platform (Payment aggregate) and Platform → Seller (Payout aggregate).

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Buyer | Pay at checkout |
| Seller | View payout history, receive payouts |
| Admin | Configure surcharge rates, view payment reports |
| System | Process payout batches, handle refunds |

---

## US-PAY-001: Capture Payment on Checkout

**As the** platform, **I want to** capture payment when a buyer checks out, **so that** funds are secured before order processing begins.

### Acceptance Criteria

- [ ] Given the buyer submits payment at checkout, When the charge succeeds, Then a Payment record is created with `Status = Captured`
- [ ] Given `ChargedAmount = Order.BuyerTotal`, Then the amounts match exactly (invariant F7)
- [ ] Given capture succeeds, Then `PaymentCapturedEvent` is raised (advances order to Confirmed)
- [ ] Given the payment method is CreditCard, Then `SurchargeSnapshot` reflects the order's surcharge amount
- [ ] Given `GatewayCost` is calculated (2.9% + $0.30 stub), Then it's stored internally (not shown to buyer)

### Business Rules

- `ChargedAmount` must equal `Order.BuyerTotal` (invariant F7)
- Payment methods (Phase 1 stub): CreditCard, BankTransfer
- SurchargeSnapshot: copy from Order.PaymentSurcharge
- GatewayCost: platform internal cost (not buyer-facing)
- GatewayReference: external payment gateway ID (Phase 1: mock)

### Technical Notes

- Domain event: `PaymentCapturedEvent` → Orders (advance to Confirmed)
- Phase 1: mock payment gateway (always succeeds)
- Phase 2: integrate real payment provider (Stripe/PayPal)

### Priority

Phase 1

---

## US-PAY-002: Payment Failure Handling

**As the** platform, **I want to** handle payment failures gracefully, **so that** buyers can retry and orders aren't left in limbo.

### Acceptance Criteria

- [ ] Given the payment charge fails, When the error is returned, Then Payment status is set to `Failed`
- [ ] Given payment fails, Then `PaymentFailedEvent` is raised
- [ ] Given payment fails, Then the buyer sees an error message with option to retry
- [ ] Given the order's payment fails, Then the order remains in `Pending` (not advanced to Confirmed)
- [ ] Given 3 consecutive failures, Then the order is auto-cancelled

### Business Rules

- Failed payment does not advance order state
- Buyer can retry with same or different payment method
- After 3 consecutive failures: auto-cancel order
- Error messages: generic (don't expose gateway internals)

### Technical Notes

- Domain event: `PaymentFailedEvent`
- Retry mechanism: new Payment attempt linked to same Order
- Phase 1: mock (can simulate failure for testing)

### Priority

Phase 1

---

## US-PAY-003: Full Refund on Cancellation

**As the** platform, **I want to** automatically refund the buyer when an order is cancelled, **so that** they get their money back promptly.

### Acceptance Criteria

- [ ] Given `OrderCancelledEvent` is received, When refund is processed, Then Payment.ChargedAmount is returned to buyer
- [ ] Given refund succeeds, Then Payment status changes to `Refunded`
- [ ] Given refund succeeds, Then `PaymentRefundedEvent` is raised (notification to buyer)
- [ ] Given a payout was already scheduled for this order, Then payout is cancelled

### Business Rules

- Full refund: entire ChargedAmount returned
- Triggers: buyer cancel, seller cancel, auto-cancel (48h timeout)
- If payout already scheduled (Pending): cancel it
- If payout already disbursed (Paid): trigger clawback (US-PAY-007)

### Technical Notes

- Event handler: listens for `OrderCancelledEvent`
- Domain event: `PaymentRefundedEvent` → Notifications (buyer)
- Phase 1: mock refund (instant)

### Priority

Phase 1

---

## US-PAY-004: Partial Refund After Dispute Resolution

**As the** platform, **I want to** process partial refunds when admin resolves a dispute in the buyer's favor, **so that** fair compensation is provided.

### Acceptance Criteria

- [ ] Given `DisputeResolvedEvent` with decision `PartialRefund`, When processed, Then the specified refund amount is returned to buyer
- [ ] Given `DisputeResolvedEvent` with decision `FullRefund`, Then full ChargedAmount is refunded
- [ ] Given a partial refund, Then the remaining amount still goes to seller payout (adjusted)
- [ ] Given `DismissBuyerClaim` decision, Then no refund occurs and payout proceeds normally

### Business Rules

- Partial refund amount: specified by Admin during dispute resolution
- Partial refund cannot exceed original ChargedAmount
- Payout adjusted: seller receives remainder minus commission
- If payout already disbursed: clawback for the refund portion

### Technical Notes

- Event handler: listens for `DisputeResolvedEvent`
- RefundAmount from Resolution entity
- Payout recalculation needed for partial refunds

### Priority

Phase 1

---

## US-PAY-005: Schedule Payout on Order COMPLETED

**As the** platform, **I want to** schedule a seller payout when an order completes, **so that** sellers receive their earnings.

### Acceptance Criteria

- [ ] Given `OrderCompletedEvent` is received, When payout is calculated, Then a Payout record is created with `Status = Pending`
- [ ] Given the payout calculation uses snapshotted rates, Then `CommissionRateSnapshot` from order time is used
- [ ] Given the seller had a shop voucher discount, Then `CommissionBase = SellerSubtotal - ShopProductDiscount`
- [ ] Given `NetAmount` is calculated, Then it follows the formula: `CommissionBase - CommissionAmount - ShopShippingDiscount + GrossShippingFee`
- [ ] Given payout is scheduled, Then `PayoutScheduledEvent` is raised

### Business Rules

- Commission formula: `CommissionBase × CommissionRateSnapshot`
- CommissionBase = SellerSubtotal - ShopProductDiscount
- Platform voucher does NOT reduce CommissionBase (platform absorbs)
- Commission does NOT apply to ShippingFee or PaymentSurcharge
- CommissionRateSnapshot: captured at order placed time
- NetAmount ≥ 0 (if negative: flag alert)

### Technical Notes

- See §3.5.1 Payout Aggregate and §10 Financial Reference
- Invariants: F5, F9, F10
- Domain event: `PayoutScheduledEvent`
- Per-seller payout (multi-seller orders create multiple Payout records)

### Priority

Phase 1

---

## US-PAY-006: Process Payout Batch

**As the** platform, **I want** a daily batch job to process pending payouts, **so that** sellers receive their money on schedule.

### Acceptance Criteria

- [ ] Given `ProcessPayoutBatch` job runs at 02:00 UTC, When there are Pending payouts past their scheduled date, Then they are processed
- [ ] Given processing succeeds, Then Payout status changes to `Paid` and `ProcessedAt` is recorded
- [ ] Given processing succeeds, Then `PayoutProcessedEvent` is raised (notification to seller)
- [ ] Given processing fails for a payout, Then status changes to `Failed` and alert is generated

### Business Rules

- Schedule: Daily at 02:00 UTC
- Only processes payouts with `ScheduledFor ≤ now`
- Each payout processed independently (one failure doesn't block others)
- Seller receives notification on successful payout

### Technical Notes

- Background job: `ProcessPayoutBatch`
- Job key: `payments.payout.process-batch`
- Domain event: `PayoutProcessedEvent` → Notifications (seller)
- Phase 1: mock disbursement (mark as Paid immediately)
- Phase 2: real bank transfer/payment provider integration

### Priority

Phase 1

---

## US-PAY-007: Payout Clawback

**As the** platform, **I want to** claw back a payout if a refund occurs after the payout was already disbursed, **so that** platform funds are protected.

### Acceptance Criteria

- [ ] Given a payout was already `Paid` and a refund is needed, When a clawback is triggered, Then Payout status changes to `Clawback`
- [ ] Given clawback occurs, Then `PayoutClawbackRequiredEvent` is raised
- [ ] Given a clawback event, Then admin is notified to resolve the balance manually

### Business Rules

- Clawback occurs when: payout already disbursed + subsequent refund needed
- Clawback amount = refund amount (may be partial or full)
- Phase 1: manual resolution (admin notification + tracking)
- Phase 2: automatic deduction from next payout

### Technical Notes

- Domain event: `PayoutClawbackRequiredEvent`
- Status transition: Paid → Clawback
- Admin notification for manual follow-up

### Priority

Phase 1

---

## US-PAY-008: Payment Surcharge Display at Checkout

**As a** buyer, **I want to** see the payment surcharge clearly during checkout, **so that** I understand the total I'll be charged.

### Acceptance Criteria

- [ ] Given I select CreditCard as payment method (surcharge 2%), Then the surcharge amount is shown as a separate line
- [ ] Given I select BankTransfer (surcharge 0%), Then no surcharge line is shown
- [ ] Given surcharge is displayed, Then it shows: "(NetProductAmount + NetShippingFee) × 2% = $X.XX"
- [ ] Given I switch payment method, Then surcharge recalculates immediately

### Business Rules

- Surcharge rate: Admin-configured per PaymentMethod
- Transparency: surcharge shown as separate line item (not hidden)
- Surcharge calculated AFTER voucher discounts are applied (invariant F8)
- Formula: `PaymentSurcharge = (NetProductAmount + NetShippingFee) × SurchargeRate`

### Technical Notes

- Surcharge rates stored in master data (Admin module)
- `PaymentSurchargeRate` snapshot on Order at checkout
- UI: dynamic recalculation via HTMX when method changes
- See §10.3 PaymentSurcharge Configuration

### Priority

Phase 1

---

## US-PAY-009: Seller Payout History

**As a** seller, **I want to** view my payout history, **so that** I can track my earnings and when they were disbursed.

### Acceptance Criteria

- [ ] Given I am a seller, When I view my payout history page, Then I see all payouts with status, amount, and dates
- [ ] Given a payout is Pending, Then I see expected disbursement date
- [ ] Given a payout is Paid, Then I see the ProcessedAt date and NetAmount
- [ ] Given I want details, When I click a payout, Then I see the full breakdown (SellerSubtotal, commission, discounts, shipping)

### Business Rules

- Seller can only see their own payouts
- History includes all statuses: Pending, Processing, Paid, Failed, Clawback
- Detail view shows commission calculation breakdown

### Technical Notes

- Query: `GetPayoutHistoryQuery` (paged)
- Route: in seller dashboard area
- Payout aggregate fields displayed

### Priority

Phase 1

