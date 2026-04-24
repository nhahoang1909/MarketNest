# MarketNest — Frontend Component Library & Common Patterns

> Version: 0.1 | Status: Draft | Date: 2026-04  
> Defines every reusable component, Alpine.js magic, HTMX helper, and layout pattern.  
> **Rule: Before building a new UI element — check here first. If it exists, use it. If it's generic, add it here.**

---

## 1. Component Registry

All shared components live in `src/MarketNest.Web/Views/Shared/Components/`.  
All Alpine.js reusable logic lives in `src/MarketNest.Web/wwwroot/js/components/`.

```
Views/Shared/
├── _Layout.cshtml                  ← Master layout (topnav, footer, Alpine/HTMX init)
├── _LayoutSeller.cshtml            ← Seller dashboard layout (sidebar nav)
├── _LayoutAdmin.cshtml             ← Admin layout
│
└── Components/
    ├── Forms/
    │   ├── _TextField.cshtml       ← Input + label + validation message
    │   ├── _TextArea.cshtml
    │   ├── _SelectField.cshtml     ← Native <select> with options
    │   ├── _CheckboxField.cshtml
    │   ├── _RadioGroup.cshtml
    │   ├── _DatePicker.cshtml      ← Alpine date picker
    │   ├── _MoneyInput.cshtml      ← Decimal input with currency prefix
    │   ├── _SearchInput.cshtml     ← HTMX-wired search box with debounce
    │   ├── _MultiSelect.cshtml     ← Tag-style multi-select (Alpine)
    │   ├── _ImageUpload.cshtml     ← Drag-drop with preview
    │   └── _FormSection.cshtml     ← Grouped form fields with heading
    │
    ├── Display/
    │   ├── _StatusBadge.cshtml     ← Colored status badge (configurable per domain)
    │   ├── _StarRating.cshtml      ← Display-only stars (0–5)
    │   ├── _StarRatingInput.cshtml ← Interactive star input (Alpine)
    │   ├── _Avatar.cshtml          ← User/store avatar with fallback initials
    │   ├── _EmptyState.cshtml      ← Empty list illustration + CTA
    │   ├── _LoadingSpinner.cshtml  ← HTMX indicator
    │   ├── _Alert.cshtml           ← Info/success/warning/error alert block
    │   ├── _Breadcrumb.cshtml      ← Breadcrumb navigation
    │   └── _PriceDisplay.cshtml    ← Price + compare-at price + discount badge
    │
    ├── Navigation/
    │   ├── _Pagination.cshtml      ← Page number nav with HTMX
    │   ├── _Tabs.cshtml            ← Tab bar (Alpine state)
    │   └── _SidebarNav.cshtml      ← Sidebar nav item (active state)
    │
    ├── Data/
    │   ├── _DataTable.cshtml       ← Sortable table with HTMX reload
    │   ├── _DataTableRow.cshtml    ← Individual row (used inside _DataTable)
    │   ├── _FilterBar.cshtml       ← Horizontal filter chips + clear all
    │   └── _SortHeader.cshtml      ← Clickable column header (HTMX sort)
    │
    ├── Overlays/
    │   ├── _Modal.cshtml           ← Alpine modal wrapper
    │   ├── _ConfirmDialog.cshtml   ← Delete/irreversible action confirm
    │   ├── _Drawer.cshtml          ← Side drawer (mobile nav, detail panel)
    │   └── _Toast.cshtml           ← Toast container (reads Alpine store)
    │
    └── Domain/
        ├── _ProductCard.cshtml     ← Product grid card
        ├── _OrderStatusBadge.cshtml
        ├── _CartSummaryBadge.cshtml ← Nav cart icon with count
        ├── _StoreCard.cshtml       ← Storefront grid card
        └── _ReviewCard.cshtml      ← Single review display
```

---

## 2. Form Field Components

### Design Contract
Every form field component accepts the same core parameters:

```csharp
// Shared ViewModel for form fields
// Views/Shared/Components/Forms/_TextField.cshtml
@model TextFieldViewModel
// OR use asp-for + tag helpers

// Parameters (all fields support these):
// - asp-for: model expression
// - label: display label (defaults to property name if omitted)
// - placeholder: input placeholder
// - helpText: hint text below input
// - required: bool (renders * and aria-required)
// - disabled: bool
// - cssClass: additional CSS classes
```

