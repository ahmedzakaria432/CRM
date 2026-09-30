using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>Tracked: sign-in writes the failed-attempt counter on the user it finds. Email is unique globally,
/// so there is no tenant criterion.</summary>
public sealed class UserByEmailSpecification : Specification<User>
{
    public UserByEmailSpecification(EmailAddress email)
    {
        ArgumentNullException.ThrowIfNull(email);

        string value = email.Value;
        Where(u => u.Email.Value == value);
    }
}
