# Reviews Module — Functional Specification

> Module: `MarketNest.Reviews` | Schema: `reviews` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Reviews module enables buyers to leave ratings and text reviews on products they've purchased and received. It includes seller replies, community votes, and aggregate rating calculations.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Buyer | Submit review, edit review (24h), vote on reviews |
| Seller | Reply to reviews (once per review) |
| Admin | Hide/flag reviews |

---

## US-REVIEW-001: Submit Review

**As a** buyer who completed an order, **I want to** submit a review for a product, **so that** I can share my experience with other buyers.

### Acceptance Criteria

- [ ] Given I have a COMPLETED order containing product X, When I submit a review, Then it's published with my rating (1–5), optional title, and optional body
- [ ] Given I haven't purchased this product (no completed order), When I try to review, Then I see "You must purchase this product to leave a review"
- [ ] Given I already reviewed this product for this order, When I try again, Then I see "You've already reviewed this product"
- [ ] Given my order is in DISPUTED or REFUNDED state, When I try to review, Then I see "Cannot review disputed/refunded orders"
- [ ] Given my review is submitted, Then `ReviewSubmittedEvent` is raised

### Business Rules

- **Review Gate (Anti-Fraud):**
  - Must be authenticated Buyer
  - Must have Order containing this Product in COMPLETED state
  - Has NOT already reviewed for this Order
  - NOT if Order in DISPUTED/REFUNDED state (configurable)
- Rating: 1–5 stars (required)
- Title: optional, max 100 characters
- Body: optional, max 1000 characters
- Status: Published (default)

### Technical Notes

- Domain event: `ReviewSubmittedEvent` → Catalog (recalculate product rating), Notifications (seller)
- Review gate implemented as service/specification pattern
- Template: `review.received.seller`

### Priority

Phase 1

---

## US-REVIEW-002: Edit Review Within 24 Hours

**As a** buyer, **I want to** edit my review within 24 hours of submission, **so that** I can correct mistakes.

### Acceptance Criteria

- [ ] Given my review was submitted less than 24h ago, When I edit it, Then the changes are saved
- [ ] Given my review was submitted more than 24h ago, When I try to edit, Then I see "Review can no longer be edited"
- [ ] Given I edit the rating, Then the product's aggregate rating is recalculated

### Business Rules

- Reviews are editable for exactly 24 hours after submission
- After 24h: `IsEditable = false` (immutable)
- Rating changes trigger aggregate recalculation

### Technical Notes

- `IsEditable` computed from `CreatedAt + 24h > now`
- On edit: re-raise event for rating recalculation if rating changed

### Priority

Phase 1

---

## US-REVIEW-003: Seller Reply

**As a** seller, **I want to** reply to a review on my product, **so that** I can address feedback publicly.

### Acceptance Criteria

- [ ] Given I own the product being reviewed, When I submit a reply, Then it's stored as `SellerReply` on the review
- [ ] Given I already replied to this review, When I try to reply again, Then I see "You've already replied"
- [ ] Given I don't own the product, When I try to reply, Then I see 403 Forbidden
- [ ] Given the reply is saved, Then `SellerReplyAddedEvent` is raised

### Business Rules

- One reply per review (seller replies once)
- Reply body: max 500 characters
- Only the owning seller can reply
- Reply is appended — cannot edit or delete after submission

### Technical Notes

- `SellerReply` value object: Body (max 500) + RepliedAt
- Domain event: `SellerReplyAddedEvent`
- Ownership check: review.ProductId → product.StoreId → seller

### Priority

Phase 1

---

## US-REVIEW-004: Vote on Review

**As a** buyer, **I want to** vote on a review as helpful, **so that** the community can surface the most useful reviews.

### Acceptance Criteria

- [ ] Given I am a logged-in buyer, When I vote on a review, Then my vote is recorded
- [ ] Given I have already voted on this review, When I try to vote again, Then I see "Already voted" (or toggle off)
- [ ] Given votes are recorded, Then the review shows vote count
- [ ] Given I am the review author, When I try to vote on my own review, Then I see "Cannot vote on your own review"

### Business Rules

- One vote per buyer per review
- Cannot vote on own review
- Vote is a simple helpful/unhelpful (or just helpful count)
- Vote count displayed on review

### Technical Notes

- `ReviewVote` child entity: VoterId + VotedAt
- Unique constraint: (ReviewId, VoterId)

### Priority

Phase 1

---

## US-REVIEW-005: Hide/Flag Review (Admin)

**As an** admin, **I want to** hide or flag inappropriate reviews, **so that** the platform maintains quality standards.

### Acceptance Criteria

- [ ] Given I am an admin, When I flag a review, Then its status changes to `Flagged`
- [ ] Given I hide a review, Then its status changes to `Hidden` and it's no longer shown publicly
- [ ] Given a review is hidden, Then the product's aggregate rating is recalculated without it
- [ ] Given I hide a review, Then `ReviewHiddenEvent` is raised

### Business Rules

- Admin can flag (mark for review) or hide (remove from public view)
- Hidden reviews excluded from aggregate rating calculation
- Flagged reviews remain visible but marked for team review
- Status transitions: Published → Flagged, Published → Hidden, Flagged → Hidden

### Technical Notes

- Domain event: `ReviewHiddenEvent`
- Recalculation triggered on status change to Hidden

### Priority

Phase 1

---

## US-REVIEW-006: Product Rating Aggregation

**As the** platform, **I want** product and storefront ratings to be automatically recalculated, **so that** aggregate scores are always accurate.

### Acceptance Criteria

- [ ] Given a new review is submitted, Then the product's average rating is recalculated
- [ ] Given a review is edited (rating changed), Then the average is recalculated
- [ ] Given a review is hidden, Then it's excluded from the average
- [ ] Given a storefront, Then its rating is the weighted average of all its products' ratings

### Business Rules

- Product rating = average of all Published review ratings
- Storefront rating = weighted average of product ratings
- Recalculation is event-driven (not real-time per query)
- Cached value updated on each review event

### Technical Notes

- Event handler in Catalog module: recalculates on `ReviewSubmittedEvent`, `ReviewHiddenEvent`
- Stores pre-computed average on Product entity
- Could be cached in Redis for performance

### Priority

Phase 1

---

## US-REVIEW-007: Block Review on Disputed/Refunded Orders

**As the** platform, **I want to** prevent reviews on disputed or refunded orders, **so that** reviews reflect genuine completed transactions.

### Acceptance Criteria

- [ ] Given an order is in DISPUTED status, Then the "Write Review" button is not shown
- [ ] Given an order is in REFUNDED status, Then attempting to submit a review returns an error
- [ ] Given an order was completed then later refunded (post-completion dispute), Then existing reviews remain but no new ones can be added

### Business Rules

- Review gate checks: order status must be COMPLETED
- DISPUTED and REFUNDED orders cannot receive new reviews
- Existing reviews from before dispute/refund remain (audit trail)
- Configurable: could allow reviews on partially-refunded orders (Phase 2)

### Technical Notes

- Part of the Review Gate validation logic
- Checked at command handler level before allowing review creation

### Priority

Phase 1