### _TextField.cshtml
```html
@* Views/Shared/Components/Forms/_TextField.cshtml *@
@model TextFieldModel

<div class="form-field @Model.CssClass">
  <label asp-for="@Model.For" class="form-label @(Model.Required ? "required" : "")">
    @(Model.Label ?? Model.For.Name)
    @if (Model.Required) { <span class="text-red-500 ml-1" aria-hidden="true">*</span> }
  </label>
  
  <input asp-for="@Model.For"
         placeholder="@Model.Placeholder"
         class="form-input @(context.ViewData.ModelState[Model.For.Name]?.Errors.Any() == true ? "input-error" : "")"
         @(Model.Disabled ? "disabled" : "")
         @(Model.Required ? "required" : "")
         aria-required="@Model.Required.ToString().ToLower()" />
  
  @if (!string.IsNullOrEmpty(Model.HelpText))
  {
    <p class="form-help">@Model.HelpText</p>
  }
  
  <span asp-validation-for="@Model.For" class="form-error"></span>
</div>
```

### _DatePicker.cshtml
```html
@* Alpine.js date picker — ISO date string binding *@
<div x-data="datePicker({ 
       value: '@Model.Value?.ToString("yyyy-MM-dd")',
       minDate: '@Model.MinDate?.ToString("yyyy-MM-dd")',
       maxDate: '@Model.MaxDate?.ToString("yyyy-MM-dd")'
     })"
     class="relative">
  
  <input type="text" 
         :value="displayValue"
         @click="open = true"
         class="form-input pr-10 cursor-pointer"
         placeholder="Select date..."
         readonly>
  
  <input type="hidden" name="@Model.FieldName" :value="isoValue">
  
  @* Calendar dropdown (Alpine renders) *@
  <div x-show="open" 
       @click.outside="open = false"
       x-transition
       class="absolute z-50 mt-1 bg-white rounded-lg shadow-lg border border-gray-200 p-4">
    @* Month/year nav + day grid — Alpine renders dynamically *@
    <div class="grid grid-cols-7 gap-1">
      <template x-for="day in calendarDays">
        <button type="button"
                :class="dayClass(day)"
                @click="selectDay(day)"
                x-text="day.number">
        </button>
      </template>
    </div>
  </div>
</div>
```

### _MultiSelect.cshtml (Tag-style)
```html
@* Multi-select with tag chips — for categories, tags, etc. *@
<div x-data="multiSelect({
       options: @Html.Raw(Json.Serialize(Model.Options)),
       selected: @Html.Raw(Json.Serialize(Model.Selected)),
       max: @Model.Max
     })"
     class="relative">
  
  @* Selected tags *@
  <div class="flex flex-wrap gap-1 mb-2">
    <template x-for="item in selectedItems">
      <span class="inline-flex items-center gap-1 bg-brand-100 text-brand-800 text-sm px-2 py-1 rounded-full">
        <span x-text="item.label"></span>
        <button type="button" @click="remove(item.value)" class="hover:text-red-500">×</button>
      </span>
    </template>
  </div>
  
  @* Search + dropdown *@
  <input type="text"
         x-model="search"
         @focus="isOpen = true"
         @click.outside="isOpen = false"
         class="form-input"
         placeholder="@Model.Placeholder">
  
  <div x-show="isOpen && filtered.length > 0"
       class="absolute z-50 w-full bg-white border rounded shadow-lg max-h-48 overflow-y-auto">
    <template x-for="option in filtered">
      <button type="button"
              @click="select(option)"
              :class="isSelected(option.value) ? 'bg-brand-50' : 'hover:bg-gray-50'"
              class="w-full text-left px-3 py-2 text-sm">
        <span x-text="option.label"></span>
      </button>
    </template>
  </div>
  
  @* Hidden inputs for form submission *@
  <template x-for="item in selectedItems">
    <input type="hidden" name="@Model.FieldName" :value="item.value">
  </template>
</div>
```

---

## 3. Data Table Component

