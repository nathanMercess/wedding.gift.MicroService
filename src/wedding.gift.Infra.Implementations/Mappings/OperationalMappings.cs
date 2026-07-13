using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class PaymentOrderLookupTokenMapping : IEntityTypeConfiguration<PaymentOrderLookupToken>
{
    public void Configure(EntityTypeBuilder<PaymentOrderLookupToken> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.PaymentId, x.ExpiresAtUtc });
    }
}

public sealed class OrderLookupAttemptMapping : IEntityTypeConfiguration<OrderLookupAttempt>
{
    public void Configure(EntityTypeBuilder<OrderLookupAttempt> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IpHash).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EmailHash).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => new { x.IpHash, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.EmailHash, x.CreatedAtUtc });
    }
}

public sealed class EmailOutboxMessageMapping : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RecipientEmail).IsRequired().HasMaxLength(180);
        builder.Property(x => x.RecipientName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.GiftName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.CoupleNames).IsRequired().HasMaxLength(200);
        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Method).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Message).IsRequired(false).HasMaxLength(500);
        builder.Property(x => x.Amount).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.LastError).IsRequired(false).HasMaxLength(500);
        builder.HasIndex(x => new { x.PaymentId, x.Type }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
    }
}

public sealed class AuditLogMapping : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EntityId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CorrelationId).IsRequired(false).HasMaxLength(100);
        builder.HasIndex(x => new { x.CoupleId, x.CreatedAtUtc });
    }
}

public sealed class PaymentRefundOperationMapping : IEntityTypeConfiguration<PaymentRefundOperation>
{
    public void Configure(EntityTypeBuilder<PaymentRefundOperation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.IsFullRefund).IsRequired();
        builder.Property(x => x.RefundedAmount).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.PaymentId, x.CreatedAt });
    }
}
