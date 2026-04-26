using MediatR;
using MarketNest.Core.Common;
using MarketNest.Core.Common.Cqrs;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public class UpdateTestHandler(ITestRepository repository) : ICommandHandler<UpdateTestCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(UpdateTestCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
            return Result<Unit, Error>.Failure(
                Error.NotFound(nameof(TestEntity), request.Id.ToString()));

        entity.Update(request.Name, request.Value);

        repository.RemoveSubEntities(entity.SubEntities.ToList());

        if (request.SubTitles is not null)
            foreach (var title in request.SubTitles)
                repository.AddSubEntity(new TestSubEntity(Guid.NewGuid(), request.Id, title));

        await repository.SaveChangesAsync(cancellationToken);
        return Result<Unit, Error>.Success(Unit.Value);
    }
}
