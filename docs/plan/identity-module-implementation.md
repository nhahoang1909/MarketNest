# Identity Module — Implementation Plan (ADR-044)

> **Status**: Ready to implement  
> **Date**: 2026-05-01  
> **Design source**: `downloads/identity-rbac-solution-design.md` + ADR-044 in `docs/project_notes/decisions.md`  
> **Estimated steps**: 9 phases, implement one phase at a time

---

## Overview

The Identity module is currently a stub (`DependencyInjection.cs` returns empty). This plan walks through building it ground-up in strict dependency order:

```
Phase 1: Base.Common additions (permission enums, ICurrentUser update)
Phase 2: Domain layer (User, Role, SellerApplication aggregates)
Phase 3: Infrastructure — DbContext, EF configs, BaseRepository/BaseQuery thin wrappers
Phase 4: Infrastructure — Repositories + Queries
Phase 5: Infrastructure — PermissionResolver + JwtTokenService
Phase 6: Infrastructure — Seeders (RoleSeeder, AdminUserSeeder)
Phase 7: Application layer — Commands + Handlers
Phase 8: Application layer — Queries + Validators
Phase 9: Web layer — AccessFilter, HttpCurrentUser update, Razor Pages, Routes
```

**Convention reminders before you start:**
- Namespace: always `MarketNest.Identity.Domain`, `MarketNest.Identity.Application`, `MarketNest.Identity.Infrastructure` — never include sub-folder names
- All logging: `[LoggerMessage]` inside nested `private static partial class Log`, outer class must be `partial`
- LogEventId range: **20000–29999** (Identity block)
- DI: `AddIdentityModule()` in `Infrastructure/DependencyInjection.cs`
- No `uow.CommitAsync()` in HTTP command handlers — the `RazorPageTransactionFilter` handles it
- Background jobs calling `uow.CommitAsync()` explicitly is OK (they run outside the HTTP pipeline)

---

## Phase 1 — Base.Common: Permission Enums + ICurrentUser Update

> **No Identity module code yet. Only `Base.Common` changes.**  
> **Where**: `src/Base/MarketNest.Base.Common/`

### 1.1 Create `Authorization/` folder structure

```
src/Base/MarketNest.Base.Common/
└── Authorization/
    ├── Permissions.cs         ← all [Flags] enums
    └── PermissionModule.cs    ← module discriminator enum
```

### 1.2 Create `PermissionModule.cs`

```csharp
// namespace MarketNest.Base.Common
namespace MarketNest.Base.Common;

/// <summary>
///     Discriminator for per-module permission storage.
///     Each value maps to one row in identity.role_permissions and one JWT claim.
/// </summary>
public enum PermissionModule
{
    Order      = 1,
    Catalog    = 2,
    Storefront = 3,
    Dispute    = 4,
    Review     = 5,
    Payment    = 6,
    User       = 7,
    Config     = 8,
    Promotion  = 9,
}
```

### 1.3 Create `Permissions.cs`

Copy exactly from the solution design (ADR-044 §3.1). Each module enum uses non-overlapping bit positions:

| Enum | Bit range | Example |
|------|-----------|---------|
| `OrderPermission` | bits 0–4 | `View = 1 << 0` |
| `CatalogPermission` | bits 8–11 | `View = 1 << 8` |
| `StorefrontPermission` | bits 16–18 | `View = 1 << 16` |
| `DisputePermission` | bits 24–27 | `View = 1 << 24` |
| `ReviewPermission` | bits 32–34 | `View = 1 << 32` |
| `PaymentPermission` | bits 40–42 | `ViewPayout = 1 << 40` |
| `UserPermission` | bits 48–51 | `View = 1 << 48` |
| `ConfigPermission` | bits 56–57 | `View = 1 << 56` |
| `PromotionPermission` | bits 60–62 | `View = 1 << 60` |

> ⚠️ Use `enum : long` for all — `[Flags]` values beyond bit 31 require `long`.

### 1.4 Update `ICurrentUser.cs` in `Contracts/Contracts/`

Add to the interface:
- `IReadOnlyList<string> Roles { get; }` — all roles (multi-role)
- `bool IsAdmin { get; }` — convenience: `Roles.Contains("Administrator")`
- `bool IsSeller { get; }` — convenience: `Roles.Contains("Seller")`
- `bool IsBuyer { get; }` — convenience: `Roles.Contains("Buyer")`
- `Guid? SellerStoreId { get; }` — storefront ID from `mn.store` claim, null if not seller
- One `HasPermission(TPermission required)` overload per enum (9 total)

```csharp
// Add to ICurrentUser interface:
IReadOnlyList<string> Roles { get; }
bool IsAdmin    => Roles.Contains(IdentityConstants.Roles.Administrator);
bool IsSeller   => Roles.Contains(IdentityConstants.Roles.Seller);
bool IsBuyer    => Roles.Contains(IdentityConstants.Roles.Buyer);
Guid? SellerStoreId { get; }

bool HasPermission(OrderPermission required);
bool HasPermission(CatalogPermission required);
bool HasPermission(StorefrontPermission required);
bool HasPermission(DisputePermission required);
bool HasPermission(ReviewPermission required);
bool HasPermission(PaymentPermission required);
bool HasPermission(UserPermission required);
bool HasPermission(ConfigPermission required);
bool HasPermission(PromotionPermission required);
```

> The default implementations (`IsAdmin`, `IsSeller`, `IsBuyer`) can be written as interface default methods. They use constants from `IdentityConstants` (see §1.5).

### 1.5 Create `IdentityConstants.cs` in `Base.Common`

```csharp
namespace MarketNest.Base.Common;

/// <summary>Well-known role names and claim type prefixes shared by all modules.</summary>
public static class IdentityConstants
{
    public static class Roles
    {
        public const string SystemAdmin    = "SystemAdmin";
        public const string Administrator  = "Administrator";
        public const string Seller         = "Seller";
        public const string Buyer          = "Buyer";
    }

    public static class ClaimTypes
    {
        public const string SellerStoreId   = "mn.store";
        public const string PermissionPrefix = "mn.perm.";

        // Per-module claim keys
        public const string PermOrder      = "mn.perm.order";
        public const string PermCatalog    = "mn.perm.catalog";
        public const string PermStorefront = "mn.perm.storefront";
        public const string PermDispute    = "mn.perm.dispute";
        public const string PermReview     = "mn.perm.review";
        public const string PermPayment    = "mn.perm.payment";
        public const string PermUser       = "mn.perm.user";
        public const string PermConfig     = "mn.perm.config";
        public const string PermPromotion  = "mn.perm.promotion";
    }

    /// <summary>Well-known built-in user IDs (seeded, never change between environments).</summary>
    public static class WellKnownIds
    {
        /// <summary>
        ///     System Administrator — used by background jobs, migrations.
        ///     Appears in audit trail as "Modified by: System Administrator".
        ///     Never a real human login.
        /// </summary>
        public static readonly Guid SystemAdminUserId = new("00000000-0000-0000-0000-000000000001");
    }
}
```

### 1.6 Update `AppConstants.Roles` in `MarketNest.Web`

Replace the old role strings with the `IdentityConstants.Roles.*` values so everything is consistent:

```csharp
// AppConstants.cs — Roles section
public static class Roles
{
    public const string SystemAdmin   = IdentityConstants.Roles.SystemAdmin;
    public const string Administrator = IdentityConstants.Roles.Administrator;
    public const string Seller        = IdentityConstants.Roles.Seller;
    public const string Buyer         = IdentityConstants.Roles.Buyer;
    // Keep legacy aliases if needed for backward compat during migration
}
```

### ✅ Phase 1 Checklist

- [ ] `Base.Common/Authorization/PermissionModule.cs` created
- [ ] `Base.Common/Authorization/Permissions.cs` created (9 `[Flags]` enums, all `enum : long`)
- [ ] `Base.Common/IdentityConstants.cs` created (Roles, ClaimTypes, WellKnownIds)
- [ ] `ICurrentUser` updated: multi-role list, `IsAdmin/IsSeller/IsBuyer`, `SellerStoreId`, 9 `HasPermission` overloads
- [ ] `AppConstants.Roles` updated to use `IdentityConstants.Roles.*`
- [ ] `dotnet build` passes (no errors)

---

## Phase 2 — Domain Layer

> **Where**: `src/MarketNest.Identity/Domain/`

### Folder structure to create

```
Domain/
├── Entities/
│   ├── User.cs
│   ├── Role.cs
│   ├── RolePermission.cs
│   ├── UserRole.cs
│   ├── UserPermissionOverride.cs
│   └── SellerApplication.cs
├── Enums/
│   ├── UserStatus.cs
│   └── SellerApplicationStatus.cs
└── Events/
    ├── UserRegisteredEvent.cs
    ├── EmailVerifiedEvent.cs
    ├── RoleAssignedEvent.cs
    ├── RoleRevokedEvent.cs
    ├── UserSuspendedEvent.cs
    ├── UserReinstatedEvent.cs
    ├── UserDeletedEvent.cs
    ├── PasswordChangedEvent.cs
    ├── SellerApplicationSubmittedEvent.cs
    ├── SellerApplicationApprovedEvent.cs
    └── SellerApplicationRejectedEvent.cs
```

### 2.1 Enums

**`UserStatus.cs`**:
```csharp
namespace MarketNest.Identity.Domain;
public enum UserStatus { Active = 1, Suspended = 2, Deleted = 3 }
```

**`SellerApplicationStatus.cs`**:
```csharp
namespace MarketNest.Identity.Domain;
public enum SellerApplicationStatus { Pending = 1, UnderReview = 2, Approved = 3, Rejected = 4, Cancelled = 5 }
```

