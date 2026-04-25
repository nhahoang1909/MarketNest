---
name: api-contract-review
description: >
  Quét toàn bộ endpoint layer của MarketNest để review chất lượng API contract: Minimal API
  design, HTTP status code convention, Problem Details RFC 7807, FluentValidation wiring,
  response schema consistency, HTMX partial endpoint pattern, antiforgery, rate limit annotation,
  và HX-Trigger event contract. Sử dụng skill này khi người dùng muốn: review API endpoint,
  kiểm tra HTTP status code, check Problem Details, review validation wiring, kiểm tra HTMX
  response header, review rate limit, check antiforgery, hoặc nói bất kỳ cụm từ nào như
  "review API", "check endpoint", "HTTP contract", "HTMX pattern", "Problem Details",
  "rate limit annotation", "validation response", "status code", "api design review".
  Kích hoạt khi người dùng upload file PageModel, Minimal API handler, Program.cs, middleware.
compatibility:
  tools: [bash, read_file, write_file, list_files]
  agents: [claude-code, gemini-cli, cursor, continue, aider]
  stack: [.NET 10, ASP.NET Core 10, Razor Pages, Minimal API, HTMX 2, FluentValidation 11, MediatR 12]
---

# API Contract Review Skill — MarketNest

Skill này review toàn bộ endpoint layer của MarketNest theo 7 nhóm rule, từ HTTP status code
đến HTMX-specific contract. Output là báo cáo phân loại **BLOCKER / HIGH / MEDIUM** với
fix code sẵn sàng copy-paste.

---

## API Topology của MarketNest

```
Client (Browser)
    │
    ├─ Full page request → Razor Pages (Pages/**/*.cshtml.cs)
    │      OnGet / OnPost / OnPut / OnDelete handlers
    │      → returns Page() hoặc Partial() tuỳ IsHtmx()
    │
    ├─ HTMX partial request (HX-Request: true) → cùng PageModel
    │      → returns Partial("_PartialName", model)
    │      → response headers: HX-Redirect, HX-Retarget, HX-Trigger
    │
    └─ Minimal API (Phase 2+) → app.Map*() trong Program.cs / Extensions/
           → returns TypedResults.* (không phải Results.* thuần)
           → dùng cho: health check, webhook, mobile API

Rate limit layers:
    Nginx: coarse (30r/s api / 5r/m auth)
    ASP.NET Core RateLimiter: fine-grained per policy ("public" 60/min, "auth" 5/15min)
```

---

## Quy trình thực thi

```
Phase 1: SCAN    → Thu thập toàn bộ endpoint files
Phase 2: ANALYZE → 7 rule groups
Phase 3: REPORT  → BLOCKER / HIGH / MEDIUM với file:line
Phase 4: FIX     → Code fix sẵn sàng (hỏi xác nhận trước khi apply)
Phase 5: VERIFY  → Integration test checklist
```

---

## Phase 1: SCAN — Thu thập endpoint inventory

### 1.1 Razor Pages handlers

```bash
# Liệt kê tất cả PageModel files
find src/MarketNest.Web/Pages/ -name "*.cshtml.cs" | sort

# Đếm số handler methods per page
find src/MarketNest.Web/Pages/ -name "*.cshtml.cs" | while read f; do
    count=$(grep -c "public.*Task.*On\(Get\|Post\|Put\|Delete\|Patch\)" "$f" 2>/dev/null || echo 0)
    echo "$count  $(basename $f)"
done | sort -rn | head -20

# Tìm tất cả HTMX partial endpoints (handlers trả về Partial())
grep -rn "return Partial\|\.Partial(\|Partial(\"_" \
  src/MarketNest.Web/ --include="*.cshtml.cs" | grep -v "bin/\|obj/" | head -30
```

### 1.2 Minimal API endpoints

```bash
# Tìm tất cả Map*() calls
grep -rn "app\.MapGet\|app\.MapPost\|app\.MapPut\|app\.MapDelete\|app\.MapPatch\|\.MapGroup" \
  src/MarketNest.Web/ --include="*.cs" | grep -v "bin/\|obj/" | sort

# Tìm endpoint extension files
find src/MarketNest.Web/Extensions/ -name "*Endpoints.cs" -o -name "*Api.cs" \
  | grep -v "bin/\|obj/" | sort

# Liệt kê tất cả route patterns
grep -rn "\"\/api\/" src/MarketNest.Web/ --include="*.cs" \
  | grep -v "bin/\|obj/" | head -30
```

### 1.3 Middleware pipeline (kiểm tra thứ tự)

```bash
# Đọc Program.cs để verify middleware order
cat src/MarketNest.Web/Program.cs | grep -E "app\.Use|app\.Map|app\.Run" | head -30
```

---

## Phase 2: ANALYZE — 7 Rule Groups

---