### _DataTable.cshtml — The Universal List Screen

```html
@* Views/Shared/Components/Data/_DataTable.cshtml *@
@model DataTableModel

<div id="@Model.TableId" class="data-table-container">
  
  @* Filter + Search bar *@
  <div class="flex items-center justify-between mb-4 gap-3">
    <partial name="Components/Forms/_SearchInput"
             model="@new SearchInputModel(Model.SearchParam, Model.HtmxTarget, Model.SearchEndpoint)" />
    
    @if (Model.Filters.Any())
    {
      <partial name="Components/Data/_FilterBar" model="@Model.Filters" />
    }
    
    @if (Model.CreateAction is not null)
    {
      <a href="@Model.CreateAction" class="btn-primary">+ @Model.CreateLabel</a>
    }
  </div>
  
  @* Table *@
  <div class="overflow-x-auto rounded-lg border border-gray-200">
    <table class="w-full text-sm text-left">
      <thead class="bg-gray-50 border-b">
        <tr>
          @foreach (var col in Model.Columns)
          {
            <th class="px-4 py-3 font-medium text-gray-600">
              @if (col.Sortable)
              {
                <partial name="Components/Data/_SortHeader" 
                         model="@new SortHeaderModel(col.Label, col.SortKey, Model.CurrentSort, Model.SortDesc, Model.HtmxTarget)" />
              }
              else { @col.Label }
            </th>
          }
        </tr>
      </thead>
      <tbody class="divide-y divide-gray-100">
        @RenderSection("Rows", required: true)
      </tbody>
    </table>
  </div>
  
  @* Pagination *@
  <div class="mt-4">
    <partial name="Components/Navigation/_Pagination" model="@Model.PagedResult" />
  </div>
  
  @* HTMX loading indicator *@
  <div id="table-loading" class="htmx-indicator absolute inset-0 bg-white/50 flex items-center justify-center">
    <partial name="Components/Display/_LoadingSpinner" />
  </div>
</div>
```

### _SortHeader.cshtml
```html
@model SortHeaderModel

@{
    var isActive  = Model.CurrentSort == Model.SortKey;
    var nextDesc  = isActive ? !Model.SortDesc : false;
    var url       = $"?sortBy={Model.SortKey}&sortDesc={nextDesc.ToString().ToLower()}";
}

<button hx-get="@url"
        hx-target="@Model.HtmxTarget"
        hx-push-url="true"
        class="flex items-center gap-1 group font-medium text-gray-600 hover:text-gray-900">
  @Model.Label
  <span class="text-gray-400 @(isActive ? "text-brand-500" : "")">
    @if (!isActive)     { <span>↕</span> }
    else if (!Model.SortDesc) { <span>↑</span> }
    else                { <span>↓</span> }
  </span>
</button>
```

---

## 4. Overlay Components

### _Modal.cshtml
```html
@* Full Alpine.js modal — server can open via HX-Trigger: {"modalOpen": {"target": "#myModal"}} *@
<div id="@Model.ModalId"
     x-data="{ open: false }"
     x-on:modalopen.window="if ($event.detail.target === '#@Model.ModalId') open = true"
     x-on:modalclose.window="open = false"
     x-show="open"
     x-cloak
     class="fixed inset-0 z-50 overflow-y-auto">
  
  @* Backdrop *@
  <div class="fixed inset-0 bg-black/50" @click="open = false"></div>
  
  @* Panel *@
  <div class="relative bg-white rounded-xl shadow-xl mx-auto mt-20 max-w-lg w-full mx-4"
       x-transition:enter="transition ease-out duration-200"
       x-transition:enter-start="opacity-0 scale-95"
       x-transition:enter-end="opacity-100 scale-100"
       @click.stop>
    
    @* Header *@
    <div class="flex items-center justify-between px-6 py-4 border-b">
      <h2 class="text-lg font-semibold">@Model.Title</h2>
      <button @click="open = false" class="text-gray-400 hover:text-gray-600">✕</button>
    </div>
    
    @* Content — loaded via HTMX or static *@
    <div id="@Model.ContentId" class="px-6 py-4">
      @RenderSection("Content", required: false)
    </div>
  </div>
</div>
```

