namespace Crm.SharedKernel.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void EqualComponentsAreEqualWithEqualHashCodes()
    {
        var left = new Pair("a", 1);
        var right = new Pair("a", 1);

        Assert.True(left.Equals(right));
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void DifferentComponentsAreNotEqual()
    {
        Assert.False(new Pair("a", 1).Equals(new Pair("a", 2)));
        Assert.True(new Pair("a", 1) != new Pair("b", 1));
    }

    [Fact]
    public void DifferentTypesWithEqualComponentsAreNotEqual()
    {
        ValueObject pair = new Pair("a", 1);
        ValueObject other = new OtherPair("a", 1);

        Assert.False(pair.Equals(other));
        Assert.True(pair != other);
    }

    [Fact]
    public void NullComponentsAreSupported()
    {
        var left = new Pair(null, 1);
        var right = new Pair(null, 1);

        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.False(left.Equals(new Pair("a", 1)));
    }

    [Fact]
    public void ComponentOrderMatters()
    {
        Assert.NotEqual(new Sequence("a", "b"), new Sequence("b", "a"));
    }

    [Fact]
    public void EqualityOperatorsHandleNulls()
    {
        Pair? none = null;
        Pair? other = null;
        var pair = new Pair("a", 1);

        Assert.True(none == other);
        Assert.False(none != other);
        Assert.False(none == pair);
        Assert.True(pair != none);
    }

    [Fact]
    public void EqualsObjectUsesValueEquality()
    {
        Assert.True(new Pair("a", 1).Equals((object)new Pair("a", 1)));
        Assert.False(new Pair("a", 1).Equals((object?)null));
        Assert.False(new Pair("a", 1).Equals("a"));
    }

    [Fact]
    public void AValueObjectEqualsItself()
    {
        var pair = new Pair("a", 1);

        Assert.True(pair.Equals(pair));
    }

    [Fact]
    public void ValueObjectsWorkAsDictionaryKeys()
    {
        var map = new Dictionary<Pair, string> { [new Pair("a", 1)] = "found" };

        Assert.Equal("found", map[new Pair("a", 1)]);
        Assert.False(map.ContainsKey(new Pair("a", 2)));
    }

    private sealed class Pair(string? name, int number) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return name;
            yield return number;
        }
    }

    private sealed class OtherPair(string? name, int number) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return name;
            yield return number;
        }
    }

    private sealed class Sequence(string first, string second) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return first;
            yield return second;
        }
    }
}
