using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;

namespace wedding.gift.Infra.Implementations.Repositories;

public sealed class PaymentRepository(AppDbContext context) : IPaymentRepository
{
    public async Task SaveAsync(Payment payment, CancellationToken cancellationToken)
    {
        await context.Payments.AddAsync(payment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Payment?> GetByNsuAsync(string nsu, CancellationToken cancellationToken)
    {
        return await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Nsu == nsu, cancellationToken);
    }

    public async Task<Payment?> GetByMpOrderIdAsync(string mpOrderId, CancellationToken cancellationToken)
    {
        return await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MpOrderId == mpOrderId, cancellationToken);
    }

    public async Task<Payment?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken)
    {
        return await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);
    }

    public async Task UpdateStatusAsync(string orderId, string status, string? statusDetail, CancellationToken cancellationToken)
    {
        Payment payment = await context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

        if (payment != null)
        {
            payment.Status = status;
            payment.StatusDetail = statusDetail;
            payment.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