### _ConfirmDialog.cshtml
```html
@* Reusable confirm dialog for destructive actions *@
@* Usage: hx-confirm is NOT used — use this component for custom confirm UX *@

<div x-data="confirmDialog()"
     x-on:confirmdialog.window="open($event.detail)">
  
  <div x-show="isOpen" class="fixed inset-0 z-50 flex items-center justify-center">
    <div class="fixed inset-0 bg-black/40" @click="cancel()"></div>
    <div class="relative bg-white rounded-xl shadow-xl p-6 max-w-sm w-full mx-4">
      <h3 class="font-semibold text-gray-900 mb-2" x-text="title"></h3>
      <p class="text-gray-600 text-sm mb-6" x-text="message"></p>
      <div class="flex gap-3 justify-end">
        <button @click="cancel()" class="btn-secondary">Cancel</button>
        <button @click="confirm()" class="btn-danger" x-text="confirmLabel"></button>
      </div>
    </div>
  </div>
</div>
```

```javascript
// wwwroot/js/components/confirmDialog.js
Alpine.data('confirmDialog', () => ({
    isOpen: false,
    title: '',
    message: '',
    confirmLabel: 'Confirm',
    _resolve: null,

    open({ title, message, confirmLabel = 'Confirm' }) {
        this.title = title;
        this.message = message;
        this.confirmLabel = confirmLabel;
        this.isOpen = true;
        return new Promise(resolve => this._resolve = resolve);
    },

    confirm() { this.isOpen = false; this._resolve?.(true); },
    cancel()  { this.isOpen = false; this._resolve?.(false); }
}));

// Usage: trigger from any HTMX button
// hx-on:click="
//   const ok = await $dispatch('confirmdialog', { title: 'Delete product?', message: 'This cannot be undone.' });
//   if (!ok) event.preventDefault();
// "
```

---

## 5. Alpine.js Component Scripts

All Alpine components live in `wwwroot/js/components/`. Loaded in `_Layout.cshtml` bottom of body.

```
wwwroot/js/
├── app.js                      ← Entry: Alpine.start(), global stores, magic helpers
├── components/
│   ├── datePicker.js           ← Calendar date picker
│   ├── multiSelect.js          ← Tag-style multi select
│   ├── imageUploader.js        ← Drag-drop image upload with preview
│   ├── starRating.js           ← Interactive star rating input
│   ├── confirmDialog.js        ← Confirm modal (see above)
│   ├── reservationTimer.js     ← Cart TTL countdown
│   └── infiniteScroll.js       ← Scroll-to-load for product grids
├── stores/
│   ├── cart.js                 ← $store.cart
│   ├── toasts.js               ← $store.toasts
│   └── user.js                 ← $store.user
└── magic/
    └── htmxHelpers.js          ← $htmx magic property, HTMX event helpers
```

### app.js — Bootstrap
```javascript
// wwwroot/js/app.js
import Alpine from 'alpinejs';

// Stores
import './stores/cart.js';
import './stores/toasts.js';
import './stores/user.js';

// Components
import './components/datePicker.js';
import './components/multiSelect.js';
import './components/imageUploader.js';
import './components/starRating.js';
import './components/confirmDialog.js';
import './components/reservationTimer.js';
import './components/infiniteScroll.js';

// Alpine magic helpers
Alpine.magic('currency', () => (amount) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount));

Alpine.magic('date', () => (iso) =>
    new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' }));

Alpine.magic('timeAgo', () => (iso) => {
    const seconds = Math.floor((Date.now() - new Date(iso)) / 1000);
    if (seconds < 60)   return `${seconds}s ago`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400)return `${Math.floor(seconds / 3600)}h ago`;
    return `${Math.floor(seconds / 86400)}d ago`;
});

window.Alpine = Alpine;
Alpine.start();
```

### starRating.js
```javascript
// wwwroot/js/components/starRating.js
Alpine.data('starRating', (initialValue = 0) => ({
    value: initialValue,
    hover: 0,

    get stars() {
        return [1, 2, 3, 4, 5].map(n => ({
            n,
            filled: n <= (this.hover || this.value)
        }));
    },

    setHover(n)  { this.hover = n; },
    clearHover() { this.hover = 0; },
    select(n)    { this.value = n; }
}));
```

