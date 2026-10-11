using Crm.Modules.Tickets.Application.Commands.TransferTicket;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.TransferTicket;

public sealed class TransferTicketCommandValidatorTests
{
    private readonly TransferTicketCommandValidator _validator = new();

    private ValidationResult Validate(Guid? id = null, Guid? department = null, byte[]? rowVersion = null) =>
        _validator.Validate(new TransferTicketCommand(id ?? Ids.Ticket1, department ?? Ids.DepartmentA2.Value, rowVersion ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Fact]
    public void AnEmptyDepartmentIsInvalid() => Expect.Invalid(Validate(department: Guid.Empty), "DepartmentId");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() => Expect.Invalid(Validate(rowVersion: []), "RowVersion");
}
