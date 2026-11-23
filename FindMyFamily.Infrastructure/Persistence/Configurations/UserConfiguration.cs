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

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .HasDefaultValue("user")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");

        // Sin HasDefaultValue a propósito: con un bool cuyo valor por defecto en la base es
        // true, EF no distingue "false" de "sin asignar" y en un INSERT ignoraría el false.
        // El valor inicial lo da la entidad (= true); la migración pone true a las filas
        // que ya existían.
        builder.Property(u => u.LocationSharingEnabled)
            .HasColumnName("location_sharing_enabled")
            .IsRequired();

        builder.Property(u => u.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();


        // Filtro global: toda consulta sobre Users excluye las cuentas borradas sin que
        // cada repositorio tenga que recordarlo. Para operar sobre ellas (restaurar, o
        // detectar que un teléfono pertenece a una cuenta borrada) hay que pedirlo
        // explícitamente con IgnoreQueryFilters().
        builder.HasQueryFilter(u => !u.IsDeleted);

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
