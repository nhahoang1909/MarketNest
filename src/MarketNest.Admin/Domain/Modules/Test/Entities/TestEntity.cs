

namespace MarketNest.Admin.Domain;

public class TestEntity : Entity<Guid>
{
    private readonly List<TestSubEntity> _subEntities = [];

#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    protected TestEntity()
    {
    }
#pragma warning restore CS8618

    public TestEntity(Guid id, string name, TestValueObject value)
    {
        Id = id;
        Name = name;
        Value = value;
    }

    public string Name { get; private set; }
    public TestValueObject Value { get; private set; }
    public IReadOnlyList<TestSubEntity> SubEntities => _subEntities.AsReadOnly();

    public void Update(string name, TestValueObject value)
    {
        Name = name;
        Value = value;
    }

    public void AddSubEntity(TestSubEntity sub)
    {
        _subEntities.Add(sub);
    }
}
