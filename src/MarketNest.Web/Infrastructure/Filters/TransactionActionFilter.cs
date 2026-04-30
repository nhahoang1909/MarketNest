using System.Data;
using System.Reflection;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Action filter that wraps API controller actions in a database transaction.
///     Registered globally; activates when the action's controller class or the action
///     method itself carries a <c>[Transaction]</c> attribute (e.g.
///     <c>WriteApiV1ControllerBase</c> applies it at class level).
///
///     <para>
///         Transaction lifecycle:
///         <list type="number">
///             <item>Call <see cref="IUnitOfWork.BeginTransactionAsync"/> on all module DbContexts.</item>
///             <item>Execute the action (mutates entities via repositories).</item>
///             <item>Call <see cref="IUnitOfWork.CommitAsync"/> (pre-commit events + SaveChanges).</item>
///             <item>Call <see cref="IUnitOfWork.CommitTransactionAsync"/> to finalize DB transactions.</item>
///             <item>Call <see cref="IUnitOfWork.DispatchPostCommitEventsAsync"/> to dispatch post-commit events.</item>
///             <item>Call <see cref="IUnitOfWork.RollbackAsync"/> on any unhandled exception.</item>
///             <item>Call <see cref="IUnitOfWork.DisposeAsync"/> to clean up resources.</item>
///         </list>
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

        try
        {
            await uow.BeginTransactionAsync(isolation, cts.Token);
            Log.InfoTxBegin(logger, actionName, isolation);

            ActionExecutedContext executed = await next();

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                await uow.RollbackAsync(cts.Token);
                Log.WarnTxRolledBackOnResult(logger, actionName);
                return;
            }

            await uow.CommitAsync(cts.Token);
            await uow.CommitTransactionAsync(cts.Token);

            Log.InfoTxCommitted(logger, actionName);

            await uow.DispatchPostCommitEventsAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            await uow.RollbackAsync(cts.Token).ConfigureAwait(false);
            Log.ErrorTxRolledBackOnException(logger, actionName, ex);
            throw;
        }
        finally
        {
            await uow.DisposeAsync();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.ActionTxBegin, LogLevel.Debug,
            "Action TX BEGIN — Action={Action} Isolation={Isolation}")]
        public static partial void InfoTxBegin(
            ILogger logger, string action, IsolationLevel isolation);

        [LoggerMessage((int)LogEventId.ActionTxCommitted, LogLevel.Debug,
            "Action TX COMMITTED — Action={Action}")]
        public static partial void InfoTxCommitted(
            ILogger logger, string action);

        [LoggerMessage((int)LogEventId.ActionTxRolledBackOnResult, LogLevel.Warning,
            "Action TX ROLLED BACK (result exception not handled) — Action={Action}")]
        public static partial void WarnTxRolledBackOnResult(ILogger logger, string action);

        [LoggerMessage((int)LogEventId.ActionTxRolledBackOnException, LogLevel.Error,
            "Action TX ROLLED BACK (unhandled exception) — Action={Action}")]
        public static partial void ErrorTxRolledBackOnException(
            ILogger logger, string action, Exception ex);
    }
}

