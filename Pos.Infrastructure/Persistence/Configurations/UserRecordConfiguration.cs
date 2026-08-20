using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class UserRecordConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        builder.ToTable("users");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(record => record.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(record => record.Username)
            .HasColumnName("username")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(record => record.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(record => record.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(record => record.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.HasIndex(record => record.RoleId);
        builder.HasIndex(record => record.IsActive);

        // Unicidad de Username por tenant confirmada por BASIC-USR-01 (sección 10 de la tarea):
        // UserManagementService ya valida duplicados a nivel de Application antes de escribir
        // (comparando contra el mismo valor normalizado que User.NormalizeUsername almacena en
        // mayúsculas), este índice único es la defensa adicional a nivel de base de datos.
        // No se agrega un índice adicional solo por OrganizationId: este índice compuesto
        // ya cubre ese prefijo.
        builder.HasIndex(record => new { record.OrganizationId, record.Username }).IsUnique();

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(record => record.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RoleRecord>()
            .WithMany()
            .HasForeignKey(record => record.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
