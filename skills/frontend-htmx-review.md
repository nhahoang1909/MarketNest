---
name: frontend-htmx-review
description: >
  Quét toàn bộ frontend layer của MarketNest để review chất lượng: HTMX attribute correctness,
  Alpine.js pattern, Tailwind CSS class audit, Razor Page partial structure, hx-boost, hx-swap,
  hx-trigger, anti-flicker pattern, SSE/polling endpoint. Sử dụng skill này khi người dùng muốn:
  review frontend, kiểm tra HTMX, audit Alpine.js, review Tailwind, check Razor partial,
  kiểm tra hx-boost, hx-swap, hx-trigger, antiforgery token, SSE setup, hoặc nói bất kỳ cụm từ
  nào như "review frontend", "check HTMX", "Alpine pattern", "Tailwind audit", "hx-swap",
  "anti-flicker", "SSE endpoint", "x-cloak", "kiểm tra frontend", "review cshtml".
  Kích hoạt khi người dùng upload *.cshtml, *.js file, hoặc hỏi về UI behavior.
compatibility:
  tools: [bash, read_file, write_file, list_files]
  agents: [claude-code, gemini-cli, cursor, continue, aider]
  stack: [HTMX 2.x, Alpine.js 3.x, Tailwind CSS 4.x, Razor Pages .NET 10, Flowbite]
---

# Frontend HTMX Review Skill — MarketNest

Skill này review toàn bộ frontend layer theo 7 rule groups, từ HTMX attribute correctness
đến Alpine.js state management, Tailwind consistency, và SSE/polling pattern.
Output: báo cáo **BLOCKER / HIGH / MEDIUM / SUGGESTION** với HTML/C# fix sẵn sàng.

---

## Frontend Architecture của MarketNest

```
Browser
  │
  ├─ Full page load → Razor Pages SSR → _Layout.cshtml
  │    ├─ HTMX 2.x (CDN)   — partial page swaps, form submissions
  │    ├─ Alpine.js 3.x (CDN) — client reactivity (no server roundtrip needed)
  │    └─ Tailwind CSS 4.x  — utility classes, brand: orange #f97316
  │
  ├─ HTMX request (HX-Request: true) → PageModel handler → Partial view
  │    Response headers: HX-Redirect | HX-Retarget | HX-Reswap | HX-Trigger
  │
  └─ SSE / Polling → GET /disputes/{id}/messages → _MessageList partial

Alpine.js Stores:  $store.cart | $store.toasts | $store.user
Alpine.js Components: confirmDialog | imageUploader | starRating | reservationTimer
HTMX Events → Alpine: cartUpdated | toastShow | modalClose | inventoryLow

Component library: Pages/Shared/ (30+ partials, organized by category)
JS components: wwwroot/js/components/ + wwwroot/js/stores/
```

---

## Quy trình thực thi

```
Phase 1: SCAN    → Inventory tất cả cshtml, js files liên quan
Phase 2: ANALYZE → 7 rule groups với grep + manual review
Phase 3: REPORT  → BLOCKER / HIGH / MEDIUM / SUGGESTION
Phase 4: FIX     → Code fix HTML/C#/JS sẵn sàng
Phase 5: VERIFY  → Visual regression checklist
```

---

## Phase 1: SCAN — Thu thập frontend inventory

```bash
# 1A. Tất cả Razor view files
echo "=== Razor Pages & Partials ==="
find src/MarketNest.Web/Pages/ -name "*.cshtml" | grep -v "bin/\|obj/" | sort
echo ""
echo "=== Shared Components ==="
find src/MarketNest.Web/Pages/Shared/ -name "*.cshtml" | grep -v "bin/\|obj/" | sort

# 1B. JavaScript files
echo "=== Alpine.js Components & Stores ==="
find src/MarketNest.Web/wwwroot/js/ -name "*.js" | grep -v "bin/\|obj/\|min\." | sort

# 1C. Thống kê HTMX usage
echo "=== HTMX attribute usage count ==="
find src/MarketNest.Web/ -name "*.cshtml" -not -path "*/bin/*" | xargs grep -oh \
    "hx-get\|hx-post\|hx-put\|hx-delete\|hx-patch\|hx-boost\|hx-push-url\|hx-swap\|hx-trigger\|hx-target\|hx-indicator\|hx-vals\|hx-headers\|hx-select\|hx-ext\|hx-confirm" \
    2>/dev/null | sort | uniq -c | sort -rn

# 1D. Alpine.js directive usage
echo "=== Alpine.js directive count ==="
find src/MarketNest.Web/ \( -name "*.cshtml" -o -name "*.js" \) -not -path "*/bin/*" \
    | xargs grep -oh "x-data\|x-init\|x-show\|x-if\|x-for\|x-text\|x-html\|x-model\|x-ref\|x-cloak\|x-transition\|@click\|@change\|x-on:" \
    2>/dev/null | sort | uniq -c | sort -rn
```

---

## Phase 2: ANALYZE — 7 Rule Groups

---

### Rule Group 1: HTMX Core Attributes — Correctness

