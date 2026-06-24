using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class ContributionMapping : IEntityTypeConfiguration<Contribution>
{
    public void Configure(EntityTypeBuilder<Contribution> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContributorName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Message).HasMaxLength(500);
        builder.Property(x => x.Amount).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.PaymentMethod).HasMaxLength(50);
        builder.Property(x => x.PaidAt).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(x => x.Gift)
            .WithMany(x => x.Contributions)
            .HasForeignKey(x => x.GiftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
