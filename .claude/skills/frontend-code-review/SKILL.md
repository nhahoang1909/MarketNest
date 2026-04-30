---
name: frontend-code-review
description: >
  Scan the entire frontend codebase for comprehensive quality review: CSS variables and design
  tokens, HTML semantics, JavaScript patterns, performance (Core Web Vitals, image, loading),
  accessibility (WCAG AA, ARIA, keyboard), HTMX attribute correctness, Alpine.js patterns,
  Tailwind CSS audit, Razor partial structure, antiforgery, SSE/polling, component design.
  Use this skill when the user wants to: review frontend, check code quality, audit UI,
  check CSS, review HTML, check accessibility, optimize performance, review Alpine, check HTMX,
  audit Tailwind, find frontend anti-patterns, or says "review frontend", "check UI",
  "frontend audit", "CSS review", "a11y", "web vitals", "frontend code rules".
  Activate when the user uploads *.cshtml, *.css, *.js, *.html files or asks about UI/UX issues.
compatibility:
  tools: [bash, read_file, write_file, list_files, grep_search, run_in_terminal]
  agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
  stack: [HTMX 2.x, Alpine.js 3.x, Tailwind CSS 4.x, Razor Pages .NET 10, Vanilla JS]
---

# Frontend Code Review Skill — MarketNest

Skill duy nhất cover toàn bộ frontend quality: từ CSS architecture đến performance, từ semantic HTML
đến accessibility, từ JS patterns đến HTMX/Alpine specifics của MarketNest.

Mỗi rule group độc lập — chạy group nào phù hợp với file được review.

---

## Quy trình thực thi

```
Phase 1: SCAN    → Xác định stack, liệt kê files, chọn rule groups phù hợp
Phase 2: ANALYZE → Chạy grep + đọc file theo 8 rule groups
Phase 3: REPORT  → BLOCKER / HIGH / MEDIUM / SUGGESTION với fix code
Phase 4: FIX     → Apply fix (hỏi xác nhận trước)
Phase 5: VERIFY  → Build CSS, browser checklist
```

---

## Phase 1: SCAN — Xác định scope

```bash
# 1A. Detect stack
echo "=== Frontend stack detection ==="
find . -name "*.cshtml" | grep -v "bin/\|obj/" | wc -l | xargs echo "Razor files:"
find . -name "*.css" -o -name "*.scss" | grep -v "bin/\|node_modules\|min\." | wc -l | xargs echo "CSS files:"
find . \( -name "*.tsx" -o -name "*.jsx" \) | grep -v "node_modules\|bin/" | wc -l | xargs echo "React files:"
find . -name "*.vue" | grep -v "node_modules" | wc -l | xargs echo "Vue files:"
find . \( -name "*.js" -o -name "*.ts" \) \
  | grep -v "node_modules\|bin/\|obj/\|\.min\.\|\.spec\.\|\.test\." | wc -l | xargs echo "JS/TS files:"

# 1B. Quick inventory
echo "=== File inventory ==="
find . \( -name "*.cshtml" -o -name "*.html" -o -name "input.css" -o -name "*.scss" \) \
  -not -path "*/bin/*" -not -path "*/node_modules/*" | sort | head -30

echo "=== JS components ==="
find . \( -name "*.js" -o -name "*.ts" \) \
  -not -path "*/node_modules/*" -not -path "*/bin/*" \
  -not -name "*.min.js" -not -name "*.spec.*" | sort | head -20
```

---

## Phase 2: ANALYZE — 8 Rule Groups

---

### Rule Group 1: CSS Variables & Design Tokens

**Rule**: Màu sắc, spacing, font phải dùng CSS variable hoặc design token — không hardcode giá trị
raw trong component. Một chỗ thay → toàn site cập nhật.

