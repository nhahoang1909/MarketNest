using System.Data;
using System.Reflection;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Globally-registered filter that automatically wraps every <c>OnPost*</c> (and
///     <c>OnPut*</c>, <c>OnDelete*</c>, <c>OnPatch*</c>) Razor Page handler in a
///     database transaction using all registered module DbContexts.
///
///     <para>
///         Transaction lifecycle:
///         <list type="number">
///             <item>BeginTransaction on every module DbContext.</item>
///             <item>Execute the page handler (which calls <see cref="IUnitOfWork.CommitAsync"/>).</item>
///             <item>Commit all transactions if the handler completes without error.</item>
///             <item>Dispatch post-commit events via <see cref="IUnitOfWork.DispatchPostCommitEventsAsync"/>.</item>
///             <item>Rollback all transactions on unhandled exception.</item>
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
    IEnumerable<IModuleDbContext> moduleContexts,
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

        var dbContextList = moduleContexts.Select(m => m.AsDbContext()).ToList();
        var transactions = new List<IDbContextTransaction>(dbContextList.Count);

        try
        {
            foreach (var db in dbContextList)
                transactions.Add(await db.Database.BeginTransactionAsync(isolation, cts.Token));

            Log.InfoTxBegin(logger, handlerName, isolation, dbContextList.Count);

            PageHandlerExecutedContext executed = await next();

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                await RollbackAllAsync(transactions);
                Log.WarnTxRolledBackOnResult(logger, handlerName);
                return;
            }

            foreach (var tx in transactions)
                await tx.CommitAsync(cts.Token);

            Log.InfoTxCommitted(logger, handlerName, dbContextList.Count);

            await uow.DispatchPostCommitEventsAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            await RollbackAllAsync(transactions);
            Log.ErrorTxRolledBackOnException(logger, handlerName, ex);
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

    private static bool HasNoTransaction(MemberInfo member) =>
        member.GetCustomAttributes(typeof(NoTransactionAttribute), inherit: true).Length > 0;

    private static TransactionAttribute? GetTransactionAttribute(MemberInfo member) =>
        member.GetCustomAttributes(typeof(TransactionAttribute), inherit: true)
            .OfType<TransactionAttribute>()
            .FirstOrDefault();

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.RazorPageTxBegin, LogLevel.Debug,
            "Razor Page TX BEGIN — Handler={Handler} Isolation={Isolation} Contexts={ContextCount}")]
        public static partial void InfoTxBegin(
            ILogger logger, string handler, IsolationLevel isolation, int contextCount);

        [LoggerMessage((int)LogEventId.RazorPageTxCommitted, LogLevel.Debug,
            "Razor Page TX COMMITTED — Handler={Handler} Contexts={ContextCount}")]
        public static partial void InfoTxCommitted(
            ILogger logger, string handler, int contextCount);

        [LoggerMessage((int)LogEventId.RazorPageTxRolledBackOnResult, LogLevel.Warning,
            "Razor Page TX ROLLED BACK (result exception not handled) — Handler={Handler}")]
        public static partial void WarnTxRolledBackOnResult(ILogger logger, string handler);

        [LoggerMessage((int)LogEventId.RazorPageTxRolledBackOnException, LogLevel.Error,
            "Razor Page TX ROLLED BACK (unhandled exception) — Handler={Handler}")]
        public static partial void ErrorTxRolledBackOnException(
            ILogger logger, string handler, Exception ex);
    }
}

