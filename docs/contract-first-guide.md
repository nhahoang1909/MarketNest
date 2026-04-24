# MarketNest — Contract-First Development Guide

> Version: 0.1 | Status: Draft | Date: 2026-04  
> **The golden rule: define the contract (interface) first. Implementation comes second.**  
> Every module communicates through contracts. No module knows another module's internals.

---

## 1. Why Contract-First?

```
Without contracts                 With contracts
─────────────────                 ──────────────
OrderHandler calls                OrderHandler calls
  EmailService.SendAsync()          INotificationService.NotifyAsync()
  ↓                                 ↓
Hard dependency on                Notification module could be:
  EmailService impl.                - SMTP (Phase 1)
Swapping = refactoring             - RabbitMQ event (Phase 3)
  all callers                       - SMS, Push, Webhook
                                  Zero changes to OrderHandler
```

---

## 2. Core Contracts (Shared Kernel)

These interfaces live in `MarketNest.Core` — every module can reference them.

### 2.1 CQRS Marker Interfaces

```csharp
// Core/Common/Cqrs/ICommand.cs
public interface ICommand<TResult> : IRequest<Result<TResult, Error>> { }
public interface ICommand : ICommand<Unit> { }

// Core/Common/Cqrs/IQuery.cs
public interface IQuery<TResult> : IRequest<TResult> { }

// Core/Common/Cqrs/ICommandHandler.cs
public interface ICommandHandler<TCommand, TResult>
    : IRequestHandler<TCommand, Result<TResult, Error>>
    where TCommand : ICommand<TResult> { }

// Core/Common/Cqrs/IQueryHandler.cs
public interface IQueryHandler<TQuery, TResult>
    : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult> { }

// Core/Common/Events/IDomainEvent.cs
public interface IDomainEvent : INotification
{
    Guid   EventId    => Guid.NewGuid();
    DateTime OccurredAt => DateTime.UtcNow;
}

// Core/Common/Events/IDomainEventHandler.cs
public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent { }
```

### 2.2 Common Result Types

```csharp
// Core/Common/Result.cs — the ONLY way to return errors from handlers
public class Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public TValue Value   => IsSuccess ? _value! : throw new InvalidOperationException("Result has no value");
    public TError Error   => IsFailure ? _error! : throw new InvalidOperationException("Result has no error");

    protected Result(TValue value)  { _value = value; IsSuccess = true; }
    protected Result(TError error)  { _error = error; IsSuccess = false; }

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);

    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    public Result<TNew, TError> Map<TNew>(Func<TValue, TNew> mapper)
        => IsSuccess
            ? Result<TNew, TError>.Success(mapper(_value!))
            : Result<TNew, TError>.Failure(_error!);

    public async Task<Result<TNew, TError>> MapAsync<TNew>(Func<TValue, Task<TNew>> mapper)
        => IsSuccess
            ? Result<TNew, TError>.Success(await mapper(_value!))
            : Result<TNew, TError>.Failure(_error!);
}

// Convenience static class
public static class Result
{
    public static Result<TValue, Error> Success<TValue>(TValue value)
        => Result<TValue, Error>.Success(value);

    public static Result<TValue, Error> Failure<TValue>(Error error)
        => Result<TValue, Error>.Failure(error);

    public static Result<Unit, Error> Success() => Success(Unit.Value);
}

// Error record
public record Error(string Code, string Message, ErrorType Type = ErrorType.Validation)
{
    public static Error NotFound(string entity, string id)
        => new($"{entity.ToUpper()}.NOT_FOUND", $"{entity} '{id}' not found", ErrorType.NotFound);

    public static Error Unauthorized(string? detail = null)
        => new("UNAUTHORIZED", detail ?? "Authentication required", ErrorType.Unauthorized);

    public static Error Forbidden(string? detail = null)
        => new("FORBIDDEN", detail ?? "Insufficient permissions", ErrorType.Forbidden);

    public static Error Conflict(string code, string message)
        => new(code, message, ErrorType.Conflict);

    public static Error Unexpected(string? detail = null)
        => new("UNEXPECTED_ERROR", detail ?? "An unexpected error occurred", ErrorType.Unexpected);
}

public enum ErrorType { Validation, NotFound, Conflict, Unauthorized, Forbidden, Unexpected }
```