```bash
echo "=== 1A. Hardcoded hex colors in CSS/HTML ==="
# Tìm màu hex không phải trong :root hoặc theme definition
grep -rn "#[0-9a-fA-F]\{3,6\}" . \
  --include="*.css" --include="*.scss" --include="*.cshtml" --include="*.html" \
  | grep -v ":root\|@theme\|tailwind.config\|input.css\|node_modules\|bin/\|<!--\|//" \
  | grep -v "^--\|svg\|stroke=\|fill=" | head -20

echo "=== 1B. Hardcoded pixel values (magic numbers) ==="
# spacing/size không phải từ token
grep -rn "style=\".*[0-9]px\|style=\".*[0-9]rem" . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "display:none\|0px\|bin/\|obj/" | head -20

echo "=== 1C. CSS variables defined but not used in :root ==="
grep -rn "^--" . --include="*.css" --include="*.scss" \
  | grep -v "node_modules\|bin/" | head -20

echo "=== 1D. Dark mode — hardcoded light colors (won't invert) ==="
grep -rn "color:\s*#[0-9a-fA-F]\|background:\s*#[0-9a-fA-F]" . \
  --include="*.css" --include="*.scss" \
  | grep -v ":root\|@media.*dark\|node_modules\|bin/" | head -15
```

**Fix:**
```css
/* ❌ Hardcoded everywhere — change 1 color = update 50 files */
.btn { background: #f97316; color: #ffffff; }
.card { border: 1px solid #e5e7eb; }

/* ✅ CSS variables — change :root → whole site updates */
:root {
  --color-brand:       #f97316;
  --color-brand-hover: #ea6a0a;
  --color-surface:     #ffffff;
  --color-border:      #e5e7eb;
  --color-text:        #111827;

  /* Dark mode overrides */
  @media (prefers-color-scheme: dark) {
    --color-surface: #1f2937;
    --color-border:  #374151;
    --color-text:    #f9fafb;
  }
}

.btn  { background: var(--color-brand); color: #fff; }
.card { border: 1px solid var(--color-border); }

/* Tailwind 4.x: use @theme block */
@theme {
  --color-brand-500: #f97316;
  --color-brand-600: #ea6a0a;
  --font-sans: 'Inter var', system-ui, sans-serif;
}
```

---

### Rule Group 2: CSS Architecture & Naming

**Rule**: Consistent naming (BEM hoặc utility-first), không dùng `!important`, không global selector
override, specificity tối thiểu, `@layer` cho cascade control.

```bash
echo "=== 2A. !important usage ==="
grep -rn "!important" . --include="*.css" --include="*.scss" \
  | grep -v "node_modules\|bin/" | head -20
# !important = specificity war symptom

echo "=== 2B. ID selectors for styling ==="
grep -rn "^#[a-zA-Z]\|^\s*#[a-zA-Z]" . --include="*.css" --include="*.scss" \
  | grep -v "node_modules\|bin/\|//" | head -10
# #id { style } → specificity 100, impossible to override

echo "=== 2C. Duplicate class definitions ==="
# Extract class names, find duplicates
grep -rn "^\.[a-zA-Z]" . --include="*.css" --include="*.scss" \
  | grep -v "node_modules\|bin/\|hover:\|focus:\|media" \
  | awk -F: '{print $2}' | sort | uniq -d | head -10

echo "=== 2D. Deeply nested selectors (specificity issue) ==="
grep -rn "\s\s\s\s\.[a-zA-Z].*\.[a-zA-Z].*\.[a-zA-Z]" . \
  --include="*.css" --include="*.scss" \
  | grep -v "node_modules\|bin/" | head -10
# 4+ levels deep = specificity too high

echo "=== 2E. Global element selector override ==="
grep -rn "^a\s*{\|^p\s*{\|^h[1-6]\s*{\|^button\s*{" . \
  --include="*.css" --include="*.scss" \
  | grep -v "node_modules\|bin/\|:root\|normalize\|reset" | head -10
```

