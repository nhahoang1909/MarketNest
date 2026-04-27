# ADR-020: Consolidate agent guidelines into a single canonical file

Status: Accepted
Date: 2026-04-26

Context
-------
There were multiple overlapping agent-facing rule documents in the repository: `AGENTS.md`, `CLAUDE.md`, and files under `agents/rules/`. This caused duplication and the risk of divergence between agent behaviors when different assistants (Copilot, Claude, Gemini) read different files.

Decision
--------
Create a single canonical agent guideline file at `agents/GUIDELINES.md`. Replace the long duplicated rule sections in `AGENTS.md` and `CLAUDE.md` with pointers to the canonical file. Archive the original `agents/rules/*.md` files under `agents/rules/archive/` and replace the working copies with short pointers.

Consequences
------------
- Single source of truth reduces maintenance and the risk of inconsistent agent behavior.
- Agents and humans will consult `agents/GUIDELINES.md` for agent-facing rules and `docs/*` for deep, authoritative specs (code style, architecture, testing, security).
- Original files are preserved in `agents/rules/archive/` for auditing and rollback.

Implementation
--------------
Files changed:
- Added: `agents/GUIDELINES.md`
- Added: `agents/rules/archive/*` (original rule files)
- Updated: `agents/rules/*` (short pointers)
- Updated: `AGENTS.md` and `CLAUDE.md` (replaced agent rule blocks with pointer)
- Added: this ADR at `docs/adr/ADR-020-canonical-agent-guidelines.md`

Rollback
--------
If the consolidation causes regressions, restore original files from `agents/rules/archive/` or revert the PR that merged this change. Document the reason for rollback in this ADR.