**Convention MarketNest:**

| Action | Method | hx-swap | hx-target | hx-push-url |
|---|---|---|---|---|
| Load partial | GET | `innerHTML` | `#region-id` | `true` nếu navigable |
| Form POST (create) | POST | — | — | dùng `HX-Redirect` |
| Form PUT (update) | PUT/PATCH | `outerHTML` | element itself | `false` |
| Delete item | DELETE | `outerHTML` | `closest tr` hoặc element | `false` |
| Search/filter | GET | `innerHTML` | `#results` | `true` |
| Error response | — | `outerHTML` | `#error-region` | — |

```bash
# 1A. hx-swap dùng sai strategy
echo "=== Potentially wrong hx-swap strategy ==="
# outerHTML trên DELETE phải đúng (removes element)
# innerHTML cho swap nội dung vào container
# outerHTML replace cả element (dùng khi target = element itself)
grep -rn "hx-swap" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/\|<!--" | head -30

# 1B. hx-trigger thiếu debounce trên input/search (gây quá nhiều requests)
echo "=== Input HTMX triggers missing debounce ==="
grep -rn "hx-trigger" src/MarketNest.Web/ --include="*.cshtml" \
  | grep "keyup\|input\|change" | grep -v "delay:\|bin/\|obj/" | head -20
# Mọi keyup/input trigger phải có delay: hoặc changed modifier

# 1C. hx-target trỏ đến ID không tồn tại
echo "=== HTMX targets — collect all IDs ==="
# Manual check: mỗi hx-target="#some-id" phải có element <div id="some-id"> tương ứng
grep -rn "hx-target=\"#" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/" | grep -oP '#\w+' | sort -u > /tmp/htmx_targets.txt
grep -rn "\bid=\"" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/" | grep -oP '(?<=id=")\w+' | sort -u > /tmp/html_ids.txt
echo "HTMX targets referencing IDs not found in HTML:"
comm -23 /tmp/htmx_targets.txt /tmp/html_ids.txt | head -20

# 1D. hx-vals với Razor expression bị quote sai (common bug)
echo "=== hx-vals quoting issues ==="
grep -rn "hx-vals" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/" | head -20
# Kiểm tra: hx-vals='{"key": "@Model.Id"}' vs hx-vals='{"key": @Model.Id}'

# 1E. POST/PUT/DELETE form thiếu hx-indicator
echo "=== Mutating HTMX calls missing hx-indicator (no loading feedback) ==="
grep -rn "hx-post\|hx-put\|hx-delete\|hx-patch" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "hx-indicator\|bin/\|obj/" | head -20

# 1F. hx-boost dùng sai scope (nên trên <nav> hoặc <main>, không phải toàn body)
echo "=== hx-boost scope ==="
grep -rn "hx-boost" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/" | head -10
```

**Fix patterns — HTMX core attributes:**

```html
<!-- ❌ Sai: keyup không có debounce → floods server -->
<input hx-get="/search/results"
       hx-trigger="keyup"
       hx-target="#product-grid">

<!-- ✅ Đúng: debounce 400ms + changed modifier (only fires if value changed) -->
<input hx-get="/search/results"
       hx-trigger="keyup changed delay:400ms, search"
       hx-target="#product-grid"
       hx-push-url="true"
       hx-indicator="#search-spinner">

<!-- ❌ Sai: DELETE dùng innerHTML (content replaced, element stays) -->
<button hx-delete="/cart/items/@item.Id"
        hx-target="closest tr"
        hx-swap="innerHTML">Remove</button>

<!-- ✅ Đúng: outerHTML removes the row entirely + transition -->
<button hx-delete="/cart/items/@item.Id"
        hx-target="closest tr"
        hx-swap="outerHTML swap:200ms"
        hx-indicator="#cart-loading">Remove</button>

<!-- ❌ Sai: hx-vals với Razor string cần quote -->
<button hx-post="/cart/items"
        hx-vals='{"variantId": @Model.VariantId, "qty": 1}'>
<!-- ↑ Guid without quotes = invalid JSON -->

<!-- ✅ Đúng: Razor Guid phải có quotes trong JSON string -->
<button hx-post="/cart/items"
        hx-vals='{"variantId": "@Model.VariantId", "qty": 1}'
        hx-indicator="#btn-add-spinner">
  Add to Cart
</button>

<!-- ✅ Loading indicator pattern -->
<button hx-post="/cart/items"
        hx-vals='{"variantId": "@Model.VariantId"}'
        hx-indicator="#add-spinner">
  <span id="add-spinner" class="htmx-indicator">
    <svg class="animate-spin h-4 w-4">...</svg>
  </span>
  Add to Cart
</button>
```

---

### Rule Group 2: hx-boost & hx-push-url

**Quy tắc**: `hx-boost` biến `<a>` thành HTMX request — enhance navigation không full reload.
`hx-push-url` cập nhật URL bar cho shareable state. Cả hai phải dùng đúng scope.

