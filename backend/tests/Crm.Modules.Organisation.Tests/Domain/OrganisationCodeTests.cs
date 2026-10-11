using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class OrganisationCodeTests
{
    [Theory]
    [InlineData(" ryd-01 ", "RYD-01")]
    [InlineData("ab", "AB")]
    [InlineData("Jed-Care", "JED-CARE")]
    [InlineData("12", "12")]
    [InlineData("A-1", "A-1")]
    public void CreateTrimsAndUpperCases(string input, string expected)
    {
        Assert.Equal(expected, OrganisationCode.Create(input).Value);
    }

    [Fact]
    public void CreateAcceptsTwentyCharacters()
    {
        string twenty = new('A', 20);

        Assert.Equal(twenty, OrganisationCode.Create(twenty).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("A B")]
    [InlineData("A_B")]
    [InlineData("A.B")]
    [InlineData("رمز")]
    public void CreateRejectsInvalidCodes(string input)
    {
        var exception = Assert.Throws<DomainException>(() => OrganisationCode.Create(input));

        Assert.Equal("Organisation.CodeInvalid", exception.Error.Code);
    }

    [Fact]
    public void CreateRejectsTwentyOneCharacters()
    {
        string tooLong = new('A', 21);

        var exception = Assert.Throws<DomainException>(() => OrganisationCode.Create(tooLong));

        Assert.Equal("Organisation.CodeInvalid", exception.Error.Code);
    }

    [Fact]
    public void CreateRejectsNull()
    {
        Func<string, OrganisationCode> create = OrganisationCode.Create;

        NullArguments.AssertThrowsArgumentNull(create, new object?[] { null });
    }

    [Fact]
    public void CodesWithTheSameValueAreEqual()
    {
        OrganisationCode first = OrganisationCode.Create("ryd");
        OrganisationCode second = OrganisationCode.Create(" RYD ");

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void CodesWithDifferentValuesAreNotEqual()
    {
        Assert.NotEqual(OrganisationCode.Create("RYD"), OrganisationCode.Create("JED"));
    }
}
