namespace Crm.Modules.Customers.Application.Specifications;

public enum CustomerSort
{
    CreatedAt,
    Number,
    NameEn,
    NameAr,
}

public static class CustomerSortParser
{
    public static bool TryParse(string value, out CustomerSort sort)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Equals("createdAt", StringComparison.OrdinalIgnoreCase))
        {
            sort = CustomerSort.CreatedAt;
        }
        else if (value.Equals("number", StringComparison.OrdinalIgnoreCase))
        {
            sort = CustomerSort.Number;
        }
        else if (value.Equals("nameEn", StringComparison.OrdinalIgnoreCase))
        {
            sort = CustomerSort.NameEn;
        }
        else if (value.Equals("nameAr", StringComparison.OrdinalIgnoreCase))
        {
            sort = CustomerSort.NameAr;
        }
        else
        {
            sort = CustomerSort.CreatedAt;
            return false;
        }

        return true;
    }
}