### Rule Group 1: HTTP Status Code Convention

**Chuẩn MarketNest cho từng tình huống:**

| Tình huống | HTTP Status | Body |
|---|---|---|
| Create thành công | `201 Created` + `Location` header | `{ id }` hoặc resource |
| Read thành công | `200 OK` | Resource / DTO |
| Update thành công | `200 OK` hoặc `204 No Content` | Updated resource hoặc empty |
| Delete thành công | `204 No Content` | Empty |
| Validation lỗi | `422 Unprocessable Entity` | `ValidationProblemDetails` |
| Not found | `404 Not Found` | `ProblemDetails` |
| Unauthorized (chưa login) | `401 Unauthorized` | `ProblemDetails` |
| Forbidden (sai role/owner) | `403 Forbidden` | `ProblemDetails` |
| Conflict (duplicate) | `409 Conflict` | `ProblemDetails` |
| Rate limited | `429 Too Many Requests` | `ProblemDetails` + `Retry-After` |
| Server error | `500 Internal Server Error` | `ProblemDetails` (không expose stack trace) |

```bash
# 1A. Tìm Create endpoint trả về 200 thay vì 201
echo "=== Create endpoints returning 200 instead of 201 ==="
grep -rn "MapPost\|OnPost" src/ --include="*.cs" -A5 \
  | grep -v "bin/\|obj/" \
  | grep "Results\.Ok\|TypedResults\.Ok\|return Ok(" \
  | grep -v "Results\.Created\|TypedResults\.Created" | head -20

# 1B. Tìm endpoint dùng Results.BadRequest cho validation (nên là 422)
echo "=== BadRequest (400) used for validation — should be 422 ==="
grep -rn "Results\.BadRequest\|TypedResults\.BadRequest\|return BadRequest\|StatusCode(400)" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -20

# 1C. Tìm Delete endpoint trả về 200 với body (nên là 204)
echo "=== Delete endpoints returning 200 with body ==="
grep -rn "MapDelete\|OnDelete" src/ --include="*.cs" -A5 \
  | grep -v "bin/\|obj/" \
  | grep "Results\.Ok\|TypedResults\.Ok" | head -10

# 1D. Tìm endpoint hardcode status code thay vì dùng TypedResults
echo "=== Hardcoded status codes ==="
grep -rn "StatusCode(200)\|StatusCode(201)\|StatusCode(400)\|StatusCode(404)" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" | head -20
```

**Fix pattern — Minimal API:**
```csharp
// ❌ Sai: Create trả 200, dùng Results (không typed)
app.MapPost("/api/cart/items", async (AddToCartRequest req, ISender sender) =>
{
    var result = await sender.Send(req.ToCommand());
    return Results.Ok(result); // 200 không phải 201
});

// ✅ Đúng: 201 + Location, dùng TypedResults (strongly-typed)
app.MapPost("/api/cart/items", async (
    AddToCartRequest req,
    ISender sender,
    HttpContext ctx) =>
{
    var result = await sender.Send(req.ToCommand(ctx.User.GetUserId()));
    return result.Match(
        cartItemId => TypedResults.Created($"/api/cart/items/{cartItemId}", new { id = cartItemId }),
        error => error.ToTypedProblem()
    );
}).RequireAuthorization()
  .RequireRateLimiting("public")
  .WithName("AddCartItem")
  .WithTags("Cart");
```

**Fix pattern — Razor Pages:**
```csharp
// ❌ Sai: POST trả 400 cho validation
public async Task<IActionResult> OnPostAsync(PlaceOrderRequest request)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState); // 400, không đúng format
}

// ✅ Đúng: 422 + ValidationProblemDetails qua middleware
// (ValidationBehavior trong MediatR pipeline throw ValidationException)
// (ExceptionMiddleware bắt → trả 422 ValidationProblemDetails tự động)
public async Task<IActionResult> OnPostAsync(PlaceOrderRequest request)
{
    // Không cần validate thủ công — FluentValidation pipeline lo
    var result = await _sender.Send(request.ToCommand(User.GetUserId()));
    return result.Match(
        orderId => RedirectToPage("/Account/Orders/Detail", new { id = orderId }),
        error => this.HandleError(error) // extension method → ModelState + Page()
    );
}
```

---

### Rule Group 2: Problem Details RFC 7807

**Chuẩn RFC 7807 — mọi error response phải có đủ 5 fields:**
```json
{
  "type":     "https://marketnest.com/errors/not-found",
  "title":    "Order not found",
  "status":   404,
  "detail":   "Order 3fa85f64 does not exist or you don't have access",
  "instance": "/api/orders/3fa85f64",
  "correlationId": "a1b2c3d4"  // extension field — bắt buộc với MarketNest
}
```

