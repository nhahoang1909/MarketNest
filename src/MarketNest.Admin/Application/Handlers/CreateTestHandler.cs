using MarketNest.Core.Common;
using MarketNest.Core.Common.Cqrs;
using MarketNest.Admin.Domain;
using MarketNest.Admin.Infrastructure;
using MarketNest.Admin.Application;

namespace MarketNest.Admin.Application;

public class CreateTestHandler : ICommandHandler<CreateTestCommand, Guid>
{
    private readonly AdminDbContext _db;

    public CreateTestHandler(AdminDbContext db) => _db = db;

    public async Task<Result<Guid, Error>> Handle(CreateTestCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var entity = new TestEntity(id, request.Name, request.Value);

        if (request.SubTitles is not null)
        {
            foreach (var t in request.SubTitles)
            {
                entity.AddSubEntity(new TestSubEntity(Guid.NewGuid(), id, t));
            }
        }

        _db.Tests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid, Error>.Success(id);
    }
}

