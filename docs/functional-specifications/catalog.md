# Catalog Module — Functional Specification

> Module: `MarketNest.Catalog` | Schema: `catalog` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Catalog module manages storefronts, products, product variants, inventory, and sale pricing. It is the core commerce module that sellers use to list and manage their merchandise, and buyers use to browse and discover products.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Guest | Browse products, view storefronts |
| Buyer | Browse, search, follow/unfollow storefronts |
| Seller | Manage storefront, products, variants, inventory, sale prices |
| Admin | Suspend storefront/product, force-remove sale prices |

---

## US-CATALOG-001: Create Storefront

**As a** seller, **I want to** create my storefront with a name, slug, and description, **so that** I have a public presence on the marketplace.

### Acceptance Criteria

- [ ] Given I have the Seller role and verified email, When I submit storefront details, Then a storefront is created in `Draft` status
- [ ] Given I provide a slug that already exists, When submitted, Then I see "Slug already taken"
- [ ] Given the slug contains invalid characters, When submitted, Then I see validation errors (must be 3–50 lowercase alphanumeric/hyphens)
- [ ] Given I already have a storefront, When I try to create another, Then I see "You already have a storefront"

### Business Rules

- Each seller has exactly **one** storefront
- Slug format: `^[a-z0-9-]{3,50}$` (validated by `StorefrontSlug` value object)
- Storefront created in `Draft` status — must be explicitly activated
- Slug is immutable after activation

### Technical Notes

- `StorefrontSlug` value object enforces format
- Domain event: none at creation (Draft state)
- Invariant 7: slug immutable after activation

### Priority

Phase 1

---

## US-CATALOG-002: Activate Storefront

**As a** seller, **I want to** activate my storefront, **so that** my products become visible to buyers.

### Acceptance Criteria

- [ ] Given my storefront is in `Draft` status and my email is verified, When I activate it, Then status changes to `Active`
- [ ] Given my email is not verified, When I try to activate, Then I see "Email verification required"
- [ ] Given activation succeeds, Then `ActivatedAt` timestamp is recorded
- [ ] Given my storefront is already `Active`, Then the activate button is not shown

### Business Rules

- Email verification required for activation
- Slug becomes immutable after activation
- `ActivatedAt` recorded for the first activation only

### Technical Notes

- Domain event: `StorefrontActivatedEvent` → Notifications (seller confirmation)
- Status transitions: Draft → Active (Activate), Active → Suspended (Admin), Active → Closed (Seller)

### Priority

Phase 1

---

## US-CATALOG-003: Create Product with Variants

**As a** seller, **I want to** create a product with at least one variant (SKU, price, stock), **so that** buyers can purchase my items.

### Acceptance Criteria

- [ ] Given my storefront is Active, When I submit product details with ≥1 variant, Then the product is created in `Draft` status
- [ ] Given I provide a SKU that already exists platform-wide, When submitted, Then I see "SKU already taken"
- [ ] Given variant price ≤ 0, When submitted, Then I see "Price must be greater than zero"
- [ ] Given I submit with no variants, When submitted, Then I see "At least one variant is required"
- [ ] Given I provide a CompareAtPrice ≤ Price, When submitted, Then I see "Compare-at price must be greater than base price"

### Business Rules

- Product belongs to exactly one storefront
- At least 1 variant required
- SKU: platform-unique (DB unique index), max 100 characters
- Price: must be > 0 (Money value object)
- CompareAtPrice: optional, must be strictly > Price if set
- Stock quantity: ≥ 0
- Product created in Draft status
- Tags: max 10 per product
- Title: max 200 chars; Description: max 5000 chars

### Technical Notes

- Product is Aggregate Root; Variants are child entities
- Invariant: stock ≥ 0 (DB check constraint + application level)
- Uses `_MoneyInput`, `_TextField`, `_TextArea` shared components

### Priority

Phase 1

---

## US-CATALOG-004: Publish Product

**As a** seller, **I want to** publish my draft product, **so that** it becomes visible in the marketplace.

### Acceptance Criteria