```bash
# 2A. Navigation links thiếu hx-boost (full page reload không cần thiết)
echo "=== Navigation areas missing hx-boost ==="
grep -rn "<nav\|<header" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "hx-boost\|bin/\|obj/" | head -10

# 2B. hx-push-url thiếu trên filter/search (URL không shareable)
echo "=== Filter/search HTMX missing hx-push-url ==="
grep -rn "hx-get.*search\|hx-get.*filter\|hx-get.*page=" \
  src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "hx-push-url\|bin/\|obj/" | head -15

# 2C. hx-boost dùng trên element không phải link/form
echo "=== hx-boost on non-link elements ==="
grep -rn "hx-boost" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "<nav\|<main\|<section\|<header\|<a\|bin/\|obj/" | head -10

# 2D. hx-push-url dùng sai value (phải là URL string hoặc true/false)
echo "=== Potentially wrong hx-push-url values ==="
grep -rn "hx-push-url" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "hx-push-url=\"true\"\|hx-push-url=\"false\"\|hx-push-url=\"/\|bin/\|obj/" | head -10
```

**Fix patterns:**

```html
<!-- ✅ hx-boost trên navigation — tất cả links trong nav sẽ dùng HTMX -->
<nav hx-boost="true">
  <a href="/search">Browse</a>        <!-- → HTMX GET, no full reload -->
  <a href="/account/orders">Orders</a>
  <a href="/seller/dashboard">Dashboard</a>
</nav>

<!-- ✅ Filter với hx-push-url — URL được cập nhật, page shareable -->
<div class="flex gap-2">
  <button hx-get="/seller/products"
          hx-vals='{"status": "active"}'
          hx-target="#product-list"
          hx-push-url="true">            <!-- URL = /seller/products?status=active -->
    Active
  </button>
  <button hx-get="/seller/products"
          hx-vals='{"status": "draft"}'
          hx-target="#product-list"
          hx-push-url="true">
    Draft
  </button>
</div>

<!-- ✅ Pagination — dùng hx-boost hoặc explicit hx-get + hx-push-url -->
<nav hx-boost="true">
  <a href="?page=@(Model.PageNumber + 1)"
     hx-target="#list-container"
     hx-swap="innerHTML">Next →</a>
</nav>
```

---

### Rule Group 3: hx-trigger & Event Patterns

**Quy tắc**: `hx-trigger` phải explicit, có modifier phù hợp. SSE và polling phải có
`Cache-Control: no-store`. HTMX events → Alpine events qua `HX-Trigger` response header.

```bash
# 3A. Polling endpoint thiếu interval specification
echo "=== Polling triggers without proper interval ==="
grep -rn "hx-trigger.*every\|sse:" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/" | head -10

# 3B. SSE endpoint setup
echo "=== SSE endpoints in use ==="
grep -rn "hx-ext=\"sse\"\|sse-connect\|sse-swap" \
  src/MarketNest.Web/ --include="*.cshtml" | grep -v "bin/\|obj/" | head -10
# Server side:
grep -rn "text/event-stream\|EventSource\|IAsyncEnumerable" \
  src/MarketNest.Web/ --include="*.cs" | grep -v "bin/\|obj/" | head -10

# 3C. Polling endpoint thiếu Cache-Control (server side)
echo "=== Polling/SSE endpoints missing Cache-Control: no-store ==="
grep -rn "hx-trigger.*every" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/" | head -10
# Sau đó kiểm tra backend handler tương ứng có Cache-Control không

# 3D. hx-trigger với revealed (infinite scroll) pattern
echo "=== Infinite scroll patterns ==="
grep -rn "hx-trigger.*revealed\|intersect" \
  src/MarketNest.Web/ --include="*.cshtml" | grep -v "bin/\|obj/" | head -5

# 3E. HTMX → Alpine event bus wiring
echo "=== HX-Trigger events fired server-side ==="
grep -rn "HxTrigger\|HX-Trigger\|HX_Trigger" \
  src/MarketNest.Web/ --include="*.cs" | grep -v "bin/\|obj/" | head -20
echo "=== Alpine listeners for HTMX events ==="
grep -rn "htmx:after-request\|x-on:cartUpdated\|x-on:toastShow\|window.addEventListener.*cart\|window.addEventListener.*toast" \
  src/MarketNest.Web/ \( -name "*.cshtml" -o -name "*.js" \) | grep -v "bin/\|obj/" | head -15
```

**Fix patterns — triggers, SSE, polling:**

