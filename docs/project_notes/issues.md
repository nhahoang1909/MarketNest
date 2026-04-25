# Work Log

Quick-reference log of completed and in-progress work. Full details live in GitHub issues/PRs.
Each entry should be **scannable in 30 seconds** — link to PRs for full context.

**Archiving policy**: Move entries older than 3 months to `issues-archive-YYYY.md`.
Keep a reference: _"See `issues-archive-2026.md` for older entries."_

**When this file exceeds ~20 entries**: Add a Table of Contents at the top.

## Format

### YYYY-MM-DD - Brief Description
- **Status**: Completed / In Progress / Blocked
- **Description**: 1-2 line summary
- **PR/Issue**: Link if available
- **Notes**: Any important context

---

## Entries

### 2026-04-25 - PR #2: Database initializer foundation
- **Status**: Completed (merged to main)
- **Description**: Added `DatabaseInitializer`, `DatabaseTracker`, `IModuleDbContext`, `ModelHasher`, and `DatabaseServiceExtensions` to bootstrap EF Core per-module migrations on startup
- **PR**: merged via `feature/matthew` → main
- **Notes**: Auto-migration on startup approach — no manual `dotnet ef database update` needed in dev

### 2026-04-25 - PR #1: Frontend base layouts redesign
- **Status**: Completed (merged to main)
- **Description**: Redesigned frontend base layouts with two distinct aesthetics (buyer-facing and seller/admin dashboards)
- **PR**: merged via worktree branch → main
- **Notes**: Seller layout (`_LayoutSeller.cshtml`) and buyer layout are now separated

### 2026-04-25 - feature/foundation: Core infrastructure wiring
- **Status**: In Progress
- **Description**: Wiring up `AssemblyReference`, logging infrastructure, `productForm.js`, and lib assets on the foundation branch
- **Notes**: Branch has several uncommitted modifications — see git status
