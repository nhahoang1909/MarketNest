using MarketNest.Admin.Domain;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Application;

public partial class CreateTestHandler(
    ITestRepository repository,
    IAppLogger<CreateTestHandler> logger) : ICommandHandler<CreateTestCommand, Guid>
{
    public async Task<Result<Guid, Error>> Handle(CreateTestCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Name);

        var id = Guid.NewGuid();
        var entity = new TestEntity(id, request.Name, request.Value);

        repository.Add(entity);

        if (request.SubTitles is not null)
            foreach (string title in request.SubTitles)
                repository.AddSubEntity(new TestSubEntity(Guid.NewGuid(), id, title));

        await repository.SaveChangesAsync(cancellationToken);

        Log.InfoSuccess(logger, id);
        return Result<Guid, Error>.Success(id);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminCreateTestStart, LogLevel.Information,
            "CreateTest Start - Name={Name}")]
        public static partial void InfoStart(ILogger logger, string name);

        [LoggerMessage((int)LogEventId.AdminCreateTestSuccess, LogLevel.Information,
            "CreateTest Success - Id={Id}")]
        public static partial void InfoSuccess(ILogger logger, Guid id);
    }
}
