## Project Notes — Usage

This folder contains short, scannable project memory files used by developers and automated agents:

- `bugs.md` — bug log with root cause, solution, and prevention
- `decisions.md` — Architectural Decision Records (ADRs)
- `issues.md` — concise work log of completed/in-progress items
- `key_facts.md` — non-sensitive project facts (ports, URLs)

Guidelines
- Each entry must be scannable in ~30 seconds and follow the file's header format.
- Date entries as `YYYY-MM-DD` and include a one-line description, status, and optional PR/issue link.
- When an agent or developer makes a code/doc change, add a short entry to the appropriate file describing the change and linking to the PR/commit.

Logging helper
- A simple PowerShell helper `scripts/log_project_memory.ps1` is provided to append entries to `issues.md`.
- Usage example (PowerShell):

```powershell
# Append an issues.md entry interactively
.
\scripts\log_project_memory.ps1 -Title "2026-04-26 - Fix auth token bug" -Status "Completed" -Description "Fixed token refresh logic in Identity module. Added unit tests." -Pr "#123"
```

Agent behavior
- The assistant will offer to append a short project note when it modifies code or documentation. If you prefer fully automatic commit-time logging, we can add a Git hook or CI step — ask and I will implement it.

If you want a different format or stricter automation rules (e.g., only log entries for PR merges), tell me and I'll modify the helper and README accordingly.

