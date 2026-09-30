using Crm.SharedKernel.Results;

namespace Crm.Modules.Customers.Application;

internal static class CustomersErrors
{
    public static Error CustomerNotFound(Guid id) =>
        Error.NotFound("Customers.CustomerNotFound", $"Customer '{id}' was not found.");

    public static Error ContactNotFound(Guid id) =>
        Error.NotFound("Customers.ContactNotFound", $"Contact '{id}' was not found on this customer.");

    public static Error PortalContactNotFound(Guid id) =>
        Error.NotFound("Customers.PortalContactNotFound", $"Contact '{id}' has no portal access.");

    public static Error NoteNotFound(Guid id) =>
        Error.NotFound("Customers.NoteNotFound", $"Note '{id}' was not found on this customer.");

    public static Error DepartmentOutOfScope() =>
        Error.Forbidden("Organisation.OutOfScope", "The requested department is outside your scope.");

    public static Error DepartmentRequired() =>
        Error.Validation("Organisation.DepartmentRequired", "DepartmentId: Choose the department this record belongs to.");
}
