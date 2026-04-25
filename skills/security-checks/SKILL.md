---
name: security-checks
description: >
  Scan the entire project directory, analyze code at a granular level to detect common and critical
  security vulnerabilities. Use this skill when the user wants to: check code security, find
  security vulnerabilities, security audit, security review, pentest codebase, find security bugs,
  check for SQL injection, XSS, CSRF, race condition, authentication bypass, IDOR, access control,
  broken auth, mass assignment, backdoor, secrets leak, or says anything like "check security",
  "find vulnerabilities", "is the code secure", "security audit", "security scan",
  "vulnerability check", "CVE", "OWASP". Activate even when the user simply says
  "can this code be hacked" or "review security for me".
compatibility:
  tools: [bash, read_file, write_file, list_files, grep_search, run_in_terminal]
  agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
---

# Security Checks Skill

This skill guides the agent to scan the entire codebase, detect critical security vulnerabilities per **OWASP Top 10** and **CWE** standards, then propose specific fixes for each issue.

> **Target project**: MarketNest — .NET 10 Modular Monolith, Razor Pages + HTMX + Alpine.js, PostgreSQL, Redis, RabbitMQ.
> Always read `CLAUDE.md` and `AGENTS.md` at the repo root to understand conventions before modifying code.

> ⚠️ **Disclaimer**: This skill is for defensive purposes — helping developers find and patch vulnerabilities in their own code. Do not use it to attack other systems.

---

## Execution Flow (Mandatory order)

```
Phase 1: SCAN      → Scan directory structure, identify attack surface
Phase 2: ANALYZE   → Find each vulnerability type per checklist
Phase 3: REPORT    → Report classified by severity (CVSS)
Phase 4: FIX       → Propose and apply patches (if permitted)
Phase 5: VERIFY    → Validate after patching
```

---

## Phase 1: SCAN — Identify Attack Surface

### Step 1.1 — Scan directory structure & identify stack

**On Windows (PowerShell):**
```powershell
# List all files (exclude bin, obj, node_modules, .git)
Get-ChildItem -Recurse -File -Exclude *.dll,*.exe |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|\.git|dist|wwwroot\\lib)\\' } |
  Select-Object FullName | Sort-Object FullName

# Count by extension
Get-ChildItem -Recurse -File |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|\.git)\\' } |
  Group-Object Extension | Sort-Object Count -Descending | Select-Object -First 20 Count, Name
```

**On Linux/macOS (Bash):**
```bash
find . -type f \
  -not -path "*/bin/*" -not -path "*/obj/*" \
  -not -path "*/node_modules/*" -not -path "*/.git/*" \
  | sed 's/.*\.//' | sort | uniq -c | sort -rn | head -20
```

Identify:
- **Primary languages**: C# (.cs), Razor (.cshtml), JavaScript (.js)
- **Frameworks**: ASP.NET Core, EF Core 10, MediatR (CQRS), Razor Pages, HTMX, Alpine.js
- **Auth**: JWT + Refresh tokens, `[Access]` attribute, Role-based
- **Database**: PostgreSQL 16 (EF Core), Redis (caching/session)

### Step 1.2 — Find sensitive entry points

```powershell
# API endpoints (Minimal API + Razor Pages)
Select-String -Path src/**/*.cs -Pattern 'app\.Map(Get|Post|Put|Delete|Patch)|\.MapRazorPages|\.MapHealthChecks' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Razor Pages — PageModel handlers
Select-String -Path src/**/*.cs -Pattern 'public.*IActionResult\s+On(Get|Post|Put|Delete)' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# User input points (model binding, query string, form)
Select-String -Path src/**/*.cs -Pattern '\[FromBody\]|\[FromQuery\]|\[FromForm\]|\[FromRoute\]|Request\.Form|Request\.Query|HttpContext\.Request' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# File upload endpoints
Select-String -Path src/**/*.cs -Pattern 'IFormFile|IFormFileCollection|multipart|upload' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Configuration / secrets access
Select-String -Path src/**/*.cs -Pattern 'IConfiguration|GetConnectionString|GetValue<|GetSection' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

### Step 1.3 — Find accidentally committed sensitive files

```powershell
# Hardcoded secrets / credentials in code
Select-String -Path src/**/*.cs,src/**/*.json,src/**/*.cshtml -Pattern 'password\s*=\s*"|secret\s*=\s*"|api_key\s*=\s*"|token\s*=\s*"' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# .env file committed
Get-ChildItem -Recurse -Name '.env' |
  Where-Object { $_ -notmatch '(\.git|node_modules)' }

