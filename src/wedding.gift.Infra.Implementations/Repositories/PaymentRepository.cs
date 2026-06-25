using System.Data;
using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;

namespace wedding.gift.Infra.Implementations.Repositories;

public sealed class PaymentRepository(AppDbContext context) : IPaymentRepository
{
    public IQueryable<Payment> Query()
        => context.Payments.AsNoTracking();

    public async Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken)
        => await context.Payments.AsNoTracking().ToListAsync(cancellationToken);

    public async Task SaveAsync(Payment payment, CancellationToken cancellationToken)
    {
        await context.Payments.AddAsync(payment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Payment?> GetByNsuAsync(string nsu, CancellationToken cancellationToken)
        => await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Nsu == nsu, cancellationToken);

    public async Task<Payment?> GetByMpOrderIdAsync(string mpOrderId, CancellationToken cancellationToken)
        => await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MpOrderId == mpOrderId, cancellationToken);

    public async Task<Payment?> GetByMpOrderIdForUpdateAsync(string mpOrderId, CancellationToken cancellationToken)
        => await context.Payments.FirstOrDefaultAsync(p => p.MpOrderId == mpOrderId, cancellationToken);

    public async Task<Payment?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken)
        => await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

    public async Task UpdateStatusAsync(string orderId, string status, string? statusDetail, CancellationToken cancellationToken)
    {
        Payment payment = await context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

        if (payment is null)
            return;

        payment.UpdateProviderStatus(status, statusDetail);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IRepositoryTransaction?> BeginSerializableTransactionAsync(CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
            return null;

        return new RepositoryTransaction(await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken));
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await context.SaveChangesAsync(cancellationToken);
}
