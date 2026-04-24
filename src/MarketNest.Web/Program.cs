using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
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
        .WriteTo.Console()
        .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

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
    builder.Services.AddValidatorsFromAssemblies([
        typeof(MarketNest.Identity.AssemblyReference).Assembly,
        typeof(MarketNest.Catalog.AssemblyReference).Assembly,
        typeof(MarketNest.Cart.AssemblyReference).Assembly,
        typeof(MarketNest.Orders.AssemblyReference).Assembly,
        typeof(MarketNest.Payments.AssemblyReference).Assembly,
        typeof(MarketNest.Reviews.AssemblyReference).Assembly,
        typeof(MarketNest.Disputes.AssemblyReference).Assembly,
    ]);

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

    // ── Database Initialize (dev only) ────────────────────────────────
    // TODO: Uncomment when DatabaseInitializer is implemented
    // if (app.Environment.IsDevelopment())
    // {
    //     using var scope = app.Services.CreateScope();
    //     await scope.ServiceProvider
    //                .GetRequiredService<DatabaseInitializer>()
    //                .InitializeAsync();
    // }

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

// Required for WebApplicationFactory in integration tests
public partial class Program;