```html
<!-- ── Polling Pattern (Dispute messages) ──────────────────────────────── -->

<!-- ✅ HTMX polling — server returns fresh HTML every 5s -->
<div id="message-list"
     hx-get="/disputes/@Model.DisputeId/messages"
     hx-trigger="every 5s"
     hx-swap="innerHTML"
     hx-indicator="#msg-spinner">
  @await Html.PartialAsync("_MessageList", Model.Messages)
</div>

<!-- Server handler phải có Cache-Control: no-store -->
<!-- PageModel: -->
<!--
public async Task<IActionResult> OnGetMessagesAsync(Guid disputeId)
{
    Response.Headers["Cache-Control"] = "no-store";
    Response.Headers["Vary"] = "HX-Request";
    var messages = await _sender.Send(new GetMessagesQuery(disputeId));
    return Request.IsHtmx() ? Partial("_MessageList", messages) : RedirectToPage();
}
-->

<!-- ── SSE Pattern (Real-time dispute updates, Phase 2+) ───────────────── -->

<!-- ✅ SSE setup với hx-ext -->
<div hx-ext="sse"
     sse-connect="/disputes/@Model.DisputeId/stream"
     sse-swap="message"
     hx-target="#message-list"
     hx-swap="beforeend">
</div>

<!-- ── HTMX → Alpine Event Bus ─────────────────────────────────────────── -->

<!-- Server fires HX-Trigger header, Alpine listens -->
<!-- PageModel OnPostAddToCartAsync: -->
<!--
    Response.HxTrigger("cartUpdated", new { count = cart.ItemCount, total = cart.TotalFormatted });
    Response.HxTrigger("toastShow", new { message = "Added to cart!", type = "success" });
-->

<!-- Alpine store listener (in _Layout.cshtml or stores/cart.js) -->
<script>
  // ✅ HTMX fires event, Alpine updates store
  document.addEventListener('cartUpdated', (e) => {
    Alpine.store('cart').count = e.detail.count;
    Alpine.store('cart').total = e.detail.total;
  });
  document.addEventListener('toastShow', (e) => {
    Alpine.store('toasts').add(e.detail.message, e.detail.type);
  });

  // ❌ Sai: listening on wrong event name (case-sensitive!)
  // document.addEventListener('CartUpdated', ...) // HTMX triggers are camelCase
</script>

<!-- ── Infinite Scroll ─────────────────────────────────────────────────── -->

<!-- ✅ Load more on scroll into view -->
<div id="product-grid">
  @foreach (var product in Model.Products.Items)
  {
    @await Html.PartialAsync("_ProductCard", product)
  }
</div>

@if (Model.Products.HasNextPage)
{
  <!-- Sentinel element: triggers when scrolled into view -->
  <div hx-get="/search/results?page=@(Model.Products.PageNumber + 1)"
       hx-trigger="revealed"
       hx-target="#product-grid"
       hx-swap="beforeend"
       hx-indicator="#load-more-spinner"
       class="h-4">
  </div>
}
```

---

### Rule Group 4: Antiforgery & Security

**Quy tắc**: Mọi POST/PUT/DELETE HTMX request phải gửi `RequestVerificationToken`.
Config trong `_Layout.cshtml` → `htmx:configRequest` event. Không dùng `hx-headers` thủ công.

```bash
# 4A. HTMX antiforgery config trong _Layout.cshtml
echo "=== Antiforgery config in _Layout ==="
grep -rn "RequestVerificationToken\|htmx:configRequest\|htmx-config.*antiForgery\|AntiForgery" \
  src/MarketNest.Web/Pages/Shared/ --include="*.cshtml" | grep -v "bin/\|obj/" | head -10

# 4B. Form POST thiếu AntiForgeryToken
echo "=== Forms missing AntiForgeryToken ==="
find src/MarketNest.Web/Pages/ -name "*.cshtml" -not -path "*/bin/*" | while read f; do
    has_post=$(grep -c "hx-post\|method=\"post\"\|asp-action\|@Html.BeginForm" "$f" 2>/dev/null || echo 0)
    has_token=$(grep -c "AntiForgeryToken\|__RequestVerificationToken\|asp-antiforgery" "$f" 2>/dev/null || echo 0)
    if [ "$has_post" -gt 0 ] && [ "$has_token" -eq 0 ]; then
        echo "⚠️  $f — has POST but no AntiForgeryToken"
    fi
done

# 4C. Handler thiếu [ValidateAntiForgeryToken] (cho non-HTMX forms)
echo "=== PageModel POST handlers missing [ValidateAntiForgeryToken] ==="
find src/MarketNest.Web/Pages/ -name "*.cshtml.cs" -not -path "*/bin/*" | while read f; do
    has_post=$(grep -c "public.*Task.*OnPost\|public.*IActionResult.*OnPost" "$f" 2>/dev/null || echo 0)
    if [ "$has_post" -gt 0 ]; then
        if ! grep -q "ValidateAntiForgeryToken\|IgnoreAntiforgery\|UseAntiforgery" "$f"; then
            echo "ℹ️  $f — verify antiforgery is handled globally via app.UseAntiforgery()"
        fi
    fi
done

# 4D. hx-headers dùng thủ công trên từng element (nên dùng global config)
echo "=== Manual hx-headers for token (should use global htmx:configRequest) ==="
grep -rn "hx-headers.*RequestVerificationToken\|hx-headers.*antiforgery" \
  src/MarketNest.Web/ --include="*.cshtml" | grep -v "bin/\|obj/" | head -10
```

**Fix — Antiforgery global config trong `_Layout.cshtml`:**

