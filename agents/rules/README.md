Agent rule files for AI assistants

Purpose
- Central location for machine-readable and human guidance files used by repository-aware agents (Claude, Gemini, Copilot skills, internal bots).

Usage
- Agents should read this folder when enforcing repository-specific code rules (architecture, codestyle, git, security, testing).
- Keep files concise and English-only. Do NOT include secrets.

Maintenance
- If you move or rename this folder, update `AGENTS.md` and `CLAUDE.md` to point to the new location.
- Use commit messages with the `chore(rules):` prefix when changing rules.

