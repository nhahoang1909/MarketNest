using MarketNest.Core.Common;
using MarketNest.Core.Common.Events;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Domain;

public class TestEntity : Entity<Guid>
{
    protected TestEntity() { }

    public TestEntity(Guid id, string name, TestValueObject value)
    {
        Id = id;
        Name = name;
        Value = value;
        SubEntities = new List<TestSubEntity>();
    }

    public string Name { get; private set; } = string.Empty;
    public TestValueObject Value { get; private set; } = new();
    public IReadOnlyList<TestSubEntity> SubEntities { get; private set; } = new List<TestSubEntity>();

    public void Update(string name, TestValueObject value)
    {
        Name = name;
        Value = value;
    }

    public void AddSubEntity(TestSubEntity sub)
    {
        var list = SubEntities.ToList();
        list.Add(sub);
        SubEntities = list.AsReadOnly();
    }
}