# Private key / certificate files
Get-ChildItem -Recurse -Include *.pem,*.key,*.p12,*.pfx |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|\.git)\\' }

# Check if .gitignore protects properly
Select-String -Path .gitignore -Pattern '\.env|secret|\.key|\.pem|\.pfx|appsettings\.Production'

# Connection string with password in appsettings
Select-String -Path src/**/*.json -Pattern 'Password=|pwd=' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

---

## Phase 2: ANALYZE — Analyze Each Vulnerability Type

### 2.1 SQL Injection (CWE-89)

**Risk**: Attacker injects SQL code via input → read/delete/modify database, bypass authentication.

```powershell
# Raw SQL with string concatenation (most dangerous)
Select-String -Path src/**/*.cs -Pattern 'FromSqlRaw|ExecuteSqlRaw|FromSql\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# String interpolation in SQL (EF Core)
Select-String -Path src/**/*.cs -Pattern 'FromSqlRaw\(\$"|ExecuteSqlRaw\(\$"' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# ADO.NET direct command (if any)
Select-String -Path src/**/*.cs -Pattern 'SqlCommand|NpgsqlCommand|DbCommand' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Dapper raw query
Select-String -Path src/**/*.cs -Pattern '\.Query<|\.Execute\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**Danger signs**:
- `FromSqlRaw($"SELECT * FROM users WHERE id = {userId}")` → use `FromSqlInterpolated` or parameterized
- String concat in SQL command → always use parameterized query
- `ExecuteSqlRaw` with direct user input

**Fix pattern**:
```csharp
// ❌ Vulnerable — string interpolation in FromSqlRaw
var users = await _context.Users
    .FromSqlRaw($"SELECT * FROM users WHERE email = '{email}'")
    .ToListAsync();

// ✅ Safe — Use FromSqlInterpolated (auto-parameterize)
var users = await _context.Users
    .FromSqlInterpolated($"SELECT * FROM users WHERE email = {email}")
    .ToListAsync();

// ✅ Safe — Use FromSqlRaw with parameters
var users = await _context.Users
    .FromSqlRaw("SELECT * FROM users WHERE email = {0}", email)
    .ToListAsync();
```

---

### 2.2 Cross-Site Scripting — XSS (CWE-79)

**Risk**: Inject JavaScript into web page → steal cookies, sessions, execute code in browser.

```powershell
# Razor: @Html.Raw() — renders HTML without escaping (Stored/Reflected XSS)
Select-String -Path src/**/*.cshtml -Pattern 'Html\.Raw\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# JavaScript: innerHTML / document.write
Select-String -Path src/**/*.cshtml,src/**/*.js -Pattern 'innerHTML\s*=|document\.write\(|\.html\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj|node_modules|wwwroot\\lib)\\' }

# Alpine.js: x-html directive (renders raw HTML)
Select-String -Path src/**/*.cshtml -Pattern 'x-html=' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# HTMX: hx-swap="innerHTML" combined with user-generated content
Select-String -Path src/**/*.cshtml -Pattern 'hx-swap.*innerHTML' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# eval / new Function (JavaScript)
Select-String -Path src/**/*.js,src/**/*.cshtml -Pattern '\beval\s*\(|new Function\s*\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj|node_modules|wwwroot\\lib)\\' }
```

**Danger signs**:
- `@Html.Raw(userInput)` → only use `@Html.Raw()` with sanitized content
- `x-html="userComment"` → use `x-text` unless content is sanitized
- Razor `@` auto-encodes, but `@Html.Raw()` does not

**Fix pattern**:
```html
<!-- ❌ XSS: render user content without escaping -->
<div>@Html.Raw(Model.UserComment)</div>
<div x-html="comment"></div>