**Fix:**
```css
/* ❌ Specificity wars */
#main .content div.wrapper p.text { color: red !important; }

/* ✅ Single class, @layer for ordering */
@layer base { p { line-height: 1.6; } }
@layer components { .text-body { color: var(--color-text); } }
@layer utilities  { .text-error { color: var(--color-danger); } }

/* ✅ BEM naming — flat, predictable specificity */
.product-card { }                    /* block */
.product-card__title { }             /* element */
.product-card--featured { }          /* modifier */
.product-card__price--discounted { } /* element + modifier */
```

---

### Rule Group 3: Semantic HTML & Structure

**Rule**: Dùng đúng HTML element cho ngữ nghĩa. Heading hierarchy không skip cấp.
Form label liên kết đúng. Không dùng `<div>` / `<span>` khi có semantic element.

```bash
echo "=== 3A. Heading hierarchy violations ==="
# Tìm trang có h3 nhưng thiếu h2 (skip level)
find . \( -name "*.cshtml" -o -name "*.html" \) -not -path "*/bin/*" | while read f; do
    has_h3=$(grep -c "<h3" "$f" 2>/dev/null || echo 0)
    has_h2=$(grep -c "<h2" "$f" 2>/dev/null || echo 0)
    if [ "$has_h3" -gt 0 ] && [ "$has_h2" -eq 0 ]; then
        echo "⚠️  $f — has h3 but no h2 (skipped heading level)"
    fi
done | head -10

echo "=== 3B. Clickable divs (should be button/a) ==="
grep -rn "<div.*onclick\|<span.*onclick\|<div.*@click\|<span.*@click" . \
  --include="*.cshtml" --include="*.html" --include="*.jsx" --include="*.tsx" \
  | grep -v "bin/\|obj/" | head -15
# Div không có keyboard access, no role → không accessible

echo "=== 3C. Images missing alt text ==="
grep -rn "<img " . \
  --include="*.cshtml" --include="*.html" --include="*.jsx" --include="*.tsx" \
  | grep -v "alt=\|bin/\|obj/" | head -15

echo "=== 3D. Icon-only buttons missing accessible label ==="
grep -rn "<button\|<a " . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "aria-label\|aria-labelledby\|bin/\|obj/" \
  | grep -i "icon\|svg\|×\|✕\|close\|menu" | head -10

echo "=== 3E. Form inputs missing label ==="
grep -rn "<input " . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "type=\"hidden\"\|aria-label\|id=\|bin/\|obj/" | head -15
# Input phải có <label for="id"> hoặc aria-label

echo "=== 3F. Missing lang attribute on html ==="
grep -rn "<html" . --include="*.cshtml" --include="*.html" \
  | grep -v "lang=\|bin/\|obj/" | head -5
```

**Fix:**
```html
<!-- ❌ Clickable div — not keyboard accessible -->
<div onclick="addToCart()" class="cursor-pointer">Add to Cart</div>

<!-- ✅ Button — keyboard, focus, ENTER/SPACE, screen reader -->
<button type="button" onclick="addToCart()" class="btn-primary">Add to Cart</button>

<!-- ❌ Image: no alt -->
<img src="/products/shirt.webp">

<!-- ✅ Descriptive alt; empty alt="" for decorative -->
<img src="/products/shirt.webp"
     alt="Navy blue linen shirt, front view"
     width="400" height="400"
     loading="lazy">

<!-- ❌ Icon button: no label -->
<button><svg>...</svg></button>

<!-- ✅ aria-label for icon-only -->
<button type="button" aria-label="Remove item from cart">
  <svg aria-hidden="true" focusable="false">...</svg>
</button>

<!-- ❌ Input without label -->
<input type="text" placeholder="Search products">

<!-- ✅ Explicit label association -->
<label for="search-input" class="sr-only">Search products</label>
<input type="text" id="search-input"
       placeholder="Search products"
       autocomplete="off">
```

---

### Rule Group 4: JavaScript Patterns

**Rule**: Event delegation, cleanup listeners, async error handling, không block main thread,
không do money math on client, không duplicate server validation logic.

