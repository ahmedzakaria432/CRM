using Crm.Modules.Tickets.Application.Commands.UpdateCategory;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandValidatorTests
{
    private readonly UpdateCategoryCommandValidator _validator = new();

    private ValidationResult Validate(
        Guid? id = null, string ar = "شكوى", string en = "Complaint", int order = 10, byte[]? rowVersion = null) =>
        _validator.Validate(new UpdateCategoryCommand(id ?? Ids.Category1, ar, en, order, true, rowVersion ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() => Expect.Invalid(Validate(rowVersion: []), "RowVersion");

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankNameSideIsInvalid(string blank)
    {
        Expect.Invalid(Validate(ar: blank), "NameAr");
        Expect.Invalid(Validate(en: blank), "NameEn");
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void EachNameSideIsLimitedToTwoHundredCharacters(int length, bool valid)
    {
        Assert.Equal(valid, Validate(ar: new string('ع', length)).IsValid);
        Assert.Equal(valid, Validate(en: new string('e', length)).IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(9999, true)]
    [InlineData(10000, false)]
    public void TheDisplayOrderIsBetweenZeroAndNineThousandNineHundredNinetyNine(int order, bool valid)
    {
        Assert.Equal(valid, Validate(order: order).IsValid);
    }
}
