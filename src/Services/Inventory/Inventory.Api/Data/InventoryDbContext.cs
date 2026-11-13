using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Store.Messaging;

namespace Inventory.Api.Data;

/// <summary>Inventory's own database (database-per-service).</summary>
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    /// <summary>Stock per product.</summary>
    public DbSet<StockItem> StockItems => Set<StockItem>();

    /// <summary>Admin stock adjustment audit trail.</summary>
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    /// <summary>Stock held per order by the saga.</summary>
    public DbSet<Reservation> Reservations => Set<Reservation>();

    /// <summary>Messages already handled by Inventory's consumers.</summary>
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.ToTable("StockItems", table =>
            {
                table.HasCheckConstraint("CK_StockItems_QuantityOnHand_NonNegative", "\"QuantityOnHand\" >= 0");
                table.HasCheckConstraint("CK_StockItems_QuantityReserved_Range", "\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"");
            });
            entity.HasKey(s => s.ProductId);
            entity.Property(s => s.ProductId).ValueGeneratedNever();
            entity.Property(s => s.QuantityOnHand).IsRequired();
            entity.Property(s => s.QuantityReserved).IsRequired();
            entity.Property(s => s.UpdatedAt).IsRequired();
            entity.Property(s => s.Version).IsRowVersion();
            entity.Ignore(s => s.QuantityAvailable);
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.ToTable("StockAdjustments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedNever();
            entity.Property(a => a.Reason).HasMaxLength(StockAdjustment.ReasonMaxLength).IsRequired();
            entity.Property(a => a.AdjustedBy).HasMaxLength(StockAdjustment.AdjustedByMaxLength);
            entity.HasIndex(a => new { a.ProductId, a.CreatedAt });
            entity.HasOne<StockItem>().WithMany().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservations");
            entity.HasKey(r => r.OrderId);
            entity.Property(r => r.OrderId).ValueGeneratedNever();
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(r => r.ReleaseReason).HasMaxLength(Reservation.ReleaseReasonMaxLength);
            entity.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ReservationLine>(entity =>
        {
            entity.ToTable("ReservationLines", table =>
                table.HasCheckConstraint("CK_ReservationLines_Quantity_Positive", "\"Quantity\" > 0"));
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Id).ValueGeneratedNever();
            entity.HasIndex(l => new { l.OrderId, l.ProductId }).IsUnique();
            entity.HasOne<StockItem>().WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.AddProcessedMessages();
    }
}
