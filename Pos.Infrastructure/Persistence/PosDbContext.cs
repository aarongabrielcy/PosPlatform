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

    internal DbSet<SaleRecord> Sales => Set<SaleRecord>();

    internal DbSet<SaleLineRecord> SaleLines => Set<SaleLineRecord>();

    internal DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    public Task CommitAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InventoryItemRecordConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryMovementRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SaleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SaleLineRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentRecordConfiguration());
    }
}
