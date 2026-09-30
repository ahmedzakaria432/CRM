using System.Globalization;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

/// <summary>Every 422 the Tickets domain raises. The codes are the frontend's errors.code.* contract - never rename one.</summary>
internal static class TicketDomainErrors
{
    public static readonly DomainError ScopeRequired = new("Tickets.ScopeRequired", "A ticket must belong to a tenant, branch, and department.");
    public static readonly DomainError NumberInvalid = new("Tickets.NumberInvalid", "The ticket number sequence value is not valid.");
    public static readonly DomainError SubjectRequired = new("Tickets.SubjectRequired", "Enter a subject for the ticket.");
    public static readonly DomainError SubjectTooLong = new("Tickets.SubjectTooLong", "A subject cannot be more than 200 characters.");
    public static readonly DomainError DescriptionRequired = new("Tickets.DescriptionRequired", "Enter a description for the ticket.");
    public static readonly DomainError DescriptionTooLong = new("Tickets.DescriptionTooLong", "A description cannot be more than 4000 characters.");
    public static readonly DomainError CustomerRequired = new("Tickets.CustomerRequired", "A ticket must belong to a customer.");
    public static readonly DomainError ActorRequired = new("Tickets.ActorRequired", "The user performing the action could not be identified.");
    public static readonly DomainError ChannelUnsupported = new("Tickets.ChannelUnsupported", "The ticket channel is not supported.");
    public static readonly DomainError PriorityUnsupported = new("Tickets.PriorityUnsupported", "The priority must be Low, Normal, High or Urgent.");
    public static readonly DomainError PriorityUnchanged = new("Tickets.PriorityUnchanged", "The ticket already has this priority.");
    public static readonly DomainError AlreadyAssigned = new("Tickets.AlreadyAssigned", "The ticket is already assigned; unassign it first.");
    public static readonly DomainError AssigneeRequired = new("Tickets.AssigneeRequired", "Choose the agent to assign the ticket to.");
    public static readonly DomainError AssigneeUnchanged = new("Tickets.AssigneeUnchanged", "The ticket is already assigned to this agent.");
    public static readonly DomainError NotAssigned = new("Tickets.NotAssigned", "The ticket is not assigned.");
    public static readonly DomainError TransferToSameDepartment = new("Tickets.TransferToSameDepartment", "The ticket already belongs to this department.");
    public static readonly DomainError CategoryInactive = new("Tickets.CategoryInactive", "The category is inactive.");
    public static readonly DomainError CategoryDepartmentMismatch = new("Tickets.CategoryDepartmentMismatch", "The category belongs to another department.");
    public static readonly DomainError CategoryUnchanged = new("Tickets.CategoryUnchanged", "The ticket already has this category.");
    public static readonly DomainError CategoryNameRequired = new("Tickets.CategoryNameRequired", "Both the Arabic and English category names are required.");
    public static readonly DomainError CategoryNameTooLong = new("Tickets.CategoryNameTooLong", "A category name cannot be more than 200 characters.");
    public static readonly DomainError CategoryDisplayOrderInvalid = new("Tickets.CategoryDisplayOrderInvalid", "The display order must be between 0 and 9999.");
    public static readonly DomainError CategoryAlreadyActive = new("Tickets.CategoryAlreadyActive", "The category is already active.");
    public static readonly DomainError CategoryAlreadyInactive = new("Tickets.CategoryAlreadyInactive", "The category is already inactive.");
    public static readonly DomainError CategoryScopeRequired = new("Tickets.CategoryScopeRequired", "A category must belong to a tenant and a department.");
    public static readonly DomainError BodyRequired = new("Tickets.BodyRequired", "Enter the text of the message or note.");
    public static readonly DomainError BodyTooLong = new("Tickets.BodyTooLong", "A message or note cannot be more than 10000 characters.");

    public static DomainError TransitionNotAllowed(TicketAction action, TicketStatus status) =>
        new("Tickets.TransitionNotAllowed", string.Create(CultureInfo.InvariantCulture, $"'{action}' is not allowed while the ticket is {status}."));
}