```bash
# 2A. Tìm error response không phải ProblemDetails (raw JSON, string...)
echo "=== Non-ProblemDetails error responses ==="
grep -rn "return.*error\.Message\|Results\.Json.*error\|WriteAsJsonAsync.*error" \
  src/ --include="*.cs" | grep -v "bin/\|obj/" \
  | grep -v "ProblemDetails\|problem\|Problem" | head -20

# 2B. Tìm exception middleware / handler
echo "=== Exception handling middleware ==="
find src/ -name "ExceptionMiddleware.cs" -o -name "*ExceptionHandler*" \
  | grep -v "bin/\|obj/"

# 2C. Kiểm tra ProblemDetails có correlationId không
grep -rn "correlationId\|CorrelationId\|correlation_id" \
  src/MarketNest.Web/ --include="*.cs" \
  | grep "ProblemDetails\|Extensions\[" | grep -v "bin/\|obj/" | head -10

# 2D. Tìm 500 response expose stack trace / inner exception
grep -rn "ex\.Message\|ex\.StackTrace\|InnerException" \
  src/MarketNest.Web/ --include="*.cs" \
  | grep "ProblemDetails\|StatusCode\|WriteAsJson" | grep -v "bin/\|obj/" | head -10

# 2E. Content-Type header phải là application/problem+json (không phải application/json)
grep -rn "ContentType\|content-type\|Content-Type" \
  src/MarketNest.Web/ --include="*.cs" \
  | grep "problem\|application/json" | grep -v "bin/\|obj/" | head -10
```

**Fix — ExceptionMiddleware chuẩn:**
```csharp
// Web/Middleware/ExceptionMiddleware.cs
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (ValidationException ex)
        {
            await WriteProblemAsync(ctx, new ValidationProblemDetails(
                ex.Errors.GroupBy(e => e.PropertyName)
                         .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Type   = "https://marketnest.com/errors/validation",
                Title  = "One or more validation errors occurred",
                Status = 422,
                Extensions = { ["correlationId"] = GetCorrelationId(ctx) }
            }, 422);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(ctx, new ProblemDetails
            {
                Type     = "https://marketnest.com/errors/not-found",
                Title    = "Resource not found",
                Status   = 404,
                Detail   = ex.Message,
                Instance = ctx.Request.Path,
                Extensions = { ["correlationId"] = GetCorrelationId(ctx) }
            }, 404);
        }
        catch (ForbiddenException)
        {
            await WriteProblemAsync(ctx, new ProblemDetails
            {
                Type   = "https://marketnest.com/errors/forbidden",
                Title  = "Access denied",
                Status = 403,
                Extensions = { ["correlationId"] = GetCorrelationId(ctx) }
            }, 403);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(ctx, new ProblemDetails
            {
                Type     = $"https://marketnest.com/errors/{ex.Code.ToLower().Replace('_', '-')}",
                Title    = ex.Message,
                Status   = 409, // business rule conflict
                Extensions =
                {
                    ["code"]          = ex.Code,
                    ["correlationId"] = GetCorrelationId(ctx)
                }
            }, 409);
        }
        catch (Exception ex)
        {
            // ⚠️ KHÔNG expose ex.Message hay ex.StackTrace ra ngoài
            logger.LogError(ex, "Unhandled exception. CorrelationId: {Id}", GetCorrelationId(ctx));
            await WriteProblemAsync(ctx, new ProblemDetails
            {
                Type     = "https://marketnest.com/errors/internal",
                Title    = "An unexpected error occurred",
                Status   = 500,
                Extensions = { ["correlationId"] = GetCorrelationId(ctx) }
                // ✅ Không có Detail, không có StackTrace
            }, 500);
        }
    }

    private static string? GetCorrelationId(HttpContext ctx)
        => ctx.Items["CorrelationId"]?.ToString();

    private static Task WriteProblemAsync(HttpContext ctx, ProblemDetails problem, int status)
    {
        ctx.Response.StatusCode  = status;
        ctx.Response.ContentType = "application/problem+json"; // RFC 7807
        return ctx.Response.WriteAsJsonAsync(problem);
    }
}
```

**Extension method — Error → TypedProblem cho Minimal API:**
```csharp
// Web/Extensions/ErrorExtensions.cs
public static class ErrorExtensions
{
    public static IResult ToTypedProblem(this Error error)
    {
        return error.Type switch
        {
            ErrorType.NotFound     => TypedResults.Problem(
                type:   "https://marketnest.com/errors/not-found",
                title:  error.Message,
                statusCode: 404),
            ErrorType.Validation   => TypedResults.Problem(
                type:   "https://marketnest.com/errors/validation",
                title:  error.Message,
                statusCode: 422),
            ErrorType.Unauthorized => TypedResults.Problem(
                type:   "https://marketnest.com/errors/unauthorized",
                title:  "Authentication required",
                statusCode: 401),
            ErrorType.Conflict     => TypedResults.Problem(
                type:   "https://marketnest.com/errors/conflict",
                title:  error.Message,
                statusCode: 409),
            _                      => TypedResults.Problem(
                type:   "https://marketnest.com/errors/internal",
                title:  "An unexpected error occurred",
                statusCode: 500)
        };
    }
}
```

