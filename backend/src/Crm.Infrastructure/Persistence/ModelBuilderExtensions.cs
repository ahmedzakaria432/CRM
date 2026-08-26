using System.Linq.Expressions;
using Crm.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// EF Core 10's NAMED HasQueryFilter overload. Verified by probe: two named filters compose and
    /// AND together, and IgnoreQueryFilters([name]) lifts only that one filter. The unnamed overload
    /// silently REPLACES a prior filter instead of composing with it - using it here would let
    /// Story 06's tenant filter overwrite this one and resurrect soft-deleted rows product-wide with
    /// no error. Do not switch to the unnamed overload; do not rename CrmDbContext.SoftDeleteFilterName
    /// without updating Story 06.
    /// </summary>
    public static void ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                // Owned types inherit the owner's filter - they have no query of their own to filter.
                continue;
            }

            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "e");
            UnaryExpression body = Expression.Not(
                Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(CrmDbContext.SoftDeleteFilterName, Expression.Lambda(body, parameter));
        }
    }

    /// <summary>
    /// Every persisted DateTimeOffset round-trips through UTC. DateTime loses its offset on SQL
    /// Server and reads back as Unspecified - the standard source of an SLA clock reading three
    /// hours off for a product whose users are in Asia/Riyadh.
    /// </summary>
    public static void ApplyUtcDateTimeOffsetConverter(this ModelBuilder modelBuilder)
    {
        var converter = new ValueConverter<DateTimeOffset, DateTimeOffset>(
            v => v.ToUniversalTime(),
            v => v.ToUniversalTime());

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(converter);
                }
            }
        }
    }
}