```bash
echo "=== 4A. Event listeners without cleanup (memory leak) ==="
grep -rn "addEventListener" . \
  --include="*.js" --include="*.ts" --include="*.jsx" --include="*.tsx" \
  | grep -v "removeEventListener\|AbortController\|node_modules\|bin/" | head -20

echo "=== 4B. Unhandled Promise rejections ==="
grep -rn "\.catch\b\|try\s*{" . \
  --include="*.js" --include="*.ts" | grep -v "node_modules\|bin/" | wc -l
# Và tìm fetch/async không có catch
grep -rn "await fetch\|\.then(" . \
  --include="*.js" --include="*.ts" \
  | grep -v "\.catch\|try\|node_modules\|bin/" | head -15

echo "=== 4C. Synchronous localStorage/sessionStorage (blocks render) ==="
grep -rn "localStorage\.\|sessionStorage\." . \
  --include="*.js" --include="*.ts" --include="*.jsx" --include="*.tsx" \
  | grep -v "node_modules\|bin/\|//" | head -10

echo "=== 4D. Money math on client (floating point) ==="
grep -rn "\.toFixed\|price.*\*\|qty.*price\|amount.*\+" . \
  --include="*.js" --include="*.ts" --include="*.cshtml" \
  | grep -v "node_modules\|bin/\|test\|Test" | head -10
# Money = server side ONLY

echo "=== 4E. eval() or dangerous string execution ==="
grep -rn "\beval\b\|new Function\|innerHTML\s*=" . \
  --include="*.js" --include="*.ts" \
  | grep -v "node_modules\|bin/\|//\|test" | head -10

echo "=== 4F. Await in loop (sequential, should be Promise.all) ==="
grep -rn "for.*await\|forEach.*await" . \
  --include="*.js" --include="*.ts" \
  | grep -v "node_modules\|bin/" | head -10
```

**Fix patterns:**
```javascript
// ── Memory-safe event listener ──────────────────────────────────────────────
// ❌ Leak: listener not removed when component unmounts
element.addEventListener('click', handleClick)

// ✅ AbortController — bulk cleanup
const controller = new AbortController()
element.addEventListener('click', handleClick, { signal: controller.signal })
element.addEventListener('keydown', handleKey, { signal: controller.signal })
// Cleanup:
controller.abort() // removes ALL listeners registered with this signal

// ── Promise.all vs sequential ───────────────────────────────────────────────
// ❌ Sequential (slow: 3 × network latency)
const user     = await fetchUser(id)
const orders   = await fetchOrders(id)
const wishlist = await fetchWishlist(id)

// ✅ Parallel
const [user, orders, wishlist] = await Promise.all([
  fetchUser(id),
  fetchOrders(id),
  fetchWishlist(id)
])

// ── Client-side money: NEVER ────────────────────────────────────────────────
// ❌ Float precision bug
const total = (2.99 + 1.01).toFixed(2) // "4.00"? Actually: 3.9999999...

// ✅ Format server-side, display only
// C#: order.Total.ToString("C", CultureInfo.GetCultureInfo("en-SG"))
// HTML: <span>@Model.TotalFormatted</span>
```

---

### Rule Group 5: Performance — Core Web Vitals

**Targets MarketNest**: LCP < 2.5s, CLS < 0.1, INP < 200ms, JS bundle < 50KB gzipped.

