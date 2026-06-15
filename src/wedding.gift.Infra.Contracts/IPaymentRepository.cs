using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IPaymentRepository
{
    Task SaveAsync(Payment payment, CancellationToken cancellationToken);
    Task<Payment?> GetByNsuAsync(string nsu, CancellationToken cancellationToken);
    Task UpdateStatusAsync(string orderId, string status, CancellationToken cancellationToken);
}
