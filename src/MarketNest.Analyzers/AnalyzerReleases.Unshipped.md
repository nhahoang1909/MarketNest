### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MN001 | Naming | Error | Private field must use _camelCase
MN002 | Naming | Warning | BannedClassSuffix
MN003 | AsyncRules | Error | AsyncVoidMethod
MN004 | AsyncRules | Error | BlockingAsyncCall
MN005 | Logging | Error | DirectLoggerCall
MN006 | Logging | Error | LoggingClassMustBePartial
MN007 | Logging | Error | MustInjectAppLoggerNotILogger
MN012 | Naming | Warning | CommandClassNamingConvention
MN013 | Naming | Warning | QueryClassNamingConvention
MN014 | Naming | Warning | HandlerClassNamingConvention
MN015 | Naming | Warning | EventRecordNamingConvention
MN017 | AsyncRules | Warning | UnnecessaryTaskFromResult
MN011 | AsyncRules | Warning | PublicAsyncApiMissingCancellationToken
MN008 | Architecture | Error | NamespaceMustBeFlatLayerLevel
MN009 | Architecture | Warning | DateTimeMustUseDateTimeOffset
MN010 | Architecture | Error | ServiceLocatorAntiPattern
MN016 | Architecture | Error | EntityAggregatePropertyMustNotHavePublicSetter
MN018 | Security | Error | InsecureHashAlgorithm
MN019 | Architecture | Warning | HandlerMustNotReturnEntityDirectly
MN020 | Architecture | Warning | QueryHandlerMissingSelectProjection
MN021 | Naming | Warning | BannedServiceSuffixOnConcreteClass
MN022 | Naming | Warning | BannedImplSuffix
MN023 | AsyncRules | Error | FireAndForgetAsyncCall
MN024 | Architecture | Error | HandlerMustNotCallSaveChanges
MN025 | Architecture | Error | HandlerMustNotManageTransactions
MN026 | Architecture | Error | DomainMustNotReferenceInfrastructure
MN027 | Architecture | Warning | RepositoryMustNotReturnIQueryable
MN028 | Architecture | Error | EntityMustNotUseInitAccessor
MN029 | Architecture | Warning | QueryHandlerMissingAsNoTracking
MN030 | Architecture | Warning | InjectInterfaceNotConcreteClass
MN031 | Architecture | Error | QueryHandlerMustNotCallSaveChanges
MN032 | Architecture | Warning | DeepIncludeChainExceedsThreeLevels
MN033 | Architecture | Error | CacheUsageInDomainLayer
MN034 | Architecture | Error | CommandHandlerMustNotInjectQuerySideTypes
MN035 | Architecture | Error | QueryHandlerMustNotInjectWriteSideOrHandlerTypes

