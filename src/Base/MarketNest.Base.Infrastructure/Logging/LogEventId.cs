namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Centralized EventId registry for all [LoggerMessage] delegates.
///
///     Each module owns a block of 10,000 IDs. Sub-allocation within each block:
///       X0000–X1999  Infrastructure / Persistence layer
///       X2000–X5999  Application layer (Command/Query handlers)
///       X6000–X7999  Web Pages (PageModel handlers)
///       X8000–X9999  Reserved for future use
///
///     Usage in [LoggerMessage]:
///       [LoggerMessage((int)LogEventId.DbInitStart, LogLevel.Information, "...")]
/// </summary>
public enum LogEventId
{
    #region Infrastructure / Middleware — 10000-19999

    // DatabaseInitializer — 10000-10050
    DbInitStart = 10000,
    DbInitCompleted = 10001,
    DbInitNoContexts = 10002,
    DbInitModelUnchanged = 10003,
    DbInitNoMigrationFiles = 10004,
    DbInitHashChangedNoMigrations = 10005,
    DbInitApplyingMigrations = 10006,
    DbInitMigrationsApplied = 10007,
    DbInitMigrationCritical = 10008,
    DbInitNoSeeders = 10009,
    DbInitSeedEvaluating = 10010,
    DbInitSeedSkippedProd = 10011,
    DbInitSeedSkippedVersion = 10012,
    DbInitSeedRunning = 10013,
    DbInitSeedCompleted = 10014,
    DbInitSeedFailed = 10015,

    // DatabaseTracker — 10060-10070
    DbTrackerTablesEnsured = 10060,
    DbTrackerLockAcquired = 10061,
    DbTrackerLockReleased = 10062,
    DbTrackerHashSaved = 10063,
    DbTrackerSeedVersionSaved = 10064,

    // InProcessEventBus — 10100-10110
    EventBusPublishStart = 10100,
    EventBusPublishSuccess = 10101,
    EventBusPublishError = 10102,

    // MassTransitEventBus — 10120-10130 (Phase 3)
    MassTransitPublishStart = 10120,
    MassTransitPublishSuccess = 10121,
    MassTransitPublishError = 10122,

    // ApiContractGenerator — 10200-10210
    ApiContractFetchStart = 10200,
    ApiContractUpdated = 10201,
    ApiContractFetchFailed = 10202,
    ApiContractGenerationFailed = 10203,

    // RouteWhitelistMiddleware — 10300
    RouteBlocked = 10300,

    // UnitOfWork — 10400-10420
    UoWPreCommitDispatching = 10400,
    UoWPostCommitFailed = 10401,
    UoWCommitting = 10402,
    UoWTxBegin = 10403,
    UoWTxCommitted = 10404,
    UoWTxRolledBack = 10405,

    // RazorPageTransactionFilter — 10500-10520
    RazorPageTxBegin = 10500,
    RazorPageTxCommitted = 10501,
    RazorPageTxRolledBackOnResult = 10502,
    RazorPageTxRolledBackOnException = 10503,

    // TransactionActionFilter — 10600-10620
    ActionTxBegin = 10600,
    ActionTxCommitted = 10601,
    ActionTxRolledBackOnResult = 10602,
    ActionTxRolledBackOnException = 10603,

    // RuntimeContextMiddleware — 10700-10710
    RuntimeContextRequestStart = 10700,
    RuntimeContextRequestEnd = 10701,

    // Reserved — 10800-19999

    #endregion

    #region Identity — 20000-29999

    // Infrastructure layer — 20000-21999 (reserved for Identity DbContext, repositories)

    // Application layer — 22000-25999 (reserved for Identity handlers: Login, Register, etc.)

    // Auth Pages — 26000-26199
    AuthLoginStart = 26000,
    AuthLoginSuccess = 26001,
    AuthLoginFailed = 26002,
    AuthLoginError = 26003,
    AuthRegisterStart = 26010,
    AuthRegisterSuccess = 26011,
    AuthRegisterFailed = 26012,
    AuthRegisterError = 26013,
    AuthForgotPasswordStart = 26020,
    AuthForgotPasswordSuccess = 26021,
    AuthForgotPasswordError = 26022,

