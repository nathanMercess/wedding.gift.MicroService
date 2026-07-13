using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public async Task<IReadOnlyList<Payment>> GetApprovedWithoutContributionAsync(CancellationToken cancellationToken)
        => await context.Payments
            .AsNoTracking()
            .Where(payment => (payment.Status == "approved" || payment.Status == "processed") && !payment.ContributionCreated)
            .OrderBy(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(Payment payment, CancellationToken cancellationToken)
    {
        await context.Payments.AddAsync(payment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
        => await context.Payments.AddAsync(payment, cancellationToken);

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

    public async Task<Payment?> GetByProviderIdAsync(string providerId, CancellationToken cancellationToken)
        => await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MpOrderId == providerId || p.MpPaymentId == providerId, cancellationToken);

    public async Task<Payment?> GetByProviderIdForUpdateAsync(string providerId, CancellationToken cancellationToken)
        => await context.Payments.FirstOrDefaultAsync(p => p.MpOrderId == providerId || p.MpPaymentId == providerId, cancellationToken);

    public async Task<Payment?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken)
        => await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

    public async Task<Payment?> GetByOrderIdForUpdateAsync(string orderId, CancellationToken cancellationToken)
        => await context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

    public async Task<PaymentRefundOperation?> GetRefundOperationByIdempotencyKeyForUpdateAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken)
        => await context.PaymentRefundOperations.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddRefundOperationAsync(PaymentRefundOperation operation, CancellationToken cancellationToken)
        => await context.PaymentRefundOperations.AddAsync(operation, cancellationToken);

    public async Task UpdateStatusAsync(string orderId, string status, string? statusDetail, CancellationToken cancellationToken)
    {
        Payment payment = await context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

        if (payment is null)
            return;

        payment.UpdateProviderStatus(status, statusDetail);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
            return await operation(cancellationToken);

        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            TResult result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task<TResult> ExecutePaymentLockAsync<TResult>(
        Guid paymentId,
        Guid idempotencyKey,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
            return await operation(cancellationToken);

        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await AcquireLockAsync($"payment-refund-operation:{idempotencyKey:N}", cancellationToken);
            await AcquireLockAsync($"payment-refund:{paymentId:N}", cancellationToken);
            TResult result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await context.SaveChangesAsync(cancellationToken);

    private async Task AcquireLockAsync(string resourceName, CancellationToken cancellationToken)
    {
        DbConnection connection = context.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource, 'Exclusive', 'Transaction', @LockTimeout; SELECT @result;";

        DbParameter resource = command.CreateParameter();
        resource.ParameterName = "@Resource";
        resource.Value = resourceName;
        command.Parameters.Add(resource);

        DbParameter timeout = command.CreateParameter();
        timeout.ParameterName = "@LockTimeout";
        timeout.Value = 10000;
        command.Parameters.Add(timeout);

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        if (Convert.ToInt32(result) < 0)
            throw new TimeoutException("Payment refund lock timeout.");
    }
}
