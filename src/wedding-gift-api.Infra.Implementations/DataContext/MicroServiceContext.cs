using InfluencyMe.Framework.Repository.Pattern.DataContext;
using Microsoft.EntityFrameworkCore;

namespace wedding-gift-api.Infra.Implementations.DataContext;

public sealed class MicroServiceContext : DbContext, IEfCoreDataContext
{
    public MicroServiceContext(DbContextOptions<MicroServiceContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(MicroServiceContext).Assembly);
        base.OnModelCreating(builder);
    }

    public async Task<int> SaveChangesAsync() => await base.SaveChangesAsync();
}