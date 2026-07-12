using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class CoupleMapping : IEntityTypeConfiguration<Couple>
{
    public void Configure(EntityTypeBuilder<Couple> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Names).IsRequired().HasMaxLength(200);
        builder.Property(x => x.WeddingDate).IsRequired();
        builder.Property(x => x.PhotoUrl).HasMaxLength(500);
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.EventLocation).HasMaxLength(500).HasDefaultValue(string.Empty);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.GiftDisplayMode).HasMaxLength(40).HasDefaultValue(GiftDisplayModes.Traditional);
    }
}
