
namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Centralized EventId registry for all [LoggerMessage] delegates.
///
///     Each module owns a block of 1000 IDs. Sub-allocation within each block:
///       X000–X199  Infrastructure / Persistence layer
///       X200–X599  Application layer (Command/Query handlers)
///       X600–X799  Web Pages (PageModel handlers)
///       X800–X999  Reserved for future use
///
///     Usage in [LoggerMessage]:
///       [LoggerMessage((int)LogEventId.DbInitStart, LogLevel.Information, "...")]
/// </summary>
public enum LogEventId
{
    #region Infrastructure / Middleware — 1000-1999

    // DatabaseInitializer — 1000-1015
    DbInitStart = 1000,
    DbInitCompleted = 1001,
    DbInitNoContexts = 1002,
    DbInitModelUnchanged = 1003,
    DbInitNoMigrationFiles = 1004,
    DbInitHashChangedNoMigrations = 1005,
    DbInitApplyingMigrations = 1006,
    DbInitMigrationsApplied = 1007,
    DbInitMigrationCritical = 1008,
    DbInitNoSeeders = 1009,
    DbInitSeedEvaluating = 1010,
    DbInitSeedSkippedProd = 1011,
    DbInitSeedSkippedVersion = 1012,
    DbInitSeedRunning = 1013,
    DbInitSeedCompleted = 1014,
    DbInitSeedFailed = 1015,

    // DatabaseTracker — 1016-1020
    DbTrackerTablesEnsured = 1016,
    DbTrackerLockAcquired = 1017,
    DbTrackerLockReleased = 1018,
    DbTrackerHashSaved = 1019,
    DbTrackerSeedVersionSaved = 1020,

    // InProcessEventBus — 1040-1042
    EventBusPublishStart = 1040,
    EventBusPublishSuccess = 1041,
    EventBusPublishError = 1042,

    // MassTransitEventBus — 1050-1052 (Phase 3)
    MassTransitPublishStart = 1050,
    MassTransitPublishSuccess = 1051,
    MassTransitPublishError = 1052,

    // ApiContractGenerator — 1060-1063
    ApiContractFetchStart = 1060,
    ApiContractUpdated = 1061,
    ApiContractFetchFailed = 1062,
    ApiContractGenerationFailed = 1063,

    // RouteWhitelistMiddleware — 1070
    RouteBlocked = 1070,

    // UnitOfWork — 1071-1079
    UoWPreCommitDispatching = 1071,
    UoWPostCommitFailed = 1072,
    UoWCommitting = 1073,
    UoWTxBegin = 1074,
    UoWTxCommitted = 1075,
    UoWTxRolledBack = 1076,

    // RazorPageTransactionFilter — 1080-1089
    RazorPageTxBegin = 1080,
    RazorPageTxCommitted = 1081,
    RazorPageTxRolledBackOnResult = 1082,
    RazorPageTxRolledBackOnException = 1083,

    // TransactionActionFilter — 1090-1099
    ActionTxBegin = 1090,
    ActionTxCommitted = 1091,
    ActionTxRolledBackOnResult = 1092,
    ActionTxRolledBackOnException = 1093,

    // RuntimeContextMiddleware — 1094-1095
    RuntimeContextRequestStart = 1094,
    RuntimeContextRequestEnd = 1095,

    // Reserved — 1096-1999

    #endregion

    #region Identity — 2000-2999

    // Infrastructure layer — 2000-2199 (reserved for Identity DbContext, repositories)

    // Application layer — 2200-2599 (reserved for Identity handlers: Login, Register, etc.)

    // Auth Pages — 2600-2649
    AuthLoginStart = 2600,
    AuthLoginSuccess = 2601,
    AuthLoginFailed = 2602,
    AuthLoginError = 2603,
    AuthRegisterStart = 2610,
    AuthRegisterSuccess = 2611,
    AuthRegisterFailed = 2612,
    AuthRegisterError = 2613,
    AuthForgotPasswordStart = 2620,
    AuthForgotPasswordSuccess = 2621,
    AuthForgotPasswordError = 2622,

    // Account Pages — 2650-2699
    AccountOrdersIndexStart = 2650,
    AccountOrdersIndexSuccess = 2651,
    AccountOrdersDetailStart = 2652,
    AccountOrdersDetailSuccess = 2653,
    AccountOrdersDetailNotFound = 2654,
    AccountOrdersReviewStart = 2660,
    AccountOrdersReviewSuccess = 2661,
    AccountOrdersReviewFailed = 2662,
    AccountOrdersReviewError = 2663,
    AccountSettingsStart = 2670,
    AccountSettingsSuccess = 2671,
    AccountSettingsError = 2672,
    AccountDisputesIndexStart = 2680,
    AccountDisputesIndexSuccess = 2681,
    AccountDisputesDetailStart = 2682,
    AccountDisputesDetailSuccess = 2683,
    AccountDisputesDetailNotFound = 2684,

    // Reserved — 2700-2999

    #endregion

    #region Catalog — 3000-3999

    // Infrastructure layer — 3000-3199 (reserved for Catalog DbContext, repositories)

