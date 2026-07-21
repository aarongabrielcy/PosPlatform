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

    internal DbSet<OrganizationRecord> Organizations => Set<OrganizationRecord>();

    internal DbSet<BranchRecord> Branches => Set<BranchRecord>();

    internal DbSet<RegisterRecord> Registers => Set<RegisterRecord>();

    internal DbSet<ProductRecord> Products => Set<ProductRecord>();

    internal DbSet<RoleRecord> Roles => Set<RoleRecord>();

    internal DbSet<RolePermissionRecord> RolePermissions => Set<RolePermissionRecord>();

    internal DbSet<UserRecord> Users => Set<UserRecord>();

    internal DbSet<RegisterSessionRecord> RegisterSessions => Set<RegisterSessionRecord>();

    public Task CommitAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InventoryItemRecordConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryMovementRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SaleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SaleLineRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new BranchRecordConfiguration());
        modelBuilder.ApplyConfiguration(new RegisterRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProductRecordConfiguration());
        modelBuilder.ApplyConfiguration(new RoleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserRecordConfiguration());
        modelBuilder.ApplyConfiguration(new RegisterSessionRecordConfiguration());
    }
}