    // Account Pages — 26200-26599
    AccountOrdersIndexStart = 26200,
    AccountOrdersIndexSuccess = 26201,
    AccountOrdersDetailStart = 26210,
    AccountOrdersDetailSuccess = 26211,
    AccountOrdersDetailNotFound = 26212,
    AccountOrdersReviewStart = 26220,
    AccountOrdersReviewSuccess = 26221,
    AccountOrdersReviewFailed = 26222,
    AccountOrdersReviewError = 26223,
    AccountSettingsStart = 26300,
    AccountSettingsSuccess = 26301,
    AccountSettingsError = 26302,
    AccountDisputesIndexStart = 26400,
    AccountDisputesIndexSuccess = 26401,
    AccountDisputesDetailStart = 26410,
    AccountDisputesDetailSuccess = 26411,
    AccountDisputesDetailNotFound = 26412,

    // Reserved — 27000-29999

    #endregion

    #region Catalog — 30000-39999

    // Infrastructure layer — 30000-31999 (reserved for Catalog DbContext, repositories)

    // Timer Jobs — 31000-31099
    CatalogSaleExpiryJobStart = 31000,
    CatalogSaleExpiryJobExpired = 31001,
    CatalogSaleExpiryJobCompleted = 31002,
    CatalogSaleExpiryJobError = 31003,

    // Application layer — 32000-35999
    CatalogSetSalePriceStart = 32000,
    CatalogSetSalePriceSuccess = 32001,
    CatalogSetSalePriceFailed = 32002,
    CatalogRemoveSalePriceStart = 32010,
    CatalogRemoveSalePriceSuccess = 32011,
    CatalogRemoveSalePriceFailed = 32012,

    // Seller/Products Pages — 36000-36499
    SellerProductsIndexStart = 36000,
    SellerProductsIndexSuccess = 36001,
    SellerProductsIndexError = 36002,
    SellerProductsCreateStart = 36010,
    SellerProductsCreateSuccess = 36011,
    SellerProductsCreateFailed = 36012,
    SellerProductsCreateError = 36013,
    SellerProductsEditStart = 36020,
    SellerProductsEditSuccess = 36021,
    SellerProductsEditNotFound = 36022,
    SellerProductsEditFailed = 36023,
    SellerProductsEditError = 36024,
    SellerProductsVariantsStart = 36030,
    SellerProductsVariantsSuccess = 36031,
    SellerProductsVariantsNotFound = 36032,
    SellerProductsVariantsError = 36033,

    // Shop / Search Pages — 37000-37499
    ShopIndexStart = 37000,
    ShopIndexSuccess = 37001,
    ShopIndexError = 37002,
    ShopProductDetailStart = 37010,
    ShopProductDetailSuccess = 37011,
    ShopProductDetailNotFound = 37012,
    ShopProductDetailError = 37013,
    SearchIndexStart = 37020,
    SearchIndexSuccess = 37021,
    SearchIndexError = 37022,

    // Reserved — 38000-39999

    #endregion

    #region Cart — 40000-49999

    // Infrastructure layer — 40000-41999 (reserved for Cart DbContext, repositories)

    // Application layer — 42000-45999 (reserved for Cart handlers: AddToCart, RemoveFromCart, etc.)

    // Cart Pages — 46000-46499
    CartIndexStart = 46000,
    CartIndexSuccess = 46001,
    CartIndexError = 46002,

    // Reserved — 47000-49999

    #endregion

    #region Orders — 50000-59999

    // Infrastructure layer — 50000-51999 (reserved for Orders DbContext, repositories)

    // Application layer — 52000-55999 (reserved for Orders handlers: PlaceOrder, CancelOrder, etc.)

    // Checkout Pages — 56000-56499
    CheckoutIndexStart = 56000,
    CheckoutIndexSuccess = 56001,
    CheckoutIndexFailed = 56002,
    CheckoutIndexError = 56003,