---

### Rule Group 3: FluentValidation Wiring

**Quy tắc**: Mọi Command phải có Validator pair. Validation xảy ra tự động trong MediatR pipeline — endpoint không validate thủ công.

```bash
# 3A. Tìm Command không có Validator pair
echo "=== Commands missing paired Validator ==="
find src/ -name "*Command.cs" -not -path "*/bin/*" | while read f; do
    dir=$(dirname "$f")
    cmdbase=$(basename "$f" .cs)
    if ! find "${dir}/../Validators" "${dir}" -name "${cmdbase}Validator.cs" 2>/dev/null | grep -q .; then
        echo "⚠️  No validator found for: $cmdbase ($f)"
    fi
done

# 3B. Tìm PageModel handler validate thủ công thay vì dùng pipeline
echo "=== Manual validation in PageModel handlers (should use MediatR pipeline) ==="
grep -rn "ModelState\.IsValid\|if.*==.*null.*return\|ArgumentNullException\|throw.*Validation" \
  src/MarketNest.Web/Pages/ --include="*.cshtml.cs" \
  | grep -v "//\|bin/\|obj/" | head -20

# 3C. Kiểm tra ValidationBehavior được register đúng chưa
grep -rn "ValidationBehavior\|AddBehavior.*Validation" \
  src/MarketNest.Web/Program.cs src/MarketNest.Web/Extensions/ --include="*.cs" 2>/dev/null \
  | grep -v "bin/\|obj/" | head -5

# 3D. Tìm endpoint bind request object nhưng không có [FromBody] (Minimal API)
echo "=== Minimal API missing [FromBody] or binding source ==="
grep -rn "app\.Map" src/MarketNest.Web/ --include="*.cs" -A3 \
  | grep -v "bin/\|obj/" | grep "Request\|Command" | grep -v "FromBody\|FromQuery\|FromRoute" | head -10

# 3E. Kiểm tra FluentValidation auto-discovery được setup chưa
grep -rn "AddValidatorsFromAssembly\|AddFluentValidationAutoValidation\|services\.AddValidators" \
  src/MarketNest.Web/ --include="*.cs" | grep -v "bin/\|obj/" | head -5
```

**Dấu hiệu HIGH:**
```csharp
// ❌ HIGH: Manual validation trong PageModel (redundant và error-prone)
public async Task<IActionResult> OnPostAsync(PlaceOrderRequest request)
{
    if (request.CartId == Guid.Empty)     // manual!
        ModelState.AddModelError("CartId", "Cart ID is required");
    if (!ModelState.IsValid)
        return Page();
    // ...
}

// ✅ Validation xảy ra tự động trong MediatR pipeline
// PlaceOrderCommandValidator.cs tự động chạy trước handler
// Nếu fail → ValidationException → ExceptionMiddleware → 422 response
public async Task<IActionResult> OnPostAsync(PlaceOrderRequest request)
{
    // Không cần check ModelState — pipeline đã handle
    var result = await _sender.Send(request.ToCommand(User.GetUserId()));
    return result.Match(
        id => RedirectToPage("/Account/Orders/Confirmation", new { id }),
        error => this.HandleError(error)
    );
}
```

**Form binding — Razor Pages phải dùng [BindProperty]:**
```csharp
// ✅ BindProperty cho form inputs
public class CreateProductModel : BasePageModel
{
    [BindProperty]
    public CreateProductInputModel Input { get; set; } = new();

    // ✅ Input model riêng biệt, không dùng Command trực tiếp
    public class CreateProductInputModel
    {
        [Required] public string Title { get; set; } = "";
        [Required] public string Description { get; set; } = "";
        public decimal Price { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Map Input → Command (không expose Command shape ra form)
        var command = new CreateProductCommand(
            StoreId: User.GetStoreId(),
            Title: Input.Title,
            Description: Input.Description,
            Price: Input.Price);

        var result = await _sender.Send(command);
        return this.RedirectOnSuccess(result, "/Seller/Products");
    }
}
```

---

### Rule Group 4: HTMX Endpoint Pattern

**Quy tắc**: Endpoint biết mình đang serve HTMX request hay full page request và trả về đúng loại response.