<!-- ✅ Safe: Razor auto-encode -->
<div>@Model.UserComment</div>
<div x-text="comment"></div>

<!-- ✅ If HTML rendering is needed: sanitize first -->
@Html.Raw(Html.Encode(Model.UserComment))
```

**CSP Header** (mandatory — check in Program.cs or middleware):
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self'; object-src 'none'");
    await next();
});
```

---

### 2.3 Cross-Site Request Forgery — CSRF (CWE-352)

**Risk**: Attacker tricks user into executing actions unknowingly (transfer funds, change password, delete account).

```powershell
# Razor Pages: POST handler missing [ValidateAntiForgeryToken] or asp-antiforgery
Select-String -Path src/**/*.cs -Pattern 'public.*IActionResult\s+OnPost' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Check forms missing antiforgery token
Select-String -Path src/**/*.cshtml -Pattern '<form' -Recurse |
  Where-Object { $_.Line -notmatch 'asp-antiforgery|__RequestVerificationToken|@Html\.AntiForgeryToken' -and $_.Path -notmatch '\\(bin|obj)\\' }

# Minimal API: POST/PUT/DELETE missing ValidateAntiforgeryToken
Select-String -Path src/**/*.cs -Pattern 'app\.Map(Post|Put|Delete)' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Check global antiforgery config
Select-String -Path src/**/*.cs -Pattern 'AddAntiforgery|ValidateAntiForgeryToken|AutoValidateAntiforgeryToken' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**Mandatory checklist**:
- [ ] Razor Pages auto-add antiforgery for `<form method="post">` (when using tag helpers)
- [ ] Minimal API endpoints have antiforgery validation if called from forms
- [ ] AJAX calls (HTMX) send antiforgery token via header
- [ ] `SameSite=Strict` or `SameSite=Lax` on cookies

**Fix pattern**:
```html
<!-- ✅ Razor Pages auto-inject token when using asp-page / asp-route -->
<form method="post" asp-page="/Order/Create">
    <!-- Token is automatically added -->
    <button type="submit">Place Order</button>
</form>

<!-- ✅ HTMX: send token via header -->
<meta name="RequestVerificationToken" content="@Html.AntiForgeryToken()" />
<script>
  document.body.addEventListener('htmx:configRequest', (event) => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (token) event.detail.headers['RequestVerificationToken'] = token;
  });
</script>
```

---

### 2.4 Broken Authentication & Session Management (CWE-287, CWE-384)

**Risk**: Login as another user, hijack admin privileges, bypass 2FA.

```powershell
# JWT configuration
Select-String -Path src/**/*.cs -Pattern 'AddJwtBearer|JwtBearerDefaults|TokenValidationParameters|JwtSecurityToken' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# JWT secret key hardcoded or weak
Select-String -Path src/**/*.cs,src/**/*.json -Pattern 'SigningKey|IssuerSigningKey|SecurityKey|SymmetricSecurityKey' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Password hashing — check what's used
Select-String -Path src/**/*.cs -Pattern 'PasswordHasher|BCrypt|Argon2|PBKDF2|SHA1|SHA256|MD5|HashPassword|VerifyHashedPassword' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Cookie configuration
Select-String -Path src/**/*.cs -Pattern 'CookieOptions|HttpOnly|Secure|SameSite|CookieAuthenticationDefaults|CookiePolicy' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Refresh token implementation
Select-String -Path src/**/*.cs -Pattern 'RefreshToken|refresh_token|TokenRefresh' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Missing rate limit on auth endpoints
Select-String -Path src/**/*.cs -Pattern 'RateLimiter|RateLimiting|FixedWindowRateLimiter|SlidingWindowRateLimiter' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**Mandatory checklist**:
- [ ] JWT verify is complete: signature + expiry + issuer + audience
- [ ] JWT signing key is strong enough (min 256 bit) and from config/env, not hardcoded
- [ ] Password hash using ASP.NET Core Identity `PasswordHasher` (PBKDF2) or BCrypt/Argon2
- [ ] Cookie has `HttpOnly = true`, `Secure = true`, `SameSite = Strict/Lax`
- [ ] Refresh token has expiry, rotation, and revocation
- [ ] Rate limit on `/login`, `/register`, `/forgot-password`
- [ ] Lockout policy after N failed login attempts

