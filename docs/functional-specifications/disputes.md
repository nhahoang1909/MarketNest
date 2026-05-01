# Disputes Module — Functional Specification

> Module: `MarketNest.Disputes` | Schema: `disputes` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Disputes module handles buyer–seller conflicts after order delivery. It provides a structured process for evidence submission, seller response deadlines, admin arbitration, and resolution outcomes that trigger payment actions.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Buyer | Open dispute, submit evidence/messages |
| Seller | Respond to dispute, submit evidence/messages |
| Admin | Review disputes, arbitrate, resolve |
| System | Auto-escalate on seller timeout |

---

## US-DISPUTE-001: Open Dispute

**As a** buyer, **I want to** open a dispute within 3 days of delivery, **so that** I can seek resolution for issues with my order.

### Acceptance Criteria

- [ ] Given my order is in `Delivered` status and it's within 3 days of `DeliveredAt`, When I open a dispute with a reason, Then dispute is created with `Status = Open`
- [ ] Given it's more than 3 days since delivery, When I try to open a dispute, Then I see "Dispute window has expired"
- [ ] Given I already have an open dispute for this order, When I try to open another, Then I see "Dispute already exists for this order"
- [ ] Given dispute is opened, Then order status changes to `Disputed`
- [ ] Given dispute is opened, Then seller has 72 hours to respond (`SellerResponseDeadline`)

### Business Rules

- Only within 3 days of DELIVERED status
- One dispute per order (DB unique constraint + domain guard)
- Reason required: NotReceived | NotAsDescribed | Damaged | WrongItem | Other
- Order transitions to DISPUTED status
- Seller gets 72h response deadline from dispute open time
- Invariant 5, 9

### Technical Notes

- Domain event: `DisputeOpenedEvent` → Orders (set DISPUTED), Notifications (seller + admin)
- DB unique constraint: one dispute per OrderId
- SellerResponseDeadline = OpenedAt + 72h
- Template: `dispute.opened.seller`, `dispute.opened.admin`

### Priority

Phase 1

---

## US-DISPUTE-002: Submit Evidence

**As a** buyer or seller, **I want to** submit evidence (text and photos) for my dispute case, **so that** the resolution is fair and well-informed.

### Acceptance Criteria

- [ ] Given the dispute is open, When I submit a message with text, Then a `DisputeMessage` is created
- [ ] Given I attach photos (up to 5), When submitted, Then evidence URLs are stored with the message
- [ ] Given the dispute is resolved, When I try to submit more evidence, Then I see "Dispute is already resolved"
- [ ] Given all messages are stored, Then they form an immutable audit trail

### Business Rules

- Both Buyer and Seller can submit messages
- Text body: required per message
- Evidence: up to 5 photo URLs per message
- All messages are immutable (cannot edit or delete — audit trail)
- Cannot submit after dispute is resolved

### Technical Notes

- `DisputeMessage` child entity: AuthorRole (Buyer|Seller|Admin), Body, EvidenceUrls (max 5)
- `AuthorRole` enum to distinguish message authors
- Image upload through `IAntivirusScanner` pipeline

### Priority

Phase 1

---

## US-DISPUTE-003: Seller Response Within 72h Deadline

**As a** seller, **I want to** respond to a dispute before the deadline, **so that** my side of the story is heard.

### Acceptance Criteria

- [ ] Given a dispute is opened against me, When I submit a response, Then status changes to `AwaitingSellerResponse` → `UnderReview`
- [ ] Given I respond within 72h, Then the dispute remains in normal flow for admin review
- [ ] Given I respond, Then `DisputeSellerRespondedEvent` is raised (buyer notified)
- [ ] Given I include evidence with my response, Then it's stored as a `DisputeMessage` with `AuthorRole = Seller`

### Business Rules

- Seller has 72 hours from dispute open to respond
- Response = submitting at least one DisputeMessage with Seller role
- After response: dispute moves to UnderReview for admin

### Technical Notes

- Domain event: `DisputeSellerRespondedEvent` → Notifications (buyer)
- Status transition: Open/AwaitingSellerResponse → UnderReview
- Template: `dispute.responded.buyer`

### Priority

Phase 1

---

## US-DISPUTE-004: Auto-Escalate on Seller Timeout

**As the** platform, **I want** disputes to auto-escalate if the seller doesn't respond within 72h, **so that** buyers aren't left waiting indefinitely.

### Acceptance Criteria