### 2.2 Domain Events

All events implement `IDomainEvent` from `Base.Domain`. They are **post-commit** events (dispatched after TX commit).

Pattern for each:
```csharp
namespace MarketNest.Identity.Domain;

public sealed record UserRegisteredEvent(Guid UserId, string Email) : IDomainEvent;
public sealed record EmailVerifiedEvent(Guid UserId) : IDomainEvent;
public sealed record RoleAssignedEvent(Guid UserId, Guid RoleId, Guid GrantedByAdminId) : IDomainEvent;
public sealed record RoleRevokedEvent(Guid UserId, Guid RoleId, Guid RevokedByAdminId) : IDomainEvent;
public sealed record UserSuspendedEvent(Guid UserId, string Reason, Guid SuspendedByAdminId) : IDomainEvent;
public sealed record UserReinstatedEvent(Guid UserId, Guid ReinstatedByAdminId) : IDomainEvent;
public sealed record UserDeletedEvent(Guid UserId, Guid DeletedByAdminId) : IDomainEvent;
public sealed record PasswordChangedEvent(Guid UserId) : IDomainEvent;
public sealed record SellerApplicationSubmittedEvent(Guid ApplicationId, Guid ApplicantUserId) : IDomainEvent;
public sealed record SellerApplicationApprovedEvent(Guid ApplicationId, Guid ApplicantUserId, Guid AdminId) : IDomainEvent;
public sealed record SellerApplicationRejectedEvent(Guid ApplicationId, Guid ApplicantUserId, Guid AdminId, string Reason) : IDomainEvent;
```

### 2.3 `Role` Entity

```csharp
namespace MarketNest.Identity.Domain;

public class Role : Entity<Guid>
{
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }
    public string? Description { get; private set; }   // nullable: optional description
    public bool IsSystem { get; private set; }         // true → cannot be deleted via UI

    private readonly List<RolePermission> _permissions = [];
    public IReadOnlyList<RolePermission> Permissions => _permissions.AsReadOnly();

    // EF Core constructor
    #pragma warning disable CS8618
    private Role() { }
    #pragma warning restore CS8618

    public static Role Create(string name, string? description = null, bool isSystem = false)
        => new() { Name = name, NormalizedName = name.ToUpperInvariant(), Description = description, IsSystem = isSystem };

    public void SetPermission(PermissionModule module, long flags)
    {
        var existing = _permissions.FirstOrDefault(p => p.Module == module);
        if (existing is not null) _permissions.Remove(existing);
        if (flags != 0)
            _permissions.Add(new RolePermission(Id, module, flags));
    }

    /// <summary>Gets effective flags for a module. Returns 0 if no permission row exists.</summary>
    public long GetFlags(PermissionModule module)
        => _permissions.FirstOrDefault(p => p.Module == module)?.Flags ?? 0L;
}
```

### 2.4 `RolePermission` Child Entity

```csharp
namespace MarketNest.Identity.Domain;

public class RolePermission : Entity<Guid>
{
    public Guid RoleId { get; private set; }
    public PermissionModule Module { get; private set; }
    public long Flags { get; private set; }   // bitwise combined

    #pragma warning disable CS8618
    private RolePermission() { }
    #pragma warning restore CS8618

    internal RolePermission(Guid roleId, PermissionModule module, long flags)
    {
        RoleId = roleId;
        Module = module;
        Flags  = flags;
    }
}
```

### 2.5 `UserRole` Child Entity (join table)

```csharp
namespace MarketNest.Identity.Domain;

public class UserRole : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? GrantedBy { get; private set; }   // nullable: null = system-assigned at registration
    public DateTimeOffset GrantedAt { get; private set; }

    #pragma warning disable CS8618
    private UserRole() { }
    #pragma warning restore CS8618

    internal UserRole(Guid userId, Guid roleId, Guid? grantedBy)
    {
        UserId    = userId;
        RoleId    = roleId;
        GrantedBy = grantedBy;
        GrantedAt = DateTimeOffset.UtcNow;
    }
}
```

### 2.6 `UserPermissionOverride` Child Entity

```csharp
namespace MarketNest.Identity.Domain;

public class UserPermissionOverride : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public PermissionModule Module { get; private set; }
    public long GrantedFlags { get; private set; }
    public long DeniedFlags { get; private set; }
    public Guid GrantedBy { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }   // nullable: null = no expiry

    #pragma warning disable CS8618
    private UserPermissionOverride() { }
    #pragma warning restore CS8618

    internal UserPermissionOverride(
        Guid userId, PermissionModule module,
        long grantedFlags, long deniedFlags,
        Guid grantedBy, DateTimeOffset? expiresAt)
    {
        UserId       = userId;
        Module       = module;
        GrantedFlags = grantedFlags;
        DeniedFlags  = deniedFlags;
        GrantedBy    = grantedBy;
        GrantedAt    = DateTimeOffset.UtcNow;
        ExpiresAt    = expiresAt;
    }

    public bool IsExpired(DateTimeOffset utcNow) => ExpiresAt.HasValue && ExpiresAt.Value <= utcNow;
}
```

### 2.7 `User` Aggregate Root

Key points:
- `AccessFailedCount`: `5` failures → lockout 15 min — use `AppConstants.Validation` for thresholds or create `IdentityConstants.Security`
- Soft delete: `NormalizedEmail` prefixed with `DELETED_{Id}_` to free email slot
- All mutable properties: `{ get; private set; }`
- Collection navigation: `private readonly List<T> _xxx = [];` + `IReadOnlyList<T> Xxx => _xxx.AsReadOnly();`

```csharp
namespace MarketNest.Identity.Domain;

[Auditable]
public class User : AggregateRoot
{
    private const int MaxLoginFailures = 5;
    private const int LockoutMinutes = 15;

    // Core identity
    public string Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public bool EmailVerified { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }   // null until verified

    // Profile
    public string? PhoneNumber { get; private set; }   // null: optional
    public Guid? AvatarFileId { get; private set; }    // null: no avatar uploaded yet
    public string? PublicBio { get; private set; }     // null: seller-only, not set by default

    // Account state
    public UserStatus Status { get; private set; }
    public string? SuspensionReason { get; private set; }   // null: not suspended
    public DateTimeOffset? SuspendedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }   // null: not deleted

    // Security
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }   // null: not locked out

    // Collections
    private readonly List<UserRole> _roles = [];
    public IReadOnlyList<UserRole> Roles => _roles.AsReadOnly();

    private readonly List<UserPermissionOverride> _permissionOverrides = [];
    public IReadOnlyList<UserPermissionOverride> PermissionOverrides => _permissionOverrides.AsReadOnly();

    // EF Core constructor
    #pragma warning disable CS8618
    private User() { }
    #pragma warning restore CS8618

    // ── Factory ───────────────────────────────────────────────────────

    public static User Register(string email, string displayName, string passwordHash)
    {
        var user = new User
        {
            Email             = email.Trim(),
            NormalizedEmail   = email.Trim().ToUpperInvariant(),
            DisplayName       = displayName.Trim(),
            PasswordHash      = passwordHash,
            Status            = UserStatus.Active,
            EmailVerified     = false,
            AccessFailedCount = 0,
        };
        user.RaiseDomainEvent(new UserRegisteredEvent(user.Id, user.Email));
        return user;
    }

    // ── Domain Methods ─────────────────────────────────────────────────

    public void VerifyEmail()
    {
        EmailVerified   = true;
        EmailVerifiedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new EmailVerifiedEvent(Id));
    }

    public Result<Unit, Error> AssignRole(Role role, Guid? grantedByAdminId)
    {
        if (_roles.Any(r => r.RoleId == role.Id))
            return Error.Conflict("ROLE.ALREADY_ASSIGNED", $"User already has role {role.Name}");

        _roles.Add(new UserRole(Id, role.Id, grantedByAdminId));
        RaiseDomainEvent(new RoleAssignedEvent(Id, role.Id, grantedByAdminId ?? Guid.Empty));
        return Result.Success(Unit.Value);
    }

    public Result<Unit, Error> RevokeRole(Guid roleId, Guid revokedByAdminId)
    {
        var userRole = _roles.FirstOrDefault(r => r.RoleId == roleId);
        if (userRole is null)
            return Error.NotFound("ROLE", roleId.ToString());

        _roles.Remove(userRole);
        RaiseDomainEvent(new RoleRevokedEvent(Id, roleId, revokedByAdminId));
        return Result.Success(Unit.Value);
    }

    public void GrantPermissionOverride(
        PermissionModule module, long grantedFlags, long deniedFlags,
        Guid grantedByAdminId, DateTimeOffset? expiresAt)
    {
        var existing = _permissionOverrides.FirstOrDefault(p => p.Module == module);
        if (existing is not null) _permissionOverrides.Remove(existing);

        _permissionOverrides.Add(new UserPermissionOverride(
            Id, module, grantedFlags, deniedFlags, grantedByAdminId, expiresAt));
    }

    public void Suspend(string reason, Guid suspendedByAdminId)
    {
        Status            = UserStatus.Suspended;
        SuspensionReason  = reason;
        SuspendedAt       = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new UserSuspendedEvent(Id, reason, suspendedByAdminId));
    }

    public void Reinstate(Guid reinstatedByAdminId)
    {
        Status           = UserStatus.Active;
        SuspensionReason = null;
        SuspendedAt      = null;
        RaiseDomainEvent(new UserReinstatedEvent(Id, reinstatedByAdminId));
    }

    public void SoftDelete(Guid deletedByAdminId)
    {
        Status          = UserStatus.Deleted;
        DeletedAt       = DateTimeOffset.UtcNow;
        NormalizedEmail = $"DELETED_{Id}_{NormalizedEmail}";   // frees email for re-use
        RaiseDomainEvent(new UserDeletedEvent(Id, deletedByAdminId));
    }

    public Result<Unit, Error> ChangePassword(string newPasswordHash)
    {
        PasswordHash      = newPasswordHash;
        AccessFailedCount = 0;
        LockoutEnd        = null;
        RaiseDomainEvent(new PasswordChangedEvent(Id));
        return Result.Success(Unit.Value);
    }

    public Result<Unit, Error> UpdateProfile(string displayName, string? phoneNumber, string? publicBio)
    {
        DisplayName = displayName.Trim();
        PhoneNumber = phoneNumber;
        PublicBio   = publicBio;
        return Result.Success(Unit.Value);
    }

    public void SetAvatarFileId(Guid fileId) => AvatarFileId = fileId;

    public void RecordLoginFailure()
    {
        AccessFailedCount++;
        if (AccessFailedCount >= MaxLoginFailures)
            LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(LockoutMinutes);
    }

    public void RecordSuccessfulLogin()
    {
        AccessFailedCount = 0;
        LockoutEnd        = null;
    }

    public bool IsLockedOut() => LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;
    public bool IsActive()    => Status == UserStatus.Active;
}
```