```html
@* _StarRatingInput.cshtml *@
<div x-data="starRating(@Model.Value)" class="flex gap-1">
  <template x-for="star in stars" :key="star.n">
    <button type="button"
            @mouseenter="setHover(star.n)"
            @mouseleave="clearHover()"
            @click="select(star.n)"
            :class="star.filled ? 'text-yellow-400' : 'text-gray-300'"
            class="text-2xl transition-colors">
      ★
    </button>
  </template>
  <input type="hidden" name="@Model.FieldName" :value="value">
</div>
```

### imageUploader.js
```javascript
// wwwroot/js/components/imageUploader.js
Alpine.data('imageUploader', ({ maxFiles = 3, maxSizeMb = 5, accept = 'image/*' } = {}) => ({
    files: [],
    previews: [],
    isDragging: false,
    error: '',

    handleFiles(event) {
        this.addFiles(Array.from(event.target.files));
    },

    handleDrop(event) {
        this.isDragging = false;
        this.addFiles(Array.from(event.dataTransfer.files));
    },

    addFiles(incoming) {
        this.error = '';
        const allowed = incoming.filter(f => {
            if (f.size > maxSizeMb * 1024 * 1024) {
                this.error = `${f.name} exceeds ${maxSizeMb}MB limit`;
                return false;
            }
            if (!f.type.startsWith('image/')) {
                this.error = `${f.name} is not an image`;
                return false;
            }
            return true;
        });

        const available = maxFiles - this.files.length;
        const toAdd = allowed.slice(0, available);

        toAdd.forEach(file => {
            this.files.push(file);
            const reader = new FileReader();
            reader.onload = e => this.previews.push({ url: e.target.result, name: file.name });
            reader.readAsDataURL(file);
        });

        if (allowed.length > available)
            this.error = `Maximum ${maxFiles} images allowed`;
    },

    removeFile(index) {
        this.files.splice(index, 1);
        this.previews.splice(index, 1);
    }
}));
```

---

## 6. HTMX Common Patterns (Reusable Snippets)

### 6.1 _SearchInput.cshtml — Universal Search Box
```html
@* Views/Shared/Components/Forms/_SearchInput.cshtml *@
@model SearchInputModel
@* SearchInputModel: { Param, HtmxTarget, Endpoint, Placeholder, DebounceMs } *@

<div class="relative">
  <span class="absolute left-3 top-2.5 text-gray-400">🔍</span>
  <input type="search"
         name="@Model.Param"
         value="@Model.CurrentValue"
         placeholder="@(Model.Placeholder ?? "Search...")"
         hx-get="@Model.Endpoint"
         hx-trigger="keyup changed delay:@(Model.DebounceMs)ms, search"
         hx-target="@Model.HtmxTarget"
         hx-push-url="true"
         hx-indicator="#table-loading"
         class="form-input pl-9 w-64">
</div>
```

### 6.2 _FilterBar.cshtml — Filter Chips
```html
@* Horizontal filter chips: each chip fires HTMX reload *@
@model FilterBarModel

<div class="flex items-center gap-2 flex-wrap">
  @foreach (var filter in Model.Filters)
  {
    <div class="flex items-center gap-1">
      <span class="text-xs text-gray-500">@filter.Label:</span>
      @foreach (var option in filter.Options)
      {
        <button hx-get="@Model.Endpoint"
                hx-vals='@Html.Raw($"{{\"{filter.Param}\": \"{option.Value}\"}}")'
                hx-target="@Model.HtmxTarget"
                hx-push-url="true"
                class="filter-chip @(option.Value == filter.CurrentValue ? "filter-chip-active" : "")">
          @option.Label
        </button>
      }
    </div>
  }
  
  @if (Model.HasActiveFilters)
  {
    <button hx-get="@Model.ClearUrl"
            hx-target="@Model.HtmxTarget"
            hx-push-url="true"
            class="text-xs text-gray-400 hover:text-red-500">
      Clear all
    </button>
  }
</div>
```