**Fix pattern**:
```csharp
// ❌ Weak — MD5/SHA is insufficient for password hashing
var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password)));

// ✅ Correct — ASP.NET Core Identity PasswordHasher (PBKDF2)
var hasher = new PasswordHasher<User>();
var hash = hasher.HashPassword(user, password);

// ✅ Complete JWT config
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,            // ✅ Validate issuer
            ValidateAudience = true,          // ✅ Validate audience
            ValidateLifetime = true,          // ✅ Check expiry
            ValidateIssuerSigningKey = true,  // ✅ Verify signature
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)), // From env/config
            ClockSkew = TimeSpan.Zero         // No clock skew tolerance
        };
    });
```

---

### 2.5 Insecure Direct Object Reference — IDOR (CWE-639)

**Risk**: User changes ID in URL/body to access another user's resources without authorization check.

```powershell
# Route parameter using ID directly — check if ownership is verified
Select-String -Path src/**/*.cs -Pattern '\[FromRoute\].*[Ii]d|\{id\}|\{orderId\}|\{productId\}' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# CQRS Query/Command handler: fetching entity by ID without user check
Select-String -Path src/**/*.cs -Pattern 'FindAsync\(|FirstOrDefaultAsync\(|SingleOrDefaultAsync\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Razor Pages: OnGet/OnPost receiving ID directly
Select-String -Path src/**/*.cs -Pattern 'OnGet.*int.*id|OnPost.*int.*id|OnGet.*Guid.*id' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**Verification**: Every place that fetches an entity by ID from user input must have at least one of:
- Filter by `UserId` / `OwnerId` / `StoreId` belonging to current user
- `[Access]` attribute checking ownership
- Authorization policy checking resource-based access

**Fix pattern**:
```csharp
// ❌ IDOR: fetch any order, no ownership check
var order = await _context.Orders.FindAsync(orderId);

// ✅ Always filter by owner
var order = await _context.Orders
    .AsNoTracking()
    .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerId == currentUserId);
if (order is null)
    return Result<OrderDto, Error>.Failure(OrderErrors.NotFound);
```

---

### 2.6 Access Control Problems (CWE-284, CWE-285)

**Risk**: Regular user accesses admin functionality, or bypasses role-based authorization.

```powershell
# Razor Pages / Controllers missing [Authorize]
Select-String -Path src/**/*.cs -Pattern 'class\s+\w+.*PageModel|class\s+\w+.*Controller' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
# Then check if [Authorize] exists at class or method level

# Admin pages missing role check
Select-String -Path src/**/*.cs -Pattern '\[Authorize\]' -Recurse |
  Where-Object { $_.Path -match 'Admin' -and $_.Path -notmatch '\\(bin|obj)\\' }

# [AllowAnonymous] on sensitive endpoints
Select-String -Path src/**/*.cs -Pattern '\[AllowAnonymous\]' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# RouteWhitelistMiddleware — check if whitelist is too broad
Select-String -Path src/**/*.cs -Pattern 'WhitelistedPrefixes|RouteWhitelist' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# CORS configuration
Select-String -Path src/**/*.cs -Pattern 'AddCors|WithOrigins|AllowAnyOrigin|AllowAnyMethod|AllowAnyHeader' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**MarketNest-specific checklist**:
- [ ] All Admin pages have `[Authorize(Roles = "Admin")]`
- [ ] Seller pages have `[Authorize(Roles = "Seller")]`
- [ ] `[Access]` attribute properly protects resources
- [ ] `[AllowAnonymous]` only on public pages (home, browse, login, register)
- [ ] CORS does not use `AllowAnyOrigin()` in production
- [ ] `RouteWhitelistMiddleware` blocks unregistered routes

**Fix pattern**:
```csharp
// ❌ Admin page missing role authorization
[Authorize]
public class ManageUsersModel : PageModel { }

// ✅ Admin page with role check
[Authorize(Roles = "Admin")]
public class ManageUsersModel : PageModel { }

// ❌ CORS too permissive
builder.Services.AddCors(o => o.AddPolicy("all", p => p.AllowAnyOrigin()));

// ✅ CORS restricted by domain
builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.WithOrigins("https://marketnest.com")
     .AllowCredentials()
     .WithMethods("GET", "POST", "PUT", "DELETE")));
```

