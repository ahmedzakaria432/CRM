namespace Crm.Modules.Tickets.Contracts.DevSeed;

/// <summary>Well-known Development ticket and category ids, as primitives - for Stories 18, 20, 47 and the .http
/// files. Never used outside Development. The seed takes T-000001..T-000003.</summary>
public static class TicketsDevSeed
{
    public static readonly Guid RyCareGeneralCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000001");
    public static readonly Guid RyCareTechnicalCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000002");
    public static readonly Guid RyCareComplaintCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000003");

    /// <summary>Active, not customer-visible.</summary>
    public static readonly Guid RyCareInternalCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000004");

    /// <summary>Inactive - never in a picker.</summary>
    public static readonly Guid RyCareLegacyCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000005");
    public static readonly Guid RyBillInvoiceCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000006");
    public static readonly Guid JeCareGeneralCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000007");

    /// <summary>T-000001: Ahmed, RYD-CARE, New, Normal, General, unassigned, Arabic subject.</summary>
    public static readonly Guid AhmedNewTicketId = Guid.Parse("15000000-0000-0000-0000-000000000001");

    /// <summary>T-000002: Najd (contact Sara), RYD-CARE, Open, High, Technical, assigned to agent A.</summary>
    public static readonly Guid NajdOpenTicketId = Guid.Parse("15000000-0000-0000-0000-000000000002");

    /// <summary>T-000003: Red Sea (contact Fahad), JED-CARE, New, Urgent, JED General, unassigned.</summary>
    public static readonly Guid RedSeaNewTicketId = Guid.Parse("15000000-0000-0000-0000-000000000003");
}