### 6.3 HTMX Global Config (in _Layout.cshtml)
```html
@* Configure HTMX globally — in <head> *@
<meta name="htmx-config" content='{
  "defaultSwapStyle": "outerHTML",
  "defaultSettleDelay": 100,
  "historyCacheSize": 10,
  "refreshOnHistoryMiss": true,
  "globalViewTransitions": true,
  "antiForgery": {
    "headerName": "RequestVerificationToken",
    "formFieldName": "__RequestVerificationToken"
  }
}'>

@* Anti-forgery token for HTMX requests *@
<script>
  document.addEventListener('htmx:configRequest', (e) => {
    e.detail.headers['RequestVerificationToken'] =
      document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
  });
</script>
```

---

## 7. CSS Utility Classes (Tailwind Components)

Define these in `wwwroot/css/components.css` with `@layer components`:

```css
/* wwwroot/css/components.css */
@layer components {

  /* ── Buttons ─────────────────────────────────────────── */
  .btn-primary {
    @apply inline-flex items-center gap-2 px-4 py-2 bg-brand-500 text-white
           font-medium text-sm rounded-lg hover:bg-brand-600 
           disabled:opacity-50 disabled:cursor-not-allowed
           transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2;
  }

  .btn-secondary {
    @apply inline-flex items-center gap-2 px-4 py-2 bg-white text-gray-700 border border-gray-300
           font-medium text-sm rounded-lg hover:bg-gray-50
           disabled:opacity-50 transition-colors;
  }

  .btn-danger {
    @apply inline-flex items-center gap-2 px-4 py-2 bg-red-600 text-white
           font-medium text-sm rounded-lg hover:bg-red-700 transition-colors;
  }

  .btn-ghost {
    @apply inline-flex items-center gap-2 px-3 py-1.5 text-gray-600
           text-sm rounded-lg hover:bg-gray-100 transition-colors;
  }

  /* ── Form ────────────────────────────────────────────── */
  .form-label {
    @apply block text-sm font-medium text-gray-700 mb-1;
  }

  .form-label.required::after {
    content: ' *';
    @apply text-red-500;
  }

  .form-input {
    @apply block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm
           placeholder:text-gray-400
           focus:border-brand-500 focus:ring-1 focus:ring-brand-500 focus:outline-none
           disabled:bg-gray-50 disabled:text-gray-500;
  }

  .input-error {
    @apply border-red-500 focus:border-red-500 focus:ring-red-500;
  }

  .form-error {
    @apply block text-xs text-red-600 mt-1;
  }

  .form-help {
    @apply text-xs text-gray-500 mt-1;
  }

  /* ── Cards ───────────────────────────────────────────── */
  .card {
    @apply bg-white rounded-xl border border-gray-200 shadow-sm;
  }

  .card-header {
    @apply px-6 py-4 border-b border-gray-100 flex items-center justify-between;
  }

  .card-body {
    @apply px-6 py-4;
  }

  /* ── Status badges ───────────────────────────────────── */
  .badge            { @apply inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium; }
  .badge-gray       { @apply badge bg-gray-100 text-gray-700; }
  .badge-blue       { @apply badge bg-blue-100 text-blue-700; }
  .badge-green      { @apply badge bg-green-100 text-green-700; }
  .badge-yellow     { @apply badge bg-yellow-100 text-yellow-700; }
  .badge-red        { @apply badge bg-red-100 text-red-700; }
  .badge-orange     { @apply badge bg-orange-100 text-orange-700; }
  .badge-purple     { @apply badge bg-purple-100 text-purple-700; }

  /* ── Filter chips ────────────────────────────────────── */
  .filter-chip {
    @apply px-3 py-1 rounded-full text-xs font-medium border border-gray-200
           text-gray-600 hover:bg-gray-50 transition-colors cursor-pointer;
  }

  .filter-chip-active {
    @apply bg-brand-500 text-white border-brand-500 hover:bg-brand-600;
  }

  /* ── Table ───────────────────────────────────────────── */
  .table-th { @apply px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider; }
  .table-td { @apply px-4 py-3 text-sm text-gray-700; }

  /* ── HTMX loading state ──────────────────────────────── */
  .htmx-indicator       { display: none; }
  .htmx-request .htmx-indicator { display: flex; }
  .htmx-request.htmx-indicator  { display: flex; }
}
```