    // Application layer — 3200-3599
    CatalogSetSalePriceStart = 3200,
    CatalogSetSalePriceSuccess = 3201,
    CatalogSetSalePriceFailed = 3202,
    CatalogRemoveSalePriceStart = 3210,
    CatalogRemoveSalePriceSuccess = 3211,
    CatalogRemoveSalePriceFailed = 3212,

    // Timer Jobs — 3100-3199
    CatalogSaleExpiryJobStart = 3100,
    CatalogSaleExpiryJobExpired = 3101,
    CatalogSaleExpiryJobCompleted = 3102,
    CatalogSaleExpiryJobError = 3103,

    // Seller/Products Pages — 3600-3649
    SellerProductsIndexStart = 3600,
    SellerProductsIndexSuccess = 3601,
    SellerProductsIndexError = 3602,
    SellerProductsCreateStart = 3610,
    SellerProductsCreateSuccess = 3611,
    SellerProductsCreateFailed = 3612,
    SellerProductsCreateError = 3613,
    SellerProductsEditStart = 3620,
    SellerProductsEditSuccess = 3621,
    SellerProductsEditNotFound = 3622,
    SellerProductsEditFailed = 3623,
    SellerProductsEditError = 3624,
    SellerProductsVariantsStart = 3630,
    SellerProductsVariantsSuccess = 3631,
    SellerProductsVariantsNotFound = 3632,
    SellerProductsVariantsError = 3633,

    // Shop / Search Pages — 3700-3749
    ShopIndexStart = 3700,
    ShopIndexSuccess = 3701,
    ShopIndexError = 3702,
    ShopProductDetailStart = 3710,
    ShopProductDetailSuccess = 3711,
    ShopProductDetailNotFound = 3712,
    ShopProductDetailError = 3713,
    SearchIndexStart = 3720,
    SearchIndexSuccess = 3721,
    SearchIndexError = 3722,

    // Reserved — 3750-3999

    #endregion

    #region Cart — 4000-4999

    // Infrastructure layer — 4000-4199 (reserved for Cart DbContext, repositories)

    // Application layer — 4200-4599 (reserved for Cart handlers: AddToCart, RemoveFromCart, etc.)

    // Cart Pages — 4600-4649
    CartIndexStart = 4600,
    CartIndexSuccess = 4601,
    CartIndexError = 4602,

    // Reserved — 4650-4999

    #endregion

    #region Orders — 5000-5999

    // Infrastructure layer — 5000-5199 (reserved for Orders DbContext, repositories)

    // Application layer — 5200-5599 (reserved for Orders handlers: PlaceOrder, CancelOrder, etc.)

    // Checkout Pages — 5600-5649
    CheckoutIndexStart = 5600,
    CheckoutIndexSuccess = 5601,
    CheckoutIndexFailed = 5602,
    CheckoutIndexError = 5603,

    // Seller/Orders + Confirmation Pages — 5700-5749
    SellerOrdersIndexStart = 5700,
    SellerOrdersIndexSuccess = 5701,
    SellerOrdersIndexError = 5702,
    SellerOrdersDetailStart = 5710,
    SellerOrdersDetailSuccess = 5711,
    SellerOrdersDetailNotFound = 5712,
    SellerOrdersDetailError = 5713,
    OrdersConfirmationStart = 5720,
    OrdersConfirmationSuccess = 5721,
    OrdersConfirmationNotFound = 5722,

    // Reserved — 5750-5999

    #endregion

    #region Payments — 6000-6999

    // Infrastructure layer — 6000-6199 (reserved)
    // Application layer — 6200-6599 (reserved)

    // Timer Jobs — 6100-6109
    PaymentsReconciliationJobStart = 6100,
    PaymentsReconciliationJobMismatch = 6101,
    PaymentsReconciliationJobOrphan = 6102,
    PaymentsReconciliationJobNegativePayout = 6103,
    PaymentsReconciliationJobCompleted = 6104,

    // Web Pages — 6600-6799 (reserved)
    // Reserved — 6800-6999

    #endregion

    #region Reviews — 7000-7999

    // Infrastructure layer — 7000-7199 (reserved)
    // Application layer — 7200-7599 (reserved)
    // Web Pages — 7600-7799 (reserved)
    // Reserved — 7800-7999

    #endregion

    #region Disputes — 8000-8999

    // Infrastructure layer — 8000-8199 (reserved)
    // Application layer — 8200-8599 (reserved)
    // Web Pages — 8600-8799 (reserved)
    // Reserved — 8800-8999

    #endregion

    #region Notifications — 9000-9999

    // Infrastructure layer — 9000-9199 (reserved)
    // Application layer — 9200-9599 (reserved)
    // Web Pages — 9600-9799 (reserved)
    // Reserved — 9800-9999

    #endregion

    #region Admin — 10000-10999

    // Infrastructure layer — 10000-10199 (reserved for Admin DbContext, repositories)

