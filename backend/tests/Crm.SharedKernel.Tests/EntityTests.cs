namespace Crm.SharedKernel.Tests;

public sealed class EntityTests
{
    private static readonly Guid IdOne = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid IdTwo = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void EntitiesOfTheSameTypeAndIdAreEqual()
    {
        var left = new PersonEntity(IdOne);
        var right = new PersonEntity(IdOne);

        Assert.True(left.Equals(right));
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void EntitiesWithDifferentIdsAreNotEqual()
    {
        var left = new PersonEntity(IdOne);
        var right = new PersonEntity(IdTwo);

        Assert.False(left.Equals(right));
        Assert.True(left != right);
    }

    [Fact]
    public void EntitiesOfDifferentTypesWithTheSameIdAreNotEqual()
    {
        Entity<Guid> person = new PersonEntity(IdOne);
        Entity<Guid> company = new CompanyEntity(IdOne);

        Assert.False(person.Equals(company));
        Assert.True(person != company);
        Assert.NotEqual(person.GetHashCode(), company.GetHashCode());
    }

    [Fact]
    public void NullIsNeverEqualToAnEntity()
    {
        var person = new PersonEntity(IdOne);

        Assert.False(person.Equals((Entity<Guid>?)null));
        Assert.False(person.Equals((object?)null));
    }

    [Fact]
    public void AnEntityEqualsItself()
    {
        var person = new PersonEntity(IdOne);

        Assert.True(person.Equals(person));
        Assert.True(AreEqual(person, person));
    }

    private static bool AreEqual(Entity<Guid>? left, Entity<Guid>? right) => left == right;

    [Fact]
    public void EqualityOperatorsHandleNulls()
    {
        PersonEntity? none = null;
        PersonEntity? other = null;
        var person = new PersonEntity(IdOne);

        Assert.True(none == other);
        Assert.False(none != other);
        Assert.False(none == person);
        Assert.True(person != none);
        Assert.False(person == none);
    }

    [Fact]
    public void EqualsWithANonEntityIsFalse()
    {
        var person = new PersonEntity(IdOne);

        Assert.False(person.Equals("not an entity"));
        Assert.False(person.Equals((object)IdOne));
    }

    [Fact]
    public void EqualsObjectUsesEntityEquality()
    {
        var person = new PersonEntity(IdOne);

        Assert.True(person.Equals((object)new PersonEntity(IdOne)));
    }

    [Fact]
    public void IdIsTheConstructorArgument()
    {
        Assert.Equal(IdTwo, new PersonEntity(IdTwo).Id);
    }

    [Fact]
    public void EntitiesWorkAsHashSetMembers()
    {
        var set = new HashSet<Entity<Guid>> { new PersonEntity(IdOne), new PersonEntity(IdOne), new CompanyEntity(IdOne) };

        Assert.Equal(2, set.Count);
    }

    private sealed class PersonEntity(Guid id) : Entity<Guid>(id);

    private sealed class CompanyEntity(Guid id) : Entity<Guid>(id);
}
