using Crm.Modules.Organisation.Application.Commands.AddMembership;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.AddMembership;

public sealed class AddMembershipCommandValidatorTests
{
    private static readonly Guid Department = Ids.DeptA1.Value;
    private readonly AddMembershipCommandValidator _validator = new();

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Supervisor")]
    [InlineData("Agent")]
    public void EveryRoleNameIsValid(string role)
    {
        Expect.Valid(_validator.Validate(new AddMembershipCommand(Ids.User, Department, role)));
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("")]
    [InlineData("Owner")]
    public void AnythingElseIsNotARole(string role)
    {
        Expect.Invalid(_validator.Validate(new AddMembershipCommand(Ids.User, Department, role)), nameof(AddMembershipCommand.Role));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("99")]
    public void ANumericStringIsAcceptedBecauseEnumTryParseAcceptsNumbers(string role)
    {
        // Pinned production behaviour (Follow-up): the rule is Enum.TryParse, so "1" passes and "99" is not even a defined role.
        Expect.Valid(_validator.Validate(new AddMembershipCommand(Ids.User, Department, role)));
    }

    [Fact]
    public void TheRoleMessageNamesTheThreeRoles()
    {
        var result = _validator.Validate(new AddMembershipCommand(Ids.User, Department, "Owner"));

        Assert.Equal("Role must be Administrator, Supervisor, or Agent.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void AnEmptyUserIdIsInvalid()
    {
        Expect.Invalid(_validator.Validate(new AddMembershipCommand(Guid.Empty, Department, "Agent")), nameof(AddMembershipCommand.UserId));
    }

    [Fact]
    public void AnEmptyDepartmentIdIsInvalid()
    {
        Expect.Invalid(_validator.Validate(new AddMembershipCommand(Ids.User, Guid.Empty, "Agent")), nameof(AddMembershipCommand.DepartmentId));
    }
}