### 2.8 `SellerApplication` Aggregate

```csharp
namespace MarketNest.Identity.Domain;

[Auditable]
public class SellerApplication : AggregateRoot
{
    public Guid ApplicantUserId { get; private set; }
    public SellerApplicationStatus Status { get; private set; }

    public string BusinessName { get; private set; }
    public string? TaxId { get; private set; }                      // null: optional
    public Guid? BusinessLicenseFileId { get; private set; }        // null: not provided
    public Guid? IdentityDocFileId { get; private set; }            // null: not provided
    public string? AdditionalNotes { get; private set; }            // null: not provided

    public Guid? ReviewedByAdminId { get; private set; }            // null: not yet reviewed
    public string? ReviewNote { get; private set; }                  // null: no note
    public DateTimeOffset? ReviewedAt { get; private set; }          // null: not yet reviewed

    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    #pragma warning disable CS8618
    private SellerApplication() { }
    #pragma warning restore CS8618

    public static SellerApplication Submit(
        Guid userId, string businessName, string? taxId,
        Guid? businessLicenseFileId, Guid? identityDocFileId, string? additionalNotes)
    {
        var app = new SellerApplication
        {
            ApplicantUserId        = userId,
            Status                 = SellerApplicationStatus.Pending,
            BusinessName           = businessName.Trim(),
            TaxId                  = taxId,
            BusinessLicenseFileId  = businessLicenseFileId,
            IdentityDocFileId      = identityDocFileId,
            AdditionalNotes        = additionalNotes,
            SubmittedAt            = DateTimeOffset.UtcNow,
            UpdatedAt              = DateTimeOffset.UtcNow,
        };
        app.RaiseDomainEvent(new SellerApplicationSubmittedEvent(app.Id, userId));
        return app;
    }

    public Result<Unit, Error> Approve(Guid adminId, string? note)
    {
        if (Status != SellerApplicationStatus.Pending && Status != SellerApplicationStatus.UnderReview)
            return Error.Conflict("APPLICATION.CANNOT_APPROVE", "Application is not pending review");

        Status              = SellerApplicationStatus.Approved;
        ReviewedByAdminId   = adminId;
        ReviewNote          = note;
        ReviewedAt          = DateTimeOffset.UtcNow;
        UpdatedAt           = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new SellerApplicationApprovedEvent(Id, ApplicantUserId, adminId));
        return Result.Success(Unit.Value);
    }

    public Result<Unit, Error> Reject(Guid adminId, string reason)
    {
        if (Status is SellerApplicationStatus.Approved or
            SellerApplicationStatus.Rejected or
            SellerApplicationStatus.Cancelled)
            return Error.Conflict("APPLICATION.CANNOT_REJECT", "Application already finalized");

        Status              = SellerApplicationStatus.Rejected;
        ReviewedByAdminId   = adminId;
        ReviewNote          = reason;
        ReviewedAt          = DateTimeOffset.UtcNow;
        UpdatedAt           = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new SellerApplicationRejectedEvent(Id, ApplicantUserId, adminId, reason));
        return Result.Success(Unit.Value);
    }

    public Result<Unit, Error> Cancel(Guid applicantUserId)
    {
        if (Status is SellerApplicationStatus.Approved or SellerApplicationStatus.Rejected)
            return Error.Conflict("APPLICATION.CANNOT_CANCEL", "Application already finalized");

        if (ApplicantUserId != applicantUserId)
            return Error.Forbidden("APPLICATION.NOT_OWNER", "You cannot cancel this application");

        Status    = SellerApplicationStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success(Unit.Value);
    }
}
```

### ✅ Phase 2 Checklist

- [ ] `Domain/Enums/UserStatus.cs` + `SellerApplicationStatus.cs`
- [ ] `Domain/Events/` — 11 event records (all `sealed record`, all implement `IDomainEvent`)
- [ ] `Domain/Entities/Role.cs` + `RolePermission.cs` + `UserRole.cs` + `UserPermissionOverride.cs`
- [ ] `Domain/Entities/User.cs` (AggregateRoot, `[Auditable]`, all domain methods)
- [ ] `Domain/Entities/SellerApplication.cs` (AggregateRoot, `[Auditable]`)
- [ ] `dotnet build` passes

---

## Phase 3 — Infrastructure: DbContext + EF Configurations

> **Where**: `src/MarketNest.Identity/Infrastructure/Persistence/`

### 3.1 Create the thin `BaseRepository` / `BaseQuery` wrappers

**`BaseRepository.cs`**:
```csharp
namespace MarketNest.Identity.Infrastructure;

public abstract class BaseRepository<TEntity, TKey>(IdentityDbContext db)
    : BaseRepository<TEntity, TKey, IdentityDbContext>(db)
    where TEntity : Entity<TKey>;
```

**`BaseQuery.cs`**:
```csharp
namespace MarketNest.Identity.Infrastructure;

public abstract class BaseQuery<TEntity, TKey>(IdentityReadDbContext db)
    : BaseQuery<TEntity, TKey, IdentityReadDbContext>(db)
    where TEntity : Entity<TKey>;
```

### 3.2 Create `IdentityDbContext.cs`

```csharp
namespace MarketNest.Identity.Infrastructure;

public class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    IAppLogger<IdentityDbContext> logger)
    : DbContext(options), IModuleDbContext
{
    public const string Schema = "identity";
    public string SchemaName => Schema;
    public string ContextName => nameof(IdentityDbContext);

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<SellerApplication> SellerApplications => Set<SellerApplication>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
    }
}
```

### 3.3 Create `IdentityReadDbContext.cs`

Same DbSets, no tracking, no migrations, no interceptors:

```csharp
namespace MarketNest.Identity.Infrastructure;

public class IdentityReadDbContext(DbContextOptions<IdentityReadDbContext> options)
    : DbContext(options)
{
    // same DbSets as IdentityDbContext but without IModuleDbContext (no migrations)
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    // ... all the same DbSets ...

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(IdentityDbContext.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
    }
}
```

### 3.4 Create supporting token entities (simple — no domain methods needed)

Create these in `Domain/Entities/` but keep them minimal:

**`RefreshToken.cs`** — columns: Id, UserId, TokenHash (SHA-512), ExpiresAt, RevokedAt?, CreatedAt

**`PasswordResetToken.cs`** — columns: Id, UserId, TokenHash, ExpiresAt, Used (bool), CreatedAt

**`EmailVerificationCode.cs`** — columns: Id, UserId, Code (6 digits), ExpiresAt, Used (bool), CreatedAt

### 3.5 Create EF Configurations

Create one configuration class per entity in `Infrastructure/Persistence/Configurations/`:

**`UserConfiguration.cs`** — key config points:
- Table: `identity.users`
- Unique index on `NormalizedEmail WHERE status != 3` (partial — deleted users free email)
- `HasMany(u => u._roles)` → owned navigation, `WithOne()`, FK `user_id`
- `HasMany(u => u._permissionOverrides)` → same pattern
- Navigation via `UsePropertyAccessMode(PropertyAccessMode.Field)` (handled by `ApplyDddPropertyAccessConventions`)
- Status stored as `smallint`

**`RoleConfiguration.cs`**:
- Unique index on `NormalizedName`
- `HasMany` for `_permissions` navigation

**`UserRoleConfiguration.cs`**:
- Primary key: composite `(user_id, role_id)`
- No cascade delete (preserve audit history)

**`SellerApplicationConfiguration.cs`**:
- Index on `applicant_user_id` for lookup
- Index on `status` for admin queue queries

**`RefreshTokenConfiguration.cs`**:
- Unique index on `token_hash`
- Index on `user_id`
- Index on `expires_at WHERE revoked_at IS NULL`

### 3.6 EF Migration

After writing all configurations:

```bash
dotnet ef migrations add InitIdentityModule --project src/MarketNest.Identity
```

> DatabaseInitializer handles auto-apply on startup — you don't need `database update`.

### ✅ Phase 3 Checklist

- [ ] `Infrastructure/Persistence/BaseRepository.cs` (2-line thin wrapper)
- [ ] `Infrastructure/Persistence/BaseQuery.cs` (2-line thin wrapper)
- [ ] Token entities in `Domain/Entities/`: `RefreshToken`, `PasswordResetToken`, `EmailVerificationCode`
- [ ] `IdentityDbContext.cs` (write-side, implements `IModuleDbContext`)
- [ ] `IdentityReadDbContext.cs` (read-side, no tracking)
- [ ] EF configurations for all entities in `Infrastructure/Persistence/Configurations/`
- [ ] Migration created: `dotnet ef migrations add InitIdentityModule --project src/MarketNest.Identity`
- [ ] `dotnet build` passes

