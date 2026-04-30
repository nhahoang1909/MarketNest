

namespace MarketNest.Admin.Domain;

public class TestSubEntity : Entity<Guid>
{
#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    protected TestSubEntity()
    {
    }
#pragma warning restore CS8618

    public TestSubEntity(Guid id, Guid parentId, string title)
    {
        Id = id;
        ParentId = parentId;
        Title = title;
    }

    public Guid ParentId { get; private set; }
    public string Title { get; private set; }
}
