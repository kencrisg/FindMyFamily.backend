using FindMyFamily.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FindMyFamily.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id");

        builder.Property(d => d.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(d => d.InstallationId)
            .HasColumnName("installation_id")
            .HasMaxLength(128);

        builder.Property(d => d.FcmToken)
            .HasColumnName("fcm_token");

        builder.Property(d => d.RefreshTokenHash)
            .HasColumnName("refresh_token_hash")
            .HasMaxLength(64);

        builder.Property(d => d.RefreshTokenExpiry)
            .HasColumnName("refresh_token_expiry");

        builder.Property(d => d.DeviceModel)
            .HasColumnName("device_model")
            .HasMaxLength(100);

        builder.Property(d => d.LastActiveAt)
            .HasColumnName("last_active_at");

        builder.Property(d => d.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        // El refresh se resuelve buscando por hash: sin índice era un recorrido completo de tabla.
        builder.HasIndex(d => d.RefreshTokenHash);

        // Un InstallationId identifica un dispositivo por usuario de forma única.
        builder.HasIndex(d => new { d.UserId, d.InstallationId })
            .IsUnique()
            .HasFilter("installation_id IS NOT NULL");

        // Relación con User
        builder.HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
