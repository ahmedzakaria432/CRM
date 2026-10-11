using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Persistence;

public sealed class ModelBuilderExtensionsTests
{
    private static IModel DesignModel(TestDbContext context) => context.GetService<IDesignTimeModel>().Model;

    private static List<string?> FilterNames(IEntityType entityType) =>
        [.. entityType.GetDeclaredQueryFilters().Select(f => f.Key)];

    [Fact]
    public async Task EverySoftDeletableEntityCarriesTheNamedSoftDeleteFilter()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var model = DesignModel(context);

        foreach (Type type in new[] { typeof(DepartmentRow), typeof(ContactRow) })
        {
            var entityType = Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(type));
            Assert.Contains(ModuleDbContext.SoftDeleteFilterName, FilterNames(entityType));
        }
    }

    [Fact]
    public async Task EntitiesThatAreNotSoftDeletableCarryNoSoftDeleteFilter()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var model = DesignModel(context);

        foreach (Type type in new[] { typeof(TenantRow), typeof(TransferableRow), typeof(OwnedRow), typeof(UnscopedRow), typeof(EventfulRow) })
        {
            var entityType = Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(type));
            Assert.DoesNotContain(ModuleDbContext.SoftDeleteFilterName, FilterNames(entityType));
        }
    }

    [Fact]
    public async Task TheScopeFilterIsASecondNamedFilterNextToTheSoftDeleteFilter()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var model = DesignModel(context);

        var department = Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(typeof(DepartmentRow)));
        var unscoped = Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(typeof(UnscopedRow)));

        Assert.Equal(
            [ModuleDbContext.ScopeFilterName, ModuleDbContext.SoftDeleteFilterName],
            FilterNames(department).Order(StringComparer.Ordinal));
        Assert.Empty(unscoped.GetDeclaredQueryFilters());
    }

    [Fact]
    public async Task OwnedTypesGetNoFilterOfTheirOwn()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var address = Assert.IsAssignableFrom<IEntityType>(DesignModel(context).FindEntityType(typeof(AddressValue)));

        Assert.True(address.IsOwned());
        Assert.Empty(address.GetDeclaredQueryFilters());
    }

    [Fact]
    public async Task EveryDateTimeOffsetPropertyHasAConverterIncludingOwnedAndNullable()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var properties = DesignModel(context).GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?))
            .ToList();

        Assert.NotEmpty(properties);
        Assert.Contains(properties, p => p.DeclaringType.ClrType == typeof(AddressValue));
        Assert.All(properties, p => Assert.NotNull(p.GetValueConverter()));
    }

    [Fact]
    public async Task TheConverterNormalisesToUtc()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var property = Assert.IsAssignableFrom<IProperty>(
            DesignModel(context).FindEntityType(typeof(UnscopedRow))?.FindProperty(nameof(UnscopedRow.SeenAtUtc)));
        var converter = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter>(property.GetValueConverter());
        var local = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(3));

        var toProvider = Assert.IsType<DateTimeOffset>(converter.ConvertToProvider(local));
        var fromProvider = Assert.IsType<DateTimeOffset>(converter.ConvertFromProvider(local));

        Assert.Equal(TimeSpan.Zero, toProvider.Offset);
        Assert.Equal(local, toProvider);
        Assert.Equal(TimeSpan.Zero, fromProvider.Offset);
    }

    [Fact]
    public async Task ScopeIdsAreMappedThroughAConventionConverter()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var entity = Assert.IsAssignableFrom<IEntityType>(DesignModel(context).FindEntityType(typeof(DepartmentRow)));

        foreach (string name in new[] { nameof(DepartmentRow.TenantId), nameof(DepartmentRow.BranchId), nameof(DepartmentRow.DepartmentId) })
        {
            var converter = entity.FindProperty(name)?.GetValueConverter();
            Assert.NotNull(converter);
            Assert.Equal(typeof(Guid), converter.ProviderClrType);
        }
    }
}
