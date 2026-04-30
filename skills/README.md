# Skills — MarketNest AI Agent Library

This folder contains **domain-specific skill files** that guide AI agents (GitHub Copilot,
Claude Code, Gemini CLI, Cursor, Continue) through complex review and analysis tasks
on the MarketNest codebase.

Each skill lives in its own subfolder as `SKILL.md`. All skills are registered in:
- `.github/copilot-instructions.md` (GitHub Copilot)
- `AGENTS.md` (all agents)

---

## How to Use

When you ask an agent to perform a task, it will match your request to a skill,
read the SKILL.md file, and follow its structured workflow.

**You can also explicitly invoke a skill:**

> "Use the `dotnet-code-review` skill to review this handler."

> "Run the `architecture-guard` skill on the Catalog module."

> "Use `test-quality-check` to audit the Orders test suite."

---

## Skill Index

| Skill | When to Use | File |
|---|---|---|
| **dotnet-code-review** | Review C# code — naming, async, DI, Result pattern, EF Core, HTMX handlers | [`dotnet-code-review/SKILL.md`](dotnet-code-review/SKILL.md) |
| **roslyn-analyzer-review** | MN001–MN018 build errors, add new analyzer rule, write analyzer tests | [`roslyn-analyzer-review/SKILL.md`](roslyn-analyzer-review/SKILL.md) |
| **architecture-guard** | Layer boundary violations, module isolation, DDD aggregate integrity | [`architecture-guard/SKILL.md`](architecture-guard/SKILL.md) |
| **database-review** | EF Core migrations, N+1 queries, PostgreSQL indexes, Redis TTL, schema isolation | [`database-review/SKILL.md`](database-review/SKILL.md) |
| **security-checks** | Security audit — SQL injection, XSS, IDOR, race conditions, OWASP Top 10 | [`security-checks/SKILL.md`](security-checks/SKILL.md) |
| **performance-optimizer** | Slow queries, bottleneck analysis, EF Core / Redis / frontend optimization | [`performance-optimizer/SKILL.md`](performance-optimizer/SKILL.md) |
| **test-quality-check** | xUnit/FluentAssertions/NSubstitute convention, Testcontainers, NetArchTest coverage | [`test-quality-check/SKILL.md`](test-quality-check/SKILL.md) |
| **api-contract-review** | HTTP status codes, Problem Details RFC 7807, HTMX patterns, rate limits | [`api-contract-review/SKILL.md`](api-contract-review/SKILL.md) |
| **domain-model-review** | DDD aggregates, value objects, state machines, invariants, anemic model detection | [`domain-model-review/SKILL.md`](domain-model-review/SKILL.md) |
| **frontend-code-review** | CSS/HTML/JS quality, accessibility, Web Vitals, Alpine.js, Tailwind CSS | [`frontend-code-review/SKILL.md`](frontend-code-review/SKILL.md) |
| **frontend-htmx-review** | HTMX attributes, hx-swap/hx-trigger, partial responses, Alpine.js patterns | [`frontend-htmx-review/SKILL.md`](frontend-htmx-review/SKILL.md) |

---

## Skill Workflow (all skills follow this pattern)

```
Phase 1: SCAN    → Collect relevant files using PowerShell/search tools
Phase 2: ANALYZE → Check against rule groups with concrete examples
Phase 3: REPORT  → Classify findings: BLOCKER / HIGH / MEDIUM / SUGGESTION
Phase 4: FIX     → Provide before/after code fixes (confirm before applying)
Phase 5: VERIFY  → Run tests / re-scan to confirm fixes
```

---

## Adding a New Skill

1. Create folder `skills/<skill-name>/`
2. Create `skills/<skill-name>/SKILL.md` with the frontmatter:
   ```yaml
   ---
   name: skill-name
   description: >
     One paragraph describing EXACTLY when this skill should be activated.
     Include trigger phrases (English only).
   compatibility:
     tools: [read_file, write_file, grep_search, run_in_terminal]
     agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
     stack: [relevant stack items]
   ---
   ```
3. Add a row to the table above in `skills/README.md`
4. Add a row to the Skill Library table in `.github/copilot-instructions.md`
5. Add the same row to `AGENTS.md`

