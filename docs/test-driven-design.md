# Test-Driven Design (TDD) — Project Policy

Version: 0.1 | Status: Active | Date: 2026-04-26

Purpose
-------

This document formalizes the project's Test-Driven Design (TDD) policy. The goal is to ensure business rules are specified as automated tests before implementation, improving design quality, preventing regressions, and making behavior explicit.

Policy statement
----------------

- All new or changed business behavior MUST be accompanied by automated tests written before implementation (write the failing test first). "Business behavior" includes domain invariants, domain method logic, application command/query handlers implementing business rules, and calculations or decision logic that impacts users or data integrity.
- Unit tests are the primary vehicle for business rules. Integration tests are required for API endpoints, database interactions, and cross-module flows. Higher-level acceptance or feature tests are recommended for complex workflows.
- Tests must be committed on the feature branch before (or together with) the implementation. PRs should show that tests were authored with the intent of failing first and then the implementation was added to make them pass.

Why this rule
-------------

- Forces precise specification of behavior before implementation
- Prevents feature regressions and clarifies intent for reviewers
- Encourages modular design and better separation of concerns
- Makes future refactoring safer

Scope
-----

This policy applies to:

- Feature work that implements or changes business rules
- Bug fixes that correct business logic
- Any change to domain code, application handlers, or validation that expresses business intent

Exemptions (rare)
------------------

Small cosmetic changes (styles, text copy) and documentation-only updates do not require TDD-first tests. If an exemption is taken, document the rationale in the PR description.

Workflow (recommended)
----------------------

1. Create a short description of the business rule in the issue/PR and link to it from the test commit.
2. Add a failing test that captures the expected business behavior. Keep tests small and focused — one assertion per behavior when practical.
   - Place unit tests under: `tests/MarketNest.UnitTests/{Module}/` (follow existing project layout).
   - Name tests using the pattern: `{UnitOfWork}_{Condition}_Should_{ExpectedOutcome}` or Use the BDD style: `Given_When_Then`.
3. Run the test suite locally (or the subset) and confirm the new test fails for the right reason.
4. Implement the minimum code to make the test pass.
5. Run tests again and commit the passing test + implementation.
6. Push the branch and open a PR. In the PR description add a short note explaining the TDD steps taken (link commits if desired).

Example Git commit strategy (recommended):

- Commit A (tests): `test({module}): add failing unit test for CalculateCommission when seller fee applies`
- Commit B (impl): `feat({module}): implement commission calculation to satisfy tests`

Test types and where to place them
---------------------------------

- Unit tests (fast, isolated): `tests/MarketNest.UnitTests/{Module}/...`
  - Use xUnit + FluentAssertions. Mock dependencies (use Moq or project's test helpers).
- Integration tests (DB, HTTP): `tests/MarketNest.IntegrationTests/...`
  - Use Testcontainers for database-backed tests where appropriate.
- Architecture and layer tests: `tests/MarketNest.ArchitectureTests/...` (NetArchTest)

Test naming conventions
-----------------------

- Prefer descriptive test method names rather than numeric or terse names.
- Styles accepted:
  - Method style: `CalculateCommission_WithSellerFee_ReturnsExpectedAmount()`
  - BDD style: `GivenSellerWithFee_WhenCalculateCommission_ThenExpectedAmount()`
  - Should style: `CalculateCommission_ShouldApplySellerFee()`

Small xUnit example
-------------------

```csharp
// tests/MarketNest.UnitTests.Catalog/CommissionCalculatorTests.cs
using FluentAssertions;
using Xunit;

public class CommissionCalculatorTests
{
    [Fact]
    public void CalculateCommission_WithSellerFee_ReturnsExpectedAmount()
    {
        // Arrange
        var calculator = new CommissionCalculator(commissionRate: 0.05m);

        // Act
        var result = calculator.Calculate(100m);

        // Assert
        result.Should().Be(5m);
    }
}
```

Mocking & test helpers
----------------------

- Use the project's test helpers and factories where available to create domain objects (avoid heavy constructor setup in every test).
- For collaborators (repositories, external services), prefer lightweight mocks (Moq) for unit tests; reserve Testcontainers/in-memory DBs for integration tests.

Continuous Integration (CI)
--------------------------

- CI runs all unit and integration tests. Tests must pass before merging.
- PRs without tests for new business behavior should be flagged and may be blocked by reviewers.
- Consider incremental coverage gates for critical modules later; for now, ensure tests run reliably in CI.

PR checklist additions
---------------------

Add the following items to every PR that adds or modifies business logic:

- [ ] Tests included: unit tests exist for the new/changed business rules
- [ ] Tests demonstrate failing-first workflow where feasible (commits or PR description explain the test-first approach)
- [ ] Integration tests added/updated for API/DB changes

Review guidance for maintainers and reviewers
-------------------------------------------

- Confirm tests are meaningful and cover business behavior (not only trivial getters/setters).
- Prefer small, focused tests over large end-to-end tests for business logic.
- If a test depends on time-sensitive behavior, use deterministic time providers (IClock / IUserTimeZoneProvider) and inject a fake in tests.

Legacy code & backfilling tests
------------------------------

For existing features without tests, prioritize adding tests when you modify that area. When making a bug fix, add a regression test that would have caught the bug.

Tools & commands
----------------

Run all unit tests locally:

```powershell
dotnet test tests/MarketNest.UnitTests --no-build --verbosity minimal
```

Run all tests (unit + integration):

```powershell
dotnet test
```

Run a single test method using dotnet test filter:

```powershell
dotnet test --filter DisplayName~CalculateCommission
```

Further reading and references
------------------------------

- This policy complements the project's testing guidelines in `docs/backend-infrastructure.md` and the CI configuration in the repo root.
- If you want to propose refinements (coverage gates, stricter enforcement), open an issue referencing this doc so the team can discuss trade-offs.

Appendix: Example PR description template
---------------------------------------

```
Summary
- Implement commission calculation for seller payouts.

TDD notes
- I added `CommissionCalculatorTests.CalculateCommission_WithSellerFee_ReturnsExpectedAmount` first (commit 1) which failed.
- Implemented `CommissionCalculator` to make the test pass (commit 2).

Files changed
- src/MarketNest.Payments/...CommissionCalculator.cs
- tests/MarketNest.UnitTests.Payments/CommissionCalculatorTests.cs

How to run locally
- dotnet test tests/MarketNest.UnitTests.Payments

CI
- Unit and integration tests pass in CI

Notes
- No breaking changes
```

---

If you have questions or want a template for team automation (pre-commit hooks, CI checks), open an issue and we can add helper scripts or PR templates.