- [ ] Given `SellerResponseDeadline` has passed with no seller response, When the check runs, Then dispute status changes to `UnderReview`
- [ ] Given auto-escalation occurs, Then `DisputeEscalatedEvent` is raised
- [ ] Given escalation, Then admin is notified to review the dispute
- [ ] Given the seller didn't respond, Then this is noted in the dispute record

### Business Rules

- Deadline: 72 hours from dispute open
- If seller doesn't respond: auto-escalate to admin review
- Non-response may be considered in admin's decision
- `DisputeEscalatedEvent` → Notifications (admin)

### Technical Notes

- Can be checked by a background job or triggered by deadline check
- Domain event: `DisputeEscalatedEvent`
- Template: notification to admin team

### Priority

Phase 1

---

## US-DISPUTE-005: Admin Reviews and Resolves

**As an** admin, **I want to** review dispute evidence and issue a resolution, **so that** the conflict is fairly resolved.

### Acceptance Criteria

- [ ] Given the dispute is `UnderReview`, When I review all messages/evidence, Then I can select a decision
- [ ] Given I choose `FullRefund`, When I resolve, Then buyer receives full refund
- [ ] Given I choose `PartialRefund` with an amount, When I resolve, Then buyer receives partial refund
- [ ] Given I choose `DismissBuyerClaim`, When I resolve, Then no refund and order moves to Completed
- [ ] Given I include an `AdminNote`, Then it's stored for audit
- [ ] Given resolution is saved, Then `DisputeResolvedEvent` is raised

### Business Rules

- Resolution decisions: FullRefund | PartialRefund | DismissBuyerClaim
- Partial refund: must specify amount (≤ ChargedAmount)
- Admin note: required (explanation of decision)
- Resolution is final (no appeal in Phase 1)
- Only Admin role can resolve disputes

### Technical Notes

- `Resolution` value object on Dispute: Decision, RefundAmount, AdminNote
- Domain event: `DisputeResolvedEvent` → Orders (update status), Payments (process refund), Notifications (both parties)
- Status transition: UnderReview → Resolved
- Template: `dispute.resolved.buyer`, `dispute.resolved.seller`

### Priority

Phase 1

---

## US-DISPUTE-006: Resolution Triggers Payment Action

**As the** platform, **I want** dispute resolution to automatically trigger the correct payment action, **so that** financial outcomes are processed promptly.

### Acceptance Criteria

- [ ] Given `FullRefund` decision, When resolved, Then Payment is refunded in full and Payout is cancelled/clawed back
- [ ] Given `PartialRefund` decision, When resolved, Then specified amount is refunded, remainder goes to seller
- [ ] Given `DismissBuyerClaim`, When resolved, Then order moves to COMPLETED and payout proceeds normally
- [ ] Given resolution triggers refund, Then order status changes to `Refunded`
- [ ] Given resolution dismisses claim, Then order status changes to `Completed`

### Business Rules

- FullRefund → Payment.Refunded + Order.Refunded + Payout cancelled/clawback
- PartialRefund → Partial payment refund + adjusted Payout
- DismissBuyerClaim → Order.Completed + Payout proceeds
- If payout already disbursed: clawback event raised

### Technical Notes

- Cross-module: Disputes → Payments (refund), Orders (status update)
- `DisputeResolvedEvent` handled by Payments and Orders modules
- Follows existing refund logic (US-PAY-003, US-PAY-004)

### Priority

Phase 1

---

## US-DISPUTE-007: Immutable Message Audit Trail

**As the** platform, **I want** all dispute messages to be immutable and timestamped, **so that** there's a complete audit trail for legal and compliance purposes.

### Acceptance Criteria

- [ ] Given any message is submitted, Then it cannot be edited or deleted by anyone (including admin)
- [ ] Given messages are viewed, Then they show author role, timestamp, body, and any evidence URLs
- [ ] Given the dispute is resolved, Then the full message history remains accessible for audit
- [ ] Given admin views the dispute, Then they see the complete chronological message thread

### Business Rules

- All DisputeMessages: immutable after creation
- No edit, no delete — append only
- Timestamps in UTC, displayed in user's timezone
- Evidence URLs: permanent links (not expiring signed URLs)

### Technical Notes

- DB: no UPDATE/DELETE operations on dispute_messages table
- EF Core: no Update method exposed on DisputeMessage repository
- Read-only projection for display

### Priority

Phase 1