---

## 8. Status Badge Mapping (Domain → CSS)

Single source of truth for all status → badge color mappings:

```csharp
// Web/Infrastructure/StatusBadgeHelper.cs
public static class StatusBadgeHelper
{
    public static string GetCssClass(string status) => status switch
    {
        // Order status
        "Pending"    => "badge-yellow",
        "Confirmed"  => "badge-blue",
        "Processing" => "badge-blue",
        "Shipped"    => "badge-purple",
        "Delivered"  => "badge-blue",
        "Completed"  => "badge-green",
        "Cancelled"  => "badge-red",
        "Disputed"   => "badge-orange",
        "Refunded"   => "badge-red",
        // Storefront
        "Draft"      => "badge-gray",
        "Active"     => "badge-green",
        "Suspended"  => "badge-orange",
        "Closed"     => "badge-red",
        // Payout
        "Paid"       => "badge-green",
        "Failed"     => "badge-red",
        "Clawback"   => "badge-red",
        _            => "badge-gray"
    };
}
```

```html
@* _StatusBadge.cshtml *@
@model StatusBadgeModel

<span class="@StatusBadgeHelper.GetCssClass(Model.Status)">
  @Model.Status
</span>
```

---

## 9. Razor Tag Helpers (Custom)

```csharp
// Web/Infrastructure/TagHelpers/HtmxConfirmTagHelper.cs
/// <summary>
/// Adds confirm dialog before HTMX action.
/// Usage: <button hx-delete="/api/x" mn-confirm="Delete this item?" mn-confirm-title="Confirm Delete">
/// </summary>
[HtmlTargetElement("*", Attributes = "mn-confirm")]
public class HtmxConfirmTagHelper : TagHelper
{
    [HtmlAttributeName("mn-confirm")]
    public string Message { get; set; } = "Are you sure?";

    [HtmlAttributeName("mn-confirm-title")]
    public string Title { get; set; } = "Confirm";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.SetAttribute("hx-confirm", " "); // disable default hx-confirm
        output.Attributes.SetAttribute(
            "@click.prevent",
            $"$dispatch('confirmdialog', {{title: '{Title}', message: '{Message}'}})" +
            $".then(ok => ok && htmx.trigger($el, 'confirmed'))");
        output.Attributes.SetAttribute("hx-trigger", "confirmed");
    }
}

// Web/Infrastructure/TagHelpers/ActiveNavTagHelper.cs
/// <summary>Adds active CSS class to nav link when URL matches current page</summary>
[HtmlTargetElement("a", Attributes = "mn-nav")]
public class ActiveNavTagHelper(IHttpContextAccessor accessor) : TagHelper
{
    [HtmlAttributeName("mn-nav")]
    public string? ActiveClass { get; set; } = "nav-active";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var href    = output.Attributes["href"]?.Value?.ToString() ?? "";
        var current = accessor.HttpContext?.Request.Path.Value ?? "";
        if (current.StartsWith(href, StringComparison.OrdinalIgnoreCase))
            output.Attributes.SetAttribute("class",
                (output.Attributes["class"]?.Value?.ToString() + " " + ActiveClass).Trim());
    }
}
```

---

## 10. Page Layout Conventions

### Base Layout Data (every page must provide)
```csharp
// Web/Infrastructure/BasePageModel.cs
// All PageModels inherit from this
public abstract class BasePageModel : PageModel
{
    [ViewData] public string PageTitle  { get; set; } = "MarketNest";
    [ViewData] public string? MetaDesc  { get; set; }
    [ViewData] public BreadcrumbItem[] Breadcrumbs { get; set; } = [];

    protected void SetBreadcrumbs(params BreadcrumbItem[] items) => Breadcrumbs = items;
    protected void SetTitle(string title) => PageTitle = $"{title} — MarketNest";
}

public record BreadcrumbItem(string Label, string? Url = null);
```

```html
@* Usage in any page *@
@{
    SetTitle("My Orders");
    SetBreadcrumbs(
        new("Home", "/"),
        new("Account", "/account"),
        new("Orders")  // last item has no URL
    );
}
```