---

### 2.7 Mass Assignment / Over-Posting (CWE-915)

**Risk**: Attacker adds fields to request body to overwrite unintended properties (role, price, balance).

```powershell
# Model binding directly to entity (not through DTO)
Select-String -Path src/**/*.cs -Pattern '\[BindProperty\].*public.*Entity|\[FromBody\].*Entity' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Razor Pages: BindProperty on too many properties
Select-String -Path src/**/*.cs -Pattern '\[BindProperty\]' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# TryUpdateModelAsync used directly on entity
Select-String -Path src/**/*.cs -Pattern 'TryUpdateModelAsync' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**Fix pattern**:
```csharp
// ❌ Over-posting: bind directly to entity
[BindProperty]
public Product Product { get; set; }  // Attacker can set Price, IsApproved, etc.

// ✅ Use separate DTO/Command (MarketNest convention: record + { get; init; })
[BindProperty]
public CreateProductCommand Input { get; set; }

public record CreateProductCommand
{
    public string Name { get; init; }
    public string Description { get; init; }
    // Not exposed: Price, IsApproved, SellerId — set server-side
}
```

---

### 2.8 Race Conditions (CWE-362)

**Risk**: Two simultaneous requests → double-spend, oversell inventory, duplicate order.

```powershell
# Check-then-act pattern (TOCTOU): read then write
Select-String -Path src/**/*.cs -Pattern 'if.*\.Stock|if.*\.Balance|if.*\.Quantity|if.*\.Count' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# SaveChanges in loop
Select-String -Path src/**/*.cs -Pattern 'SaveChangesAsync|SaveChanges' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Concurrency token / RowVersion — check if used
Select-String -Path src/**/*.cs -Pattern 'IsConcurrencyToken|IsRowVersion|\[ConcurrencyCheck\]|\[Timestamp\]|xmin' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Redis distributed lock
Select-String -Path src/**/*.cs -Pattern 'RedLock|LockAsync|AcquireLock|IDistributedLock' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**MarketNest-specific danger signs**:
- **Inventory**: read stock → check >= quantity → decrement stock without lock/concurrency
- **Cart reservation**: two users reserve the last item simultaneously
- **Payment**: double-charge if user submits form twice
- **Order placement**: duplicate order creation

**Fix pattern**:
```csharp
// ❌ Race condition: check-then-act is not atomic
var product = await _context.Products.FindAsync(productId);
if (product.Stock >= quantity)
{
    product.Stock -= quantity;  // Race window here!
    await _context.SaveChangesAsync();
}

// ✅ Optimistic concurrency (EF Core + PostgreSQL xmin)
// Entity config:
builder.Property<uint>("xmin")
    .HasColumnType("xid")
    .IsRowVersion();

// Handler: catch DbUpdateConcurrencyException to retry

// ✅ Atomic SQL update
await _context.Database.ExecuteSqlInterpolatedAsync(
    $"UPDATE products SET stock = stock - {quantity} WHERE id = {productId} AND stock >= {quantity}");
```

---

### 2.9 Secrets & Configuration Exposure (CWE-200, CWE-798)

**Risk**: Secrets committed to Git, hardcoded in code, or leaked via error responses.