- [ ] Given my product has ≥1 active variant, When I publish it, Then status changes to `Active`
- [ ] Given my product has no active variants, When I try to publish, Then I see "At least one active variant required"
- [ ] Given my storefront is suspended, When I try to publish, Then I see "Storefront must be active"
- [ ] Given the product is published, Then it appears in search/browse results

### Business Rules

- Cannot publish without at least 1 active variant (VariantStatus = Active)
- Only products with Active status appear in search/browse
- Storefront must be Active (suspended storefront hides all products)

### Technical Notes

- Domain event: `ProductPublishedEvent` (future: search index update)
- Status transitions: Draft → Active (Publish), Active → Archived (Archive)

### Priority

Phase 1

---

## US-CATALOG-005: Archive Product

**As a** seller, **I want to** archive a product I no longer want to sell, **so that** it's removed from the marketplace without losing data.

### Acceptance Criteria

- [ ] Given my product is Active, When I archive it, Then status changes to `Archived`
- [ ] Given the product is archived, Then it no longer appears in search/browse
- [ ] Given the product has items in buyer carts, When archived, Then cart items show "Product unavailable" on next view
- [ ] Given I want to re-list it, Then I can change status back to Draft or Active

### Business Rules

- Archived products hidden from public view
- Existing orders with this product remain unaffected
- Soft-archive — data retained for order history and analytics

### Technical Notes

- Domain event: `ProductArchivedEvent`
- Does NOT affect existing order lines (snapshot data)

### Priority

Phase 1

---

## US-CATALOG-006: Update Product Details

**As a** seller, **I want to** update my product's title, description, category, and tags, **so that** the listing stays accurate.

### Acceptance Criteria

- [ ] Given I own the product, When I update title/description/tags, Then changes are saved
- [ ] Given I try to update a product I don't own, Then I see a 403 Forbidden
- [ ] Given I exceed max 10 tags, When submitted, Then I see validation error
- [ ] Given I change category, Then the product appears under the new category in browse

### Business Rules

- Only the owning seller can modify their products
- Title: max 200 chars
- Description: max 5000 chars
- Tags: max 10 items
- Category: valid from administered category list

### Technical Notes

- Ownership check: product.StoreId must match seller's storefront
- `[Audited("PRODUCT_UPDATED")]` on the command

### Priority

Phase 1

---

## US-CATALOG-007: Manage Variant Inventory

**As a** seller, **I want to** update stock quantities for my product variants, **so that** inventory levels are accurate.

### Acceptance Criteria

- [ ] Given I update stock quantity to a valid number (≥ 0), When saved, Then the new quantity is reflected
- [ ] Given stock drops below 5, Then an `InventoryLowEvent` is raised (notification to seller)
- [ ] Given stock reaches 0, Then an `InventoryDepletedEvent` is raised and variant shows "Out of Stock"
- [ ] Given I try to set stock to a negative number, Then I see validation error

### Business Rules

- Stock quantity: integer ≥ 0
- Low inventory threshold: 5 units (configurable)
- Stock can never go negative (DB check constraint + application guard)
- Phase 2: expand to QuantityOnHand / QuantityReserved / QuantityAvailable

### Technical Notes

- Domain events: `InventoryLowEvent`, `InventoryDepletedEvent`
- DB check constraint: `chk_stock_quantity_non_negative`
- Inventory reservation (Redis TTL) managed by Cart module

### Priority

Phase 1

---

## US-CATALOG-008: Set Sale Price on Variant

**As a** seller, **I want to** set a sale price with a date range on a variant, **so that** I can run limited-time promotions.

### Acceptance Criteria

- [ ] Given I set a sale price < base price with valid start/end dates, When saved, Then the sale is active during that period
- [ ] Given sale price ≥ base price, When submitted, Then I see "Sale price must be less than base price"
- [ ] Given SaleStart ≥ SaleEnd, When submitted, Then I see "Start date must be before end date"
- [ ] Given SaleEnd is in the past, When submitted, Then I see "End date must be in the future"
- [ ] Given sale duration > 90 days, When submitted, Then I see "Maximum sale duration is 90 days"
- [ ] Given an existing sale is active, When I set a new sale, Then the old sale is overwritten

