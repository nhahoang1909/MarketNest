using MarketNest.Admin.Domain;
using MediatR;

#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public class UpdateTestHandler(ITestRepository repository) : ICommandHandler<UpdateTestCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(UpdateTestCommand request, CancellationToken cancellationToken)
    {
        TestEntity? entity = await repository.GetByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
            return Result<Unit, Error>.Failure(
                Error.NotFound(nameof(TestEntity), request.Id.ToString()));

        entity.Update(request.Name, request.Value);

        repository.RemoveSubEntities(entity.SubEntities.ToList());

        if (request.SubTitles is not null)
            foreach (string title in request.SubTitles)
                repository.AddSubEntity(new TestSubEntity(Guid.NewGuid(), request.Id, title));

        await repository.SaveChangesAsync(cancellationToken);
        return Result<Unit, Error>.Success(Unit.Value);
    }
}
