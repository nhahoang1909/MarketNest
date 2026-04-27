# Code Style (moved)

Canonical code style and C# conventions are maintained in `docs/code-rules.md`.

This file was consolidated into `agents/GUIDELINES.md` and the authoritative, detailed rules live in `docs/code-rules.md`.

Original extended content preserved under `agents/rules/archive/codestyle.md`.

Agent enforcement note:

- Agents and automated reviewers MUST enforce the CancellationToken policy documented in `docs/code-rules.md` §2.9: public async/cancellable APIs, interface/abstract/base methods must include a `CancellationToken`, and handlers must accept and forward the token to downstream calls. When producing or modifying C# code, agents should add `CancellationToken` parameters to generated method signatures and ensure propagation in implementations.

