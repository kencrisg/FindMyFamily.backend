using FindMyFamily.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FindMyFamily.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id");

        builder.Property(u => u.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");

        // Relaciones
        builder.HasMany(u => u.Devices)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.FamilyMembers)
            .WithOne(fm => fm.User)
            .HasForeignKey(fm => fm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Locations)
            .WithOne(l => l.User)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