```html
<!-- ✅ Global HTMX config — one place, covers ALL requests -->
<head>
  <!-- HTMX global config -->
  <meta name="htmx-config" content='{
    "defaultSwapStyle": "outerHTML",
    "defaultSettleDelay": 100,
    "historyCacheSize": 10,
    "refreshOnHistoryMiss": true,
    "globalViewTransitions": true
  }'>

  @* Antiforgery token in hidden input — HTMX reads it *@
  @Html.AntiForgeryToken()
</head>

<script>
  // ✅ Global: inject token into every HTMX request
  document.addEventListener('htmx:configRequest', (e) => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (token) e.detail.headers['RequestVerificationToken'] = token;
  });
</script>

<!-- ❌ Anti-pattern: per-element token header -->
<button hx-post="/cart/items"
        hx-headers='{"RequestVerificationToken": "@tokenValue"}'>
<!-- ↑ Duplicates for every element, breaks if token rotates -->
```

---

### Rule Group 5: Alpine.js Patterns

**Quy tắc**: Alpine cho client-only UI state (modal, dropdown, tab). Server cho business logic.
Không duplicate validation logic trong Alpine. `x-cloak` bắt buộc cho x-show/x-if init.

```bash
# 5A. Thiếu x-cloak trên elements dùng x-show/x-if (flash of unstyled content)
echo "=== x-show/x-if missing x-cloak ==="
find src/MarketNest.Web/ -name "*.cshtml" -not -path "*/bin/*" | while read f; do
    # Find x-show or x-if without x-cloak on same or parent element
    matches=$(grep -n "x-show\|x-if" "$f" | head -5)
    if [ -n "$matches" ]; then
        if ! grep -q "x-cloak" "$f"; then
            echo "⚠️  $f — uses x-show/x-if but no x-cloak"
        fi
    fi
done | head -20

# 5B. Alpine.js store initialization từ server data
echo "=== Alpine store initialization from server ==="
grep -rn "\$store\.\|Alpine\.store" src/MarketNest.Web/ \
  \( -name "*.cshtml" -o -name "*.js" \) | grep -v "bin/\|obj/" | head -20

# 5C. x-data inline quá phức tạp (nên extract ra Alpine.data component)
echo "=== Complex inline x-data (should be Alpine.data component) ==="
find src/MarketNest.Web/ -name "*.cshtml" -not -path "*/bin/*" | while read f; do
    while IFS= read -r line; do
        char_count=${#line}
        if echo "$line" | grep -q "x-data=" && [ "$char_count" -gt 150 ]; then
            echo "⚠️  $f — complex x-data inline ($char_count chars), extract to Alpine.data"
            echo "  $line" | cut -c1-100
        fi
    done < "$f"
done | head -20

# 5D. x-html dùng với user content (XSS risk)
echo "=== x-html with potentially unsafe content ==="
grep -rn "x-html" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/\|<!--" | head -10
# x-html bypass sanitization — only use for server-controlled HTML

# 5E. Alpine listening on wrong HTMX event names
echo "=== Alpine HTMX event listeners ==="
grep -rn "htmx:after-request\|htmx:before-request\|htmx:swap\|htmx:settle" \
  src/MarketNest.Web/ \( -name "*.cshtml" -o -name "*.js" \) \
  | grep -v "bin/\|obj/" | head -10

# 5F. Business logic trong Alpine (nên là server)
echo "=== Business logic in Alpine (price calculation, validation) ==="
grep -rn "x-data.*price.*\*\|x-data.*total.*=\|x-text.*\\.toFixed\|Alpine.*calculate" \
  src/MarketNest.Web/ --include="*.cshtml" | grep -v "bin/\|obj/" | head -10
# MarketNest rule: never do money math in Alpine
```

**Fix patterns — Alpine.js:**

```html
<!-- ── x-cloak anti-flicker ──────────────────────────────────────────────── -->

<!-- In _Layout.cshtml <head>: -->
<style> [x-cloak] { display: none !important; } </style>

<!-- ❌ Flash: element visible before Alpine hides it -->
<div x-show="isOpen" class="modal">...</div>

<!-- ✅ x-cloak: hidden until Alpine initializes -->
<div x-show="isOpen" x-cloak class="modal">...</div>

<!-- ── Alpine component vs inline x-data ─────────────────────────────────── -->

<!-- ❌ Complex inline (hard to test, not reusable) -->
<div x-data="{
    ttl: 900,
    interval: null,
    formatted: '15:00',
    init() {
        this.interval = setInterval(() => {
            if (this.ttl > 0) { this.ttl--; this.formatted = ... }
            else clearInterval(this.interval);
        }, 1000);
    }
}">

<!-- ✅ Named component (extracted to wwwroot/js/components/reservationTimer.js) -->
<div x-data="reservationTimer(900)">
  <span x-text="formatted" class="font-mono text-sm"></span>
  <template x-if="ttl < 120">
    <p class="text-red-500 text-sm mt-1">⚠️ Cart expiring soon!</p>
  </template>
</div>

<!-- ── Store initialization from server ──────────────────────────────────── -->

<!-- ✅ Server initializes Alpine store — no client-side fetch needed -->
<div x-data
     x-init="
       $store.cart.count = @Model.CartItemCount;
       $store.user.name  = '@Html.Raw(Json.Serialize(Model.UserName))';
       $store.user.role  = '@Model.UserRole.ToString().ToLower()';
     "
     style="display:none"
     aria-hidden="true">
</div>

<!-- ── Money: never in Alpine ───────────────────────────────────────────────── -->

<!-- ❌ Client-side money calculation — locale/rounding issues -->
<span x-text="`$${(qty * price).toFixed(2)}`"></span>

<!-- ✅ Server formats currency, Alpine just shows/hides -->
<span>@Model.LineTotal.Formatted</span>   <!-- "$12.50" from server -->

<!-- ── x-html safety ─────────────────────────────────────────────────────── -->

<!-- ❌ x-html with user content = XSS -->
<div x-html="userReviewBody"></div>

<!-- ✅ x-text for user content (auto-escaped) -->
<div x-text="userReviewBody"></div>
<!-- Or server-sanitized HTML, marked safe explicitly -->
```

