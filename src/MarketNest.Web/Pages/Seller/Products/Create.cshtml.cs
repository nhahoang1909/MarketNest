using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public partial class CreateModel(IAppLogger<CreateModel> logger) : PageModel
{
    // ── Bound input model ────────────────────────────────────────────────
    [BindProperty]
    public CreateProductInput Input { get; set; } = new();

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Returns validation errors for a given field, suitable for passing to shared form partials.</summary>
    public IEnumerable<string> GetErrors(string field)
    {
        string key = field.StartsWith("Input.", StringComparison.Ordinal) ? field : $"Input.{field}";
        return ModelState.TryGetValue(key, out Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateEntry? entry)
            ? entry.Errors.Select(e => e.ErrorMessage)
            : [];
    }

    // ── Handlers ─────────────────────────────────────────────────────────

    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext.TraceIdentifier);

    public IActionResult OnPost()
    {
        Log.InfoOnPost(logger, HttpContext.TraceIdentifier);

        if (!ModelState.IsValid)
            return Page();

        // TODO: dispatch CreateProductCommand via MediatR
        // var result = await _mediator.Send(new CreateProductCommand { ... });
        // if (result.IsFailure) { ModelState.AddModelError(...); return Page(); }
        // return RedirectToPage("./Index");

        return Page();
    }

    // ── Logging ──────────────────────────────────────────────────────────

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsCreateStart, LogLevel.Information,
            "SellerProductCreate OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.SellerProductsCreateStart + 1, LogLevel.Information,
            "SellerProductCreate OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}

/// <summary>Form binding model for the Create Product page.</summary>
public sealed class CreateProductInput
{
    [BindProperty] public string Name { get; init; } = string.Empty;
    [BindProperty] public string Slug { get; init; } = string.Empty;

    /// <summary>HTML string from the Trix rich text editor — sanitized server-side before persistence.</summary>
    [BindProperty] public string Description { get; init; } = string.Empty;

    [BindProperty] public string ShortDescription { get; init; } = string.Empty;
    [BindProperty] public decimal Price { get; init; }
    [BindProperty] public int Stock { get; init; }
    [BindProperty] public IFormFileCollection? Images { get; init; }
}
