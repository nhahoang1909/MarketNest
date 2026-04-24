using System.Globalization;
using FluentValidation;
using MarketNest.Web.Infrastructure;
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
    builder.Services.AddRazorPages();
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
    // TODO: Replace DbContext with MarketNestDbContext once created
    // builder.Services.AddDatabaseInitializer<MarketNestDbContext>(
    //     typeof(MarketNest.Identity.AssemblyReference).Assembly,
    //     typeof(MarketNest.Catalog.AssemblyReference).Assembly,
    //     typeof(MarketNest.Orders.AssemblyReference).Assembly
    // );

    // ── Build ─────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware Pipeline ───────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapRazorPages();
    app.MapHealthChecks("/health");

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
