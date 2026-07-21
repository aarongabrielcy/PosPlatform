using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Persistence;
using Pos.Infrastructure.Persistence.Configurations;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence;

public sealed class PosDbContext : DbContext, IUnitOfWork
{
    public PosDbContext(DbContextOptions<PosDbContext> options)
        : base(options)
    {
    }

    internal DbSet<InventoryItemRecord> InventoryItems => Set<InventoryItemRecord>();

    internal DbSet<InventoryMovementRecord> InventoryMovements => Set<InventoryMovementRecord>();

    public Task CommitAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InventoryItemRecordConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryMovementRecordConfiguration());
    }
}
