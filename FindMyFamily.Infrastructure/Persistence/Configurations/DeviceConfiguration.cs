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

        builder.Property(d => d.FcmToken)
            .HasColumnName("fcm_token");

        builder.Property(d => d.RefreshToken)
            .HasColumnName("refresh_token");

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

        // Relación con User
        builder.HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