```bash
echo "=== 5A. Images missing width/height (CLS cause) ==="
grep -rn "<img " . \
  --include="*.cshtml" --include="*.html" --include="*.jsx" \
  | grep -v "width=\|height=\|bin/\|obj/" | head -15
# Missing dimensions → browser can't reserve space → layout shift

echo "=== 5B. Images missing lazy loading (below-fold) ==="
grep -rn "<img " . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "loading=\|bin/\|obj/" | head -15

echo "=== 5C. Render-blocking scripts ==="
grep -rn "<script src\|<script type" . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "defer\|async\|type=\"module\"\|bin/\|obj/" | head -10

echo "=== 5D. Missing font-display for web fonts ==="
grep -rn "@font-face\|font-family:.*url" . \
  --include="*.css" --include="*.scss" \
  | grep -v "font-display\|node_modules" | head -5

echo "=== 5E. Images without modern format (still jpg/png) ==="
grep -rn "\.jpg\"\|\.jpeg\"\|\.png\"" . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "bin/\|obj/\|og:image\|twitter:" | head -15

echo "=== 5F. No preconnect for critical origins ==="
grep -rn "preconnect\|dns-prefetch" . --include="*.cshtml" --include="*.html" \
  | grep -v "bin/\|obj/" | head -5
# CDN, fonts, analytics should have preconnect

echo "=== 5G. Animations on non-composited properties (layout thrashing) ==="
grep -rn "transition:.*width\|transition:.*height\|transition:.*top\|transition:.*left\|transition:.*margin" . \
  --include="*.css" --include="*.scss" \
  | grep -v "node_modules\|bin/" | head -10
# Only animate: transform, opacity (GPU composited)
```

**Fix:**
```html
<!-- ── LCP: hero image above fold ──────────────────────────────────────────── -->
<!-- ❌ No priority -->
<img src="/hero.jpg" alt="Hero banner">

<!-- ✅ fetchpriority=high + preload link + modern format -->
<link rel="preload" as="image" href="/hero.webp" fetchpriority="high">
<img src="/hero.webp"
     alt="MarketNest — discover unique products"
     width="1200" height="600"
     fetchpriority="high">

<!-- ── CLS: reserve image space ─────────────────────────────────────────────── -->
<!-- ❌ No dimensions → CLS as image loads -->
<img src="/products/item.jpg" alt="Product">

<!-- ✅ Explicit dimensions → browser reserves space immediately -->
<img src="/products/item.webp"
     alt="Blue ceramic mug"
     width="300" height="300"
     loading="lazy"
     decoding="async">

<!-- ── Non-blocking scripts ────────────────────────────────────────────────── -->
<!-- ❌ Render-blocking -->
<script src="/js/app.js"></script>

<!-- ✅ Deferred — executes after HTML parsed -->
<script src="/js/app.js" defer></script>

<!-- ── Font performance ─────────────────────────────────────────────────────── -->
<!-- ✅ preconnect + font-display: swap -->
<link rel="preconnect" href="https://fonts.googleapis.com">
<style>
  @font-face {
    font-family: 'Inter';
    src: url('/fonts/inter.woff2') format('woff2');
    font-display: swap; /* Show fallback font immediately */
  }
</style>
```

---

### Rule Group 6: Accessibility (WCAG AA)

**Targets**: Contrast ≥ 4.5:1, keyboard navigable, `aria-live` cho dynamic content,
focus visible, skip link, screen reader compatibility.

```bash
echo "=== 6A. Focus styles removed (keyboard navigation broken) ==="
grep -rn "outline:\s*none\|outline:\s*0\|:focus\s*{\s*outline" . \
  --include="*.css" --include="*.scss" \
  | grep -v "focus-visible\|focus-within\|node_modules\|bin/" | head -15
# outline: none WITHOUT focus-visible replacement = keyboard users lost

echo "=== 6B. Interactive elements missing focus-visible style ==="
grep -rn "\.btn\|\.button\|button\s*{" . --include="*.css" --include="*.scss" \
  | head -5
# Then check if :focus-visible is defined

echo "=== 6C. aria-live regions for dynamic content (HTMX swaps) ==="
grep -rn "aria-live\|role=\"alert\"\|role=\"status\"" . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "bin/\|obj/" | head -10
# HTMX-injected content must have aria-live="polite" parent

echo "=== 6D. Modal missing focus trap ==="
grep -rn "_Modal\|x-data.*modal\|dialog\|role=\"dialog\"" . \
  --include="*.cshtml" --include="*.html" --include="*.js" \
  | grep -v "bin/\|obj/\|//" | head -10
# Check: does modal trap focus? Does ESC close it?

echo "=== 6E. Skip navigation link ==="
grep -rn "skip.*nav\|skip.*main\|sr-only.*skip" . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "bin/\|obj/" | head -5

echo "=== 6F. prefers-reduced-motion not respected ==="
grep -rn "animation\|@keyframes\|transition" . \
  --include="*.css" --include="*.scss" \
  | grep -v "node_modules\|bin/\|prefers-reduced-motion" | wc -l
# Every animation should have reduced-motion fallback
grep -rn "prefers-reduced-motion" . --include="*.css" --include="*.scss" \
  | grep -v "node_modules" | head -5

echo "=== 6G. Color-only status indicators ==="
# Icons without text = color as sole indicator
grep -rn "badge-red\|badge-green\|text-red\|text-green" . \
  --include="*.cshtml" --include="*.html" \
  | grep -v "aria-label\|sr-only\|bin/\|obj/" | head -10
```

