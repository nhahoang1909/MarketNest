using MarketNest.Core.Common;

namespace MarketNest.Admin.Domain;

public class TestSubEntity : Entity<Guid>
{
    protected TestSubEntity() { }

    public TestSubEntity(Guid id, Guid parentId, string title)
    {
        Id = id;
        ParentId = parentId;
        Title = title;
    }

    public Guid ParentId { get; private set; }
    public string Title { get; private set; } = string.Empty;
}
