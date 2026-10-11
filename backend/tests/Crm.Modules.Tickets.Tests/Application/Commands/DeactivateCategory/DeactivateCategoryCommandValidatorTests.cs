using Crm.Modules.Tickets.Application.Commands.DeactivateCategory;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Commands.DeactivateCategory;

public sealed class DeactivateCategoryCommandValidatorTests
{
    private readonly DeactivateCategoryCommandValidator _validator = new();

    [Fact]
    public void ACompleteCommandIsValid() =>
        Expect.Valid(_validator.Validate(new DeactivateCategoryCommand(Ids.Category1, Ids.RowVersion())));

    [Fact]
    public void AnEmptyIdIsInvalid() =>
        Expect.Invalid(_validator.Validate(new DeactivateCategoryCommand(Guid.Empty, Ids.RowVersion())), "Id");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() =>
        Expect.Invalid(_validator.Validate(new DeactivateCategoryCommand(Ids.Category1, [])), "RowVersion");
}