```powershell
# Hardcoded connection string
Select-String -Path src/**/*.cs,src/**/*.json -Pattern 'Host=|Server=|Data Source=|Password=' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' -and $_.Path -notmatch 'appsettings\.Development' }

# Hardcoded API keys / secrets
Select-String -Path src/**/*.cs -Pattern 'private.*string.*(key|secret|password|token)\s*=\s*"' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Error detail leak — Development exception page in production
Select-String -Path src/**/*.cs -Pattern 'UseDeveloperExceptionPage|UseExceptionHandler|app\.Environment\.IsDevelopment' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Stack trace in response
Select-String -Path src/**/*.cs -Pattern 'ex\.StackTrace|ex\.Message|Exception.*ToString' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Sensitive data in logs
Select-String -Path src/**/*.cs -Pattern 'Log(Information|Warning|Error|Debug).*password|Log.*token|Log.*secret|Log.*credit' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**Mandatory checklist**:
- [ ] `appsettings.Production.json` does not contain secrets (use env vars or secrets manager)
- [ ] `UseDeveloperExceptionPage()` only in Development
- [ ] Error responses never leak stack traces / internal details to clients
- [ ] Logging does not log sensitive data (password, token, PII)
- [ ] `.env` in `.gitignore`
- [ ] Docker Compose secrets use env_file, not hardcoded

---

### 2.10 Security Headers & HTTPS (CWE-693, CWE-319)

**Risk**: Missing security headers → clickjacking, MIME sniffing, protocol downgrade.

```powershell
# Check security headers in Program.cs / middleware
Select-String -Path src/**/*.cs -Pattern 'X-Frame-Options|X-Content-Type-Options|X-XSS-Protection|Strict-Transport-Security|Content-Security-Policy|Referrer-Policy|Permissions-Policy' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# HTTPS redirection
Select-String -Path src/**/*.cs -Pattern 'UseHttpsRedirection|UseHsts' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Nginx config — check security headers
Select-String -Path infra/**/*.conf -Pattern 'X-Frame-Options|X-Content-Type-Options|Strict-Transport-Security|Content-Security-Policy' -Recurse
```

**Required headers** (check in Program.cs or Nginx config):
```csharp
// Middleware or configuration in Program.cs
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.Append("X-Frame-Options", "DENY");
    headers.Append("X-Content-Type-Options", "nosniff");
    headers.Append("X-XSS-Protection", "0"); // Disable, use CSP instead
    headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; object-src 'none'");
    await next();
});

app.UseHsts();               // Strict-Transport-Security
app.UseHttpsRedirection();   // Redirect HTTP → HTTPS
```

---

### 2.11 File Upload Vulnerabilities (CWE-434)

**Risk**: Upload malicious file (webshell, executable) → RCE on server.

```powershell
# Find file upload handling
Select-String -Path src/**/*.cs -Pattern 'IFormFile|SaveAsAsync|CopyToAsync|FileStream|Path\.Combine.*upload' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Check file type validation
Select-String -Path src/**/*.cs -Pattern 'ContentType|\.Extension|AllowedExtensions|MimeType' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Check file size limit
Select-String -Path src/**/*.cs -Pattern 'RequestSizeLimit|MultipartBodyLengthLimit|MaxFileSize|ContentLength' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**Mandatory checklist**:
- [ ] Validate file extension whitelist (not blacklist)
- [ ] Validate MIME type (check magic bytes, not just extension)
- [ ] File size limit (e.g., 5 MB for images)
- [ ] Store in separate storage (not in wwwroot)
- [ ] Rename file (UUID) — never use original filename
- [ ] Do not serve uploaded files directly — use handler with auth check

**Fix pattern**:
```csharp
// ❌ No validation, using original filename
var filePath = Path.Combine("wwwroot/uploads", file.FileName);
using var stream = new FileStream(filePath, FileMode.Create);
await file.CopyToAsync(stream);

// ✅ Validate + rename + store outside wwwroot
private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

if (file.Length > MaxFileSize)
    return Result.Failure(FileErrors.TooLarge);

var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!AllowedExtensions.Contains(ext))
    return Result.Failure(FileErrors.InvalidType);

var fileName = $"{Guid.NewGuid()}{ext}";
var filePath = Path.Combine(_uploadDir, fileName); // _uploadDir outside wwwroot
using var stream = new FileStream(filePath, FileMode.Create);
await file.CopyToAsync(stream, cancellationToken);
```

---

### 2.12 Dependency Vulnerabilities (CWE-1104)

**Risk**: Package has CVE → attacker exploits known vulnerability.

```powershell
# List all NuGet packages
Select-String -Path Directory.Packages.props -Pattern 'PackageVersion Include' |
  ForEach-Object { $_.Line.Trim() }

# Check npm packages
if (Test-Path src/MarketNest.Web/package.json) {
    Get-Content src/MarketNest.Web/package.json | ConvertFrom-Json | Select-Object -ExpandProperty dependencies
}

# Run dotnet audit (.NET 8+)
dotnet restore ; dotnet list package --vulnerable

# Run npm audit
Push-Location src/MarketNest.Web ; npm audit ; Pop-Location
```

