using Crm.Modules.Organisation.Application.Commands.CreateDepartment;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.CreateDepartment;

public sealed class CreateDepartmentCommandValidatorTests
{
    private readonly CreateDepartmentCommandValidator _validator = new();

    private static CreateDepartmentCommand Command(Guid? branch = null, string code = "CARE", string ar = "خدمة", string en = "Care") =>
        new(branch ?? Guid.NewGuid(), code, ar, en);

    [Fact]
    public void AValidCommandPasses()
    {
        Expect.Valid(_validator.Validate(Command()));
    }

    [Fact]
    public void AnEmptyBranchIdFails()
    {
        Expect.Invalid(_validator.Validate(Command(Guid.Empty)), nameof(CreateDepartmentCommand.BranchId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("a b")]
    [InlineData("a_b")]
    public void InvalidCodesFail(string code)
    {
        Expect.Invalid(_validator.Validate(Command(code: code)), nameof(CreateDepartmentCommand.Code));
    }

    [Fact]
    public void CodeLengthBoundariesAreTwoAndTwenty()
    {
        Expect.Valid(_validator.Validate(Command(code: "AB")));
        Expect.Valid(_validator.Validate(Command(code: new string('A', 20))));
        Expect.Invalid(_validator.Validate(Command(code: new string('A', 21))), nameof(CreateDepartmentCommand.Code));
    }

    [Fact]
    public void EmptyNamesFail()
    {
        Expect.Invalid(_validator.Validate(Command(ar: "", en: "")), nameof(CreateDepartmentCommand.NameAr), nameof(CreateDepartmentCommand.NameEn));
    }

    [Fact]
    public void NameLengthBoundaryIsTwoHundred()
    {
        Expect.Valid(_validator.Validate(Command(ar: new string('ب', 200), en: new string('b', 200))));
        Expect.Invalid(_validator.Validate(Command(ar: new string('ب', 201))), nameof(CreateDepartmentCommand.NameAr));
        Expect.Invalid(_validator.Validate(Command(en: new string('b', 201))), nameof(CreateDepartmentCommand.NameEn));
    }
}
