namespace Crm.SharedKernel;

public interface IHasRowVersion
{
    byte[] RowVersion { get; }
}