```bash
# 4A. Tìm handler không kiểm tra IsHtmx() trước khi trả về partial
echo "=== Handlers returning Partial without IsHtmx check ==="
grep -rn "return Partial\|\.Partial(" src/MarketNest.Web/ --include="*.cshtml.cs" \
  | grep -v "IsHtmx\|isHtmx\|HX-Request\|bin/\|obj/" | head -20

# 4B. Tìm HTMX response header chưa được set đúng
echo "=== Missing HX-Retarget on error partials ==="
grep -rn "HX-Retarget\|HxRetarget\|HX-Reswap\|HxReswap" \
  src/MarketNest.Web/ --include="*.cshtml.cs" \
  | grep -v "bin/\|obj/" | head -20

# 4C. Tìm HX-Trigger event được fire (event bus contract)
echo "=== HX-Trigger events being fired ==="
grep -rn "HX-Trigger\|HxTrigger\|HX_Trigger" \
  src/MarketNest.Web/ --include="*.cshtml.cs" --include="*.cs" \
  | grep -v "bin/\|obj/" | head -20

# 4D. Tìm antiforgery bị thiếu trên POST/PUT/DELETE handler
echo "=== POST handlers potentially missing antiforgery ==="
grep -rn "OnPost\|OnPut\|OnDelete" src/MarketNest.Web/ --include="*.cshtml.cs" \
  | grep -v "\[IgnoreAntiforgery\]\|[ValidateAntiforgery]\|bin/\|obj/" | head -20
# Note: Antiforgery được handle toàn cục bởi app.UseAntiforgery()
# Chỉ cần kiểm tra khi handler dùng [IgnoreAntiforgery] không đúng lý do

# 4E. Kiểm tra HTMX config trong _Layout.cshtml (RequestVerificationToken header)
grep -rn "RequestVerificationToken\|htmx:configRequest\|htmx-config" \
  src/MarketNest.Web/Pages/ --include="*.cshtml" | grep -v "bin/\|obj/" | head -10
```

**Fix pattern — Handler phân biệt HTMX vs full page:**
```csharp
// ❌ Sai: luôn trả partial không phân biệt
public async Task<IActionResult> OnGetProductsAsync([FromQuery] ProductSearchQuery query)
{
    var result = await _sender.Send(query);
    return Partial("_ProductGrid", result); // sẽ hỏng nếu user navigate trực tiếp!
}

// ✅ Đúng: phân biệt request type
public async Task<IActionResult> OnGetProductsAsync([FromQuery] ProductSearchQuery query)
{
    var result = await _sender.Send(query);

    // Trả partial nếu HTMX request, full page nếu direct navigation
    return Request.IsHtmx()
        ? Partial("_ProductGrid", result)
        : Page();
}
```

**Fix pattern — Error response cho HTMX:**
```csharp
// ❌ Sai: return Page() khi HTMX request → HTMX nhận full HTML, swap sai target
public async Task<IActionResult> OnPostAddToCartAsync(AddToCartRequest request)
{
    var result = await _sender.Send(request.ToCommand(User.GetUserId()));
    if (result.IsFailure)
        return Page(); // ← HTMX sẽ swap toàn bộ page vào target!
}

// ✅ Đúng: redirect error đến đúng region
public async Task<IActionResult> OnPostAddToCartAsync(AddToCartRequest request)
{
    var result = await _sender.Send(request.ToCommand(User.GetUserId()));

    if (result.IsFailure)
    {
        if (Request.IsHtmx())
        {
            // Redirect HTMX response đến error region
            Response.Headers["HX-Retarget"] = "#cart-error";
            Response.Headers["HX-Reswap"]   = "outerHTML";
            return Partial("_CartError", result.Error.Message);
        }
        ModelState.AddModelError("", result.Error.Message);
        return Page();
    }

    // Thành công: fire HX-Trigger event để cập nhật cart count
    Response.HxTrigger("cartUpdated", new { count = result.Value.ItemCount });
    return Request.IsHtmx()
        ? Partial("_CartSummary", result.Value)
        : RedirectToPage("/Cart");
}
```

**Polling endpoint (Disputes messages):**
```csharp
// ✅ Pattern cho polling endpoint — phải trả về đúng partial, có cache prevention
public async Task<IActionResult> OnGetMessagesAsync(Guid disputeId)
{
    // Ngăn browser cache polling response
    Response.Headers["Cache-Control"] = "no-store";
    Response.Headers["Vary"]          = "HX-Request";

    var messages = await _sender.Send(new GetDisputeMessagesQuery(disputeId, User.GetUserId()));

    return Request.IsHtmx()
        ? Partial("_MessageList", messages)
        : RedirectToPage("/Account/Disputes/Detail", new { id = disputeId });
}
```

---

### Rule Group 5: Rate Limit Annotation

**Quy tắc MarketNest**: Mọi Minimal API endpoint phải có `.RequireRateLimiting()`. Razor Pages auth endpoints được bao phủ bởi global rate limiter cho `/auth/` path tại Nginx.

