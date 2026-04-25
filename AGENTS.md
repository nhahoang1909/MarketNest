# AGENTS.md

Universal agent instructions for all AI coding assistants working on this repository.
This file is the multi-agent equivalent of `CLAUDE.md` — it applies to Gemini, Codex, Copilot, and any future AI tools.

## Project Memory System

This project maintains institutional knowledge in `docs/project_notes/`. **Read these files before making changes.**

| File | Purpose | Route here when... |
|------|---------|-------------------|
| `docs/project_notes/bugs.md` | Bug log (issue → root cause → solution → prevention) | Fixing a bug or encountering an error |
| `docs/project_notes/decisions.md` | Architectural Decision Records (ADRs) | Proposing or reviewing architectural changes |
| `docs/project_notes/key_facts.md` | Non-sensitive config: ports, URLs, namespaces | Looking up project configuration |
| `docs/project_notes/issues.md` | Work log with PR references | Completing a feature or PR |

### Protocols

**Before proposing architectural changes:**
- Check `docs/project_notes/decisions.md` for existing decisions
- Verify the proposed approach doesn't conflict with past choices
- If it conflicts, acknowledge the prior ADR and explain why revisiting is warranted

**When encountering errors or bugs:**
- Search `docs/project_notes/bugs.md` for similar issues
- Apply known solutions if found
- Document new bugs and solutions when resolved

**When looking up project configuration:**
- Check `docs/project_notes/key_facts.md` for ports, URLs, Redis namespaces
- Prefer documented facts over assumptions

**When completing work:**
- Log in `docs/project_notes/issues.md` with date, description, and PR link

### Secrets Policy

**NEVER** store passwords, API keys, tokens, or connection strings with credentials in any markdown file or version-controlled config.

Secrets belong in:
- `.env` files (gitignored) — local development
- Cloud secrets managers — production (GCP Secret Manager, AWS Secrets Manager, Azure Key Vault)
- CI/CD variables — pipelines (GitHub Actions secrets)
- Kubernetes Secrets — containerized deployments

## Key Conventions

- See `docs/code-rules.md` for full coding standards
- Use `Result<T, Error>` — never throw for business failures
- No magic strings / magic numbers — extract to `const`, `static readonly`, enum, or config options
- Module boundaries: no cross-schema DB access; use service interfaces or domain events
- CQRS naming: `PlaceOrderCommand`, `GetOrderByIdQuery`, `OrderPlacedEvent`

## Specification Documents

All located in `docs/` — read before implementing any feature. See `CLAUDE.md` for the full table.

