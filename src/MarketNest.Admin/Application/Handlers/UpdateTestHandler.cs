using MarketNest.Core.Common;
using MarketNest.Core.Common.Cqrs;
using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Infrastructure;
using MarketNest.Admin.Application;
using System.Linq;
using MediatR;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public class UpdateTestHandler : ICommandHandler<UpdateTestCommand, Unit>
{
    private readonly AdminDbContext _db;

    public UpdateTestHandler(AdminDbContext db) => _db = db;

    public async Task<Result<Unit, Error>> Handle(UpdateTestCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Tests.Include(x => x.SubEntities).FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
            return Result<Unit, Error>.Failure(Error.NotFound(nameof(TestEntity), request.Id.ToString()));

        entity.Update(request.Name, request.Value);

        // Replace sub-entities naively: remove existing and add new
        var existing = _db.TestSubEntities.Where(s => s.ParentId == request.Id);
        _db.TestSubEntities.RemoveRange(existing);
        if (request.SubTitles is not null)
        {
                foreach (var t in request.SubTitles)
                {
                    _db.TestSubEntities.Add(new TestSubEntity(Guid.NewGuid(), request.Id, t));
                }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit, Error>.Success(Unit.Value);
    }
}

