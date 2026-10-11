namespace Crm.Modules.Tickets.Tests.Support;

/// <summary>The literal codes the frontend maps to errors.code.* messages. A rename breaks a test on purpose.
/// <c>TicketDomainErrors</c> is internal, so the domain codes are repeated here as literals.</summary>
public static class ErrorCodes
{
    public const string ScopeRequired = "Tickets.ScopeRequired";
    public const string NumberInvalid = "Tickets.NumberInvalid";
    public const string SubjectRequired = "Tickets.SubjectRequired";
    public const string SubjectTooLong = "Tickets.SubjectTooLong";
    public const string DescriptionRequired = "Tickets.DescriptionRequired";
    public const string DescriptionTooLong = "Tickets.DescriptionTooLong";
    public const string CustomerRequired = "Tickets.CustomerRequired";
    public const string ActorRequired = "Tickets.ActorRequired";
    public const string ChannelUnsupported = "Tickets.ChannelUnsupported";
    public const string PriorityUnsupported = "Tickets.PriorityUnsupported";
    public const string PriorityUnchanged = "Tickets.PriorityUnchanged";
    public const string AlreadyAssigned = "Tickets.AlreadyAssigned";
    public const string AssigneeRequired = "Tickets.AssigneeRequired";
    public const string AssigneeUnchanged = "Tickets.AssigneeUnchanged";
    public const string NotAssigned = "Tickets.NotAssigned";
    public const string TransferToSameDepartment = "Tickets.TransferToSameDepartment";
    public const string CategoryInactive = "Tickets.CategoryInactive";
    public const string CategoryDepartmentMismatch = "Tickets.CategoryDepartmentMismatch";
    public const string CategoryUnchanged = "Tickets.CategoryUnchanged";
    public const string CategoryNameRequired = "Tickets.CategoryNameRequired";
    public const string CategoryNameTooLong = "Tickets.CategoryNameTooLong";
    public const string CategoryDisplayOrderInvalid = "Tickets.CategoryDisplayOrderInvalid";
    public const string CategoryAlreadyActive = "Tickets.CategoryAlreadyActive";
    public const string CategoryAlreadyInactive = "Tickets.CategoryAlreadyInactive";
    public const string CategoryScopeRequired = "Tickets.CategoryScopeRequired";
    public const string BodyRequired = "Tickets.BodyRequired";
    public const string BodyTooLong = "Tickets.BodyTooLong";
    public const string TransitionNotAllowed = "Tickets.TransitionNotAllowed";

    public static readonly IReadOnlyList<string> Domain =
    [
        ScopeRequired, NumberInvalid, SubjectRequired, SubjectTooLong, DescriptionRequired, DescriptionTooLong, CustomerRequired,
        ActorRequired, ChannelUnsupported, PriorityUnsupported, PriorityUnchanged, AlreadyAssigned, AssigneeRequired,
        AssigneeUnchanged, NotAssigned, TransferToSameDepartment, CategoryInactive, CategoryDepartmentMismatch, CategoryUnchanged,
        CategoryNameRequired, CategoryNameTooLong, CategoryDisplayOrderInvalid, CategoryAlreadyActive, CategoryAlreadyInactive,
        CategoryScopeRequired, BodyRequired, BodyTooLong, TransitionNotAllowed,
    ];

    // Application results (Result failures, not exceptions).
    public const string TicketNotFound = "Tickets.TicketNotFound";
    public const string CategoryNotFound = "Tickets.CategoryNotFound";
    public const string OutOfScope = "Organisation.OutOfScope";
    public const string DepartmentRequired = "Organisation.DepartmentRequired";
    public const string CustomerInactive = "Tickets.CustomerInactive";
    public const string ContactNotOfCustomer = "Tickets.ContactNotOfCustomer";
    public const string AssigneeInactive = "Tickets.AssigneeInactive";
    public const string AssigneeNotInDepartment = "Tickets.AssigneeNotInDepartment";
    public const string TargetDepartmentInactive = "Tickets.TargetDepartmentInactive";
    public const string CategoryDepartmentInactive = "Tickets.CategoryDepartmentInactive";
    public const string AlreadyClaimed = "Tickets.AlreadyClaimed";
    public const string PortalOnly = "Authorization.PortalOnly";
}