---

## 3. Module-to-Module Contracts

Modules NEVER reference each other's concrete classes. They reference contracts only.

### 3.1 How Cross-Module Communication Works

```
❌ WRONG: Cart module directly calls OrderService
  namespace MarketNest.Cart.Application
  {
      using MarketNest.Orders.Application;  // ← cross-module concrete reference
      
      class CheckoutHandler(OrderService orders) { }
  }

✅ CORRECT: Cart module raises event; Orders module handles it
  namespace MarketNest.Cart.Application
  {
      class CheckoutHandler(IDomainEventPublisher publisher)
      {
          cart.Checkout();  // raises CartCheckedOutEvent
          await publisher.PublishAsync(new CartCheckedOutEvent(...));
      }
  }
  
  namespace MarketNest.Orders.Application
  {
      class CartCheckedOutEventHandler : IDomainEventHandler<CartCheckedOutEvent>
      {
          // Create order in response to cart checkout
      }
  }

✅ ALSO OK: Use an interface defined in Core, implemented in Orders
  namespace MarketNest.Core.Contracts
  {
      public interface IOrderCreationService
      {
          Task<Result<Guid, Error>> CreateFromCartAsync(CartSnapshot cart, CancellationToken ct);
      }
  }
```

### 3.2 Cross-Module Service Contracts (in MarketNest.Core)

```csharp
// Core/Contracts/IOrderCreationService.cs
/// Implemented by Orders module; consumed by Cart module
public interface IOrderCreationService
{
    Task<Result<Guid, Error>> CreateFromCartAsync(
        Guid buyerId,
        CartSnapshot cart,
        Address shippingAddress,
        CancellationToken ct = default);
}

// Core/Contracts/IInventoryService.cs
/// Implemented by Catalog module; consumed by Orders, Cart modules
public interface IInventoryService
{
    Task<bool> HasStockAsync(Guid variantId, int quantity, CancellationToken ct = default);
    Task<Result<Unit, Error>> ReserveAsync(Guid variantId, int quantity, Guid cartId, CancellationToken ct = default);
    Task ReleaseAsync(Guid variantId, int quantity, CancellationToken ct = default);
    Task CommitAsync(Guid variantId, int quantity, CancellationToken ct = default); // on order completion
}

// Core/Contracts/IPaymentService.cs
/// Implemented by Payments module; consumed by Orders module
public interface IPaymentService
{
    Task<Result<Guid, Error>> CaptureAsync(
        Guid orderId,
        Money amount,
        string paymentMethod,
        CancellationToken ct = default);

    Task<Result<Unit, Error>> RefundAsync(
        Guid paymentId,
        Money amount,
        string reason,
        CancellationToken ct = default);
}

// Core/Contracts/INotificationService.cs
/// Implemented by Notifications module; consumed by all modules via domain events
public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplatedEmailAsync(string to, string templateName, object model, CancellationToken ct = default);
}

// Core/Contracts/IStorefrontReadService.cs
/// Implemented by Catalog module; consumed by Payments module for commission rates
public interface IStorefrontReadService
{
    Task<decimal> GetCommissionRateAsync(Guid storeId, CancellationToken ct = default);
    Task<StorefrontInfo?> GetBySlugAsync(string slug, CancellationToken ct = default);
}
```

---

## 4. Data Transfer Object (DTO) Contracts

### 4.1 DTO Rules

```
✅ DTOs are records (immutable)
✅ DTOs live in the Application layer of the module that PRODUCES them
✅ Consumers depend on the DTO, not the domain entity
✅ Separate: Query DTOs (read) vs Command DTOs (write inputs)
❌ Never expose domain entities outside the aggregate boundary
❌ Never reuse the same DTO for both create and update forms
```

### 4.2 Naming Convention

