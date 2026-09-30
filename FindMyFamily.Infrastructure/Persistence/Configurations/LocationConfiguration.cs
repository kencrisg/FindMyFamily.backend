using FindMyFamily.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FindMyFamily.Infrastructure.Persistence.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id");

        builder.Property(l => l.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(l => l.Latitude)
            .HasColumnName("latitude")
            .HasPrecision(10, 8)
            .IsRequired();

        builder.Property(l => l.Longitude)
            .HasColumnName("longitude")
            .HasPrecision(11, 8)
            .IsRequired();

        builder.Property(l => l.CapturedAt)
            .HasColumnName("captured_at")
            .IsRequired();

        // Índices para optimizar consultas de rastreo e historial
        builder.HasIndex(l => l.UserId);
        builder.HasIndex(l => l.CapturedAt);
        builder.HasIndex(l => new { l.UserId, l.CapturedAt });

        // Relación con User
        builder.HasOne(l => l.User)
            .WithMany(u => u.Locations)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
