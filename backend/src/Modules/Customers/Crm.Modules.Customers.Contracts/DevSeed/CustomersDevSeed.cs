namespace Crm.Modules.Customers.Contracts.DevSeed;

/// <summary>Well-known Development customer and contact ids, as primitives - for later seeds (Story 15 tickets,
/// Story 47 portal accounts) and the .http files. Never used outside Development.</summary>
public static class CustomersDevSeed
{
    /// <summary>Individual, Riyadh Customer Care. Contact has portal access (Story 47).</summary>
    public static readonly Guid AhmedCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000001");
    public static readonly Guid AhmedContactId = Guid.Parse("11000000-0000-0000-0001-000000000001");
    public const string AhmedEmail = "ahmed@example.com";
    public const string AhmedPhone = "+966501234567";

    /// <summary>Organisation, Riyadh Customer Care, two contacts and a pinned note.</summary>
    public static readonly Guid NajdLogisticsCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000002");
    public static readonly Guid NajdSaraContactId = Guid.Parse("11000000-0000-0000-0001-000000000002");
    public static readonly Guid NajdOmarContactId = Guid.Parse("11000000-0000-0000-0001-000000000003");
    public static readonly Guid NajdPinnedNoteId = Guid.Parse("11000000-0000-0000-0002-000000000001");

    /// <summary>Organisation, Jeddah Customer Care (agent B's department - invisible to agent A).</summary>
    public static readonly Guid RedSeaTradingCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000003");
    public static readonly Guid RedSeaFahadContactId = Guid.Parse("11000000-0000-0000-0001-000000000004");
}
