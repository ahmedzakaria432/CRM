using Crm.Modules.Tickets.Application.Commands.CreateCategory;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.CreateCategory;

public sealed class CreateCategoryCommandValidatorTests
{
    private readonly CreateCategoryCommandValidator _validator = new();

    private ValidationResult Validate(Guid? department = null, string ar = "شكوى", string en = "Complaint", int order = 10) =>
        _validator.Validate(new CreateCategoryCommand(department ?? Ids.DepartmentA1.Value, ar, en, order, true));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyDepartmentIsInvalid() => Expect.Invalid(Validate(department: Guid.Empty), "DepartmentId");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankArabicNameIsInvalid(string ar) => Expect.Invalid(Validate(ar: ar), "NameAr");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankEnglishNameIsInvalid(string en) => Expect.Invalid(Validate(en: en), "NameEn");

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
        ValidationResult result = Validate(order: order);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Expect.Invalid(result, "DisplayOrder");
        }
    }
}