```bash
# 5A. Tìm Minimal API endpoint thiếu RequireRateLimiting
echo "=== Minimal API endpoints missing RequireRateLimiting ==="
grep -rn "app\.Map\|\.Map(" src/MarketNest.Web/ --include="*.cs" -A5 \
  | grep -v "bin/\|obj/" \
  | grep -B3 "RequireAuthorization\|\.WithName\|\.WithTags\|\.Produces" \
  | grep "app\.Map\|\.Map(" | head -20
# Manual check: mỗi endpoint có .RequireRateLimiting() không?

# 5B. Kiểm tra rate limiter policies được định nghĩa đủ chưa
grep -rn "AddFixedWindowLimiter\|AddSlidingWindowLimiter\|AddTokenBucketLimiter\|AddConcurrencyLimiter" \
  src/MarketNest.Web/ --include="*.cs" | grep -v "bin/\|obj/" | head -10

# 5C. Tìm auth endpoints không có rate limiter "auth" (stricter policy)
echo "=== Auth endpoints using 'public' policy instead of 'auth' ==="
grep -rn "MapPost.*auth\|MapPost.*login\|MapPost.*register\|MapPost.*refresh" \
  src/MarketNest.Web/ --include="*.cs" -A5 \
  | grep -v "bin/\|obj/" \
  | grep "RequireRateLimiting.*public" | head -10

# 5D. Tìm 429 response thiếu Retry-After header
echo "=== Rate limit rejection missing Retry-After header ==="
grep -rn "RejectionStatusCode\|Status429\|OnRejected" \
  src/MarketNest.Web/ --include="*.cs" | grep -v "bin/\|obj/" | head -10
```

**Fix — Rate limiter policies và endpoint annotation:**
```csharp
// Program.cs — 3 policies theo đúng spec
builder.Services.AddRateLimiter(options =>
{
    // Public endpoints: 60/min per IP
    options.AddFixedWindowLimiter("public", cfg =>
    {
        cfg.PermitLimit = 60;
        cfg.Window      = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit   = 5;
    });

    // Auth endpoints: 5/15min per IP (brute force protection)
    options.AddFixedWindowLimiter("auth", cfg =>
    {
        cfg.PermitLimit = 5;
        cfg.Window      = TimeSpan.FromMinutes(15);
        cfg.QueueLimit  = 0; // không queue — reject ngay
    });

    // Upload endpoints: 10/min per user (storage abuse protection)
    options.AddFixedWindowLimiter("upload", cfg =>
    {
        cfg.PermitLimit = 10;
        cfg.Window      = TimeSpan.FromMinutes(1);
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // ✅ Thêm Retry-After header vào response
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers["Retry-After"] =
                ((int)retryAfter.TotalSeconds).ToString();

        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type   = "https://marketnest.com/errors/rate-limited",
            Title  = "Too many requests",
            Status = 429
        }, ct);
    };
});

// ✅ Endpoint annotation chuẩn
app.MapPost("/api/cart/items", AddCartItemHandler)
   .RequireAuthorization()
   .RequireRateLimiting("public")       // ✅ bắt buộc
   .WithName("AddCartItem")
   .WithTags("Cart")
   .Produces<CartItemCreatedDto>(201)   // ✅ document response
   .ProducesProblem(400)
   .ProducesProblem(422)
   .ProducesProblem(429);

// ❌ Auth endpoint dùng "public" thay vì "auth"
app.MapPost("/api/auth/login", LoginHandler)
   .RequireRateLimiting("auth");  // ✅ stricter policy
```

---

### Rule Group 6: Response Schema Consistency

**Quy tắc**: Mọi list endpoint phải trả về `PagedResult<T>`. Mọi create endpoint trả về resource ID. Field names phải camelCase trong JSON.

```bash
# 6A. Tìm list endpoint không dùng PagedResult
echo "=== List endpoints not returning PagedResult ==="
grep -rn "MapGet\|OnGet" src/ --include="*.cs" -A5 \
  | grep -v "bin/\|obj/" \
  | grep "List<\|IEnumerable<\|IReadOnlyList<" \
  | grep -v "PagedResult\|Paged" | head -20

# 6B. Kiểm tra JSON serialization policy (phải camelCase)
grep -rn "JsonSerializerOptions\|AddJsonOptions\|PropertyNamingPolicy" \
  src/MarketNest.Web/ --include="*.cs" | grep -v "bin/\|obj/" | head -10

# 6C. Tìm DTO expose entity ID dạng Guid string vs proper Guid
grep -rn "\.ToString()\|\.ToString(\"N\")\|\.ToString(\"D\")" \
  src/ -path "*/Application/*" --include="*.cs" \
  | grep "Id\b\|id\b" | grep -v "bin/\|obj/" | head -10

# 6D. Tìm endpoint trả về domain entity trực tiếp (nên là DTO)
echo "=== Endpoints potentially returning domain entities (not DTOs) ==="
grep -rn "Results\.Ok\|TypedResults\.Ok\|return Ok(" src/ --include="*.cs" \
  | grep -v "bin/\|obj/" \
  | grep "Order\b\|Product\b\|Cart\b\|Review\b\|Dispute\b" \
  | grep -v "Dto\|dto\|Result\|View" | head -10
```

