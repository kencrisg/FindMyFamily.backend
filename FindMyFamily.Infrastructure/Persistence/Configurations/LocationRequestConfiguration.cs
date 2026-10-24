using FindMyFamily.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FindMyFamily.Infrastructure.Persistence.Configurations;

public class LocationRequestConfiguration : IEntityTypeConfiguration<LocationRequest>
{
    public void Configure(EntityTypeBuilder<LocationRequest> builder)
    {
        builder.ToTable("location_requests");

        builder.HasKey(lr => lr.Id);
        builder.Property(lr => lr.Id)
            .HasColumnName("id");

        builder.Property(lr => lr.RequesterId)
            .HasColumnName("requester_id")
            .IsRequired();

        builder.Property(lr => lr.TargetUserId)
            .HasColumnName("target_user_id")
            .IsRequired();

        builder.Property(lr => lr.FamilyId)
            .HasColumnName("family_id")
            .IsRequired();

        builder.Property(lr => lr.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(lr => lr.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(lr => lr.FulfilledAt)
            .HasColumnName("fulfilled_at");

        builder.Property(lr => lr.LocationId)
            .HasColumnName("location_id");

        // Índice para resolver "peticiones vigentes dirigidas a este usuario".
        builder.HasIndex(lr => new { lr.TargetUserId, lr.FulfilledAt, lr.ExpiresAt });

        // Índice para el control de frecuencia por par (solicitante, objetivo).
        builder.HasIndex(lr => new { lr.RequesterId, lr.TargetUserId, lr.CreatedAt });

        builder.HasOne(lr => lr.Requester)
            .WithMany()
            .HasForeignKey(lr => lr.RequesterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict en el objetivo: con Cascade en ambas FK hacia users, PostgreSQL rechaza
        // crear la tabla por caminos de borrado múltiples sobre la misma entidad.
        builder.HasOne(lr => lr.TargetUser)
            .WithMany()
            .HasForeignKey(lr => lr.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(lr => lr.Family)
            .WithMany()
            .HasForeignKey(lr => lr.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