---

### Rule Group 6: Tailwind CSS Audit

**MarketNest brand**: orange `#f97316` = `brand-500`. Tailwind 4.x. Không dùng arbitrary values cho standard UI.

```bash
# 6A. Arbitrary Tailwind values trong core UI (nên dùng design tokens)
echo "=== Arbitrary Tailwind values in views ==="
grep -rn "\[#\|px\[\|pt\[\|pb\[\|pl\[\|pr\[\|w\[\|h\[\|text\[" \
  src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/\|<!--" | head -20
# OK cho 1-off illustrations, NOT OK cho buttons, cards, inputs

# 6B. Inline style thay vì Tailwind class
echo "=== Inline styles (should use Tailwind) ==="
grep -rn "style=\"" src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "bin/\|obj/\|display:none\|aria-hidden\|<!-- " | head -20
# Exception: display:none cho Alpine init divs

# 6C. Brand color chuẩn chưa (phải dùng brand-500 không phải orange-500)
echo "=== Non-brand orange color classes ==="
grep -rn "orange-[0-9]\|text-orange-\|bg-orange-\|border-orange-" \
  src/MarketNest.Web/ --include="*.cshtml" | grep -v "bin/\|obj/\|<!--" | head -10
# Phải dùng: brand-500, brand-600, brand-100 v.v.

# 6D. htmx-indicator CSS class setup
echo "=== HTMX indicator CSS defined ==="
grep -rn "htmx-indicator\|htmx-request" \
  src/MarketNest.Web/wwwroot/css/ | head -5
# Must have: .htmx-indicator { display: none; }
#            .htmx-request .htmx-indicator { display: flex; }

# 6E. Responsive breakpoints — mobile first
echo "=== Non-responsive UI patterns ==="
grep -rn "class=\"" src/MarketNest.Web/ --include="*.cshtml" \
  | grep "grid-cols-[3-9]\|flex.*gap" | grep -v "md:\|lg:\|sm:\|bin/\|obj/" | head -10
# Grids với 3+ columns phải có responsive prefix

# 6F. Duplicate Tailwind utility classes trên cùng element
echo "=== Potentially duplicate Tailwind classes ==="
find src/MarketNest.Web/ -name "*.cshtml" -not -path "*/bin/*" | while read f; do
    grep -n "class=\"" "$f" | while read line; do
        classes=$(echo "$line" | grep -oP '(?<=class=")[^"]*')
        dupes=$(echo "$classes" | tr ' ' '\n' | sort | uniq -d | tr '\n' ' ')
        if [ -n "$dupes" ]; then
            echo "⚠️  $f — duplicate classes: $dupes"
        fi
    done
done | head -20

# 6G. Status badge consistency (phải dùng StatusBadgeHelper)
echo "=== Hardcoded status badge colors (should use StatusBadgeHelper) ==="
grep -rn "badge-\|bg-green-.*status\|bg-red-.*status\|text.*Confirmed\|text.*Shipped" \
  src/MarketNest.Web/ --include="*.cshtml" \
  | grep -v "StatusBadgeHelper\|GetCssClass\|bin/\|obj/" | head -10
```

**Fix patterns — Tailwind:**

