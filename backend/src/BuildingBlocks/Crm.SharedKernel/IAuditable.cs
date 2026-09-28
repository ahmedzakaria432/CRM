namespace Crm.SharedKernel;

public interface IAuditable
{
    string? CreatedBy { get; }

    DateTimeOffset? CreatedAtUtc { get; }

    string? ModifiedBy { get; }

    DateTimeOffset? ModifiedAtUtc { get; }
}