**Fix:**
```css
/* ── Focus styles — NEVER remove, always replace ──────────────────────────── */

/* ❌ Breaks keyboard navigation */
button:focus { outline: none; }

/* ✅ Remove default, add custom visible ring */
button:focus          { outline: none; }   /* Hide browser default */
button:focus-visible  {                    /* Show for keyboard only */
  outline: 2px solid var(--color-brand);
  outline-offset: 2px;
  border-radius: 4px;
}

/* ── prefers-reduced-motion ────────────────────────────────────────────────── */
.animate-spin {
  animation: spin 1s linear infinite;
}
@media (prefers-reduced-motion: reduce) {
  .animate-spin { animation: none; }
}

/* ── Skip link ─────────────────────────────────────────────────────────────── */
/* In _Layout.cshtml: */
```

```html
<!-- Skip navigation — first focusable element on page -->
<a href="#main-content"
   class="sr-only focus:not-sr-only focus:fixed focus:top-4 focus:left-4
          focus:z-50 focus:px-4 focus:py-2 focus:bg-brand-500 focus:text-white
          focus:rounded-lg focus:shadow-lg">
  Skip to main content
</a>

<!-- Main content landmark -->
<main id="main-content">...</main>

<!-- HTMX: aria-live for dynamic regions -->
<div id="product-grid"
     aria-live="polite"
     aria-atomic="false"
     aria-busy="false">
  <!-- HTMX injects here — screen reader announces new content -->
</div>

<!-- Color indicator with text backup -->
<!-- ❌ Color only -->
<span class="badge-red">Failed</span>

<!-- ✅ Color + text + icon -->
<span class="badge-red" role="status">
  <svg aria-hidden="true"><!-- error icon --></svg>
  Failed
</span>
```

---

### Rule Group 7: HTMX & Alpine.js (MarketNest-specific)

**Rule**: HTMX attribute correctness, Alpine patterns, antiforgery global config, x-cloak,
SSE Cache-Control, không duplicate business logic trong Alpine.

