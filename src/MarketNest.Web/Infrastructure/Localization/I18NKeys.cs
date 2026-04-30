namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Single source of truth for all I18N resource keys.
///     Enforces ADR-005 (no magic strings) — never inline key strings at call sites.
/// </summary>
public static class I18NKeys
{
    public static class Page
    {
        public const string Login = "Page.Login";
        public const string Register = "Page.Register";
        public const string ForgotPassword = "Page.ForgotPassword";
        public const string Home = "Page.Home";
    }

    public static class Label
    {
        public const string Email = "Label.Email";
        public const string Password = "Label.Password";
        public const string ConfirmPassword = "Label.ConfirmPassword";
        public const string FullName = "Label.FullName";
        public const string YouAre = "Label.YouAre";
        public const string PasswordPlaceholder = "Label.Password.Placeholder";
        public const string PasswordMinLength = "Label.Password.MinLength";
        public const string ConfirmPasswordPlaceholder = "Label.ConfirmPassword.Placeholder";
        public const string FullNamePlaceholder = "Label.FullName.Placeholder";
    }

    public static class Button
    {
        public const string Login = "Button.Login";
        public const string Register = "Button.Register";
        public const string SendResetLink = "Button.SendResetLink";
        public const string LoginGoogle = "Button.LoginGoogle";
        public const string LoginFacebook = "Button.LoginFacebook";
        public const string ShopNow = "Button.ShopNow";
        public const string BecomeSeller = "Button.BecomeSeller";
        public const string OpenStoreFree = "Button.OpenStoreFree";
    }

    public static class Text
    {
        public const string RememberMe = "Text.RememberMe";
        public const string Or = "Text.Or";
        public const string NoAccount = "Text.NoAccount";
        public const string HasAccount = "Text.HasAccount";
        public const string ForgotPasswordDescription = "Text.ForgotPassword.Description";
        public const string AgreeTerms = "Text.AgreeTerms";
        public const string And = "Text.And";
        public const string TogglePassword = "Text.TogglePassword";
        public const string RoleBuyer = "Text.Role.Buyer";
        public const string RoleBuyerDesc = "Text.Role.Buyer.Desc";
        public const string RoleSeller = "Text.Role.Seller";
        public const string RoleSellerDesc = "Text.Role.Seller.Desc";
        public const string HeroHeadline = "Text.Hero.Headline";
        public const string HeroSubtitle = "Text.Hero.Subtitle";
        public const string CategoriesEyebrow = "Text.Categories.Eyebrow";
        public const string CategoriesTitle = "Text.Categories.Title";
        public const string FeaturedEyebrow = "Text.Featured.Eyebrow";
        public const string FeaturedTitle = "Text.Featured.Title";
        public const string EditorialQuote = "Text.Editorial.Quote";
        public const string EditorialAuthor = "Text.Editorial.Author";
        public const string EditorialCtaTitle = "Text.Editorial.CtaTitle";
        public const string EditorialCtaDesc = "Text.Editorial.CtaDesc";
        public const string CategoryFashion = "Text.Category.Fashion";
        public const string CategoryHome = "Text.Category.Home";
        public const string CategoryCeramics = "Text.Category.Ceramics";
        public const string CategoryAudio = "Text.Category.Audio";
        public const string CategoryStationery = "Text.Category.Stationery";
        public const string CategoryKitchen = "Text.Category.Kitchen";
        public const string CategoryLighting = "Text.Category.Lighting";
        public const string CategoryGarden = "Text.Category.Garden";
        public const string ProductCount = "Text.ProductCount";
        public const string ByShop = "Text.ByShop";
    }

    public static class Link
    {
        public const string ForgotPassword = "Link.ForgotPassword";
        public const string RegisterNow = "Link.RegisterNow";
        public const string Login = "Link.Login";
        public const string BackToLogin = "Link.BackToLogin";
        public const string TermsOfService = "Link.TermsOfService";
        public const string PrivacyPolicy = "Link.PrivacyPolicy";
    }

    // ── Existing layout keys (reference only — used via SharedLocalizer in layouts) ──

    public static class Nav
    {
        public const string Home = "Nav.Home";
        public const string Shop = "Nav.Shop";
        public const string Cart = "Nav.Cart";
        public const string Orders = "Nav.Orders";
        public const string Search = "Nav.Search";
        public const string SearchPlaceholder = "Nav.Search.Placeholder";
        public const string Wishlist = "Nav.Wishlist";
        public const string ThemeToggle = "Nav.ThemeToggle";
        public const string SwitchLanguage = "Nav.SwitchLanguage";
    }

    public static class Auth
    {
        public const string Login = "Auth.Login";
        public const string Logout = "Auth.Logout";
        public const string LoggedInAs = "Auth.LoggedInAs";
        public const string Register = "Auth.Register";
        public const string RegisterSeller = "Auth.RegisterSeller";
    }
}