```html
<!-- ── HTMX indicator CSS (must be in input.css or _Layout style block) ──── -->
<style>
  /* Required for HTMX loading indicators */
  .htmx-indicator       { display: none; }
  .htmx-request .htmx-indicator { display: inline-flex; align-items: center; }
  .htmx-request.htmx-indicator  { display: inline-flex; }

  /* Opacity fade during swap */
  .htmx-swapping { opacity: 0; transition: opacity 200ms ease; }
</style>

<!-- ── Brand color: orange → brand ────────────────────────────────────────── -->

<!-- ❌ Direct Tailwind orange (doesn't pick up brand customization) -->
<button class="bg-orange-500 hover:bg-orange-600 text-white">Add to Cart</button>

<!-- ✅ Brand token (defined in input.css as @theme { --color-brand-500: #f97316; }) -->
<button class="bg-brand-500 hover:bg-brand-600 text-white px-4 py-2 rounded-lg
               font-medium transition-colors duration-150">Add to Cart</button>

<!-- ── Responsive grid ──────────────────────────────────────────────────── -->

<!-- ❌ 3-col grid, breaks on mobile -->
<div class="grid grid-cols-3 gap-4">

<!-- ✅ Mobile-first responsive -->
<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">

<!-- ── Status badge — use helper, not hardcoded ─────────────────────────── -->

<!-- ❌ Hardcoded per-page -->
<span class="badge bg-green-100 text-green-700">@order.Status</span>

<!-- ✅ Consistent via helper -->
<span class="badge @StatusBadgeHelper.GetCssClass(order.Status.ToString())">
  @order.Status
</span>

<!-- ── Inline style: only acceptable exception ───────────────────────────── -->
<!-- Alpine init div — legitimate use of display:none inline -->
<div x-data x-init="$store.cart.count = @Model.CartCount"
     style="display:none" aria-hidden="true"></div>
```

---

### Rule Group 7: Razor Partial Structure

**Convention**: Partials bắt đầu `_`, có strongly-typed `@model`, không có layout.
Partial được return từ PageModel khi `Request.IsHtmx()`.

```bash
# 7A. Partial không có @model declaration (untyped)
echo "=== Partials without @model declaration ==="
find src/MarketNest.Web/Pages/Shared/ -name "*.cshtml" -not -path "*/bin/*" \
  | while read f; do
    if ! grep -q "^@model " "$f"; then
        echo "⚠️  MEDIUM: $f — missing @model declaration"
    fi
done

# 7B. Partial có @{ Layout = ... } (partial không nên có layout)
echo "=== Partials with Layout set (should be null or empty) ==="
find src/MarketNest.Web/Pages/Shared/ -name "*.cshtml" -not -path "*/bin/*" \
  | xargs grep -l "Layout\s*=" 2>/dev/null | grep -v "bin/\|obj/" | head -10

# 7C. Partial không bắt đầu bằng _ (convention violation)
echo "=== Partials not following _ prefix convention ==="
find src/MarketNest.Web/Pages/Shared/ -name "*.cshtml" -not -path "*/bin/*" \
  | while read f; do
    name=$(basename "$f")
    if ! echo "$name" | grep -q "^_"; then
        echo "⚠️  $f — partial should start with _"
    fi
done

# 7D. PageModel trả về Partial() nhưng không check IsHtmx()
echo "=== Handlers returning Partial without IsHtmx check ==="
find src/MarketNest.Web/Pages/ -name "*.cshtml.cs" -not -path "*/bin/*" \
  | xargs grep -ln "return Partial\b" 2>/dev/null | while read f; do
    if ! grep -q "IsHtmx\|isHtmx\|HX-Request" "$f"; then
        echo "⚠️  $f — returns Partial() but doesn't check IsHtmx()"
    fi
done

# 7E. HTMX error response không dùng HX-Retarget
echo "=== Error handlers not setting HX-Retarget ==="
find src/MarketNest.Web/Pages/ -name "*.cshtml.cs" -not -path "*/bin/*" \
  | xargs grep -hn "IsFailure\|ModelState" 2>/dev/null \
  | grep -v "bin/\|obj/\|//" | while read line; do
    if echo "$line" | grep -q "return.*Partial\|return.*Page()"; then
        if ! echo "$line" | grep -q "HX-Retarget\|HxRetarget"; then
            echo "⚠️  Consider HX-Retarget for error: $line"
        fi
    fi
done | head -10

# 7F. Partial thiếu id root element (HTMX target cần id để swap)
echo "=== Partials missing root id element ==="
find src/MarketNest.Web/Pages/Shared/ -name "_*.cshtml" \
  -not -path "*/bin/*" | while read f; do
    if ! grep -q 'id="' "$f"; then
        echo "ℹ️  $f — no id on root element (may be intentional)"
    fi
done | head -15
```

**Fix patterns — Razor Partials:**

```razor
@* ── _CartSummary.cshtml — correct partial structure ─────────────────────── *@

@model CartSummaryViewModel  @* ✅ Strongly typed *@
@* No @{ Layout = "_Layout" } — partials have no layout *@

@* ✅ Root element with id — enables hx-target="#cart-summary" *@
<div id="cart-summary"
     class="flex items-center gap-2">
  <span class="badge badge-orange" x-text="$store.cart.count">@Model.ItemCount</span>
  <span class="text-sm text-gray-600">@Model.TotalFormatted</span>
</div>

@* ── PageModel handler — IsHtmx check ──────────────────────────────────────── *@
@* CartPage.cshtml.cs *@

public async Task<IActionResult> OnGetSummaryAsync()
{
    var cart = await _sender.Send(new GetCartSummaryQuery(User.GetUserId()));

    @* ✅ Check before returning partial *@
    return Request.IsHtmx()
        ? Partial("_CartSummary", cart)
        : RedirectToPage("/Cart");
}

public async Task<IActionResult> OnPostAddItemAsync(AddToCartRequest request)
{
    var result = await _sender.Send(request.ToCommand(User.GetUserId()));

    if (result.IsFailure)
    {
        if (Request.IsHtmx())
        {
            @* ✅ Retarget error to error region *@
            Response.Headers["HX-Retarget"] = "#cart-error";
            Response.Headers["HX-Reswap"]   = "outerHTML";
            return Partial("_CartError", result.Error.Message);
        }
        ModelState.AddModelError("", result.Error.Message);
        return Page();
    }

    @* ✅ Fire Alpine events via HX-Trigger *@
    Response.HxTrigger("cartUpdated", new { count = result.Value.ItemCount });
    Response.HxTrigger("toastShow", new { message = "Added to cart!", type = "success" });

    return Request.IsHtmx()
        ? Partial("_CartSummary", result.Value)
        : RedirectToPage("/Cart");
}
```

