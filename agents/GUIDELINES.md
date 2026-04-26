# MarketNest — Agent Guidelines (canonical)

Purpose
-------
This is the canonical, single source of truth for agent-facing guidelines used by machine and human assistants working on the MarketNest repository. It is intentionally concise and points to deeper, authoritative `docs/` pages for full specifications (code style, architecture, testing, security).

TL;DR (quick checklist)
-----------------------
- Read `docs/code-rules.md` before creating or modifying C# files.
- Use the `Result<T, Error>` pattern for business failures; do not throw for domain errors.
- Keep namespaces flat at the layer level (`MarketNest.<Module>.Application`, `.Domain`, `.Infrastructure`).
- Always preserve English-only source code and messages.
- Do minimal, surgical changes; state assumptions and provide a short plan before coding.

Agent Behavior (summary)
------------------------
- Think Before Coding: state assumptions, present multiple options when ambiguous, ask clarifying questions.
- Simplicity First: implement the minimal solution that satisfies the request; avoid speculative features.
- Surgical Changes: edit only what is necessary; do not refactor unrelated code.
- Goal-Driven Execution: transform tasks into verifiable goals and include verification steps.

Where to find authoritative rules
---------------------------------
- Code style and C# conventions: `docs/code-rules.md` (full, authoritative)
- Architecture, module boundaries, and ADRs: `docs/architecture.md` and `docs/project_notes/decisions.md`
- Testing rules and PR gate checklist: `docs/backend-patterns.md` and `docs/devops-requirements.md`
- Security rules: `docs/backend-infrastructure.md`

How agents should use this file
------------------------------
- Treat this file as the canonical agent-facing summary. When a deep rule is required (e.g., exact naming, error codes), follow the referenced `docs/*` file.
- When generating or editing C# files, enforce the flat namespace policy from `docs/code-rules.md` §2.7.
- Preserve project-specific content in `AGENTS.md` and `CLAUDE.md` (build/run commands, project overview). Replace long duplicated rule text with a pointer to this file.

Change log
----------
- 2026-04-26 — Created canonical agent guidelines and pointed `AGENTS.md` / `CLAUDE.md` and `agents/rules/*` to this file. See docs/adr/ADR-020-canonical-agent-guidelines.md for the ADR.

Sources & archives
------------------
Original rule files preserved under `agents/rules/archive/`.

Phase-branch PR rule
--------------------
Agents must open pull requests to phased feature branches (pattern `p*-main`, e.g. `p1-main`) instead of creating PRs directly against `main`. This enables the maintainers to control phased rollout and verification before merging into `main`.

Guidance:
- Create a short-lived feature branch from the current branch (example: `chore/add-phase-pr-rule`) and commit your changes.
- Push your branch and open a PR with target branch `p*-main` (e.g., `p1-main`) — do not open the PR directly to `main`.
- PR title should follow Conventional Commits style: `chore(rules): add phase-branch PR rule`.
- In the PR description reference the ADR if applicable (e.g., `ADR-020`) and include a short verification checklist (tests, architecture checks).

If you are uncertain which `p*` branch to target, ask a maintainer. The maintainer responsible for the phase will merge `p*-main` into `main` after verification.

