using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class ApiRequestLogMapping : IEntityTypeConfiguration<ApiRequestLog>
{
    public void Configure(EntityTypeBuilder<ApiRequestLog> builder)
    {
        builder.ToTable("ApiRequestLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StartedAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc).IsRequired();
        builder.Property(x => x.DurationMilliseconds).IsRequired();
        builder.Property(x => x.Method).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Path).IsRequired().HasMaxLength(500);
        builder.Property(x => x.QueryString).IsRequired(false).HasMaxLength(1000);
        builder.Property(x => x.EndpointName).IsRequired(false).HasMaxLength(300);
        builder.Property(x => x.StatusCode).IsRequired();
        builder.Property(x => x.IsSuccess).IsRequired();
        builder.Property(x => x.IsAuthenticated).IsRequired();
        builder.Property(x => x.UserId).IsRequired(false).HasMaxLength(64);
        builder.Property(x => x.UserRole).IsRequired(false).HasMaxLength(80);
        builder.Property(x => x.ClientIp).IsRequired(false).HasMaxLength(64);
        builder.Property(x => x.UserAgent).IsRequired(false).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).IsRequired(false).HasMaxLength(100);
        builder.Property(x => x.ExceptionType).IsRequired(false).HasMaxLength(200);
        builder.Property(x => x.ExceptionMessage).IsRequired(false).HasMaxLength(500);

        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => x.StatusCode);
        builder.HasIndex(x => new { x.Path, x.StartedAtUtc });
        builder.HasIndex(x => x.CorrelationId);
    }
}
