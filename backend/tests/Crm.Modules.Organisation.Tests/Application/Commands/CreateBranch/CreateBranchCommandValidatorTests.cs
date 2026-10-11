using Crm.Modules.Organisation.Application.Commands.CreateBranch;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.CreateBranch;

public sealed class CreateBranchCommandValidatorTests
{
    private readonly CreateBranchCommandValidator _validator = new();

    private static CreateBranchCommand Command(string code = "RYD", string ar = "الرياض", string en = "Riyadh") => new(code, ar, en);

    [Theory]
    [InlineData("ab")]
    [InlineData("A-1")]
    [InlineData("ryd-care")]
    public void ValidCodesPass(string code)
    {
        Expect.Valid(_validator.Validate(Command(code)));
    }

    [Fact]
    public void ATwentyCharacterCodeIsValid()
    {
        Expect.Valid(_validator.Validate(Command(new string('A', 20))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("a b")]
    [InlineData("a_b")]
    public void InvalidCodesFail(string code)
    {
        Expect.Invalid(_validator.Validate(Command(code)), nameof(CreateBranchCommand.Code));
    }

    [Fact]
    public void ATwentyOneCharacterCodeFails()
    {
        Expect.Invalid(_validator.Validate(Command(new string('A', 21))), nameof(CreateBranchCommand.Code));
    }

    [Fact]
    public void EmptyNamesFail()
    {
        Expect.Invalid(_validator.Validate(Command(ar: "", en: "")), nameof(CreateBranchCommand.NameAr), nameof(CreateBranchCommand.NameEn));
    }

    [Fact]
    public void TwoHundredCharacterNamesPassAndTwoHundredAndOneFail()
    {
        Expect.Valid(_validator.Validate(Command(ar: new string('ب', 200), en: new string('b', 200))));
        Expect.Invalid(_validator.Validate(Command(ar: new string('ب', 201))), nameof(CreateBranchCommand.NameAr));
        Expect.Invalid(_validator.Validate(Command(en: new string('b', 201))), nameof(CreateBranchCommand.NameEn));
    }
}