```csharp
// Query results: {Entity}Dto, {Entity}ListItemDto, {Entity}DetailDto
public record ProductDto(Guid Id, string Title, decimal Price, string StoreName);
public record ProductListItemDto(...);  // lighter — for grid/list views
public record ProductDetailDto(...);    // heavier — for detail/edit views

// Command inputs: {Action}{Entity}Request or embedded in the Command
public record CreateProductCommand(string Title, string Description, Guid StoreId, ...) : ICommand<Guid>;
public record UpdateProductCommand(Guid ProductId, string Title, string Description, ...) : ICommand<Unit>;

// Shared snapshots (passed between modules, serializable)
public record CartSnapshot(Guid BuyerId, IReadOnlyList<CartItemSnapshot> Items, Address ShippingAddress);
public record CartItemSnapshot(Guid VariantId, Guid StoreId, string Title, Money UnitPrice, int Quantity);
```

---

## 5. Validation Contract

Every Command has a paired Validator. No exceptions.

```csharp
// Convention: {CommandName}Validator in the same folder as the command
// Registered automatically by FluentValidation DI scan

// Core/Common/Validation/ValidatorExtensions.cs
public static class ValidatorExtensions
{
    // Common reusable rules
    public static IRuleBuilderOptions<T, string> MustBeSlug<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .Matches(@"^[a-z0-9-]{3,50}$")
            .WithMessage("Must be 3-50 lowercase letters, numbers, or hyphens");

    public static IRuleBuilderOptions<T, decimal> MustBePositiveMoney<T>(this IRuleBuilder<T, decimal> rule)
        => rule
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0")
            .LessThanOrEqualTo(999_999.99m)
            .WithMessage("Amount exceeds maximum allowed value");

    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

    public static IRuleBuilderOptions<T, Guid> MustBeValidId<T>(this IRuleBuilder<T, Guid> rule)
        => rule
            .NotEqual(Guid.Empty)
            .WithMessage("ID cannot be empty");

    public static IRuleBuilderOptions<T, int> MustBeValidQuantity<T>(this IRuleBuilder<T, int> rule)
        => rule
            .InclusiveBetween(1, 99)
            .WithMessage("Quantity must be between 1 and 99");
}
```

```csharp
// Example: CreateProductCommandValidator.cs
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.StoreId).MustBeValidId();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(5000);
        RuleFor(x => x.Category).NotEmpty();
        RuleFor(x => x.Tags).Must(t => t.Count <= 10).WithMessage("Maximum 10 tags allowed");

        RuleFor(x => x.Variants).NotEmpty().WithMessage("At least one variant required");
        RuleForEach(x => x.Variants).SetValidator(new CreateVariantDtoValidator());
    }
}

public class CreateVariantDtoValidator : AbstractValidator<CreateVariantDto>
{
    public CreateVariantDtoValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Price).MustBePositiveMoney();
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CompareAtPrice)
            .GreaterThan(x => x.Price)
            .When(x => x.CompareAtPrice.HasValue)
            .WithMessage("Compare-at price must be greater than price");
    }
}
```

---

## 6. Page Handler Contract (Razor Pages)

Every page follows this response contract:

```csharp
// Web/Infrastructure/PageHandlerExtensions.cs
/// Extension methods for consistent page handler responses
public static class PageHandlerExtensions
{
    /// Render partial if HTMX request, otherwise return full page
    public static IActionResult Page(this PageModel page, bool isHtmx, string partialName, object? model = null)
        => isHtmx
            ? page.Partial(partialName, model)
            : page.Page();

    /// Handle Result<T, Error> and return appropriate page response
    public static IActionResult HandleResult<T>(
        this PageModel page,
        Result<T, Error> result,
        Func<T, IActionResult> onSuccess,
        string? errorProperty = null)
        => result.Match(
            onSuccess,
            error => {
                page.ModelState.AddModelError(errorProperty ?? "", error.Message);
                return page.Page();
            });

    /// Handle Result and redirect on success
    public static IActionResult RedirectOnSuccess<T>(
        this PageModel page,
        Result<T, Error> result,
        string redirectPage,
        object? routeValues = null)
        => result.Match(
            _ => page.RedirectToPage(redirectPage, routeValues),
            error => {
                page.ModelState.AddModelError("", error.Message);
                return page.Page();
            });
}
```

