using System.Data;
using System.Reflection;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Globally-registered filter that automatically wraps every <c>OnPost*</c> (and
///     <c>OnPut*</c>, <c>OnDelete*</c>, <c>OnPatch*</c>) Razor Page handler in a
///     database transaction using all registered module DbContexts.
///
///     <para>
///         Transaction lifecycle:
///         <list type="number">
///             <item>Call <see cref="IUnitOfWork.BeginTransactionAsync"/> on all module DbContexts.</item>
///             <item>Execute the page handler (mutates entities via repositories — no explicit commit needed in handlers).</item>
///             <item>Call <see cref="IUnitOfWork.CommitAsync"/> — dispatches pre-commit events, then SaveChanges on all contexts.</item>
///             <item>Call <see cref="IUnitOfWork.CommitTransactionAsync"/> to finalize all DB transactions.</item>
///             <item>Call <see cref="IUnitOfWork.DispatchPostCommitEventsAsync"/> to dispatch post-commit events.</item>
///             <item>Rollback via <see cref="IUnitOfWork.RollbackAsync"/> on unhandled exception.</item>
///             <item>Call <see cref="IUnitOfWork.DisposeAsync"/> to clean up resources.</item>
///         </list>
///     </para>
///
///     <para>
///         Opt-out: apply <c>[NoTransaction]</c> on the handler method or PageModel class.
///         Override isolation level: apply <c>[Transaction(IsolationLevel.Serializable)]</c>.
///         <c>OnGet*</c> handlers are never wrapped — they are read-only.
///     </para>
/// </summary>
public sealed partial class RazorPageTransactionFilter(
    IUnitOfWork uow,
    IAppLogger<RazorPageTransactionFilter> logger)
    : IAsyncPageFilter
{
    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE", "PATCH" };

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var handlerMethod = context.HandlerMethod;

        // Only wrap write-verb handlers; GET is always read-only
        if (handlerMethod?.HttpMethod is null || !WriteMethods.Contains(handlerMethod.HttpMethod))
        {
            await next();
            return;
        }

        // [NoTransaction] on method or class → bypass
        if (HasNoTransaction(handlerMethod.MethodInfo) ||
            HasNoTransaction(context.HandlerInstance.GetType()))
        {
            await next();
            return;
        }

        var attr = GetTransactionAttribute(handlerMethod.MethodInfo)
                   ?? GetTransactionAttribute(context.HandlerInstance.GetType());

        var isolation = attr?.IsolationLevel ?? IsolationLevel.ReadCommitted;
        var timeout = TimeSpan.FromSeconds(attr?.TimeoutSeconds ?? 30);
        var handlerName = handlerMethod.MethodInfo.Name;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            context.HttpContext.RequestAborted);
        cts.CancelAfter(timeout);

        try
        {
            await uow.BeginTransactionAsync(isolation, cts.Token);
            Log.InfoTxBegin(logger, handlerName, isolation);

            PageHandlerExecutedContext executed = await next();

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                await uow.RollbackAsync(cts.Token);
                Log.WarnTxRolledBackOnResult(logger, handlerName);
                return;
            }

            await uow.CommitAsync(cts.Token);
            await uow.CommitTransactionAsync(cts.Token);

            Log.InfoTxCommitted(logger, handlerName);

            await uow.DispatchPostCommitEventsAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            await uow.RollbackAsync(cts.Token).ConfigureAwait(false);
            Log.ErrorTxRolledBackOnException(logger, handlerName, ex);
            throw;
        }
        finally
        {
            await uow.DisposeAsync();
        }
    }

    private static bool HasNoTransaction(MemberInfo member) =>
        member.GetCustomAttributes(typeof(NoTransactionAttribute), inherit: true).Length > 0;

    private static TransactionAttribute? GetTransactionAttribute(MemberInfo member) =>
        member.GetCustomAttributes(typeof(TransactionAttribute), inherit: true)
            .OfType<TransactionAttribute>()
            .FirstOrDefault();

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.RazorPageTxBegin, LogLevel.Debug,
            "Razor Page TX BEGIN — Handler={Handler} Isolation={Isolation}")]
        public static partial void InfoTxBegin(
            ILogger logger, string handler, IsolationLevel isolation);

        [LoggerMessage((int)LogEventId.RazorPageTxCommitted, LogLevel.Debug,
            "Razor Page TX COMMITTED — Handler={Handler}")]
        public static partial void InfoTxCommitted(
            ILogger logger, string handler);

        [LoggerMessage((int)LogEventId.RazorPageTxRolledBackOnResult, LogLevel.Warning,
            "Razor Page TX ROLLED BACK (result exception not handled) — Handler={Handler}")]
        public static partial void WarnTxRolledBackOnResult(ILogger logger, string handler);

        [LoggerMessage((int)LogEventId.RazorPageTxRolledBackOnException, LogLevel.Error,
            "Razor Page TX ROLLED BACK (unhandled exception) — Handler={Handler}")]
        public static partial void ErrorTxRolledBackOnException(
            ILogger logger, string handler, Exception ex);
    }
}

