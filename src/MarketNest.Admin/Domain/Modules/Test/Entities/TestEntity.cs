

namespace MarketNest.Admin.Domain;

public class TestEntity : Entity<Guid>
{
    private readonly List<TestSubEntity> _subEntities = [];

    protected TestEntity()
    {
    }

    public TestEntity(Guid id, string name, TestValueObject value)
    {
        Id = id;
        Name = name;
        Value = value;
    }

    public string Name { get; private set; } = string.Empty;
    public TestValueObject Value { get; private set; } = new();
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