**Checklist**:
- [ ] `dotnet list package --vulnerable` shows no HIGH/CRITICAL
- [ ] `npm audit` shows no HIGH/CRITICAL
- [ ] Central Package Management (`Directory.Packages.props`) pins all versions
- [ ] No EOL / unmaintained packages

---

### 2.13 Backdoor Detection (CWE-506)

**Risk**: Hidden code allowing unauthorized access — supply chain attack or insider threat.

```powershell
# Hardcoded master password / debug bypass
Select-String -Path src/**/*.cs -Pattern 'admin.*admin|root.*root|password.*1234|master.*key|backdoor|debug.*true|bypass.*auth' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj|test)\\' }

# Hidden endpoint / debug route
Select-String -Path src/**/*.cs -Pattern '/debug|/backdoor|/admin-secret|/internal|/__debug|/dev-only' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Process/shell execution
Select-String -Path src/**/*.cs -Pattern 'Process\.Start|ProcessStartInfo|System\.Diagnostics\.Process' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Obfuscated code
Select-String -Path src/**/*.cs,src/**/*.js -Pattern 'Convert\.FromBase64String|atob\(|btoa\(|fromCharCode' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj|node_modules|wwwroot\\lib)\\' }

# npm postinstall script
if (Test-Path src/MarketNest.Web/package.json) {
    $pkg = Get-Content src/MarketNest.Web/package.json | ConvertFrom-Json
    $pkg.scripts | Format-List
}
```

**Signs requiring immediate investigation**:
- `if (user.Email == "admin@hidden.com") return true;` — auth bypass
- Endpoint not listed in `AppRoutes`
- `Process.Start("cmd", userInput)` — RCE
- Base64 decode then execute

---

## Phase 3: REPORT — Security Report

Generate a report in the following format (save to `SECURITY_REPORT.md`):

```markdown
# Security Audit Report
**Project**: MarketNest
**Date**: <date>
**Auditor**: AI Security Scanner
**Standard**: OWASP Top 10 + CWE
**Stack**: .NET 10, EF Core 10, PostgreSQL 16, Redis, Razor Pages + HTMX + Alpine.js

---

## Risk Overview

| Severity | Count |
|---|---|
| 🔴 CRITICAL | X |
| 🟠 HIGH | X |
| 🟡 MEDIUM | X |
| 🟢 LOW | X |
| ℹ️ INFO | X |

**Risk Score**: X / 10

---

## Vulnerability Details

### 🔴 CRITICAL — [Vulnerability Name]
- **CWE**: CWE-XXX
- **CVSS Score**: X.X (Critical/High/Medium/Low)
- **File**: `path/to/file.cs:line`
- **Description**: ...
- **Proof of Concept**: (if safely demonstrable)
- **Fix**: ...
- **Reference**: [OWASP link]

...

---

## Summary Checklist

### Authentication & Authorization
- [ ] Password hash using PasswordHasher / BCrypt / Argon2
- [ ] JWT verify complete (signature + expiry + issuer + audience)
- [ ] Cookie has HttpOnly + Secure + SameSite
- [ ] Rate limit on login/register
- [ ] All pages/endpoints have appropriate [Authorize]
- [ ] Admin pages have role check

### Input Validation
- [ ] All user input goes through FluentValidation
- [ ] No FromSqlRaw with string concat
- [ ] File upload validates type + size + renames
- [ ] No Html.Raw() with unsanitized content

### CSRF Protection
- [ ] Razor Pages forms use tag helpers (auto-antiforgery)
- [ ] HTMX requests send antiforgery token
- [ ] SameSite cookie policy

### Secrets Management
- [ ] No hardcoded secrets in code
- [ ] .env in .gitignore
- [ ] appsettings.Production does not contain passwords
- [ ] Error responses do not leak internal details

### Security Headers
- [ ] Content-Security-Policy
- [ ] X-Frame-Options: DENY
- [ ] X-Content-Type-Options: nosniff
- [ ] Strict-Transport-Security (HSTS)
- [ ] CORS restricted (no AllowAnyOrigin)

### Data Protection
- [ ] Concurrency control on inventory/payment operations
- [ ] IDOR protection — filter by owner
- [ ] Mass assignment protection — DTO/Command, no direct entity binding
- [ ] Sensitive data not logged

### Dependencies
- [ ] dotnet list package --vulnerable is clean
- [ ] npm audit is clean
- [ ] No EOL packages
```

