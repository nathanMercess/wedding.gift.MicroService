using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class GuestConfirmationMapping : IEntityTypeConfiguration<GuestConfirmation>
{
    public void Configure(EntityTypeBuilder<GuestConfirmation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CoupleId).IsRequired();
        builder.Property(x => x.ConfirmedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasMany(x => x.Guests)
            .WithOne(x => x.GuestConfirmation)
            .HasForeignKey(x => x.GuestConfirmationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Guests).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.CoupleId, x.ConfirmedAtUtc });
    }
}
