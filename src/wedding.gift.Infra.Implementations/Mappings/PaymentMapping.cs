using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public class PaymentMapping : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Method).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Amount).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.Installments).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        builder.Property(x => x.StatusDetail).HasMaxLength(60);
        builder.Property(x => x.Nsu).HasMaxLength(100);
        builder.Property(x => x.MpOrderId).HasMaxLength(50);
        builder.Property(x => x.MpPaymentId).HasMaxLength(50);
        builder.Property(x => x.PixQrCode).HasMaxLength(1000);
        builder.Property(x => x.QrCodeBase64);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.Nsu);
        builder.HasIndex(x => x.MpOrderId);
    }
}
