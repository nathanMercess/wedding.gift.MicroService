using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IPaymentRepository
{
    IQueryable<Payment> Query();
    Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Payment>> GetApprovedWithoutContributionAsync(CancellationToken cancellationToken);
    Task SaveAsync(Payment payment, CancellationToken cancellationToken);
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
    Task<Payment?> GetByNsuAsync(string nsu, CancellationToken cancellationToken);
    Task<Payment?> GetByMpOrderIdAsync(string mpOrderId, CancellationToken cancellationToken);
    Task<Payment?> GetByMpOrderIdForUpdateAsync(string mpOrderId, CancellationToken cancellationToken);
    Task<Payment?> GetByProviderIdAsync(string providerId, CancellationToken cancellationToken);
    Task<Payment?> GetByProviderIdForUpdateAsync(string providerId, CancellationToken cancellationToken);
    Task<Payment?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken);
    Task<Payment?> GetByOrderIdForUpdateAsync(string orderId, CancellationToken cancellationToken);
    Task<PaymentRefundOperation?> GetRefundOperationByIdempotencyKeyForUpdateAsync(Guid idempotencyKey, CancellationToken cancellationToken);
    Task AddRefundOperationAsync(PaymentRefundOperation operation, CancellationToken cancellationToken);
    Task UpdateStatusAsync(string orderId, string status, string? statusDetail, CancellationToken cancellationToken);
    Task<TResult> ExecuteSerializableAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken);
    Task<TResult> ExecutePaymentLockAsync<TResult>(Guid paymentId, Guid idempotencyKey, Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
