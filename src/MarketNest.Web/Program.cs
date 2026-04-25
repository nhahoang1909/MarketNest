using System.Globalization;
using FluentValidation;
using MarketNest.Core.Logging;
using MarketNest.Web.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting MarketNest application");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────
    builder.Host.UseSerilog((context, loggerConfig) => loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "MarketNest")
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.Seq(serverUrl: context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341",
                     formatProvider: CultureInfo.InvariantCulture));

    // ── Services ──────────────────────────────────────────────────────
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
    builder.Services.AddRazorPages()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization();
    builder.Services.AddAntiforgery();
    builder.Services.AddHealthChecks();

    // MediatR — scan all module assemblies
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
        typeof(MarketNest.Core.Common.Entity<>).Assembly,
        typeof(MarketNest.Identity.AssemblyReference).Assembly,
        typeof(MarketNest.Catalog.AssemblyReference).Assembly,
        typeof(MarketNest.Cart.AssemblyReference).Assembly,
        typeof(MarketNest.Orders.AssemblyReference).Assembly,
        typeof(MarketNest.Payments.AssemblyReference).Assembly,
        typeof(MarketNest.Reviews.AssemblyReference).Assembly,
        typeof(MarketNest.Disputes.AssemblyReference).Assembly,
        typeof(MarketNest.Notifications.AssemblyReference).Assembly,
        typeof(MarketNest.Admin.AssemblyReference).Assembly
    ));

    // FluentValidation — discover validators from all module assemblies
    var validatorAssemblies = new[]
    {
        typeof(MarketNest.Identity.AssemblyReference).Assembly,
        typeof(MarketNest.Catalog.AssemblyReference).Assembly,
        typeof(MarketNest.Cart.AssemblyReference).Assembly,
        typeof(MarketNest.Orders.AssemblyReference).Assembly,
        typeof(MarketNest.Payments.AssemblyReference).Assembly,
        typeof(MarketNest.Reviews.AssemblyReference).Assembly,
        typeof(MarketNest.Disputes.AssemblyReference).Assembly,
    };

    foreach (var assembly in validatorAssemblies)
    {
        builder.Services.AddValidatorsFromAssembly(assembly);
    }

    // IAppLogger<T> — open-generic registration, resolved per class
    builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

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
    var app = builder.Build();

    // ── Middleware Pipeline ───────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler(AppRoutes.Error);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // ── Localization ─────────────────────────────────────────────────
    var supportedCultures = new[] { "en", "vi" };
    app.UseRequestLocalization(options =>
    {
        options.SetDefaultCulture("en");
        options.AddSupportedCultures(supportedCultures);
        options.AddSupportedUICultures(supportedCultures);
        options.RequestCultureProviders =
        [
            new CookieRequestCultureProvider { CookieName = ".MarketNest.Culture" },
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
    app.MapHealthChecks(AppRoutes.Health);

    // ── Language switch endpoint ──────────────────────────────────────
    app.MapPost(AppRoutes.Api.SetLanguage, (HttpContext context, string culture, string? returnUrl) =>
    {
        if (culture is not ("en" or "vi")) culture = "en";
        context.Response.Cookies.Append(
            ".MarketNest.Culture",
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
        return Results.LocalRedirect(returnUrl ?? "/");
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
