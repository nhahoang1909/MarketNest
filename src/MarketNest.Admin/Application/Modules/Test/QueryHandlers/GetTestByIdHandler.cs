using MarketNest.Admin.Domain;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Application;

public partial class GetTestByIdHandler(
    ITestQuery query,
    IAppLogger<GetTestByIdHandler> logger) : IQueryHandler<GetTestByIdQuery, TestDto?>
{
    public async Task<TestDto?> Handle(GetTestByIdQuery request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Id);

        TestEntity? entity = await query.GetByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            Log.WarnNotFound(logger, request.Id);
            return null;
        }

        return new TestDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Value = entity.Value,
            SubEntities = entity.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
        };
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminGetTestByIdStart, LogLevel.Information,
            "GetTestById Start - Id={Id}")]
        public static partial void InfoStart(ILogger logger, Guid id);

        [LoggerMessage((int)LogEventId.AdminGetTestByIdNotFound, LogLevel.Warning,
            "GetTestById NotFound - Id={Id}")]
        public static partial void WarnNotFound(ILogger logger, Guid id);
    }
}
