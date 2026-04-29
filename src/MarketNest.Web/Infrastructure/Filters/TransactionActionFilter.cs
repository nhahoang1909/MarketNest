using System.Data;
using System.Reflection;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Action filter that wraps API controller actions in a database transaction.
///     Registered globally; activates when the action's controller class or the action
///     method itself carries a <c>[Transaction]</c> attribute (e.g.
///     <c>WriteApiV1ControllerBase</c> applies it at class level).
///
///     <para>
///         Transaction lifecycle mirrors <c>RazorPageTransactionFilter</c>:
///         BeginTransaction → execute action → commit all → dispatch post-commit events,
///         or rollback on any unhandled exception.
///     </para>
///
///     <para>
///         Override: place <c>[Transaction(IsolationLevel.Serializable)]</c> on an
///         action method. Opt out: place <c>[NoTransaction]</c> on the action or class.
///         Only HTTP write verbs (POST / PUT / DELETE / PATCH) trigger the transaction;
///         GET actions are always bypassed.
///     </para>
/// </summary>
public sealed partial class TransactionActionFilter(
    IEnumerable<IModuleDbContext> moduleContexts,
    IUnitOfWork uow,
    IAppLogger<TransactionActionFilter> logger)
    : IAsyncActionFilter
{
    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE", "PATCH" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Only wrap write-verb actions
        var httpMethod = context.HttpContext.Request.Method;
        if (!WriteMethods.Contains(httpMethod))
        {
            await next();
            return;
        }

        // Resolve [Transaction] from action metadata (set by MVC from method/class attributes)
        var transactionAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<TransactionAttribute>()
            .FirstOrDefault();

        var noTransactionAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<NoTransactionAttribute>()
            .FirstOrDefault();

        // No [Transaction] on action/controller → bypass
        if (transactionAttr is null || noTransactionAttr is not null)
        {
            await next();
            return;
        }

        var isolation = transactionAttr.IsolationLevel;
        var timeout = TimeSpan.FromSeconds(transactionAttr.TimeoutSeconds);
        var actionName = context.ActionDescriptor.DisplayName ?? "unknown";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            context.HttpContext.RequestAborted);
        cts.CancelAfter(timeout);

        var dbContextList = moduleContexts.Select(m => m.AsDbContext()).ToList();
        var transactions = new List<IDbContextTransaction>(dbContextList.Count);

        try
        {
            foreach (var db in dbContextList)
                transactions.Add(await db.Database.BeginTransactionAsync(isolation, cts.Token));

            Log.InfoTxBegin(logger, actionName, isolation, dbContextList.Count);

            ActionExecutedContext executed = await next();

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                await RollbackAllAsync(transactions);
                Log.WarnTxRolledBackOnResult(logger, actionName);
                return;
            }

            foreach (var tx in transactions)
                await tx.CommitAsync(cts.Token);

            Log.InfoTxCommitted(logger, actionName, dbContextList.Count);

            await uow.DispatchPostCommitEventsAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            await RollbackAllAsync(transactions);
            Log.ErrorTxRolledBackOnException(logger, actionName, ex);
            throw;
        }
        finally
        {
            foreach (var tx in transactions)
                await tx.DisposeAsync();
        }
    }

    private static async Task RollbackAllAsync(
        IEnumerable<IDbContextTransaction> transactions)
    {
        foreach (var tx in transactions)
        {
            try { await tx.RollbackAsync(CancellationToken.None); }
            catch { /* ignore rollback errors */ }
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.ActionTxBegin, LogLevel.Debug,
            "Action TX BEGIN — Action={Action} Isolation={Isolation} Contexts={ContextCount}")]
        public static partial void InfoTxBegin(
            ILogger logger, string action, IsolationLevel isolation, int contextCount);

        [LoggerMessage((int)LogEventId.ActionTxCommitted, LogLevel.Debug,
            "Action TX COMMITTED — Action={Action} Contexts={ContextCount}")]
        public static partial void InfoTxCommitted(
            ILogger logger, string action, int contextCount);

        [LoggerMessage((int)LogEventId.ActionTxRolledBackOnResult, LogLevel.Warning,
            "Action TX ROLLED BACK (result exception not handled) — Action={Action}")]
        public static partial void WarnTxRolledBackOnResult(ILogger logger, string action);

        [LoggerMessage((int)LogEventId.ActionTxRolledBackOnException, LogLevel.Error,
            "Action TX ROLLED BACK (unhandled exception) — Action={Action}")]
        public static partial void ErrorTxRolledBackOnException(
            ILogger logger, string action, Exception ex);
    }
}