---

## 7. Full Module Contract Checklist

When creating a new module, define ALL of these before writing implementation:

```
Module: _______________

DOMAIN CONTRACTS
  [ ] Entity/Aggregate defined (name, key properties, status enum)
  [ ] Value Objects identified
  [ ] Domain Events listed (past tense, what triggers them)
  [ ] Business invariants documented

APPLICATION CONTRACTS  
  [ ] Commands listed (one per use case / user action)
  [ ] Queries listed (one per screen / data need)
  [ ] Each Command has a paired Validator class stub
  [ ] DTOs defined for each Query response
  [ ] Cross-module dependencies: which ICore contracts does this module consume?
  [ ] Cross-module exposure: which contracts does this module implement (for Core)?

INFRASTRUCTURE CONTRACTS
  [ ] Repository interface defined (IXxxRepository)
  [ ] EF Core entity configuration planned (table name, schema, indexes)
  [ ] Any Redis keys documented in CacheKeys.cs
  [ ] Background jobs identified

WEB CONTRACTS
  [ ] Pages/routes listed
  [ ] HTMX partials identified per page
  [ ] Form models defined (separate from domain commands)
```

---

## 8. Example: Full Contract Definition Before Coding

**New feature: "Seller Promotions" (discount codes)**

```csharp
// ─── DOMAIN ───────────────────────────────────────────
public record PromotionCode // Value Object
{
    public string Code { get; }
    public PromotionCode(string code) { /* validation */ }
}

public class Promotion : AggregateRoot<Guid>
{
    public Guid      StoreId       { get; private set; }
    public string    Code          { get; private set; }
    public decimal   DiscountPct   { get; private set; }
    public int       UsageLimit    { get; private set; }
    public int       UsageCount    { get; private set; }
    public DateTime  ExpiresAt     { get; private set; }
    public bool      IsActive      { get; private set; }

    // Will raise: PromotionCreatedEvent, PromotionExpiredEvent
}

// ─── COMMANDS ────────────────────────────────────────
public record CreatePromotionCommand(Guid StoreId, string Code, decimal DiscountPct, int UsageLimit, DateTime ExpiresAt) : ICommand<Guid>;
public record DeactivatePromotionCommand(Guid PromotionId, Guid RequestingUserId) : ICommand<Unit>;
public record ApplyPromotionCommand(Guid CartId, string Code, Guid BuyerId) : ICommand<ApplyPromotionResult>;

// ─── QUERIES ─────────────────────────────────────────
public record GetPromoListQuery : PagedQuery { public Guid StoreId { get; init; } }
public record ValidatePromotionQuery(string Code, Guid StoreId, Guid BuyerId) : IQuery<PromotionValidationResult>;

// ─── DTOs ────────────────────────────────────────────
public record PromoListItemDto(Guid Id, string Code, decimal DiscountPct, int UsageCount, int UsageLimit, DateTime ExpiresAt, bool IsActive);
public record ApplyPromotionResult(decimal DiscountAmount, string DisplayText);
public record PromotionValidationResult(bool IsValid, string? ErrorReason, decimal? DiscountPct);

// ─── CROSS-MODULE CONTRACT ────────────────────────────
// Core/Contracts/IPromotionService.cs
public interface IPromotionService
{
    Task<PromotionValidationResult> ValidateAsync(string code, Guid storeId, Guid buyerId, CancellationToken ct);
    Task IncrementUsageAsync(string code, Guid storeId, CancellationToken ct);
}

// ─── REPOSITORY ──────────────────────────────────────
public interface IPromotionRepository : IBaseRepository<Promotion, Guid>
{
    Task<Promotion?> GetByCodeAsync(string code, Guid storeId, CancellationToken ct);
}

// ─── PAGES (stubs only) ───────────────────────────────
// /seller/promotions         → List page
// /seller/promotions/new     → Create form
// /seller/promotions/{id}    → Edit / deactivate
```

Everything above is defined BEFORE writing a single line of implementation.  
This is the document you share with a teammate or review yourself the next morning.
