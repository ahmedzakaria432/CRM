using Crm.Modules.Tickets.Application.Commands.ReactivateCategory;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ReactivateCategory;

public sealed class ReactivateCategoryCommandValidatorTests
{
    private readonly ReactivateCategoryCommandValidator _validator = new();

    [Fact]
    public void ACompleteCommandIsValid() =>
        Expect.Valid(_validator.Validate(new ReactivateCategoryCommand(Ids.Category1, Ids.RowVersion())));

    [Fact]
    public void AnEmptyIdIsInvalid() =>
        Expect.Invalid(_validator.Validate(new ReactivateCategoryCommand(Guid.Empty, Ids.RowVersion())), "Id");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() =>
        Expect.Invalid(_validator.Validate(new ReactivateCategoryCommand(Ids.Category1, [])), "RowVersion");
}