```bash
echo "=== 7A. HTMX trigger without debounce on input ==="
grep -rn "hx-trigger.*keyup\|hx-trigger.*input" . --include="*.cshtml" \
  | grep -v "delay:\|bin/\|obj/" | head -10

echo "=== 7B. Missing hx-indicator on mutating requests ==="
grep -rn "hx-post\|hx-put\|hx-delete" . --include="*.cshtml" \
  | grep -v "hx-indicator\|bin/\|obj/" | head -10

echo "=== 7C. x-show/x-if missing x-cloak (FOUC) ==="
find . -name "*.cshtml" -not -path "*/bin/*" | while read f; do
    if grep -q "x-show\|x-if" "$f" && ! grep -q "x-cloak" "$f"; then
        echo "⚠️  $f — x-show/x-if without x-cloak (flash of content)"
    fi
done | head -10

echo "=== 7D. Antiforgery global config ==="
grep -rn "htmx:configRequest\|RequestVerificationToken" . \
  --include="*.cshtml" | grep -v "bin/\|obj/" | head -5
# Must exist in _Layout.cshtml

echo "=== 7E. Business logic in Alpine (should be server) ==="
grep -rn "x-data.*price.*\*\|x-text.*\.toFixed\|x-data.*total\s*=" . \
  --include="*.cshtml" | grep -v "bin/\|obj/" | head -10

echo "=== 7F. Partial returned without IsHtmx() check ==="
find . -name "*.cshtml.cs" -not -path "*/bin/*" \
  | xargs grep -ln "return Partial" 2>/dev/null | while read f; do
    ! grep -q "IsHtmx\|HX-Request" "$f" && echo "⚠️  $f"
done | head -10

echo "=== 7G. Polling missing Cache-Control: no-store ==="
grep -rn "hx-trigger.*every\s" . --include="*.cshtml" \
  | grep -v "bin/\|obj/" | head -5
# Corresponding server handler must set Cache-Control: no-store
```

**Fix — key HTMX/Alpine patterns:**
```html
<!-- ✅ Debounced search -->
<input hx-get="/search/results"
       hx-trigger="keyup changed delay:400ms, search"
       hx-target="#results"
       hx-push-url="true"
       hx-indicator="#search-spinner">

<!-- ✅ x-cloak (add [x-cloak]{display:none} in <head>) -->
<div x-show="isOpen" x-cloak class="modal">...</div>

<!-- ✅ Named Alpine component, not inline logic -->
<div x-data="reservationTimer(900)">
  <span x-text="formatted"></span>
</div>

<!-- ✅ Global antiforgery in _Layout.cshtml -->
<script>
  document.addEventListener('htmx:configRequest', (e) => {
    const t = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (t) e.detail.headers['RequestVerificationToken'] = t;
  });
</script>
```

---

### Rule Group 8: Component Design & Reusability

**Rule**: DRY — không duplicate UI. Partial `_` prefix + strongly typed. Không hardcode
strings trong template. Responsive mobile-first. Spacing từ scale, không magic numbers.

```bash
echo "=== 8A. Duplicate inline styles (should be class) ==="
# Find the same style value repeated 3+ times
find . \( -name "*.cshtml" -o -name "*.html" \) -not -path "*/bin/*" \
  | xargs grep -oh "style=\"[^\"]*\"" 2>/dev/null \
  | sort | uniq -c | sort -rn | awk '$1 > 2' | head -10

echo "=== 8B. Partial without @model (untyped) ==="
find . -path "*/Shared/Components/*" -name "_*.cshtml" -not -path "*/bin/*" \
  | while read f; do
    grep -q "^@model " "$f" || echo "⚠️  $f — missing @model"
done

echo "=== 8C. Non-responsive grid (fixed columns, no breakpoint) ==="
grep -rn "grid-cols-[3-9]" . --include="*.cshtml" --include="*.html" \
  | grep -v "sm:\|md:\|lg:\|bin/\|obj/" | head -10

echo "=== 8D. Hardcoded strings that should be localized ==="
grep -rn "\"Add to Cart\"\|\"Remove\"\|\"Cancel\"\|\"Submit\"" . \
  --include="*.cshtml" | grep -v "@T\[\"\\|Resources\.\|bin/\|obj/" | head -10
# Phase 2+: extract to resource file or tag helper

echo "=== 8E. Status badge hardcoded (should use StatusBadgeHelper) ==="
grep -rn "badge-green\|badge-red\|badge-yellow" . --include="*.cshtml" \
  | grep -v "StatusBadgeHelper\|GetCssClass\|bin/\|obj/" | head -10
```

---

## Phase 3: REPORT

