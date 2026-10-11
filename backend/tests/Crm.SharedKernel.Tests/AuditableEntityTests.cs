using System.Reflection;

namespace Crm.SharedKernel.Tests;

public sealed class AuditableEntityTests
{
    [Fact]
    public void AllAuditPropertiesAreNullOnANewInstance()
    {
        var entity = new Note(Guid.NewGuid());

        Assert.Null(entity.CreatedBy);
        Assert.Null(entity.CreatedAtUtc);
        Assert.Null(entity.ModifiedBy);
        Assert.Null(entity.ModifiedAtUtc);
    }

    [Theory]
    [InlineData("CreatedBy")]
    [InlineData("CreatedAtUtc")]
    [InlineData("ModifiedBy")]
    [InlineData("ModifiedAtUtc")]
    public void AuditSettersAreNotPublic(string propertyName)
    {
        var property = typeof(AuditableEntity<Guid>).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.NotNull(property.GetGetMethod());
        Assert.Null(property.GetSetMethod(nonPublic: false));
    }

    [Fact]
    public void AuditSettersExistAsPrivateSoTheInterceptorCanWriteThroughEf()
    {
        var property = typeof(AuditableEntity<Guid>).GetProperty("CreatedBy");

        Assert.NotNull(property);
        Assert.NotNull(property.GetSetMethod(nonPublic: true));
    }

    [Fact]
    public void ItImplementsIAuditable()
    {
        Assert.IsAssignableFrom<IAuditable>(new Note(Guid.NewGuid()));
    }

    [Fact]
    public void ItIsAnAggregateRoot()
    {
        Assert.IsAssignableFrom<AggregateRoot<Guid>>(new Note(Guid.NewGuid()));
    }

    [Fact]
    public void NoPublicMethodStampsTheAuditFields()
    {
        var publicMethods = typeof(AuditableEntity<Guid>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(publicMethods, m => !m.IsSpecialName);
    }

    private sealed class Note(Guid id) : AuditableEntity<Guid>(id);
}
