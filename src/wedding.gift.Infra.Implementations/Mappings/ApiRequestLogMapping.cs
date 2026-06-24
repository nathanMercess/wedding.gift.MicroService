using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public class ApiRequestLogMapping : IEntityTypeConfiguration<ApiRequestLog>
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
        builder.Property(x => x.QueryString).HasMaxLength(1000);
        builder.Property(x => x.EndpointName).HasMaxLength(300);
        builder.Property(x => x.StatusCode).IsRequired();
        builder.Property(x => x.IsSuccess).IsRequired();
        builder.Property(x => x.IsAuthenticated).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(64);
        builder.Property(x => x.UserRole).HasMaxLength(80);
        builder.Property(x => x.ClientIp).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.ExceptionType).HasMaxLength(200);
        builder.Property(x => x.ExceptionMessage).HasMaxLength(500);

        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => x.StatusCode);
        builder.HasIndex(x => new { x.Path, x.StartedAtUtc });
        builder.HasIndex(x => x.CorrelationId);
    }
}
