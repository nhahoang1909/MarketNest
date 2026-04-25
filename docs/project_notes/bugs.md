# Bug Log

Keep entries brief and chronological. Each entry should be **scannable in 30 seconds**.
If more detail is needed, link to a separate document or GitHub issue.

**Archiving policy**: Move entries older than 6–12 months to `bugs-archive-YYYY.md`.
Keep a reference: _"See `bugs-archive-2026.md` for older entries."_

**When this file exceeds ~20 entries**: Add a Table of Contents at the top.

## Format

### YYYY-MM-DD - Brief Description
- **Issue**: What went wrong
- **Root Cause**: Why it happened
- **Solution**: How it was fixed
- **Prevention**: How to avoid it in the future

---

## Entries

### 2026-04-25 - htmxHelpers.js redirect to wrong login URL
- **Issue**: On 401 responses, users were redirected to `/account/login` (non-existent) instead of `/auth/login`
- **Root Cause**: Hardcoded wrong URL string in `htmxHelpers.js` error handler
- **Solution**: Replaced with `Routes.AUTH_LOGIN` constant from `constants.js` pointing to `/auth/login`
- **Prevention**: All route URLs must use `AppRoutes` (C#) or `Routes` (JS) constants — no hardcoded paths

### 2026-04-25 - BadHttpRequestException on language switch: "string culture" not provided from query string
- **Issue**: Clicking the language switcher button crashed the app with `BadHttpRequestException: Required parameter "string culture" was not provided from query string` at `/api/set-language`
- **Root Cause**: The `MapPost` minimal API endpoint bound `culture` and `returnUrl` from query string (default), but the `<form method="post">` in `_Layout.cshtml` sends them as form body fields
- **Solution**: Added `[FromForm]` attribute to both `culture` and `returnUrl` parameters in the `MapPost` lambda
- **Prevention**: When mapping POST endpoints that receive HTML form submissions, always use `[FromForm]` for parameter binding in minimal APIs

_No other bugs logged yet. Add entries as issues are encountered and resolved._

### 2026-04-25 - Connection string key mismatch between code and appsettings.json
- **Issue**: `AppConstants.DefaultConnectionStringName` was `"DefaultConnection"` but `appsettings.json` had key `"Default"` — `GetConnectionString()` would return `null` at runtime
- **Root Cause**: Key name in `appsettings.json` wasn't updated when the constant was created during the magic-string elimination sweep
- **Solution**: Renamed `appsettings.json` key from `"Default"` to `"DefaultConnection"` to match the code constant
- **Prevention**: Always verify config key constants match their corresponding `appsettings.json` keys. Add integration test for `IConfiguration.GetConnectionString()` resolution.

### 2026-04-25 - Docker env var CONNECTION_STRINGS__DEFAULT didn't override appsettings ConnectionStrings:DefaultConnection
- **Issue**: `.env` used `CONNECTION_STRINGS__DEFAULT` but `appsettings.json` key is `ConnectionStrings:DefaultConnection` — ASP.NET env var override requires `ConnectionStrings__DefaultConnection`
- **Root Cause**: Mismatch between `.env` variable naming and ASP.NET Configuration's `__` separator convention (`ConnectionStrings__DefaultConnection` maps to `ConnectionStrings:DefaultConnection`)
- **Solution**: Renamed env var to `CONNECTION_STRINGS__DEFAULTCONNECTION` in `.env`, `.env.example`, and `docker-compose.yml`
- **Prevention**: When adding env var overrides, always verify the full key path matches the `appsettings.json` structure exactly (section__key convention)

### 2026-04-25 - Layout view '/Shared/_Layout' could not be located (app crash + stack trace in browser)
- **Issue**: All pages crashed with `InvalidOperationException: The layout view '/Shared/_Layout' could not be located`. The Error page itself also crashed (cascading failure), exposing raw stack traces to users.
- **Root Cause**: Layout paths like `/Shared/_Layout` start with `/`, making them **absolute** — the Razor engine bypasses `PageViewLocationFormats` expansion and looks for the file literally. Since layouts live at project-root `/Shared/` (not `Pages/Shared/`), the engine can't find them. The Error page inherited this broken layout from `_ViewStart.cshtml`, causing a second failure inside the exception handler.
- **Solution**: (1) Changed all layout references from absolute `/Shared/_LayoutXxx` to relative `_LayoutXxx` so the custom `PageViewLocationFormats` entry `/Shared/{0}.cshtml` can resolve them. (2) Made `Error.cshtml` self-contained (`Layout = null`) with its own HTML structure to prevent cascading layout failures.
- **Prevention**: Never use absolute paths (starting with `/`) for layout references when layouts live outside `Pages/`. Use relative names and rely on `PageViewLocationFormats` for resolution. Error pages should always be self-contained (no external layout dependency).

### 2026-04-25 - Localization keys shown as raw text ("Nav.Home") + Vietnamese missing diacritics
- **Issue**: Navigation displayed raw localization keys (e.g., "Nav.Home", "Nav.Search.Placeholder") instead of translated text. Additionally, all hardcoded Vietnamese text on `Index.cshtml` was missing diacritical marks (e.g., "Kham pha" instead of "Khám phá").
- **Root Cause**: Two separate issues: (1) `SharedResource` class was in `MarketNest.Web.Infrastructure` namespace, but resource files lived at `Resources/SharedResource.{culture}.resx`. ASP.NET Core localization with `ResourcesPath = "Resources"` expects files at `Resources/Infrastructure/SharedResource.{culture}.resx` for a class in a sub-namespace — so it couldn't find them and returned raw keys. (2) `Index.cshtml` had Vietnamese text typed without diacritics (plain ASCII).
- **Solution**: (1) Moved `SharedResource` class to `MarketNest.Web` root namespace so localization resolves `Resources/SharedResource.{culture}.resx` correctly. Added `@using MarketNest.Web` to `_ViewImports.cshtml` files. (2) Added proper Vietnamese diacritical marks to all hardcoded text in `Index.cshtml`.
- **Prevention**: Localization marker classes must live in the project's root namespace (or resource files must be nested to match the class's sub-namespace). Verify localization works by checking both languages after adding new resource keys.
