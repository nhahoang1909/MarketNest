namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Centralized shared view/partial paths used across the Web host.
/// Keep this file minimal; add new shared partial paths here so views can reference a single constant.
/// Namespace follows project convention: stop at the layer level (MarketNest.Web.Infrastructure).
/// </summary>
public static class SharedViewPaths
{
    // ── Display Components ───────────────────────────────────────────────
    public const string LoadingSpinner = "~/Pages/Shared/Display/_LoadingSpinner.cshtml";
    public const string Breadcrumb = "~/Pages/Shared/Display/_Breadcrumb.cshtml";
    public const string EmptyState = "~/Pages/Shared/Display/_EmptyState.cshtml";

    // ── Form Components ─────────────────────────────────────────────────
    public const string TextField = "~/Pages/Shared/Forms/_TextField.cshtml";
    public const string TextArea = "~/Pages/Shared/Forms/_TextArea.cshtml";
    public const string SlugField = "~/Pages/Shared/Forms/_SlugField.cshtml";
    public const string EmailField = "~/Pages/Shared/Forms/_EmailField.cshtml";
    public const string PhoneField = "~/Pages/Shared/Forms/_PhoneField.cshtml";
    public const string UrlField = "~/Pages/Shared/Forms/_UrlField.cshtml";
    public const string MoneyInput = "~/Pages/Shared/Forms/_MoneyInput.cshtml";
    public const string QuantityInput = "~/Pages/Shared/Forms/_QuantityInput.cshtml";
    public const string StockQuantityInput = "~/Pages/Shared/Forms/_StockQuantityInput.cshtml";
    public const string PercentageInput = "~/Pages/Shared/Forms/_PercentageInput.cshtml";
    public const string RatingInput = "~/Pages/Shared/Forms/_RatingInput.cshtml";
    public const string SelectField = "~/Pages/Shared/Forms/_SelectField.cshtml";
    public const string ImageUpload = "~/Pages/Shared/Forms/_ImageUpload.cshtml";
    public const string ExcelUpload = "~/Pages/Shared/Forms/_ExcelUpload.cshtml";
    public const string SearchInput = "~/Pages/Shared/Forms/_SearchInput.cshtml";
    public const string FormSection = "~/Pages/Shared/Forms/_FormSection.cshtml";
    public const string FormActions = "~/Pages/Shared/Forms/_FormActions.cshtml";
}

