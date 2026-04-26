using MarketNest.Core.Common;
using MarketNest.Core.Common.Cqrs;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public class CreateTestHandler(ITestRepository repository) : ICommandHandler<CreateTestCommand, Guid>
{
    public async Task<Result<Guid, Error>> Handle(CreateTestCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var entity = new TestEntity(id, request.Name, request.Value);

        repository.Add(entity);

        if (request.SubTitles is not null)
            foreach (var title in request.SubTitles)
                repository.AddSubEntity(new TestSubEntity(Guid.NewGuid(), id, title));

        await repository.SaveChangesAsync(cancellationToken);
        return Result<Guid, Error>.Success(id);
    }
}
