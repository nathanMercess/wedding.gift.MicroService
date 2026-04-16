using Microsoft.EntityFrameworkCore;

namespace wedding.gift.Infra.Implementations.DataContext;

public sealed class MicroServiceContext : DbContext
{
    public MicroServiceContext(DbContextOptions<MicroServiceContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(MicroServiceContext).Assembly);
        base.OnModelCreating(builder);
    }
}
