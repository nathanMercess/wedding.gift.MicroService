using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public class UserMapping : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(180);
        builder.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(180);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(300);
        builder.Property(x => x.PasswordSalt).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(30).HasDefaultValue(UserRoles.Admin);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}
