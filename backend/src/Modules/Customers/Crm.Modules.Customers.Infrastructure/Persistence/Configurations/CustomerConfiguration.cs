using Crm.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Customers.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);

        // CustomerId is not a scope type, so it needs its own conversion; TenantId/BranchId/DepartmentId are
        // converted by the ModuleDbContext convention - never configure them here.
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new CustomerId(value))
            .ValueGeneratedNever();

        // OwnsOne (not HasConversion) so c.Number.Value.Contains(term) translates. Unique globally - the sequence is global.
        builder.OwnsOne(x => x.Number, number =>
        {
            number.Property(p => p.Value).HasColumnName("Number").HasMaxLength(CustomerNumber.MaxLength).IsRequired();
            number.HasIndex(p => p.Value).IsUnique();
        });

        builder.OwnsOne(x => x.DisplayName, name =>
        {
            name.Property(p => p.Arabic).HasColumnName("DisplayNameAr").HasMaxLength(LocalizedText.MaxLength).IsRequired();
            name.Property(p => p.English).HasColumnName("DisplayNameEn").HasMaxLength(LocalizedText.MaxLength).IsRequired();
        });

        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LegalName).HasMaxLength(Customer.LegalNameMaxLength);

        builder.Property(x => x.PreferredLanguage)
            .HasConversion(l => l.Value, v => PreferredLanguage.Create(v))
            .HasColumnName("PreferredLanguage")
            .HasMaxLength(2)
            .IsRequired();

        builder.Ignore(x => x.IsActive);

        builder.OwnsMany(x => x.Contacts, contacts =>
        {
            contacts.ToTable("CustomerContacts");
            contacts.WithOwner().HasForeignKey("CustomerId");
            contacts.HasKey(c => c.Id);
            // Client-generated key: without this EF treats a contact added to a tracked customer as an existing row
            // (UPDATE, 0 rows).
            contacts.Property(c => c.Id).ValueGeneratedNever();
            contacts.Property(c => c.Name).HasMaxLength(Contact.NameMaxLength).IsRequired();
            contacts.Property(c => c.JobTitle).HasMaxLength(Contact.JobTitleMaxLength);

            // Optional owned value objects in the same table: all-null columns = no email / no phone.
            contacts.OwnsOne(c => c.Email, email =>
            {
                email.Property(p => p.Value).HasColumnName("Email").HasMaxLength(EmailAddress.MaxLength);
                email.HasIndex(p => p.Value);
            });
            contacts.OwnsOne(c => c.Phone, phone =>
            {
                phone.Property(p => p.Value).HasColumnName("Phone").HasMaxLength(PhoneNumber.MaxLength);
                phone.HasIndex(p => p.Value);
            });
        });
        builder.Navigation(x => x.Contacts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Notes, notes =>
        {
            notes.ToTable("CustomerNotes");
            notes.WithOwner().HasForeignKey("CustomerId");
            notes.HasKey(n => n.Id);
            notes.Property(n => n.Id).ValueGeneratedNever();
            notes.Property(n => n.Body).HasMaxLength(CustomerNote.BodyMaxLength).IsRequired();
        });
        builder.Navigation(x => x.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // WHERE IsDeleted = 0 and the scope filter's (TenantId, BranchId, DepartmentId) predicate on every read.
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.TenantId, x.DepartmentId });
    }
}
