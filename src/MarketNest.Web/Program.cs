using System.Globalization;
using System.Reflection;
using FluentValidation;
using MarketNest.Admin.Application;
using MarketNest.Admin.Infrastructure;
using MarketNest.Auditing;
using MarketNest.Auditing.Infrastructure;
using MarketNest.Web.BackgroundJobs;
using MarketNest.Web.Hosting;
using MarketNest.Web.Infrastructure;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

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

    // ── Services ──────────────────────────────────────────────────────
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
    builder.Services.AddRazorPages()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization();
    builder.Services.AddAntiforgery();
    // Register controllers for module APIs
    builder.Services.AddControllers();
    builder.Services.AddHealthChecks();

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
            typeof(MarketNest.Base.Domain.Entity<>).Assembly,
            typeof(MarketNest.Auditing.AssemblyReference).Assembly,
            typeof(MarketNest.Identity.AssemblyReference).Assembly,
            typeof(MarketNest.Catalog.AssemblyReference).Assembly,
            typeof(MarketNest.Cart.AssemblyReference).Assembly,
            typeof(MarketNest.Orders.AssemblyReference).Assembly,
            typeof(MarketNest.Payments.AssemblyReference).Assembly,
            typeof(MarketNest.Reviews.AssemblyReference).Assembly,
            typeof(MarketNest.Disputes.AssemblyReference).Assembly,
            typeof(MarketNest.Notifications.AssemblyReference).Assembly,
            typeof(MarketNest.Admin.AssemblyReference).Assembly);

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
        typeof(MarketNest.Disputes.AssemblyReference).Assembly
    };

    foreach (Assembly assembly in validatorAssemblies) builder.Services.AddValidatorsFromAssembly(assembly);

    // IAppLogger<T> — open-generic registration, resolved per class
    builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

    // IEventBus — Phase 1: in-process via MediatR; Phase 3: swap to MassTransitEventBus
    builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

    // ── Auditing Module ──────────────────────────────────────────────
    builder.Services.AddScoped<AuditableInterceptor>();
    builder.Services.AddModuleDbContext<AuditingDbContext>(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString(AppConstants.DefaultConnectionStringName)));
    builder.Services.AddScoped<IAuditService, AuditService>();

    // ── Admin Module (tests) ─────────────────────────────────────────
    builder.Services.AddModuleDbContext<AdminDbContext>(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString(AppConstants.DefaultConnectionStringName)));
    // Read context — NoTracking, no migrations
    builder.Services.AddDbContext<AdminReadDbContext>(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString(AppConstants.DefaultConnectionStringName))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
    builder.Services.AddScoped<ITestRepository,
        TestRepository>();
    builder.Services.AddScoped<ITestQuery,
        TestQuery>();
    builder.Services.AddScoped<IGetTestsPagedQuery,
        TestQuery>();

    // IUserTimeZoneProvider — resolves user's time zone and date format from HTTP context
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserTimeZoneProvider, HttpContextUserTimeZoneProvider>();

    // TODO: Register module DI (each module exposes AddXxxModule extension)
    // builder.Services.AddIdentityModule(builder.Configuration);
    // builder.Services.AddCatalogModule(builder.Configuration);
    // builder.Services.AddCartModule(builder.Configuration);
    // builder.Services.AddOrdersModule(builder.Configuration);
    // builder.Services.AddPaymentsModule(builder.Configuration);
    // builder.Services.AddReviewsModule(builder.Configuration);
    // builder.Services.AddDisputesModule(builder.Configuration);
    // builder.Services.AddNotificationsModule(builder.Configuration);
    // builder.Services.AddAdminModule(builder.Configuration);

    // Background jobs: registry + execution store + hosted runner (Phase 1)
    builder.Services.AddSingleton<IJobRegistry, ServiceCollectionJobRegistry>();
    builder.Services.AddScoped<IJobExecutionStore, NpgsqlJobExecutionStore>();
    // Example/demo job registration — modules should register their own jobs instead
    builder.Services.AddSingleton<IBackgroundJob, TestTimerJob>();
    builder.Services.AddHostedService<JobRunnerHostedService>();

    // ── Database: auto-migrate + seed ─────────────────────────────────
    // TODO: Register module DbContexts as they are created:
    // builder.Services.AddModuleDbContext<IdentityDbContext>(opts =>
    //     opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    // builder.Services.AddModuleDbContext<CatalogDbContext>(opts =>
    //     opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    // ... (repeat for each module that has a DbContext)

    // Register DatabaseInitializer + auto-discover seeders from module assemblies
    // builder.Services.AddDatabaseInitializer(
    //     typeof(MarketNest.Identity.AssemblyReference).Assembly,
    //     typeof(MarketNest.Catalog.AssemblyReference).Assembly,
    //     typeof(MarketNest.Orders.AssemblyReference).Assembly
    // );

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
    // TODO: Uncomment when module DbContexts and DatabaseInitializer are registered
    // await app.Services.GetRequiredService<DatabaseInitializer>()
    //     .InitializeAsync();

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
