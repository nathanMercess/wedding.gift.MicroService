using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class ConfirmedGuestMapping : IEntityTypeConfiguration<ConfirmedGuest>
{
    public void Configure(EntityTypeBuilder<ConfirmedGuest> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GuestConfirmationId).IsRequired();
        builder.Property(x => x.GuestInvitationId).IsRequired(false);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(20);
        builder.Property(x => x.IsSubmitter).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasOne(x => x.GuestInvitation)
            .WithMany()
            .HasForeignKey(x => x.GuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.GuestConfirmationId);
        builder.HasIndex(x => x.GuestInvitationId)
            .IsUnique()
            .HasFilter("[GuestInvitationId] IS NOT NULL");
    }
}