    // Seller/Orders + Confirmation Pages — 57000-57499
    SellerOrdersIndexStart = 57000,
    SellerOrdersIndexSuccess = 57001,
    SellerOrdersIndexError = 57002,
    SellerOrdersDetailStart = 57010,
    SellerOrdersDetailSuccess = 57011,
    SellerOrdersDetailNotFound = 57012,
    SellerOrdersDetailError = 57013,
    OrdersConfirmationStart = 57020,
    OrdersConfirmationSuccess = 57021,
    OrdersConfirmationNotFound = 57022,

    // Reserved — 58000-59999

    #endregion

    #region Payments — 60000-69999

    // Infrastructure layer — 60000-61999 (reserved)
    // Application layer — 62000-65999 (reserved)

    // Timer Jobs — 61000-61099
    PaymentsReconciliationJobStart = 61000,
    PaymentsReconciliationJobMismatch = 61001,
    PaymentsReconciliationJobOrphan = 61002,
    PaymentsReconciliationJobNegativePayout = 61003,
    PaymentsReconciliationJobCompleted = 61004,

    // Web Pages — 66000-67999 (reserved)
    // Reserved — 68000-69999

    #endregion

    #region Reviews — 70000-79999

    // Infrastructure layer — 70000-71999 (reserved)
    // Application layer — 72000-75999 (reserved)
    // Web Pages — 76000-77999 (reserved)
    // Reserved — 78000-79999

    #endregion

    #region Disputes — 80000-89999

    // Infrastructure layer — 80000-81999 (reserved)
    // Application layer — 82000-85999 (reserved)
    // Web Pages — 86000-87999 (reserved)
    // Reserved — 88000-89999

    #endregion

    #region Notifications — 90000-99999

    // Infrastructure layer — 90000-91999 (reserved)
    // Application layer — 92000-95999 (reserved)
    // Web Pages — 96000-97999 (reserved)
    // Reserved — 98000-99999

    #endregion

    #region Admin — 100000-109999

    // Infrastructure layer — 100000-101999 (reserved for Admin DbContext, repositories)

    // Application Handlers — 102000-102499
    AdminCreateTestStart = 102000,
    AdminCreateTestSuccess = 102001,
    AdminCreateTestError = 102002,
    AdminUpdateTestStart = 102010,
    AdminUpdateTestSuccess = 102011,
    AdminUpdateTestError = 102012,
    AdminGetTestByIdStart = 102020,
    AdminGetTestByIdSuccess = 102021,
    AdminGetTestByIdNotFound = 102022,
    AdminGetTestsPagedStart = 102030,
    AdminGetTestsPagedSuccess = 102031,

    // Reserved Application — 102500-105999

    // Admin Pages — 106000-106999
    AdminDashboardStart = 106000,
    AdminDashboardSuccess = 106001,
    AdminConfigIndexStart = 106010,
    AdminConfigIndexSuccess = 106011,
    AdminConfigCommissionStart = 106020,
    AdminConfigCommissionSuccess = 106021,
    AdminConfigCommissionFailed = 106022,
    AdminConfigCommissionError = 106023,
    AdminConfigCountryStart = 106080,
    AdminConfigGenderStart = 106082,
    AdminConfigPhoneCodeStart = 106084,
    AdminConfigProductCategoryStart = 106086,
    AdminConfigNationalityStart = 106088,
    AdminUsersIndexStart = 106100,
    AdminUsersIndexSuccess = 106101,
    AdminProductsIndexStart = 106110,
    AdminProductsIndexSuccess = 106111,
    AdminStorefrontsIndexStart = 106120,
    AdminStorefrontsIndexSuccess = 106121,
    AdminDisputesIndexStart = 106130,
    AdminDisputesIndexSuccess = 106131,
    AdminNotificationsIndexStart = 106140,
    AdminNotificationsIndexSuccess = 106141,

    // Seller Pages — 107000-107499
    SellerDashboardStart = 107000,
    SellerDashboardSuccess = 107001,
    SellerDashboardError = 107002,
    SellerStorefrontStart = 107010,
    SellerStorefrontSuccess = 107011,
    SellerStorefrontError = 107012,
    SellerReviewsIndexStart = 107020,
    SellerReviewsIndexSuccess = 107021,
    SellerReviewsIndexError = 107022,
    SellerDisputesIndexStart = 107030,
    SellerDisputesIndexSuccess = 107031,
    SellerDisputesIndexError = 107032,
    SellerPayoutsIndexStart = 107040,
    SellerPayoutsIndexSuccess = 107041,
    SellerPayoutsIndexError = 107042,