---

## Phase 4: FIX — Apply Patches

**Mandatory rules before patching**:
1. **Ask for confirmation** before each file (unless user already granted permission)
2. **Commit/backup first** — `git stash` or backup file
3. **Fix one vulnerability at a time**, in order: CRITICAL → HIGH → MEDIUM
4. **Do not change business logic**, only patch security vulnerabilities
5. **Document reasoning** for each change
6. **Follow code conventions** — read `CLAUDE.md` / `AGENTS.md` / `docs/code-rules.md`
7. **Use `Result<T, Error>`** — do not throw exceptions for business logic
8. **Flat namespaces** — do not add sub-folders to namespaces

### Template for Each Patch

```
🔒 PATCH: [Vulnerability Type]
📁 File: src/MarketNest.Xxx/path/to/file.cs:line
🔴 Severity: CRITICAL/HIGH/MEDIUM/LOW (CVSS X.X)
🪲 CWE: CWE-XXX

BEFORE (Vulnerable):
```csharp
// vulnerable code
```

AFTER (Patched):
```csharp
// patched code
```

✅ Reason: Brief explanation
📚 Reference: https://owasp.org/...
```

---

## Phase 5: VERIFY — Post-Patch Validation

```powershell
# Build project
dotnet build

# Run tests
dotnet test

# Check compile/lint errors
dotnet build --no-restore 2>&1 | Select-String -Pattern 'error|warning'

# Re-run grep checks (copy from Phase 2)
# ... grep commands corresponding to patched vulnerabilities ...

# Dependency audit
dotnet list package --vulnerable
Push-Location src/MarketNest.Web ; npm audit ; Pop-Location
```

**Post-patch report**:
- ✅/❌ Build succeeded
- ✅/❌ All tests passed
- Vulnerabilities patched: X/Y
- ⚠️ Any regressions introduced

---

## Reference Table

| Vulnerability | CWE | OWASP |
|---|---|---|
| SQL Injection | CWE-89 | A03:2021 — Injection |
| XSS | CWE-79 | A03:2021 — Injection |
| CSRF | CWE-352 | A01:2021 — Broken Access Control |
| Broken Auth | CWE-287, CWE-384 | A07:2021 — Identification & Auth Failures |
| IDOR | CWE-639 | A01:2021 — Broken Access Control |
| Access Control | CWE-284, CWE-285 | A01:2021 — Broken Access Control |
| Mass Assignment | CWE-915 | A04:2021 — Insecure Design |
| Race Condition | CWE-362 | A04:2021 — Insecure Design |
| Secrets Exposure | CWE-200, CWE-798 | A02:2021 — Cryptographic Failures |
| Security Headers | CWE-693, CWE-319 | A05:2021 — Security Misconfiguration |
| File Upload | CWE-434 | A04:2021 — Insecure Design |
| Dependency Vuln | CWE-1104 | A06:2021 — Vulnerable Components |
| Backdoor | CWE-506 | A08:2021 — Software & Data Integrity |

---

## Important Notes

- **Never log sensitive data** (password, token, PII) to console/file
- **Defense in depth**: One security layer is not enough — use multiple layers
- **Principle of Least Privilege**: Grant only the minimum permissions necessary
- **Fail securely**: On error, default to a safe state (deny all)
- **All changes must go through code review** before production
- **Audit log** all important actions (login, permission changes, data deletion)
- **Log bugs/decisions** in `docs/project_notes/` if important issues are discovered
- **Follow project conventions**: Read `CLAUDE.md`, `AGENTS.md`, `docs/code-rules.md` before editing
- **Use `Result<T, Error>`**: Do not throw exceptions for business failures
