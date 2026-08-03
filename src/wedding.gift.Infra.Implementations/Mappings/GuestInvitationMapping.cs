using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class GuestInvitationMapping : IEntityTypeConfiguration<GuestInvitation>
{
    public void Configure(EntityTypeBuilder<GuestInvitation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CoupleId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.CoupleId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.CoupleId, x.IsActive });
    }
}
