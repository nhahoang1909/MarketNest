#region

using System.Reflection;
using FluentValidation;
using MarketNest.Admin.Infrastructure;
using MarketNest.Auditing.Infrastructure;
using MarketNest.Base.Domain;
using MarketNest.Cart.Infrastructure;
using MarketNest.Catalog.Infrastructure;
using MarketNest.Disputes.Infrastructure;
using MarketNest.Identity.Infrastructure;
using MarketNest.Notifications.Infrastructure;
using MarketNest.Orders.Infrastructure;
using MarketNest.Payments.Infrastructure;
using MarketNest.Promotions.Infrastructure;
using MarketNest.Reviews.Infrastructure;
using MarketNest.Web.BackgroundJobs;
using MarketNest.Web.Hosting;
using MarketNest.Web.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;

#endregion

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting MarketNest application");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────
    builder.Host.UseSerilog((context, loggerConfig) => loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty(AppConstants.SerilogApplicationProperty, AppConstants.AppName)
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.Seq(context.Configuration[AppConstants.SeqServerUrlKey]
                     ?? throw new InvalidOperationException(
                         $"Configuration key '{AppConstants.SeqServerUrlKey}' is not set. Add it to appsettings.json."),
            formatProvider: CultureInfo.InvariantCulture));

    // ── Redis + ICacheService ─────────────────────────────────────────────
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(
            builder.Configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration key 'Redis:ConnectionString' is not set.")));
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();

    // ── Tier 3 Options (system configuration — no DB) ─────────────────────
    builder.Services.Configure<PlatformOptions>(
        builder.Configuration.GetSection(PlatformOptions.Section));
    builder.Services.Configure<ValidationOptions>(
        builder.Configuration.GetSection(ValidationOptions.Section));
    builder.Services.Configure<SecurityOptions>(
        builder.Configuration.GetSection(SecurityOptions.Section));

    // ── Services ──────────────────────────────────────────────────────────
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

    // Unit of Work — Scoped: collects domain events and saves all module DbContexts in one pass.
    // Must be registered before AddRazorPages/AddControllers so filters can inject it.
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

    // Razor Page transaction filter — auto-wraps all OnPost* handlers; registered globally.
    builder.Services.AddScoped<RazorPageTransactionFilter>();

    // API controller transaction filter — activated by [Transaction] attribute on write controllers.
    builder.Services.AddScoped<TransactionActionFilter>();

    builder.Services.AddRazorPages()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization();
    builder.Services.AddAntiforgery();
    // Register controllers for module APIs
    builder.Services.AddControllers();
    builder.Services.AddHealthChecks();

    // Global filters: RazorPageTransactionFilter applies to all OnPost* Razor Pages;
    // TransactionActionFilter activates on write controllers with [Transaction] (e.g. WriteApiV1ControllerBase).
    // Both filters check HTTP verb and attributes internally — no double-execution.
    builder.Services.Configure<MvcOptions>(options =>
    {
        options.Filters.AddService<RazorPageTransactionFilter>();
        options.Filters.AddService<TransactionActionFilter>();
    });

    // OpenAPI documentation (replaces Swagger)
    builder.Services.AddOpenApi(AppConstants.OpenApi.DocumentName, options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = AppConstants.OpenApi.Title,
                Version = AppConstants.OpenApi.Version,
                Description = AppConstants.OpenApi.Description
            };
            return Task.CompletedTask;
        });
    });

    // Auto-generate docs/api-contract.md from OpenAPI spec (dev only)
    builder.Services.AddHttpClient();
    builder.Services.AddHostedService<ApiContractGenerator>();

    // MediatR — scan all module assemblies
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblies(
            typeof(Entity<>).Assembly,
            typeof(MarketNest.Auditing.AssemblyReference).Assembly,
            typeof(MarketNest.Identity.AssemblyReference).Assembly,
            typeof(MarketNest.Catalog.AssemblyReference).Assembly,
            typeof(MarketNest.Cart.AssemblyReference).Assembly,
            typeof(MarketNest.Orders.AssemblyReference).Assembly,
            typeof(MarketNest.Payments.AssemblyReference).Assembly,
            typeof(MarketNest.Reviews.AssemblyReference).Assembly,
            typeof(MarketNest.Disputes.AssemblyReference).Assembly,
            typeof(MarketNest.Notifications.AssemblyReference).Assembly,
            typeof(MarketNest.Admin.AssemblyReference).Assembly,
            typeof(MarketNest.Promotions.AssemblyReference).Assembly);

        // Auditing pipeline behavior — records [Audited] commands automatically
        cfg.AddOpenBehavior(typeof(AuditBehavior<,>));
    });

    // FluentValidation — discover validators from all module assemblies
    Assembly[] validatorAssemblies = new[]
    {
        typeof(MarketNest.Identity.AssemblyReference).Assembly,
        typeof(MarketNest.Catalog.AssemblyReference).Assembly,
        typeof(MarketNest.Cart.AssemblyReference).Assembly,
        typeof(MarketNest.Orders.AssemblyReference).Assembly,
        typeof(MarketNest.Payments.AssemblyReference).Assembly,
        typeof(MarketNest.Reviews.AssemblyReference).Assembly,
        typeof(MarketNest.Disputes.AssemblyReference).Assembly,
        typeof(MarketNest.Promotions.AssemblyReference).Assembly
    };

    foreach (Assembly assembly in validatorAssemblies) builder.Services.AddValidatorsFromAssembly(assembly);

    // IAppLogger<T> — open-generic registration, resolved per class
    builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

    // IEventBus — Phase 1: in-process via MediatR; Phase 3: swap to MassTransitEventBus
    builder.Services.AddSingleton<IEventBus, InProcessEventBus>();


    // IUserTimeZoneProvider — resolves user's time zone and date format from HTTP context
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserTimeZoneProvider, HttpContextUserTimeZoneProvider>();

    // IRuntimeContext — populated once per request by RuntimeContextMiddleware (after auth).
    // HttpRuntimeContext is the mutable scoped backing object; IRuntimeContext and ICurrentUser
    // resolve from the same Scoped instance — no extra allocations.
    builder.Services.AddScoped<HttpRuntimeContext>();
    builder.Services.AddScoped<IRuntimeContext>(sp => sp.GetRequiredService<HttpRuntimeContext>());
    builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<IRuntimeContext>().CurrentUser);

    // ── Module DI ─────────────────────────────────────────────────────────
    builder.Services.AddAuditingModule(builder.Configuration);
    builder.Services.AddIdentityModule(builder.Configuration);
    builder.Services.AddCatalogModule(builder.Configuration);
    builder.Services.AddCartModule(builder.Configuration);
    builder.Services.AddOrdersModule(builder.Configuration);
    builder.Services.AddPaymentsModule(builder.Configuration);
    builder.Services.AddReviewsModule(builder.Configuration);
    builder.Services.AddDisputesModule(builder.Configuration);
    builder.Services.AddNotificationsModule(builder.Configuration);
    builder.Services.AddAdminModule(builder.Configuration);
    builder.Services.AddPromotionsModule(builder.Configuration);

    // ── Auto-register all IBaseRepository<,> + IBaseQuery<,> implementors ───
    // Scans Infrastructure assemblies of every module and registers concrete
    // Query/Repository classes with ALL interfaces they implement as Scoped.
    // Add a module's AssemblyReference here once it has Repository/Query classes.
    builder.Services.AddModuleInfrastructureServices(
        typeof(MarketNest.Admin.AssemblyReference).Assembly,
        typeof(MarketNest.Catalog.AssemblyReference).Assembly,
        typeof(MarketNest.Promotions.AssemblyReference).Assembly
    );

    // Background jobs: registry + execution store + hosted runner (Phase 1)
    builder.Services.AddSingleton<IJobRegistry, ServiceCollectionJobRegistry>();
    builder.Services.AddScoped<IJobExecutionStore, NpgsqlJobExecutionStore>();
    builder.Services.AddHostedService<JobRunnerHostedService>();

    // ── Database: auto-migrate + seed ─────────────────────────────────

    // Register DatabaseInitializer + auto-discover seeders from module assemblies
    builder.Services.AddDatabaseInitializer(
        typeof(MarketNest.Admin.AssemblyReference).Assembly,
        typeof(MarketNest.Catalog.AssemblyReference).Assembly,
        typeof(MarketNest.Orders.AssemblyReference).Assembly,
        typeof(MarketNest.Payments.AssemblyReference).Assembly,
        typeof(MarketNest.Promotions.AssemblyReference).Assembly
    );

    // ── Build ─────────────────────────────────────────────────────────
    WebApplication app = builder.Build();

    // ── Middleware Pipeline ───────────────────────────────────────────
    app.UseExceptionHandler(AppRoutes.Error);

    if (!app.Environment.IsDevelopment()) app.UseHsts();

    app.UseStatusCodePagesWithReExecute(AppRoutes.NotFound);

    // ── OpenAPI + Scalar (interactive API docs) ─────────────────────
    app.MapOpenApi();
    if (app.Environment.IsDevelopment())
        app.MapScalarApiReference(options =>
        {
            options.WithTitle(AppConstants.OpenApi.Title);
            options.WithTheme(ScalarTheme.BluePlanet);
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // ── Localization ─────────────────────────────────────────────────
    string[] supportedCultures = AppConstants.Cultures.Supported;
    app.UseRequestLocalization(options =>
    {
        options.SetDefaultCulture(AppConstants.Cultures.Default);
        options.AddSupportedCultures(supportedCultures);
        options.AddSupportedUICultures(supportedCultures);
        options.RequestCultureProviders =
        [
            new CookieRequestCultureProvider { CookieName = AppConstants.Cultures.CookieName },
            new AcceptLanguageHeaderRequestCultureProvider()
        ];
    });

    app.UseSerilogRequestLogging();

    // ── Route Whitelist (after static files, before routing) ──────────
    app.UseMiddleware<RouteWhitelistMiddleware>();

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    // RuntimeContextMiddleware — MUST be after UseAuthentication/UseAuthorization so that
    // HttpContext.User is already populated with JWT claims when we build ICurrentUser.
    app.UseMiddleware<RuntimeContextMiddleware>();

    app.UseAntiforgery();

    app.MapRazorPages();
    app.MapControllers();
    app.MapHealthChecks(AppRoutes.Health);

    // ── Language switch endpoint ──────────────────────────────────────
    app.MapPost(AppRoutes.Api.SetLanguage,
        (HttpContext context, [FromForm] string culture, [FromForm] string? returnUrl) =>
        {
            if (culture is not (AppConstants.Cultures.English or AppConstants.Cultures.Vietnamese))
                culture = AppConstants.Cultures.Default;
            context.Response.Cookies.Append(
                AppConstants.Cultures.CookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(AppConstants.Cookies.CultureExpirationYears),
                    IsEssential = true
                });
            return Results.LocalRedirect(returnUrl ?? AppRoutes.Home);
        }).DisableAntiforgery();

    // ── Initialize database: migrate + seed ───────────────────────────
    await app.Services.GetRequiredService<DatabaseInitializer>()
        .InitializeAsync();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
