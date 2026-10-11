using Crm.Modules.Organisation.Application.Commands.RenameDepartment;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.RenameDepartment;

public sealed class RenameDepartmentCommandValidatorTests
{
    private readonly RenameDepartmentCommandValidator _validator = new();

    private static RenameDepartmentCommand Command(Guid? id = null, string ar = "ف", string en = "B", byte[]? rowVersion = null) =>
        new(id ?? Guid.NewGuid(), ar, en, rowVersion ?? Ids.RowVersion());

    [Fact]
    public void AValidCommandPasses()
    {
        Expect.Valid(_validator.Validate(Command()));
    }

    [Fact]
    public void AnEmptyIdFails()
    {
        Expect.Invalid(_validator.Validate(Command(Guid.Empty)), nameof(RenameDepartmentCommand.Id));
    }

    [Fact]
    public void EmptyNamesFail()
    {
        Expect.Invalid(_validator.Validate(Command(ar: "", en: "")), nameof(RenameDepartmentCommand.NameAr), nameof(RenameDepartmentCommand.NameEn));
    }

    [Fact]
    public void NamesOverTwoHundredCharactersFail()
    {
        Expect.Valid(_validator.Validate(Command(ar: new string('ب', 200), en: new string('b', 200))));
        Expect.Invalid(_validator.Validate(Command(ar: new string('ب', 201))), nameof(RenameDepartmentCommand.NameAr));
        Expect.Invalid(_validator.Validate(Command(en: new string('b', 201))), nameof(RenameDepartmentCommand.NameEn));
    }

    [Fact]
    public void AnEmptyRowVersionFails()
    {
        Expect.Invalid(_validator.Validate(Command(rowVersion: [])), nameof(RenameDepartmentCommand.RowVersion));
    }
}
