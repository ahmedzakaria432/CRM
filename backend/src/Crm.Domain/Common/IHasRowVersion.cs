namespace Crm.Domain.Common;

public interface IHasRowVersion
{
    byte[] RowVersion { get; }
}