---

## Phase 4 — Infrastructure: Repositories + Query Interfaces

> **Where**: `src/MarketNest.Identity/Infrastructure/`

### 4.1 Repository Interfaces

Create in `Application/` (interfaces belong to Application layer):

```
Application/
├── Common/
│   └── IdentityAuditEvents.cs   (audit event type constants)
└── Modules/
    ├── Auth/
    │   ├── Repositories/
    │   │   └── IUserRepository.cs
    │   └── Queries/
    │       └── IGetUserByEmailQuery.cs
    ├── Roles/
    │   └── Repositories/
    │       └── IRoleRepository.cs
    ├── SellerOnboarding/
    │   ├── Repositories/
    │   │   └── ISellerApplicationRepository.cs
    │   └── Queries/
    │       └── IGetSellerApplicationsPagedQuery.cs
    └── Admin/
        └── Queries/
            ├── IGetUsersPagedQuery.cs
            └── IGetUserDetailQuery.cs
```

**`IUserRepository.cs`**:
```csharp
namespace MarketNest.Identity.Application;

public interface IUserRepository : IBaseRepository<User, Guid>
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<User?> FindByIdWithRolesAsync(Guid userId, CancellationToken ct);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);
    Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken ct);
}
```

**`IRoleRepository.cs`**:
```csharp
namespace MarketNest.Identity.Application;

public interface IRoleRepository : IBaseRepository<Role, Guid>
{
    Task<Role?> FindByNameAsync(string normalizedName, CancellationToken ct);
    Task<Role?> FindByIdWithPermissionsAsync(Guid roleId, CancellationToken ct);
    Task<List<Role>> GetAllWithPermissionsAsync(CancellationToken ct);
}
```

**`IRefreshTokenRepository.cs`**:
```csharp
namespace MarketNest.Identity.Application;

public interface IRefreshTokenRepository : IBaseRepository<RefreshToken, Guid>
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct);
}
```

**`ISellerApplicationRepository.cs`**:
```csharp
namespace MarketNest.Identity.Application;

public interface ISellerApplicationRepository : IBaseRepository<SellerApplication, Guid>
{
    Task<SellerApplication?> FindActiveByUserIdAsync(Guid userId, CancellationToken ct);
}
```

### 4.2 Repository Implementations

**`UserRepository.cs`** in `Infrastructure/Repositories/Modules/Auth/`:

```csharp
namespace MarketNest.Identity.Infrastructure;

public class UserRepository(IdentityDbContext db)
    : BaseRepository<User, Guid>(db), IUserRepository
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
        => db.Users
            .Where(u => u.NormalizedEmail == normalizedEmail && u.Status != UserStatus.Deleted)
            .FirstOrDefaultAsync(ct);

    public Task<User?> FindByIdWithRolesAsync(Guid userId, CancellationToken ct)
        => db.Users
            .Include(u => u.Roles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .Include(u => u.PermissionOverrides)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct)
        => db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail
            && u.Status != UserStatus.Deleted, ct);

    public async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken ct)
        => await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);
}
```

> ⚠️ `Include` chains on `Roles → Role → Permissions` require navigation properties configured in EF. Verify the configuration allows loading via `ThenInclude`.

**`RoleRepository.cs`** and **`SellerApplicationRepository.cs`** — follow same pattern.

**`RefreshTokenRepository.cs`**:
```csharp
namespace MarketNest.Identity.Infrastructure;

public class RefreshTokenRepository(IdentityDbContext db)
    : BaseRepository<RefreshToken, Guid>(db), IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct)
        => db.RefreshTokens.FirstOrDefaultAsync(t =>
            t.TokenHash == tokenHash && t.RevokedAt == null
            && t.ExpiresAt > DateTimeOffset.UtcNow, ct);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)
        => await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);
}
```

### 4.3 Query Implementations for Admin

**`IGetUsersPagedQuery.cs`** interface + `GetUsersPagedQuery.cs` implementation in `Infrastructure/Queries/Modules/Admin/`:

```csharp
// Interface (Application layer)
namespace MarketNest.Identity.Application;
public interface IGetUsersPagedQuery
{
    Task<PagedResult<UserSummaryDto>> ExecuteAsync(GetUsersPagedQuery query, CancellationToken ct);
}

// Implementation (Infrastructure layer)
namespace MarketNest.Identity.Infrastructure;
public class GetUsersPagedQuery(IdentityReadDbContext db) : IGetUsersPagedQuery
{
    public async Task<PagedResult<UserSummaryDto>> ExecuteAsync(GetUsersPagedQuery q, CancellationToken ct)
    {
        var dbQuery = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
            dbQuery = dbQuery.Where(u => u.Email.Contains(q.Search) || u.DisplayName.Contains(q.Search));
        if (q.Status.HasValue)
            dbQuery = dbQuery.Where(u => u.Status == q.Status.Value);

        int total = await dbQuery.CountAsync(ct);
        var items = await dbQuery
            .OrderByDescending(u => u.CreatedAt)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(u => new UserSummaryDto
            {
                Id          = u.Id,
                Email       = u.Email,
                DisplayName = u.DisplayName,
                Status      = u.Status,
            })
            .ToListAsync(ct);

        return new PagedResult<UserSummaryDto>(items, total, q.Page, q.PageSize);
    }
}
```

### ✅ Phase 4 Checklist

- [ ] Repository interfaces: `IUserRepository`, `IRoleRepository`, `IRefreshTokenRepository`, `ISellerApplicationRepository`
- [ ] Query interfaces: `IGetUsersPagedQuery`, `IGetUserDetailQuery`, `IGetSellerApplicationsPagedQuery`
- [ ] Repository implementations in `Infrastructure/Repositories/Modules/Auth/`
- [ ] Query implementations in `Infrastructure/Queries/Modules/Admin/` + `Modules/Auth/`
- [ ] `dotnet build` passes

---

## Phase 5 — Infrastructure: PermissionResolver + JwtTokenService

> **Where**: `src/MarketNest.Identity/Infrastructure/Services/`

> ⚠️ These are **not** injectable as `IBackgroundJob` — they are concrete classes with interface contracts. They follow the `I{ClassName}` rule so the `Service` suffix is allowed here (per MN021: allowed when implementing `I{ClassName}Service`).

### 5.1 Create `IPermissionResolver.cs` (Application layer contract)

```csharp
namespace MarketNest.Identity.Application;

public interface IPermissionResolver
{
    /// <summary>
    ///     Computes the effective permission flags per module for a loaded user.
    ///     Result: dictionary of PermissionModule → effective long flags.
    ///     Only modules with non-zero effective permissions appear in the result.
    /// </summary>
    Dictionary<PermissionModule, long> Resolve(User user);
}
```

### 5.2 Create `PermissionResolver.cs` (Infrastructure)

```csharp
namespace MarketNest.Identity.Infrastructure;

public class PermissionResolver : IPermissionResolver
{
    public Dictionary<PermissionModule, long> Resolve(User user)
    {
        var effectiveMap = new Dictionary<PermissionModule, long>();
        var utcNow = DateTimeOffset.UtcNow;

        // Step 1: union all role flags
        foreach (var userRole in user.Roles)
        {
            // Note: requires user loaded with .Include(ur => ur.Role).ThenInclude(r => r.Permissions)
            foreach (var permission in userRole.Role.Permissions)
            {
                effectiveMap.TryGetValue(permission.Module, out long existing);
                effectiveMap[permission.Module] = existing | permission.Flags;
            }
        }

        // Step 2: apply per-user overrides
        foreach (var ovr in user.PermissionOverrides)
        {
            if (ovr.IsExpired(utcNow)) continue;

            effectiveMap.TryGetValue(ovr.Module, out long effective);
            effective |= ovr.GrantedFlags;   // add
            effective &= ~ovr.DeniedFlags;   // remove
            effectiveMap[ovr.Module] = effective;
        }

        // Remove zeros (no permissions → no claim)
        foreach (var key in effectiveMap.Keys.ToList())
            if (effectiveMap[key] == 0) effectiveMap.Remove(key);

        return effectiveMap;
    }
}
```

### 5.3 Create `IJwtTokenService.cs` + `JwtTokenService.cs`

**Contract** (`Application/`):
```csharp
namespace MarketNest.Identity.Application;

public interface IJwtTokenService
{
    /// <summary>Issues a signed JWT access token (15 min) + a refresh token entry.</summary>
    Task<TokenPairResult> IssueTokensAsync(User user, CancellationToken ct);

    /// <summary>Validates a refresh token and issues a new token pair (rotating).</summary>
    Task<Result<TokenPairResult, Error>> RefreshAsync(string rawRefreshToken, CancellationToken ct);

    /// <summary>Revokes a refresh token (logout).</summary>
    Task RevokeRefreshTokenAsync(string rawRefreshToken, CancellationToken ct);
}

public sealed record TokenPairResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken   // raw value — caller puts in HttpOnly cookie
);
```

**Implementation** (`Infrastructure/Services/JwtTokenService.cs`):

Key responsibilities:
1. Load RSA private key from `SecurityOptions.JwtPrivateKey` (base64 PEM)
2. Build claims from user:
   - `sub` = userId
   - `email` = user.Email
   - `name` = user.DisplayName
   - `role` = each role name (multiple claims if multi-role)
   - `mn.store` = SellerStoreId (if seller) — requires a store lookup
   - `mn.perm.{module}` = effective flags per module via `IPermissionResolver`