### Business Rules

- SalePrice must be strictly < Price
- SaleStart < SaleEnd; SaleEnd must be future
- Maximum duration: 90 days (`CatalogConstants.Sale.MaxDurationDays`)
- Phase 1: one active sale per variant (overwrite behavior)
- `EffectivePrice()` must be used at all checkout integration points

### Technical Notes

- Domain method: `variant.SetSalePrice(salePrice, saleStart, saleEnd)`
- Domain event: `VariantSalePriceSetEvent`
- Invariants: S1–S5
- Seller endpoint: `PATCH api/v1/seller/products/{productId}/variants/{variantId}/sale`

### Priority

Phase 1

---

## US-CATALOG-009: Remove Sale Price

**As a** seller, **I want to** remove an active sale price from a variant, **so that** it returns to its regular price.

### Acceptance Criteria

- [ ] Given a variant has an active sale, When I remove it, Then SalePrice/SaleStart/SaleEnd are all set to null
- [ ] Given a variant has no active sale, Then the "Remove Sale" button is not shown
- [ ] Given removal succeeds, Then `VariantSalePriceRemovedEvent` is raised

### Business Rules

- All three fields (SalePrice, SaleStart, SaleEnd) must be null or all non-null together (DB CHECK)
- Removal can be done by seller (own variants) or admin (any variant)

### Technical Notes

- Domain method: `variant.RemoveSalePrice()`
- Domain event: `VariantSalePriceRemovedEvent`
- DB constraint: `chk_sale_dates_consistent`
- Admin endpoint: `DELETE api/v1/admin/catalog/variants/{id}/sale`
- Seller endpoint: `DELETE api/v1/seller/products/{productId}/variants/{variantId}/sale`

### Priority

Phase 1

---

## US-CATALOG-010: Browse/Search Active Products

**As a** guest or buyer, **I want to** browse and search active products, **so that** I can find items to purchase.

### Acceptance Criteria

- [ ] Given I am on the marketplace homepage, When I browse, Then only Active products from Active storefronts are shown
- [ ] Given I search for a keyword, When results load, Then products matching title/description/tags are shown
- [ ] Given products have active sales, Then the sale price and original strikethrough price are displayed
- [ ] Given I paginate through results, Then pagination works correctly with configurable page size

### Business Rules

- Only Active products from Active storefronts visible
- Suspended storefronts: all products hidden
- Search matches: title, description, tags, category
- Display: `EffectivePrice()` for current price, `DisplayOriginalPrice()` for strikethrough

### Technical Notes

- Paged query: inherits from `PagedQuery`
- Uses `EffectivePrice()` / `DisplayOriginalPrice()` computed helpers
- Phase 2: full-text search via PostgreSQL `tsvector` or external search

### Priority

Phase 1

---

## US-CATALOG-011: View Storefront Page

**As a** guest or buyer, **I want to** view a seller's storefront page, **so that** I can see all their products and store information.

### Acceptance Criteria

- [ ] Given the storefront is Active, When I visit `/store/{slug}`, Then I see store name, description, banner, and products
- [ ] Given the storefront is Suspended/Closed, When I visit the URL, Then I see a "Store not available" message
- [ ] Given the storefront has products, Then only Active products are shown with pagination
- [ ] Given the store has a rating, Then the aggregate rating is displayed

### Business Rules

- Only Active storefronts publicly accessible
- Storefront slug is the URL identifier (immutable after activation)
- Product list: only Active status, paginated

### Technical Notes

- Route: `/store/{slug}` (added to `AppRoutes`)
- Storefront rating: weighted average of product ratings (event-driven calculation)

### Priority

Phase 1

---

## US-CATALOG-012: Follow/Unfollow Storefront

**As a** buyer, **I want to** follow a storefront, **so that** I can get updates about their new products and sales.

### Acceptance Criteria

