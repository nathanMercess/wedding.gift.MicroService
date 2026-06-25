using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IPaymentRepository
{
    IQueryable<Payment> Query();
    Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveAsync(Payment payment, CancellationToken cancellationToken);
    Task<Payment?> GetByNsuAsync(string nsu, CancellationToken cancellationToken);
    Task<Payment?> GetByMpOrderIdAsync(string mpOrderId, CancellationToken cancellationToken);
    Task<Payment?> GetByMpOrderIdForUpdateAsync(string mpOrderId, CancellationToken cancellationToken);
    Task<Payment?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken);
    Task UpdateStatusAsync(string orderId, string status, string? statusDetail, CancellationToken cancellationToken);
    Task<IRepositoryTransaction?> BeginSerializableTransactionAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
