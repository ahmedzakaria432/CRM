namespace Crm.Modules.Tickets.Contracts.DevSeed;

/// <summary>Well-known Development ticket and category ids, as primitives - for Stories 18, 20, 47 and the .http
/// files. Never used outside Development. The seed takes T-000001..T-000015 on a fresh database (Story 59 added 04..15).</summary>
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

    /// <summary>T-000004: Al-Noor (Mona), RYD-CARE, Open, High, Technical, assigned to agent A.</summary>
    public static readonly Guid AlNoorOpenTicketId = Guid.Parse("15000000-0000-0000-0000-000000000004");

    /// <summary>T-000005: Al-Noor (Mona), RYD-CARE, PendingCustomer, Normal, General, agent A, Portal channel, Arabic text.</summary>
    public static readonly Guid AlNoorPendingTicketId = Guid.Parse("15000000-0000-0000-0000-000000000005");

    /// <summary>T-000006: Al-Noor (Yousef), RYD-CARE, New, Low, General, unassigned, Portal channel.</summary>
    public static readonly Guid AlNoorYousefNewTicketId = Guid.Parse("15000000-0000-0000-0000-000000000006");

    /// <summary>T-000007: Al-Noor (Yousef), RYD-CARE, Resolved, Normal, Complaint, supervisor B, Portal channel.</summary>
    public static readonly Guid AlNoorYousefResolvedTicketId = Guid.Parse("15000000-0000-0000-0000-000000000007");

    /// <summary>T-000008: Al-Noor (Mona), RYD-CARE, Closed, Low, General, agent A.</summary>
    public static readonly Guid AlNoorClosedTicketId = Guid.Parse("15000000-0000-0000-0000-000000000008");

    /// <summary>T-000009: Al-Noor (Mona), RYD-CARE, OnHold, Urgent, Technical, supervisor B, Arabic text.</summary>
    public static readonly Guid AlNoorOnHoldTicketId = Guid.Parse("15000000-0000-0000-0000-000000000009");

    /// <summary>T-000010: Al-Noor (Mona), RYD-CARE, Cancelled, Normal, General, unassigned.</summary>
    public static readonly Guid AlNoorCancelledTicketId = Guid.Parse("15000000-0000-0000-0000-000000000010");

    /// <summary>T-000011: Gulf Retail (Nasser), RYD-BILL, New, Normal, Invoice question, unassigned.</summary>
    public static readonly Guid GulfRetailNewTicketId = Guid.Parse("15000000-0000-0000-0000-000000000011");

    /// <summary>T-000012: Gulf Retail (Nasser), RYD-BILL, Open, High, Invoice question, assigned to the supervisor.</summary>
    public static readonly Guid GulfRetailOpenTicketId = Guid.Parse("15000000-0000-0000-0000-000000000012");

    /// <summary>T-000013: Red Sea (Fahad), JED-CARE, Open, Normal, JED General, agent B, Arabic text.</summary>
    public static readonly Guid RedSeaOpenTicketId = Guid.Parse("15000000-0000-0000-0000-000000000013");

    /// <summary>T-000014: Red Sea (Fahad), JED-CARE, Resolved, Low, JED General, agent B.</summary>
    public static readonly Guid RedSeaResolvedTicketId = Guid.Parse("15000000-0000-0000-0000-000000000014");

    /// <summary>T-000015: Red Sea (Fahad), JED-CARE, New, High, JED General, unassigned.</summary>
    public static readonly Guid RedSeaHighNewTicketId = Guid.Parse("15000000-0000-0000-0000-000000000015");
}
