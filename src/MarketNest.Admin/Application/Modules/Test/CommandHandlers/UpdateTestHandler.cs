using MarketNest.Admin.Domain;
using MarketNest.Base.Infrastructure;
using MediatR;

#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public partial class UpdateTestHandler(
    ITestRepository repository,
    IAppLogger<UpdateTestHandler> logger) : ICommandHandler<UpdateTestCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(UpdateTestCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Id);

        TestEntity? entity = await repository.GetByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            Log.WarnNotFound(logger, request.Id);
            return Result<Unit, Error>.Failure(
                Error.NotFound(nameof(TestEntity), request.Id.ToString()));
        }

        entity.Update(request.Name, request.Value);
        repository.RemoveSubEntities(entity.SubEntities.ToList());

        if (request.SubTitles is not null)
            foreach (string title in request.SubTitles)
                repository.AddSubEntity(new TestSubEntity(Guid.NewGuid(), request.Id, title));

        await repository.SaveChangesAsync(cancellationToken);

        Log.InfoSuccess(logger, request.Id);
        return Result<Unit, Error>.Success(Unit.Value);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminUpdateTestStart, LogLevel.Information,
            "UpdateTest Start - Id={Id}")]
        public static partial void InfoStart(ILogger logger, Guid id);

        [LoggerMessage((int)LogEventId.AdminUpdateTestSuccess, LogLevel.Information,
            "UpdateTest Success - Id={Id}")]
        public static partial void InfoSuccess(ILogger logger, Guid id);

        [LoggerMessage((int)LogEventId.AdminUpdateTestError, LogLevel.Warning,
            "UpdateTest NotFound - Id={Id}")]
        public static partial void WarnNotFound(ILogger logger, Guid id);
    }
}