3. Sign with RS256 (`RsaSecurityKey`)
4. Generate refresh token: `RandomNumberGenerator.GetBytes(32)` → base64url → SHA-512 hash stored in DB
5. Save `RefreshToken` entity via repository — `uow.CommitAsync()` is NOT called here — the outer transaction handles it

```csharp
// JwtTokenService reads Jwt settings from IOptions<SecurityOptions>
// Inject: IPermissionResolver, IRefreshTokenRepository,
//         IStorefrontReadService (to get SellerStoreId), IOptions<SecurityOptions>
```

> **Important**: The `JwtTokenService` uses `IStorefrontReadService` (defined in `Base.Common/Contracts/`) to look up `SellerStoreId`. This is a cross-module contract — `Identity` never touches the `catalog.*` schema directly.

### ✅ Phase 5 Checklist

- [ ] `Application/Modules/Auth/Services/IPermissionResolver.cs`
- [ ] `Application/Modules/Auth/Services/IJwtTokenService.cs` + `TokenPairResult` record
- [ ] `Infrastructure/Services/PermissionResolver.cs`
- [ ] `Infrastructure/Services/JwtTokenService.cs`
- [ ] Add `Microsoft.IdentityModel.Tokens` + `System.IdentityModel.Tokens.Jwt` to `.csproj` (check `Directory.Packages.props` for version pinning)
- [ ] `dotnet build` passes

---

## Phase 6 — Infrastructure: Seeders

> **Where**: `src/MarketNest.Identity/Infrastructure/Seeders/`

### 6.1 `IdentitySeederOrder.cs`

```csharp
namespace MarketNest.Identity.Infrastructure;

public static class IdentitySeederOrder
{
    public const int Roles     = 100;   // roles must exist before admin user
    public const int AdminUser = 200;   // first administrator account
}
```

### 6.2 `RoleSeeder.cs`

Seeds 4 system roles with default permission matrix.

```csharp
namespace MarketNest.Identity.Infrastructure;

public class RoleSeeder(IdentityDbContext db) : IDataSeeder
{
    public int Order => IdentitySeederOrder.Roles;
    public bool RunInProduction => true;
    public string Version => "1.0";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Idempotent: only insert if not present
        await EnsureRole(db, IdentityConstants.Roles.SystemAdmin,
            "System-level background jobs and migrations — never a human login.",
            BuildSystemAdminPermissions(), ct);

        await EnsureRole(db, IdentityConstants.Roles.Administrator,
            "Platform operator — full Admin panel access.",
            BuildAdminPermissions(), ct);

        await EnsureRole(db, IdentityConstants.Roles.Seller,
            "Verified seller with storefront.",
            BuildSellerPermissions(), ct);

        await EnsureRole(db, IdentityConstants.Roles.Buyer,
            "Default customer role assigned on registration.",
            BuildBuyerPermissions(), ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureRole(IdentityDbContext db, string name,
        string description, IEnumerable<(PermissionModule, long)> permissions, CancellationToken ct)
    {
        var normalized = name.ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.NormalizedName == normalized, ct)) return;

        var role = Role.Create(name, description, isSystem: true);
        foreach (var (module, flags) in permissions)
            role.SetPermission(module, flags);

        db.Roles.Add(role);
    }

    // Default permission matrix — matches §5.1 in domain-and-business-rules.md
    private static IEnumerable<(PermissionModule, long)> BuildBuyerPermissions() =>
    [
        (PermissionModule.Order,      (long)(OrderPermission.View | OrderPermission.Cancel)),
        (PermissionModule.Catalog,    (long)CatalogPermission.View),
        (PermissionModule.Storefront, (long)StorefrontPermission.View),
        (PermissionModule.Dispute,    (long)(DisputePermission.View | DisputePermission.Open)),
        (PermissionModule.Review,     (long)(ReviewPermission.View | ReviewPermission.Write)),
        (PermissionModule.Promotion,  (long)PromotionPermission.View),
    ];

    private static IEnumerable<(PermissionModule, long)> BuildSellerPermissions() =>
    [
        (PermissionModule.Order,      (long)(OrderPermission.View | OrderPermission.Edit | OrderPermission.Cancel | OrderPermission.Export)),
        (PermissionModule.Catalog,    (long)(CatalogPermission.View | CatalogPermission.Edit | CatalogPermission.Delete | CatalogPermission.Publish)),
        (PermissionModule.Storefront, (long)(StorefrontPermission.View | StorefrontPermission.Edit)),
        (PermissionModule.Dispute,    (long)(DisputePermission.View | DisputePermission.Respond)),
        (PermissionModule.Review,     (long)ReviewPermission.View),
        (PermissionModule.Payment,    (long)PaymentPermission.ViewPayout),
        (PermissionModule.Promotion,  (long)(PromotionPermission.View | PromotionPermission.Create | PromotionPermission.Pause)),
    ];

    private static IEnumerable<(PermissionModule, long)> BuildAdminPermissions() =>
    [
        (PermissionModule.Order,      (long)OrderPermission.All),
        (PermissionModule.Catalog,    (long)CatalogPermission.All),
        (PermissionModule.Storefront, (long)StorefrontPermission.All),
        (PermissionModule.Dispute,    (long)DisputePermission.All),
        (PermissionModule.Review,     (long)ReviewPermission.All),
        (PermissionModule.Payment,    (long)PaymentPermission.All),
        (PermissionModule.User,       (long)UserPermission.All),
        (PermissionModule.Config,     (long)ConfigPermission.All),
        (PermissionModule.Promotion,  (long)PromotionPermission.All),
    ];

    private static IEnumerable<(PermissionModule, long)> BuildSystemAdminPermissions() =>
        BuildAdminPermissions();   // same full access, different person
}
```

### 6.3 `AdminUserSeeder.cs`

Seeds the first Administrator user from environment config + the SystemAdmin built-in user.

```csharp
namespace MarketNest.Identity.Infrastructure;

public class AdminUserSeeder(
    IdentityDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration) : IDataSeeder
{
    private const string AdminEmailKey    = "Identity:AdminUser:Email";
    private const string AdminPasswordKey = "Identity:AdminUser:Password";
    private const string AdminNameKey     = "Identity:AdminUser:DisplayName";

    public int Order => IdentitySeederOrder.AdminUser;
    public bool RunInProduction => true;
    public string Version => "1.0";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // 1. Seed SystemAdmin built-in user (well-known ID)
        await EnsureSystemAdminAsync(ct);

        // 2. Seed first Administrator from config (env var or appsettings)
        await EnsureFirstAdminAsync(ct);
    }

    private async Task EnsureSystemAdminAsync(CancellationToken ct)
    {
        var systemAdminId = IdentityConstants.WellKnownIds.SystemAdminUserId;
        if (await db.Users.AnyAsync(u => u.Id == systemAdminId, ct)) return;

        var systemRole = await db.Roles
            .FirstOrDefaultAsync(r => r.NormalizedName == IdentityConstants.Roles.SystemAdmin.ToUpperInvariant(), ct)
            ?? throw new InvalidOperationException("SystemAdmin role not found — run RoleSeeder first");

        var systemUser = User.Register(
            "system@marketnest.internal",
            "System Administrator",
            // SystemAdmin never logs in — use a random unforgeable hash
            passwordHasher.HashPassword(null!, Guid.NewGuid().ToString("N")));

        // Patch the ID to the well-known value (EF will use the value we set)
        // Requires EF to NOT use ValueGeneratedOnAdd / database-generated for this entity
        // OR: directly insert via raw SQL. Using EF is cleaner if ID is settable.
        // See note below.

        systemUser.AssignRole(systemRole, null);
        await db.Users.AddAsync(systemUser, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureFirstAdminAsync(CancellationToken ct)
    {
        var email = configuration[AdminEmailKey];
        if (string.IsNullOrWhiteSpace(email)) return;   // skip if not configured

        var normalizedEmail = email.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct)) return;

        var adminRole = await db.Roles
            .FirstOrDefaultAsync(r => r.NormalizedName == IdentityConstants.Roles.Administrator.ToUpperInvariant(), ct)
            ?? throw new InvalidOperationException("Administrator role not found");

        var buyerRole = await db.Roles
            .FirstOrDefaultAsync(r => r.NormalizedName == IdentityConstants.Roles.Buyer.ToUpperInvariant(), ct)
            ?? throw new InvalidOperationException("Buyer role not found");

        var password    = configuration[AdminPasswordKey] ?? throw new InvalidOperationException($"Set '{AdminPasswordKey}' in config");
        var displayName = configuration[AdminNameKey] ?? "Platform Admin";

        var admin = User.Register(email.Trim(), displayName, passwordHasher.HashPassword(null!, password));
        admin.VerifyEmail();   // admin is pre-verified
        admin.AssignRole(buyerRole, null);
        admin.AssignRole(adminRole, IdentityConstants.WellKnownIds.SystemAdminUserId);

        await db.Users.AddAsync(admin, ct);
        await db.SaveChangesAsync(ct);
    }
}
```

> **Note on SystemAdmin ID**: EF Core's `ValueGeneratedOnAdd` on `Guid` keys normally lets the DB auto-generate via `gen_random_uuid()`. To force a specific ID, either:
> - Override the entity config: `HasKey(u => u.Id).ValueGeneratedNever()` — but this breaks all other users
> - **Better**: Use `db.Database.ExecuteSqlRawAsync` to directly insert the SystemAdmin row bypassing EF, or
> - Use `PostgreSQL ON CONFLICT DO NOTHING` via `db.Users.AddAsync(user)` where the entity's ID is set before Add (EF will use it if `ValueGeneratedOnAdd` is NOT set for that specific case — i.e., if the property already has a value, EF respects it)