**Fix — Consistent PagedResult:**
```csharp
// ❌ Sai: trả raw List
app.MapGet("/api/products", async (
    [FromQuery] int page,
    [FromQuery] int pageSize,
    ISender sender) =>
{
    var products = await sender.Send(new GetProductsQuery(page, pageSize));
    return TypedResults.Ok(products); // List<ProductDto> — thiếu pagination metadata
});

// ✅ Đúng: PagedResult với metadata
app.MapGet("/api/products", async (
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    ISender sender) =>
{
    var result = await sender.Send(new GetProductsQuery(page, Math.Min(pageSize, 50)));
    return TypedResults.Ok(result); // PagedResult<ProductListItemDto>
})
.RequireRateLimiting("public")
.Produces<PagedResult<ProductListItemDto>>(200)
.WithName("GetProducts");
```

**JSON naming convention — Program.cs:**
```csharp
// ✅ Đảm bảo camelCase và ignore null
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
```

---

### Rule Group 7: Endpoint Metadata & OpenAPI

**Quy tắc (Phase 2+)**: Mọi Minimal API endpoint phải có `.WithName()`, `.WithTags()`, `.Produces<T>()`, và response codes documented.

```bash
# 7A. Tìm endpoint thiếu WithName (cần cho link generation)
echo "=== Endpoints missing WithName ==="
grep -rn "app\.Map\|\.Map(" src/MarketNest.Web/ --include="*.cs" \
  | grep -v "bin/\|obj/\|MapHealthChecks\|MapRazorPages\|MapHub" \
  | grep -v "WithName" | head -20

# 7B. Tìm endpoint thiếu Produces<T> annotation
echo "=== Endpoints missing Produces annotation ==="
grep -rn "app\.Map\|\.Map(" src/MarketNest.Web/ --include="*.cs" -A10 \
  | grep -v "bin/\|obj/\|MapHealthChecks\|MapRazorPages" \
  | grep "app\.Map\|\.Map(" | grep -v "\.Produces" | head -20

# 7C. Kiểm tra WithOpenApi() được bật chưa
grep -rn "WithOpenApi\|MapOpenApi\|AddOpenApi\|UseSwagger" \
  src/MarketNest.Web/ --include="*.cs" | grep -v "bin/\|obj/" | head -5

# 7D. Endpoint group có tag nhất quán chưa
grep -rn "\.WithTags(" src/MarketNest.Web/ --include="*.cs" \
  | grep -v "bin/\|obj/" | sort | head -20
```

**Fix — đầy đủ metadata:**
```csharp
// ✅ Endpoint đầy đủ metadata
var cart = app.MapGroup("/api/cart")
    .WithTags("Cart")
    .RequireAuthorization()
    .RequireRateLimiting("public");

cart.MapPost("/items", AddCartItemHandler)
    .WithName("Cart_AddItem")
    .WithSummary("Add item to cart")
    .WithDescription("Adds a product variant to the authenticated user's active cart. Creates cart if not exists.")
    .Accepts<AddToCartRequest>("application/json")
    .Produces<CartItemCreatedDto>(201, "application/json")
    .ProducesProblem(422)   // validation error
    .ProducesProblem(404)   // variant not found
    .ProducesProblem(409)   // insufficient stock
    .ProducesProblem(429)   // rate limited
    .WithOpenApi();

cart.MapDelete("/items/{cartItemId:guid}", RemoveCartItemHandler)
    .WithName("Cart_RemoveItem")
    .Produces(204)
    .ProducesProblem(404)
    .ProducesProblem(403)
    .WithOpenApi();
```

---

## Phase 3: REPORT — Báo cáo API Contract

```markdown
# API Contract Review Report — MarketNest
**Date**: <ngày>
**Scope**: Minimal API, Razor Pages handlers, HTMX contract, Problem Details, Rate Limiting

---

## Tổng quan

| Rule Group | Violations | Severity |
|---|---|---|
| HTTP Status Codes | X | BLOCKER / HIGH |
| Problem Details RFC 7807 | X | BLOCKER |
| FluentValidation Wiring | X | HIGH |
| HTMX Endpoint Pattern | X | HIGH / MEDIUM |
| Rate Limit Annotation | X | HIGH |
| Response Schema | X | MEDIUM |
| OpenAPI Metadata | X | LOW |

---

## 🔴 BLOCKER

### [B-001] 500 response exposing exception message
- **File**: `Web/Middleware/ExceptionMiddleware.cs:47`
- **Vi phạm**: `Detail = ex.Message` trong 500 handler — expose internal error
- **Fix**: Xóa `Detail`, chỉ log internally, trả generic message + correlationId

---

## 🟠 HIGH

### [H-001] Create endpoint returning 200 instead of 201
- **File**: `Web/Extensions/CartEndpoints.cs:23`
- **Vi phạm**: `TypedResults.Ok(result)` sau POST
- **Fix**: `TypedResults.Created($"/api/cart/items/{id}", new { id })`

---

## 🟡 MEDIUM

...
```

