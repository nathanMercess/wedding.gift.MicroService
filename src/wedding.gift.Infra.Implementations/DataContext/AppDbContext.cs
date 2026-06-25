using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.DataContext;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Gift> Gifts => Set<Gift>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Couple> Couples => Set<Couple>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