```markdown
# Frontend Code Review Report
**Date**: <ngày>
**Files scanned**: <số>
**Stack**: HTMX 2.x + Alpine.js 3.x + Tailwind CSS 4.x + Razor Pages

---

## Score

| Nhóm | Score | Findings |
|---|---|---|
| CSS Variables & Tokens | X/10 | X |
| CSS Architecture | X/10 | X |
| Semantic HTML | X/10 | X |
| JavaScript Patterns | X/10 | X |
| Performance (Web Vitals) | X/10 | X |
| Accessibility (WCAG AA) | X/10 | X |
| HTMX & Alpine | X/10 | X |
| Component Design | X/10 | X |

---

## 🔴 BLOCKER
### [B-001] Images missing width/height — CLS failure
- Files: `_ProductCard.cshtml`, `Shop/Index.cshtml`
- Fix: Add `width="300" height="300"` to all product `<img>` tags

---

## 🟠 HIGH
### [H-001] outline: none without focus-visible (keyboard broken)
- File: `wwwroot/css/components.css:45`
- Fix: Replace with `button:focus-visible { outline: 2px solid var(--color-brand); }`

---

## 🟡 MEDIUM / 💡 SUGGESTION
...
```

---

## Phase 5: VERIFY

```bash
# CSS build clean
cd src/MarketNest.Web && npm run build:css 2>&1 | tail -5

# .NET build (Razor compile check)
dotnet build src/MarketNest.Web --no-incremental 2>&1 | grep -i "error" | head -10

# Quick a11y check with axe (if Playwright available)
# npx playwright test --grep "@a11y"

# Manual browser checklist
echo "[ ] Tab through page: focus ring visible on every interactive element"
echo "[ ] NVDA/VoiceOver: product grid announced when HTMX updates"
echo "[ ] Disable CSS: page still readable (semantic structure)"
echo "[ ] 400% zoom: no content overflow or horizontal scroll"
echo "[ ] Network throttle (Slow 3G): LCP image loads first"
echo "[ ] DevTools > Performance: no layout shifts on product cards"
echo "[ ] DevTools > Lighthouse: LCP < 2.5s, CLS < 0.1"
echo "[ ] Forms: submit with keyboard only (no mouse)"
echo "[ ] Dark mode (OS): no hardcoded light colors visible"
echo "[ ] prefers-reduced-motion: animations stopped"
```

---

## Quick Reference — Top 20 Rules

| # | Rule | Bad | Good |
|---|---|---|---|
| 1 | CSS color | `color: #f97316` | `color: var(--color-brand)` |
| 2 | Font size unit | `font-size: 14px` | `font-size: 0.875rem` |
| 3 | Clickable element | `<div onclick>` | `<button type="button">` |
| 4 | Image CLS | `<img src="x">` | `<img src="x" width="400" height="300">` |
| 5 | Image format | `.jpg` / `.png` | `.webp` / `.avif` |
| 6 | Hero image | `<img src="hero">` | `<img fetchpriority="high">` + preload |
| 7 | Lazy loading | (missing) | `loading="lazy"` below fold |
| 8 | Script | `<script src>` | `<script src defer>` |
| 9 | Focus | `outline: none` | `outline: none; :focus-visible { ring }` |
| 10 | Alt text | `<img>` | `<img alt="descriptive text">` |
| 11 | HTMX input | `hx-trigger="keyup"` | `hx-trigger="keyup changed delay:400ms"` |
| 12 | Alpine flicker | `x-show` | `x-show x-cloak` + CSS |
| 13 | Money client | `(qty * price).toFixed(2)` | Server formats, display string only |
| 14 | Event leak | `addEventListener(fn)` | `addEventListener(fn, {signal})` |
| 15 | Await loop | `for (await fetch)` | `await Promise.all([...])` |
| 16 | XSS | `innerHTML = userInput` | `textContent = userInput` |
| 17 | Animation | `transition: width` | `transition: transform, opacity` |
| 18 | Reduced motion | (missing) | `@media (prefers-reduced-motion)` |
| 19 | aria-live | (missing on HTMX target) | `aria-live="polite"` |
| 20 | Skip link | (missing) | First element: `<a href="#main">Skip</a>` |