---

## Phase 4: Integration Test Checklist

Sau khi fix, verify bằng integration test với WebApplicationFactory + Testcontainers:

```csharp
// tests/MarketNest.IntegrationTests/Api/CartEndpointTests.cs
public class CartEndpointTests(MarketNestWebAppFactory factory)
    : IClassFixture<MarketNestWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AddCartItem_Returns201_WithLocation()
    {
        // Arrange: auth + valid request
        await _client.AuthenticateAsBuyerAsync();
        var request = new { variantId = TestData.ActiveVariantId, quantity = 1 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart/items", request);

        // Assert: status + Location header
        response.StatusCode.Should().Be(HttpStatusCode.Created);          // 201
        response.Headers.Location.Should().NotBeNull();                    // Location header
    }

    [Fact]
    public async Task AddCartItem_InvalidRequest_Returns422_WithProblemDetails()
    {
        await _client.AuthenticateAsBuyerAsync();
        var request = new { variantId = Guid.Empty, quantity = 0 }; // invalid

        var response = await _client.PostAsJsonAsync("/api/cart/items", request);

        // Assert: 422, not 400
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity); // 422

        // Assert: Problem Details RFC 7807 shape
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Type.Should().StartWith("https://marketnest.com/errors/");
        problem.Status.Should().Be(422);
        problem.Extensions.Should().ContainKey("correlationId");
        problem.Errors.Should().NotBeEmpty();

        // Assert: Content-Type
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json"); // RFC 7807
    }

    [Fact]
    public async Task AddCartItem_Unauthenticated_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/cart/items",
            new { variantId = Guid.NewGuid(), quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized); // 401
    }

    [Fact]
    public async Task AddCartItem_RateLimited_Returns429_WithRetryAfter()
    {
        await _client.AuthenticateAsBuyerAsync();
        var request = new { variantId = TestData.ActiveVariantId, quantity = 1 };

        // Exhaust rate limit
        for (int i = 0; i < 65; i++)
            await _client.PostAsJsonAsync("/api/cart/items", request);

        var response = await _client.PostAsJsonAsync("/api/cart/items", request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests); // 429
        response.Headers.Should().ContainKey("Retry-After");             // Retry-After
    }

    [Fact]
    public async Task HtmxRequest_ReturnsPartial_NotFullPage()
    {
        await _client.AuthenticateAsBuyerAsync();
        _client.DefaultRequestHeaders.Add("HX-Request", "true");

        var response = await _client.GetAsync("/cart/summary");

        // HTMX response: không có <html> tag, chỉ có partial HTML
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("<html");
        html.Should().Contain("id=\"cart-summary\""); // partial root element
    }
}
```

---

## Quick Reference — Status Code Map

| Tình huống | Status | TypedResults method |
|---|---|---|
| GET thành công | `200` | `TypedResults.Ok(dto)` |
| POST tạo resource | `201` | `TypedResults.Created(location, dto)` |
| PUT/PATCH update | `200` | `TypedResults.Ok(dto)` |
| DELETE | `204` | `TypedResults.NoContent()` |
| Validation fail | `422` | `TypedResults.Problem(..., 422)` |
| Not found | `404` | `TypedResults.Problem(..., 404)` |
| Unauthorized | `401` | `TypedResults.Problem(..., 401)` |
| Forbidden | `403` | `TypedResults.Problem(..., 403)` |
| Conflict/Business | `409` | `TypedResults.Problem(..., 409)` |
| Rate limited | `429` | Handled by middleware |
| Server error | `500` | Handled by middleware |

## Quick Reference — HTMX Response Headers

| Header | Khi nào dùng | Giá trị ví dụ |
|---|---|---|
| `HX-Redirect` | Thành công → full page nav | `/orders/123/confirmation` |
| `HX-Refresh` | Cần reload toàn trang | `true` |
| `HX-Retarget` | Error → khác target | `#error-banner` |
| `HX-Reswap` | Override swap strategy | `outerHTML` / `beforeend` |
| `HX-Trigger` | Fire Alpine event | `{"cartUpdated": {...}}` |
| `HX-Push-Url` | Update URL bar | `/orders?page=2` |
| `Cache-Control` | Polling endpoint | `no-store` |