    // Reserved — 108000-109999

    #endregion

    #region Auditing — 110000-119999

    // AuditService — 110000-110099
    AuditSaveStart = 110000,
    AuditSaveSuccess = 110001,
    AuditSaveError = 110002,

    // AuditBehavior — 110100-110199
    AuditBehaviorCapturing = 110100,
    AuditBehaviorSuccess = 110101,
    AuditBehaviorError = 110102,

    // AuditableInterceptor — 110200-110299
    AuditInterceptorEntityChanged = 110200,
    AuditInterceptorError = 110201,

    // PerformanceBehavior — 110300-110399
    PerfBehaviorSlowRequest = 110300,
    PerfBehaviorCriticalRequest = 110301,

    // Reserved — 111000-119999

    #endregion

    #region Background Jobs — 120000-129999

    // JobRunnerHostedService — 120000-120099
    JobRunnerStarting = 120000,
    JobRunnerJobExecuting = 120001,
    JobRunnerJobCompleted = 120002,
    JobRunnerJobFailed = 120003,
    JobRunnerStopping = 120004,
    JobRunnerStopped = 120005,

    // TestTimerJob — 121000-121099
    TestTimerJobStart = 121000,
    TestTimerJobCompleted = 121001,
    TestTimerJobError = 121002,

    // Reserved — 122000-129999

    #endregion

    #region Web / Global Pages — 130000-139999

    // Global Pages (Error, Index, NotFound) — 130000-130099
    GlobalErrorDisplayed = 130000,
    GlobalIndexStart = 130010,
    GlobalIndexSuccess = 130011,
    GlobalNotFoundDisplayed = 130020,

    // Reserved — 131000-139999

    #endregion

    #region Excel Import/Export — 150000-159999

    // ClosedXmlExcelService — 150000-150099
    ExcelImportComplete = 150000,
    ExcelImportHeadersMissing = 150001,
    ExcelImportParseFailed = 150002,
    ExcelExportComplete = 150010,

    // BulkImportVariantsHandler (Catalog) — 150100-150199
    CatalogBulkImportStart = 150100,
    CatalogBulkImportSuccess = 150101,
    CatalogBulkImportFailed = 150102,
    CatalogBulkImportRowErrors = 150103,

    // Catalog Export — 150200-150299
    CatalogExportStart = 150200,
    CatalogExportComplete = 150201,

    // Antivirus scan — 150300-150399
    AntivirusScanStart = 150300,
    AntivirusScanClean = 150301,
    AntivirusScanInfected = 150302,

    // Seller Import Pages — 156000-156099
    SellerProductsImportStart = 156000,
    SellerProductsImportValidated = 156001,
    SellerProductsImportExecuted = 156002,
    SellerProductsImportError = 156003,
    SellerProductsTemplateDownload = 156010,
    SellerProductsExportStart = 156020,
    SellerProductsExportComplete = 156021,

    // Reserved — 157000-159999

    #endregion

    #region Promotions — 140000-149999

    // Infrastructure layer — 140000-141999 (reserved for Promotions DbContext, repositories)

    // Application Handlers — 142000-142999
    PromotionsCreateVoucherStart = 142000,
    PromotionsCreateVoucherSuccess = 142001,
    PromotionsCreateVoucherError = 142002,
    PromotionsActivateVoucherStart = 142010,
    PromotionsActivateVoucherSuccess = 142011,
    PromotionsActivateVoucherError = 142012,
    PromotionsPauseVoucherStart = 142020,
    PromotionsPauseVoucherSuccess = 142021,
    PromotionsPauseVoucherError = 142022,

    // Timer Jobs — 143000-143099
    PromotionsExpiryJobStart = 143000,
    PromotionsExpiryJobExpired = 143001,
    PromotionsExpiryJobDepleted = 143002,
    PromotionsExpiryJobError = 143003,
    PromotionsExpiryJobCompleted = 143004,

    // Reserved — 144000-149999

    #endregion
}
