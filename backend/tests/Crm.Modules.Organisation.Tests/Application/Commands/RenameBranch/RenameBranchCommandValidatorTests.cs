using Crm.Modules.Organisation.Application.Commands.RenameBranch;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.RenameBranch;

public sealed class RenameBranchCommandValidatorTests
{
    private readonly RenameBranchCommandValidator _validator = new();

    private static RenameBranchCommand Command(Guid? id = null, string ar = "ج", string en = "J", byte[]? rowVersion = null) =>
        new(id ?? Guid.NewGuid(), ar, en, rowVersion ?? Ids.RowVersion());

    [Fact]
    public void AValidCommandPasses()
    {
        Expect.Valid(_validator.Validate(Command()));
    }

    [Fact]
    public void AnEmptyIdFails()
    {
        Expect.Invalid(_validator.Validate(Command(Guid.Empty)), nameof(RenameBranchCommand.Id));
    }

    [Fact]
    public void EmptyNamesFail()
    {
        Expect.Invalid(_validator.Validate(Command(ar: "", en: "")), nameof(RenameBranchCommand.NameAr), nameof(RenameBranchCommand.NameEn));
    }

    [Fact]
    public void NamesOverTwoHundredCharactersFail()
    {
        Expect.Valid(_validator.Validate(Command(ar: new string('ب', 200), en: new string('b', 200))));
        Expect.Invalid(_validator.Validate(Command(ar: new string('ب', 201))), nameof(RenameBranchCommand.NameAr));
        Expect.Invalid(_validator.Validate(Command(en: new string('b', 201))), nameof(RenameBranchCommand.NameEn));
    }

    [Fact]
    public void AnEmptyRowVersionFails()
    {
        Expect.Invalid(_validator.Validate(Command(rowVersion: [])), nameof(RenameBranchCommand.RowVersion));
    }
}