---

## Phase 3: REPORT — Frontend Quality Report

```markdown
# Frontend HTMX Review Report — MarketNest
**Date**: <ngày>
**Stack**: HTMX 2.x + Alpine.js 3.x + Tailwind CSS 4.x + Razor Pages

---

## Tổng quan

| Hạng mục | Score (1-10) | Findings |
|---|---|---|
| HTMX Core Attributes | X/10 | X issues |
| hx-boost & hx-push-url | X/10 | X issues |
| Triggers & SSE/Polling | X/10 | X issues |
| Antiforgery Security | X/10 | X issues |
| Alpine.js Patterns | X/10 | X issues |
| Tailwind CSS Audit | X/10 | X issues |
| Razor Partial Structure | X/10 | X issues |

---

## 🔴 BLOCKER

### [B-001] POST requests missing antiforgery token
- **File**: `Pages/Cart/Index.cshtml`
- **Vi phạm**: `hx-post="/cart/items"` không có `RequestVerificationToken`
- **Fix**: Add global `htmx:configRequest` listener in `_Layout.cshtml`

---

## 🟠 HIGH

### [H-001] keyup trigger without debounce — search floods server
- **File**: `Pages/Search/Index.cshtml:34`
- **Fix**: `hx-trigger="keyup changed delay:400ms, search"`

---

## 🟡 MEDIUM / 💡 SUGGESTION
...
```

---

## Phase 5: VERIFY — Visual checklist

```bash
# Build Tailwind để verify không có class typo
cd src/MarketNest.Web && npm run build:css

# Check .NET build (Razor compile errors)
dotnet build src/MarketNest.Web --no-incremental 2>&1 | grep -i "error\|warning" | head -20

# Integration test: HTMX request returns partial (not full page)
dotnet test tests/MarketNest.IntegrationTests \
  --filter "FullyQualifiedName~HtmxRequest" --no-build

# Manual checklist sau fix:
echo "Manual checks:"
echo "  [ ] x-cloak: no flash of content on page load"
echo "  [ ] Forms: network tab shows RequestVerificationToken header"
echo "  [ ] Delete: element removed from DOM, not just content cleared"
echo "  [ ] Search: only fires after 400ms idle (check network tab)"
echo "  [ ] Cart update: header badge count updates without reload"
echo "  [ ] Toast: appears after add-to-cart, auto-dismisses after 4s"
echo "  [ ] Polling: dispute messages refresh every 5s"
echo "  [ ] hx-push-url: URL bar updates on filter/pagination"
echo "  [ ] Mobile: grid responsive, nav works on small screens"
echo "  [ ] HX-Redirect: full nav after checkout, no partial swap"
```

---

## Quick Reference — MarketNest HTMX/Alpine Cheatsheet

| Scenario | HTMX pattern | Alpine role |
|---|---|---|
| Load partial | `hx-get hx-target hx-swap="innerHTML"` | — |
| Form submit | `hx-post hx-indicator` → `HX-Redirect` | — |
| Delete row | `hx-delete hx-target="closest tr" hx-swap="outerHTML"` | Optional confirm |
| Search/filter | `hx-get hx-trigger="keyup delay:400ms" hx-push-url="true"` | — |
| Pagination | `hx-boost="true"` on `<nav>` | — |
| Cart update | `hx-post` → server fires `HX-Trigger: cartUpdated` | `$store.cart` listens |
| Toast | Server fires `HX-Trigger: toastShow` | `$store.toasts.add()` |
| Modal open | Server fires `HX-Trigger: modalOpen` | `x-show="open" x-cloak` |
| Polling | `hx-trigger="every 5s"` + `Cache-Control: no-store` | — |
| Infinite scroll | `hx-trigger="revealed" hx-swap="beforeend"` | — |
| Status badge | `@StatusBadgeHelper.GetCssClass(status)` | — |
| Money display | `@Model.TotalFormatted` (server formats) | Never in Alpine |
| Anti-flicker | `x-cloak` + `[x-cloak] { display: none }` in CSS | Required |
| Init from server | `x-init="$store.cart.count = @Model.Count"` | On hidden div |
| Confirm dialog | `Alpine.data('confirmDialog')` | `isOpen`, `open()`, `confirm()` |