- [ ] Given I am logged in as a buyer, When I click "Follow" on a storefront, Then a `UserFavoriteSeller` record is created
- [ ] Given I already follow a storefront, When I click "Unfollow", Then the record is deleted
- [ ] Given I follow a store, Then it appears in my "Favorites" settings tab
- [ ] Given I try to follow the same store twice, Then it's a no-op (idempotent)

### Business Rules

- One follow per user-storefront pair (unique index)
- Cross-module logical FK: UserId references Identity (no DB FK across schemas)
- Sale notifications from followed stores: Phase 2

### Technical Notes

- Entity: `UserFavoriteSeller` in catalog schema
- Unique index: `(UserId, StorefrontId)`
- Settings tab integration: US-IDENT-008 (Favorites tab)

### Priority

Phase 1

---

## US-CATALOG-013: Low Inventory Alert

**As a** seller, **I want to** be notified when my variant stock drops below threshold, **so that** I can restock in time.

### Acceptance Criteria

- [ ] Given a variant's stock quantity drops below 5, Then I receive an in-app notification
- [ ] Given stock reaches 0, Then I receive an urgent "Out of Stock" alert
- [ ] Given I view my seller dashboard, Then low-stock variants are highlighted

### Business Rules

- Low threshold: 5 units (default, configurable per seller in Phase 2)
- Notification: in-app only (per notification specification)
- Event-driven: raised when stock is decremented

### Technical Notes

- Domain events: `InventoryLowEvent`, `InventoryDepletedEvent`
- Handler: `InventoryLowNotificationHandler` (TODO)
- Template: `inventory.low.seller`

### Priority

Phase 1

---

## US-CATALOG-014: Expire Sales Background Job

**As the** platform, **I want** expired sale prices to be automatically cleared, **so that** buyers always see correct pricing.

### Acceptance Criteria

- [ ] Given a variant has `SaleEnd ≤ utcNow`, When `ExpireSalesJob` runs, Then SalePrice/SaleStart/SaleEnd are set to null
- [ ] Given the job runs, Then `VariantSalePriceRemovedEvent` is raised for each expired variant
- [ ] Given the job runs every 5 minutes, Then no expired sale remains active for more than ~5 minutes

### Business Rules

- Schedule: every 5 minutes (`CatalogConstants.Sale.ExpiryJobSchedule`)
- Queries variants: `SalePrice IS NOT NULL AND SaleEnd ≤ utcNow`
- Uses partial index: `idx_variants_active_sale`

### Technical Notes

- Job key: `catalog.variant.expire-sales`
- Implements `IBackgroundJob` with `JobDescriptor`
- Background jobs manage their own transactions (outside HTTP pipeline)
- Domain event: `VariantSalePriceRemovedEvent`

### Priority

Phase 1

---

## US-CATALOG-015: Bulk Import Variants via Excel

**As a** seller, **I want to** import product variants from an Excel file, **so that** I can quickly add many variants at once.

### Acceptance Criteria

- [ ] Given I upload a valid Excel file matching the template, When processed, Then variants are created/updated in bulk
- [ ] Given the file has invalid rows, When processed, Then I see an error table showing row-level errors
- [ ] Given the file fails antivirus scan, When uploaded, Then I see "File rejected for security reasons"
- [ ] Given the file has wrong headers, Then I see "Invalid template format"
- [ ] Given I want the template, When I click "Download Template", Then I receive the Excel template file

### Business Rules

- 4-layer validation: (1) extension + magic bytes → (2) antivirus → (3) header validation → (4) row parsing
- Template download: `AppRoutes.Seller.ProductImportTemplate`
- Max file size: per `ExcelUploadRules` constants

### Technical Notes

- `IExcelService` contract — never reference ClosedXML directly
- `ExcelTemplate<TRow>` with column setter callbacks
- Import preview: `SharedViewPaths.ImportPreview`
- Error display: `SharedViewPaths.ImportErrorTable`
- Command: `BulkImportVariantsCommand`
- ADR-037

### Priority

Phase 1

