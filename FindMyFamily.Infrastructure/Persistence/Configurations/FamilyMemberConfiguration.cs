using FindMyFamily.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FindMyFamily.Infrastructure.Persistence.Configurations;

public class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
{
    public void Configure(EntityTypeBuilder<FamilyMember> builder)
    {
        builder.ToTable("family_members");

        // Clave primaria compuesta
        builder.HasKey(fm => new { fm.FamilyId, fm.UserId });

        builder.Property(fm => fm.FamilyId)
            .HasColumnName("family_id")
            .IsRequired();

        builder.Property(fm => fm.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(fm => fm.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(fm => fm.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        // Relaciones
        builder.HasOne(fm => fm.Family)
            .WithMany(f => f.FamilyMembers)
            .HasForeignKey(fm => fm.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(fm => fm.User)
            .WithMany(u => u.FamilyMembers)
            .HasForeignKey(fm => fm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Filtro equivalente al de User: sin él, al ocultar al usuario borrado esta fila
        // quedaba huérfana a medias (visible sola, desaparecida en cuanto se hacía Include
        // del usuario, porque la relación obligatoria se traduce a INNER JOIN).
        builder.HasQueryFilter(fm => !fm.User.IsDeleted);
    }
}
