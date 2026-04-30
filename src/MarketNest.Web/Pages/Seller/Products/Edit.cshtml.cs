using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public partial class EditModel(IAppLogger<EditModel> logger) : PageModel
{
    // ── Route value ──────────────────────────────────────────────────────
    [BindProperty(SupportsGet = true)]
    public Guid ProductId { get; set; }

    // ── Bound input model ────────────────────────────────────────────────
    [BindProperty]
    public EditProductInput Input { get; set; } = new();

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
    {
        Log.InfoOnGet(logger, ProductId, HttpContext.TraceIdentifier);

        // TODO: load product from query via MediatR (GetProductByIdQuery)
        // var product = await _mediator.Send(new GetProductByIdQuery(ProductId));
        // if (product.IsFailure) { RedirectToPage("./Index"); return; }
        // Input = new EditProductInput { Name = product.Value.Name, ... };
    }

    public IActionResult OnPost()
    {
        Log.InfoOnPost(logger, ProductId, HttpContext.TraceIdentifier);

        if (!ModelState.IsValid)
            return Page();

        // TODO: dispatch UpdateProductCommand via MediatR
        // var result = await _mediator.Send(new UpdateProductCommand { ProductId = ProductId, ... });
        // if (result.IsFailure) { ModelState.AddModelError(...); return Page(); }
        // return RedirectToPage("./Index");

        return Page();
    }

    // ── Logging ──────────────────────────────────────────────────────────

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsEditStart, LogLevel.Information,
            "SellerProductEdit OnGet Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid productId, string correlationId);

        [LoggerMessage((int)LogEventId.SellerProductsEditStart + 1, LogLevel.Information,
            "SellerProductEdit OnPost Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, Guid productId, string correlationId);
    }
}

/// <summary>Form binding model for the Edit Product page.</summary>
public sealed class EditProductInput
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