> The cleanest approach: set `Entity<Guid>` base to have `protected set` on `Id` and initialize it in the static factory. In `EnsureSystemAdminAsync`, before `User.Register(...)`, manually set the ID via reflection or a dedicated `WithId(Guid id)` builder method. Or just insert via raw SQL.

### 6.4 Add `IPasswordHasher<User>` to DI

In `DependencyInjection.cs`, register:
```csharp
services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
```

### ✅ Phase 6 Checklist

- [ ] `Infrastructure/Seeders/IdentitySeederOrder.cs`
- [ ] `Infrastructure/Seeders/RoleSeeder.cs` (4 roles + default permission matrix)
- [ ] `Infrastructure/Seeders/AdminUserSeeder.cs` (SystemAdmin + first Administrator)
- [ ] `IPasswordHasher<User>` registered in DI
- [ ] `appsettings.json` / `.env.example` updated with `Identity:AdminUser:Email`, `Identity:AdminUser:Password`, `Identity:AdminUser:DisplayName` keys
- [ ] `dotnet build` passes

---

## Phase 7 — Application: Command Handlers

> **Where**: `src/MarketNest.Identity/Application/Modules/`
> Namespaces: `MarketNest.Identity.Application` (flat — no sub-folder in namespace)

### 7.1 Auth Commands (folder: `Auth/Commands/` + `Auth/CommandHandlers/`)

Each command is a `record` implementing `ICommand<TResult>`:

#### `RegisterCommand` / `RegisterCommandHandler`

Steps:
1. Check email not taken (`IUserRepository.EmailExistsAsync`)
2. Hash password: `IPasswordHasher<User>.HashPassword(null!, cmd.Password)`
3. `User.Register(email, displayName, hash)` → raises `UserRegisteredEvent`
4. Load `Buyer` role from `IRoleRepository`
5. `user.AssignRole(buyerRole, null)` — system-assigned
6. `_userRepo.Add(user)` — UoW commits automatically via filter
7. Return `RegisterResult(user.Id, user.Email)`

> Domain event `UserRegisteredEvent` → handled by `Notifications` module (send verification email). Register a `IDomainEventHandler<UserRegisteredEvent>` in the Notifications module.

#### `LoginCommand` / `LoginCommandHandler`

Steps:
1. Find user by email: `FindByEmailAsync`
2. Validate: user exists, status Active, not locked out
3. Verify password: `IPasswordHasher<User>.VerifyHashedPassword(...)`
4. On failure: `user.RecordLoginFailure()` → save, return 401
5. On success: `user.RecordSuccessfulLogin()`
6. Load user with roles + permissions: `FindByIdWithRolesAsync`
7. Issue tokens: `IJwtTokenService.IssueTokensAsync(user, ct)`
8. Return `LoginResult(accessToken, expiresAt, userSummaryDto)`

> **Note**: Step 3 uses user without roles. Step 6 reloads with roles for JWT. This is intentional — avoid always loading relationships on every auth check.

#### `LogoutCommand` / `LogoutCommandHandler`

Steps:
1. `IJwtTokenService.RevokeRefreshTokenAsync(rawToken, ct)`
2. Optionally blacklist JWT: add to Redis `marketnest:blacklist:{jti}` with TTL = remaining token expiry
3. Return `Unit.Value`

#### `RefreshTokenCommand` / `RefreshTokenCommandHandler`

Steps:
1. Find refresh token by hash in DB
2. Validate: not revoked, not expired
3. Revoke old token
4. Reload user with roles + permissions
5. Issue new token pair
6. Return new `LoginResult`

#### `ForgotPasswordCommand` / `ForgotPasswordCommandHandler`

Steps:
1. Find user by email — **always return success** (prevent enumeration)
2. If found: generate 32-byte random token, SHA-512 hash the raw token for storage
3. Save `PasswordResetToken` entity (30 min expiry)
4. `INotificationService.SendSecurityEmailAsync(...)` with reset URL
5. Return `Unit.Value`

#### `ResetPasswordCommand` / `ResetPasswordCommandHandler`

Steps:
1. SHA-512 hash the incoming raw token
2. Find `PasswordResetToken` by hash — validate not used, not expired
3. Find user by `token.UserId`
4. Hash new password
5. `user.ChangePassword(newHash)` — raises `PasswordChangedEvent`
6. Mark token as used
7. `IUserRepository.RevokeAllRefreshTokensAsync(userId, ct)`
8. Return `Unit.Value`

#### `VerifyEmailCommand` / `VerifyEmailCommandHandler`

Steps:
1. Find `EmailVerificationCode` for userId, validate not used, not expired
2. Validate code matches
3. Load user, call `user.VerifyEmail()`
4. Mark code as used
5. Return `Unit.Value`

#### `ChangePasswordCommand` / `ChangePasswordCommandHandler`

Steps:
1. Load user via `ctx.CurrentUser.RequireId()`
2. Verify current password
3. `user.ChangePassword(newHash)` — raises `PasswordChangedEvent`
4. `IUserRepository.RevokeAllRefreshTokensAsync(userId, ct)`
5. Return `Unit.Value`

### 7.2 Seller Onboarding Commands

#### `SubmitSellerApplicationCommand` / `SubmitSellerApplicationCommandHandler`

Steps:
1. Verify user is email-verified
2. Check no active application exists: `ISellerApplicationRepository.FindActiveByUserIdAsync`
3. `SellerApplication.Submit(...)` — raises `SellerApplicationSubmittedEvent`
4. `_applicationRepo.Add(application)`
5. Return `ApplicationId`

#### `AssignSellerRoleHandler` — Domain Event Handler

Handles: `SellerApplicationApprovedEvent`

Steps:
1. Load user with roles: `FindByIdWithRolesAsync(event.ApplicantUserId)`
2. Load Seller role from `IRoleRepository`
3. `user.AssignRole(sellerRole, event.AdminId)`
4. Publish `IEventBus.PublishAsync(new SellerApprovedIntegrationEvent(userId))` → Catalog creates Storefront
5. `IUnitOfWork.CommitAsync()` — ⚠️ This is a domain event handler called in a **post-commit context**, so it needs its own transaction if it mutates state. In Phase 1, it runs in the same HTTP request transaction (pre-commit if declared as `IPreCommitDomainEvent`).

> **Architecture note**: `SellerApplicationApprovedEvent` should be declared as `IPreCommitDomainEvent` so the role assignment happens atomically in the same transaction as the approval.

### 7.3 Admin Commands

#### `AssignRoleCommand` / `AssignRoleCommandHandler`

```csharp
// Guards:
// - Cannot assign SystemAdmin
// - Only Administrator can assign Administrator role (check ctx.CurrentUser.IsAdmin)
// - Seller role requires check for approved application
steps:
1. Load user with roles
2. Load role
3. Guard checks (SystemAdmin forbidden, etc.)
4. user.AssignRole(role, ctx.CurrentUser.RequireId())
5. repo.Update(user)
```

#### `RevokeRoleCommand`, `UpdateRolePermissionsCommand`, `SetUserPermissionOverrideCommand`, `SuspendUserCommand`, `ReinstateUserCommand`

Follow same CQRS pattern. Each calls one domain method and lets the UoW filter handle commit.

#### `ApproveSellerApplicationCommand` / `RejectSellerApplicationCommand`

Steps:
1. Load application by ID
2. Call `application.Approve(adminId, note)` or `application.Reject(adminId, reason)`
3. `_applicationRepo.Update(application)`
4. Domain event raised → handled by `AssignSellerRoleHandler`

### 7.4 Validators

Create `FluentValidation` validators in `Auth/Validators/`:

**`RegisterCommandValidator.cs`**:
```csharp
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).MustBeValidEmail();
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MinimumLength(FieldLimits.DisplayName.Min)
            .MaximumLength(FieldLimits.DisplayName.Max)
            .MustBeInlineStandard()
            .WithMessage(ValidationMessages.TooLong("Display name", FieldLimits.DisplayName.Max));
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(AppConstants.Validation.PasswordMinLength)
            .MaximumLength(AppConstants.Validation.PasswordMaxLength)
            .Must(p => p.Any(char.IsUpper))
                .WithMessage(ValidationMessages.PasswordMustHaveUpper)
            .Must(p => p.Any(char.IsLower))
                .WithMessage(ValidationMessages.PasswordMustHaveLower)
            .Must(p => p.Any(char.IsDigit))
                .WithMessage(ValidationMessages.PasswordMustHaveDigit)
            .Must(p => p.Any(c => !char.IsLetterOrDigit(c)))
                .WithMessage(ValidationMessages.PasswordMustHaveSpecial);
    }
}
```

Add similar validators for: `LoginCommandValidator`, `ForgotPasswordCommandValidator`, `ResetPasswordCommandValidator`, `ChangePasswordCommandValidator`, `SubmitSellerApplicationCommandValidator`.

### ✅ Phase 7 Checklist