    // Application Handlers — 10200-10249
    AdminCreateTestStart = 10200,
    AdminCreateTestSuccess = 10201,
    AdminCreateTestError = 10202,
    AdminUpdateTestStart = 10210,
    AdminUpdateTestSuccess = 10211,
    AdminUpdateTestError = 10212,
    AdminGetTestByIdStart = 10220,
    AdminGetTestByIdSuccess = 10221,
    AdminGetTestByIdNotFound = 10222,
    AdminGetTestsPagedStart = 10230,
    AdminGetTestsPagedSuccess = 10231,

    // Reserved Application — 10250-10599

    // Admin Pages — 10600-10649
    AdminDashboardStart = 10600,
    AdminDashboardSuccess = 10601,
    AdminConfigIndexStart = 10610,
    AdminConfigIndexSuccess = 10611,
    AdminConfigCommissionStart = 10620,
    AdminConfigCommissionSuccess = 10621,
    AdminConfigCommissionFailed = 10622,
    AdminConfigCommissionError = 10623,
    AdminConfigCountryStart = 10680,
    AdminConfigGenderStart = 10682,
    AdminConfigPhoneCodeStart = 10684,
    AdminConfigProductCategoryStart = 10686,
    AdminConfigNationalityStart = 10688,
    AdminUsersIndexStart = 10630,
    AdminUsersIndexSuccess = 10631,
    AdminProductsIndexStart = 10640,
    AdminProductsIndexSuccess = 10641,
    AdminStorefrontsIndexStart = 10650,
    AdminStorefrontsIndexSuccess = 10651,
    AdminDisputesIndexStart = 10660,
    AdminDisputesIndexSuccess = 10661,
    AdminNotificationsIndexStart = 10670,
    AdminNotificationsIndexSuccess = 10671,

    // Seller Pages — 10700-10749
    SellerDashboardStart = 10700,
    SellerDashboardSuccess = 10701,
    SellerDashboardError = 10702,
    SellerStorefrontStart = 10710,
    SellerStorefrontSuccess = 10711,
    SellerStorefrontError = 10712,
    SellerReviewsIndexStart = 10720,
    SellerReviewsIndexSuccess = 10721,
    SellerReviewsIndexError = 10722,
    SellerDisputesIndexStart = 10730,
    SellerDisputesIndexSuccess = 10731,
    SellerDisputesIndexError = 10732,
    SellerPayoutsIndexStart = 10740,
    SellerPayoutsIndexSuccess = 10741,
    SellerPayoutsIndexError = 10742,

    // Reserved — 10750-10999

    #endregion

    #region Auditing — 11000-11999

    // AuditService — 11000-11009
    AuditSaveStart = 11000,
    AuditSaveSuccess = 11001,
    AuditSaveError = 11002,

    // AuditBehavior — 11010-11019
    AuditBehaviorCapturing = 11010,
    AuditBehaviorSuccess = 11011,
    AuditBehaviorError = 11012,

    // AuditableInterceptor — 11020-11029
    AuditInterceptorEntityChanged = 11020,
    AuditInterceptorError = 11021,

    // PerformanceBehavior — 11030-11039
    PerfBehaviorSlowRequest = 11030,
    PerfBehaviorCriticalRequest = 11031,

    // Reserved — 11040-11999

    #endregion

    #region Background Jobs — 12000-12999

    // JobRunnerHostedService — 12000-12019
    JobRunnerStarting = 12000,
    JobRunnerJobExecuting = 12001,
    JobRunnerJobCompleted = 12002,
    JobRunnerJobFailed = 12003,
    JobRunnerStopping = 12004,
    JobRunnerStopped = 12005,

    // TestTimerJob — 12100-12109
    TestTimerJobStart = 12100,
    TestTimerJobCompleted = 12101,
    TestTimerJobError = 12102,

    // Reserved — 12110-12999

    #endregion

    #region Promotions — 14000-14999

    // Infrastructure layer — 14000-14199 (reserved for Promotions DbContext, repositories)

    // Application Handlers — 14200-14299
    PromotionsCreateVoucherStart = 14200,
    PromotionsCreateVoucherSuccess = 14201,
    PromotionsCreateVoucherError = 14202,
    PromotionsActivateVoucherStart = 14210,
    PromotionsActivateVoucherSuccess = 14211,
    PromotionsActivateVoucherError = 14212,
    PromotionsPauseVoucherStart = 14220,
    PromotionsPauseVoucherSuccess = 14221,
    PromotionsPauseVoucherError = 14222,

    // Timer Jobs — 14300-14399
    PromotionsExpiryJobStart = 14300,
    PromotionsExpiryJobExpired = 14301,
    PromotionsExpiryJobDepleted = 14302,
    PromotionsExpiryJobError = 14303,
    PromotionsExpiryJobCompleted = 14304,

    // Reserved — 14400-14999

    #endregion

    #region Web / Global Pages — 13000-13999

    // Global Pages (Error, Index, NotFound) — 13000-13019
    GlobalErrorDisplayed = 13000,
    GlobalIndexStart = 13010,
    GlobalIndexSuccess = 13011,
    GlobalNotFoundDisplayed = 13020,

    // Reserved — 13021-13999

    #endregion
}
