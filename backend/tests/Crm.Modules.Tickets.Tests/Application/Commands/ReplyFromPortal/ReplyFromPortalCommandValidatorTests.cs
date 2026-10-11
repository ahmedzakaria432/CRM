using Crm.Modules.Tickets.Application.Commands.ReplyFromPortal;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ReplyFromPortal;

public sealed class ReplyFromPortalCommandValidatorTests
{
    private readonly ReplyFromPortalCommandValidator _validator = new();

    private ValidationResult Validate(Guid? id = null, string body = "Hello") =>
        _validator.Validate(new ReplyFromPortalCommand(id ?? Ids.Ticket1, body));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankBodyIsInvalid(string body) => Expect.Invalid(Validate(body: body), "Body");

    [Theory]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void TheBodyIsLimitedToTenThousandCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(body: new string('b', length)).IsValid);
}