- [ ] `RegisterCommand` + `RegisterCommandHandler`
- [ ] `LoginCommand` + `LoginCommandHandler` + `LoginResult` DTO
- [ ] `LogoutCommand` + `LogoutCommandHandler`
- [ ] `RefreshTokenCommand` + `RefreshTokenCommandHandler`
- [ ] `ForgotPasswordCommand` + `ForgotPasswordCommandHandler`
- [ ] `ResetPasswordCommand` + `ResetPasswordCommandHandler`
- [ ] `VerifyEmailCommand` + `VerifyEmailCommandHandler`
- [ ] `ChangePasswordCommand` + `ChangePasswordCommandHandler`
- [ ] `SubmitSellerApplicationCommand` + `SubmitSellerApplicationCommandHandler`
- [ ] `AssignSellerRoleHandler` (domain event handler for `SellerApplicationApprovedEvent`)
- [ ] Admin commands: `AssignRole`, `RevokeRole`, `SuspendUser`, `ReinstateUser`, `ApproveSellerApplication`, `RejectSellerApplication`, `UpdateRolePermissions`, `SetUserPermissionOverride`
- [ ] All `FluentValidation` validators (use `ValidationMessages`, `FieldLimits`, `ValidatorExtensions`)
- [ ] `dotnet build` passes

---

## Phase 8 — Application: Queries + DTOs

> Define DTOs in `Application/Modules/{Feature}/Dtos/`

### 8.1 Key DTOs to create

```csharp
// LoginResult
public record LoginResult(string AccessToken, DateTimeOffset ExpiresAt, UserSummaryDto User);

// UserSummaryDto (used in lists + JWT payload)
public record UserSummaryDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required UserStatus Status { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}

// UserDetailDto (admin view)
public record UserDetailDto : UserSummaryDto
{
    public required bool EmailVerified { get; init; }
    public string? PhoneNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyList<UserPermissionOverrideDto> PermissionOverrides { get; init; } = [];
}

// SellerApplicationSummaryDto
public record SellerApplicationSummaryDto
{
    public required Guid Id { get; init; }
    public required string BusinessName { get; init; }
    public required SellerApplicationStatus Status { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
    // applicant info
    public required string ApplicantEmail { get; init; }
}
```

### 8.2 Query Records

```csharp
// Extends PagedQuery for filtering
public record GetUsersPagedQuery(
    UserStatus? Status = null
) : PagedQuery;   // PagedQuery provides Page, PageSize, Search, SortBy, Skip

public record GetSellerApplicationsPagedQuery(
    SellerApplicationStatus? Status = SellerApplicationStatus.Pending
) : PagedQuery;
```

### 8.3 Query Handlers

**`GetUsersPagedQueryHandler`**:
```csharp
public class GetUsersPagedQueryHandler(IGetUsersPagedQuery query)
    : IQueryHandler<GetUsersPagedQuery, PagedResult<UserSummaryDto>>
{
    public Task<PagedResult<UserSummaryDto>> Handle(GetUsersPagedQuery q, CancellationToken ct)
        => query.ExecuteAsync(q, ct);
}
```

### ✅ Phase 8 Checklist

- [ ] All DTOs created (`LoginResult`, `UserSummaryDto`, `UserDetailDto`, `SellerApplicationSummaryDto`, etc.)
- [ ] Query records: `GetUsersPagedQuery`, `GetSellerApplicationsPagedQuery`
- [ ] Query handlers for: `GetUsersPagedQuery`, `GetUserDetailQuery`, `GetSellerApplicationsPagedQuery`
- [ ] `dotnet build` passes

---

## Phase 9 — Web Layer: AccessFilter, HttpCurrentUser, Pages, DI Wiring

> **Where**: `src/MarketNest.Web/`

### 9.1 Update `CurrentUser.cs` to implement new `ICurrentUser` contract

`CurrentUser` now reads multi-role claims, `mn.store`, `mn.perm.*`:

```csharp
internal sealed class CurrentUser(ClaimsPrincipal principal) : ICurrentUser
{
    public bool IsAuthenticated => principal.Identity?.IsAuthenticated is true;
    public Guid? Id => TryParseGuid(ClaimTypes.NameIdentifier);
    public string? Name => principal.FindFirstValue(ClaimTypes.Name);
    public string? Email => principal.FindFirstValue(ClaimTypes.Email);
    public string? Role => Roles.FirstOrDefault();   // backward compat — first role

    // Multi-role: JWT "role" claim may appear multiple times
    public IReadOnlyList<string> Roles =>
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList().AsReadOnly();

    public Guid? SellerStoreId => TryParseGuid(IdentityConstants.ClaimTypes.SellerStoreId);

    // Permission checks — read per-module claims
    public bool HasPermission(OrderPermission required)     => CheckPerm(IdentityConstants.ClaimTypes.PermOrder, (long)required);
    public bool HasPermission(CatalogPermission required)   => CheckPerm(IdentityConstants.ClaimTypes.PermCatalog, (long)required);
    public bool HasPermission(StorefrontPermission required) => CheckPerm(IdentityConstants.ClaimTypes.PermStorefront, (long)required);
    public bool HasPermission(DisputePermission required)   => CheckPerm(IdentityConstants.ClaimTypes.PermDispute, (long)required);
    public bool HasPermission(ReviewPermission required)    => CheckPerm(IdentityConstants.ClaimTypes.PermReview, (long)required);
    public bool HasPermission(PaymentPermission required)   => CheckPerm(IdentityConstants.ClaimTypes.PermPayment, (long)required);
    public bool HasPermission(UserPermission required)      => CheckPerm(IdentityConstants.ClaimTypes.PermUser, (long)required);
    public bool HasPermission(ConfigPermission required)    => CheckPerm(IdentityConstants.ClaimTypes.PermConfig, (long)required);
    public bool HasPermission(PromotionPermission required) => CheckPerm(IdentityConstants.ClaimTypes.PermPromotion, (long)required);

    public Guid RequireId()
    {
        if (!IsAuthenticated || Id is null) throw new UnauthorizedException();
        return Id.Value;
    }

    private bool CheckPerm(string claimType, long required)
    {
        if (!IsAuthenticated) return false;
        var raw = principal.FindFirstValue(claimType);
        if (!long.TryParse(raw, out var effective)) return false;
        return (effective & required) == required;   // ALL required bits must be set
    }

    private Guid? TryParseGuid(string claimType)
        => Guid.TryParse(principal.FindFirstValue(claimType), out Guid id) ? id : null;
}
```

### 9.2 Create `AccessAttribute.cs`

```
src/MarketNest.Web/Infrastructure/Authorization/
├── AccessAttribute.cs
├── AccessFilter.cs
└── PermissionRequirement.cs
```

**`PermissionRequirement.cs`**:
```csharp
internal sealed class PermissionRequirement
{
    public string ClaimType { get; }
    public long Flags { get; }

    internal PermissionRequirement(string claimType, long flags)
    { ClaimType = claimType; Flags = flags; }

    public bool IsSatisfiedBy(ICurrentUser user)
    {
        // Administrator always passes (full access)
        if (user.IsAdmin) return true;
        return user switch
        {
            CurrentUser cu => cu.HasPermission(ClaimType, Flags),
            _              => false
        };
    }
}
```

> **Simplification**: Rather than 9 `HasPermission` method overloads on the internal `PermissionRequirement`, pass `(claimType, flags)` directly. `CurrentUser` exposes an internal `HasPermission(string claimType, long flags)` method used only by `PermissionRequirement`.

**`AccessAttribute.cs`** — one constructor per enum:
```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class AccessAttribute : Attribute
{
    public AccessAttribute(OrderPermission order)
        => Requirement = new PermissionRequirement(IdentityConstants.ClaimTypes.PermOrder, (long)order);

    public AccessAttribute(CatalogPermission catalog)
        => Requirement = new PermissionRequirement(IdentityConstants.ClaimTypes.PermCatalog, (long)catalog);

    public AccessAttribute(UserPermission user)
        => Requirement = new PermissionRequirement(IdentityConstants.ClaimTypes.PermUser, (long)user);

    // ... one per enum, same pattern ...

    internal PermissionRequirement Requirement { get; }
}
```

**`AccessFilter.cs`**:
```csharp
public class AccessFilter(ICurrentUser currentUser) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var attributes = context.ActionDescriptor.EndpointMetadata
            .OfType<AccessAttribute>()
            .ToList();

        if (attributes.Count == 0) return Task.CompletedTask;

        if (!currentUser.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        foreach (var attr in attributes)
        {
            if (!attr.Requirement.IsSatisfiedBy(currentUser))
            {
                context.Result = new ForbidResult();
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}
```

Register `AccessFilter` globally in `Program.cs`:
```csharp
builder.Services.AddScoped<AccessFilter>();
// In Configure<MvcOptions>:
options.Filters.AddService<AccessFilter>();
```

### 9.3 Register JWT Authentication in `Program.cs`

```csharp
// JWT Authentication (RS256)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var securityOptions = builder.Configuration
            .GetSection(SecurityOptions.Section).Get<SecurityOptions>()!;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = LoadRsaPublicKey(securityOptions.JwtPublicKey),
            ValidateIssuer   = true,
            ValidIssuer      = securityOptions.JwtIssuer,
            ValidateAudience = true,
            ValidAudience    = securityOptions.JwtAudience,
            ValidateLifetime = true,
            ClockSkew        = TimeSpan.FromSeconds(30),
        };
    });
```

Add to `SecurityOptions`:
```csharp
public class SecurityOptions
{
    public const string Section = "Security";
    public string JwtPublicKey  { get; init; } = string.Empty;
    public string JwtPrivateKey { get; init; } = string.Empty;  // stored in user-secrets/Key Vault
    public string JwtIssuer     { get; init; } = "marketnest";
    public string JwtAudience   { get; init; } = "marketnest-api";
    // existing options...
}
```

### 9.4 Update Razor Pages (Auth)

