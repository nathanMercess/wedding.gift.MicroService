using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class PaymentMapping : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GiftId).IsRequired();
        builder.Property(x => x.ContributorName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Message).IsRequired(false).HasMaxLength(500);
        builder.Property(x => x.PayerEmail).IsRequired().HasMaxLength(180);
        builder.Property(x => x.PayerDocType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.PayerDocNumber).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ContributionCreated).IsRequired();
        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Method).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Amount).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.Installments).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        builder.Property(x => x.StatusDetail).IsRequired(false).HasMaxLength(60);
        builder.Property(x => x.Nsu).IsRequired(false).HasMaxLength(100);
        builder.Property(x => x.MpOrderId).IsRequired(false).HasMaxLength(50);
        builder.Property(x => x.MpPaymentId).IsRequired(false).HasMaxLength(50);
        builder.Property(x => x.PixQrCode).IsRequired(false).HasMaxLength(1000);
        builder.Property(x => x.QrCodeBase64).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne(x => x.Contribution)
            .WithMany()
            .HasForeignKey(x => x.ContributionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.GiftId);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => x.Nsu);
        builder.HasIndex(x => x.MpOrderId).IsUnique().HasFilter("[MpOrderId] IS NOT NULL");
        builder.HasIndex(x => x.ContributionId).IsUnique().HasFilter("[ContributionId] IS NOT NULL");
    }
}
