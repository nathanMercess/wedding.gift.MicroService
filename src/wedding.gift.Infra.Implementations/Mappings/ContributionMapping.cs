using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class ContributionMapping : IEntityTypeConfiguration<Contribution>
{
    public void Configure(EntityTypeBuilder<Contribution> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CoupleId).IsRequired();
        builder.Property(x => x.OrderId).IsRequired(false).HasMaxLength(100);
        builder.Property(x => x.GuestEmail).IsRequired(false).HasMaxLength(180);
        builder.Property(x => x.ContributorName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Message).IsRequired(false).HasMaxLength(500);
        builder.Property(x => x.Amount).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.RefundedAmount).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.PaymentMethod).IsRequired(false).HasMaxLength(50);
        builder.Property(x => x.PaymentStatus).IsRequired(false).HasMaxLength(50);
        builder.Property(x => x.PaidAt).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.MessageReadAtUtc).IsRequired(false);
        builder.Property(x => x.MessageArchivedAtUtc).IsRequired(false);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(x => x.Gift)
            .WithMany(x => x.Contributions)
            .HasForeignKey(x => x.GiftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.NetAmount);

        builder.HasIndex(x => new { x.Status, x.PaidAt });
        builder.HasIndex(x => new { x.CoupleId, x.Status, x.PaidAt });
        builder.HasIndex(x => x.OrderId).IsUnique().HasFilter("[OrderId] <> ''");
        builder.HasIndex(x => new { x.MessageArchivedAtUtc, x.MessageReadAtUtc });
    }
}