**`Login.cshtml.cs`** — wire to `LoginCommand`:
```csharp
public async Task<IActionResult> OnPostAsync()
{
    if (!ModelState.IsValid) return Page();

    var result = await _mediator.Send(new LoginCommand(Email, Password));
    if (result.IsFailure)
    {
        // Generic message — never enumerate
        ModelState.AddModelError(string.Empty, "Invalid email or password");
        return Page();
    }

    // Set refresh token cookie (HttpOnly, SameSite=Strict)
    Response.Cookies.Append("mn_refresh", result.Value.RefreshToken, new CookieOptions
    {
        HttpOnly = true,
        Secure   = true,
        SameSite = SameSiteMode.Strict,
        Expires  = DateTimeOffset.UtcNow.AddDays(7),
    });

    // Set access token in memory (JS will store it — or use a session cookie)
    // ...

    return LocalRedirect(ReturnUrl ?? AppRoutes.Home);
}
```

**`Register.cshtml.cs`** — wire to `RegisterCommand`.

**ForgotPassword, ResetPassword** — wire to respective commands.

### 9.5 Add Seller Application Pages

Create:
- `Pages/Seller/Apply.cshtml` + `Apply.cshtml.cs` → `SubmitSellerApplicationCommand`
- `Pages/Seller/ApplicationStatus.cshtml` + `.cs` → read-only status query

Add to `AppRoutes.Seller`:
```csharp
public const string Apply             = "/seller/apply";
public const string ApplicationStatus = "/seller/application/status";
```

Add to `WhitelistedPrefixes`.

### 9.6 Update Admin Pages

**`Admin/Users/Index.cshtml.cs`** — wire to `GetUsersPagedQuery`.

Create:
- `Pages/Admin/Users/Detail.cshtml` → `GetUserDetailQuery`
- `Pages/Admin/SellerApplications/Index.cshtml` → `GetSellerApplicationsPagedQuery`
- Add routes: `Admin.SellerApplications = "/admin/seller-applications"` in `AppRoutes`

### 9.7 Complete `DependencyInjection.cs` for Identity module

```csharp
public static IServiceCollection AddIdentityModule(
    this IServiceCollection services,
    IConfiguration configuration)
{
    string writeConnection = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection not set");
    string readConnection  = configuration.GetConnectionString("ReadConnection")
        is { Length: > 0 } rc ? rc : writeConnection;

    // Write context
    services.AddModuleDbContext<IdentityDbContext>(opts => opts.UseNpgsql(writeConnection));

    // Read context
    services.AddDbContext<IdentityReadDbContext>(opts =>
        opts.UseNpgsql(readConnection).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

    // Password hasher
    services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

    // Repositories
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IRoleRepository, RoleRepository>();
    services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    services.AddScoped<ISellerApplicationRepository, SellerApplicationRepository>();

    // Queries
    services.AddScoped<IGetUsersPagedQuery, GetUsersPagedQueryImpl>();
    services.AddScoped<IGetUserDetailQuery, GetUserDetailQueryImpl>();
    services.AddScoped<IGetSellerApplicationsPagedQuery, GetSellerApplicationsPagedQueryImpl>();

    // Services
    services.AddScoped<IPermissionResolver, PermissionResolver>();
    services.AddScoped<IJwtTokenService, JwtTokenService>();

    return services;
}
```

Update `Program.cs`:
- Add Identity assembly to `AddDatabaseInitializer(...)` call
- Add Identity assembly to `AddModuleInfrastructureServices(...)` call

### 9.8 Add LogEventIds for Identity handlers

In `LogEventId.cs` (Identity block 20000–29999), add events for handlers:

```csharp
// Application layer — 22000–25999
IdentityRegisterStart   = 22000,
IdentityRegisterSuccess = 22001,
IdentityRegisterFailed  = 22002,
IdentityLoginStart      = 22010,
IdentityLoginSuccess    = 22011,
IdentityLoginFailed     = 22012,
IdentityLoginLockedOut  = 22013,
// ... etc.
```

### ✅ Phase 9 Checklist

- [ ] `CurrentUser.cs` updated: multi-role, `SellerStoreId`, 9 `HasPermission` overloads
- [ ] `AccessAttribute.cs` created (9 constructors, one per enum)
- [ ] `AccessFilter.cs` created (bitwise check)
- [ ] `PermissionRequirement.cs` created
- [ ] `AccessFilter` registered globally in `Program.cs`
- [ ] JWT authentication configured in `Program.cs` (RS256)
- [ ] `SecurityOptions` updated with `JwtPublicKey`, `JwtPrivateKey`, `JwtIssuer`, `JwtAudience`
- [ ] `appsettings.json` + `.env.example` updated with new JWT keys
- [ ] Auth Razor Pages wired: Login, Register, ForgotPassword, ResetPassword, VerifyEmail
- [ ] Seller pages: Apply, ApplicationStatus
- [ ] Admin pages: Users list, User detail, SellerApplications queue
- [ ] `AppRoutes` updated: new seller + admin routes added to `WhitelistedPrefixes`
- [ ] `AddIdentityModule()` fully implemented
- [ ] Identity assembly added to `AddDatabaseInitializer` + `AddModuleInfrastructureServices`
- [ ] LogEventIds added for Identity application layer
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes

---

## Cross-Cutting: After All Phases

### Update `BackgroundJobRuntimeContext`

In `BackgroundJobRuntimeContext.cs`, update `ForSystemJob` to use the well-known SystemAdmin ID:

```csharp
public static IRuntimeContext ForSystemJob(string jobKey) =>
    new BackgroundJobRuntimeContext(
        userId: IdentityConstants.WellKnownIds.SystemAdminUserId,
        userName: "System Administrator",
        jobKey: jobKey);
```

### Update Existing Handlers That Use `currentUser.Role`

Anywhere that does `currentUser.Role == AppConstants.Roles.Admin` → replace with `currentUser.IsAdmin`.

Anywhere single-role assumption broke → replace with `currentUser.Roles.Contains(...)`.

### Add `IStorefrontReadService.GetStorefrontIdBySellerAsync`

The `JwtTokenService` needs the seller's store ID. Add this method to the `IStorefrontReadService` interface (in `Base.Common/Contracts/`):

```csharp
public interface IStorefrontReadService
{
    // ...existing...
    Task<Guid?> GetStorefrontIdBySellerAsync(Guid sellerId, CancellationToken ct);
}
```

Implement it in the Catalog module's `StorefrontReadService`.

---

## Final Build Checklist

```bash
# Build all projects
dotnet build

# Run all tests (unit + architecture)
dotnet test

# Run Identity-specific tests
dotnet test --filter "Category=Identity"

# Apply EF migrations (auto-applied on startup, but verify this works)
dotnet run --project src/MarketNest.Web
# Check DB: identity.roles should have 4 rows, identity.users should have 2 (SystemAdmin + AdminUser)
```

---

## Summary Table

| Phase | Focus | Key Files | Estimated Effort |
|-------|-------|-----------|-----------------|
| 1 | Base.Common: permission enums + ICurrentUser | `Permissions.cs`, `IdentityConstants.cs`, `ICurrentUser.cs` | ~2h |
| 2 | Domain: aggregates, entities, events | 11 event records, 5 entity classes | ~3h |
| 3 | Infrastructure: DbContexts, EF configs, migration | `IdentityDbContext.cs`, 6 configurations | ~3h |
| 4 | Infrastructure: repositories + queries | 4 repos, 3 query implementations | ~3h |
| 5 | Infrastructure: PermissionResolver + JwtTokenService | 2 services | ~4h |
| 6 | Infrastructure: seeders | `RoleSeeder.cs`, `AdminUserSeeder.cs` | ~2h |
| 7 | Application: commands + handlers | ~12 command handlers | ~8h |
| 8 | Application: queries + DTOs | ~4 query handlers, 5 DTOs | ~2h |
| 9 | Web: AccessFilter, pages, DI wiring | `AccessFilter.cs`, auth pages, Program.cs | ~5h |

**Total estimated: ~32h of focused coding**

---

## Quick Reference: File Path → Namespace Mapping

| File path | Namespace |
|-----------|-----------|
| `Identity/Domain/Entities/User.cs` | `MarketNest.Identity.Domain` |
| `Identity/Domain/Events/UserRegisteredEvent.cs` | `MarketNest.Identity.Domain` |
| `Identity/Application/Modules/Auth/Commands/RegisterCommand.cs` | `MarketNest.Identity.Application` |
| `Identity/Application/Modules/Auth/CommandHandlers/RegisterCommandHandler.cs` | `MarketNest.Identity.Application` |
| `Identity/Application/Modules/Auth/Repositories/IUserRepository.cs` | `MarketNest.Identity.Application` |
| `Identity/Application/Modules/Admin/Queries/IGetUsersPagedQuery.cs` | `MarketNest.Identity.Application` |
| `Identity/Infrastructure/Persistence/IdentityDbContext.cs` | `MarketNest.Identity.Infrastructure` |
| `Identity/Infrastructure/Repositories/Modules/Auth/UserRepository.cs` | `MarketNest.Identity.Infrastructure` |
| `Identity/Infrastructure/Queries/Modules/Admin/GetUsersPagedQuery.cs` | `MarketNest.Identity.Infrastructure` |
| `Identity/Infrastructure/Services/PermissionResolver.cs` | `MarketNest.Identity.Infrastructure` |
| `Identity/Infrastructure/Seeders/RoleSeeder.cs` | `MarketNest.Identity.Infrastructure` |
| `Web/Infrastructure/Authorization/AccessFilter.cs` | `MarketNest.Web.Infrastructure` |
| `Base.Common/Authorization/Permissions.cs` | `MarketNest.Base.Common` |
| `Base.Common/IdentityConstants.cs` | `MarketNest.Base.Common` |

