using Crm.Modules.Tickets.Application.Commands.AddInternalNote;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.AddInternalNote;

public sealed class AddInternalNoteCommandValidatorTests
{
    private readonly AddInternalNoteCommandValidator _validator = new();

    private ValidationResult Validate(Guid? id = null, string body = "Note") =>
        _validator.Validate(new AddInternalNoteCommand(id ?? Ids.Ticket1, body));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankBodyIsInvalid(string body) => Expect.Invalid(Validate(body: body), "Body");

    [Theory]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void TheBodyIsLimitedToTenThousandCharacters(int length, bool valid)
    {
        Assert.Equal(valid, Validate(body: new string('b', length)).IsValid);
    }
}
