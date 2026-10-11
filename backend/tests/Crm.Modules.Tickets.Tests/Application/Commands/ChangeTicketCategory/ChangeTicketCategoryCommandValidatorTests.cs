using Crm.Modules.Tickets.Application.Commands.ChangeTicketCategory;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ChangeTicketCategory;

public sealed class ChangeTicketCategoryCommandValidatorTests
{
    private readonly ChangeTicketCategoryCommandValidator _validator = new();

    private ValidationResult Validate(Guid? id = null, Guid? category = null, byte[]? rowVersion = null) =>
        _validator.Validate(new ChangeTicketCategoryCommand(id ?? Ids.Ticket1, category ?? Ids.Category1, rowVersion ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Fact]
    public void AnEmptyCategoryIsInvalid() => Expect.Invalid(Validate(category: Guid.Empty), "CategoryId");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() => Expect.Invalid(Validate(rowVersion: []), "RowVersion");
}
