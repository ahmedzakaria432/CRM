using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Application;

internal static class TicketsErrors
{
    public static Error TicketNotFound(Guid id) =>
        Error.NotFound("Tickets.TicketNotFound", $"Ticket '{id}' was not found.");

    public static Error CategoryNotFound(Guid id) =>
        Error.NotFound("Tickets.CategoryNotFound", $"Category '{id}' was not found.");

    public static Error DepartmentOutOfScope() =>
        Error.Forbidden("Organisation.OutOfScope", "The requested department is outside your scope.");

    public static Error DepartmentRequired() =>
        Error.Validation("Organisation.DepartmentRequired", "DepartmentId: Choose the department this record belongs to.");

    public static Error CustomerInactive() =>
        Error.Domain("Tickets.CustomerInactive", "Tickets cannot be created for an inactive customer.");

    public static Error ContactNotOfCustomer(Guid contactId) =>
        Error.Domain("Tickets.ContactNotOfCustomer", $"Contact '{contactId}' does not belong to this customer.");

    public static Error AssigneeInactive(Guid agentId) =>
        Error.Domain("Tickets.AssigneeInactive", $"User '{agentId}' is inactive and cannot be assigned tickets.");

    public static Error AssigneeNotInDepartment(Guid agentId) =>
        Error.Domain("Tickets.AssigneeNotInDepartment", $"User '{agentId}' is not a member of the ticket's department.");

    public static Error TargetDepartmentInactive() =>
        Error.Domain("Tickets.TargetDepartmentInactive", "The target department is inactive.");

    public static Error CategoryDepartmentInactive() =>
        Error.Domain("Tickets.CategoryDepartmentInactive", "Categories cannot be added to an inactive department.");

    public static Error AlreadyClaimed(string detail) => Error.Conflict("Tickets.AlreadyClaimed", detail);

    public static Error TransitionNotAllowed(string detail) => Error.Domain("Tickets.TransitionNotAllowed", detail);
}
