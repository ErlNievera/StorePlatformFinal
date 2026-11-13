using Microsoft.EntityFrameworkCore;
using Order.Api.Domain;
using Store.Messaging;

namespace Order.Api.Data;

/// <summary>Order's own database (database-per-service).</summary>
public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    /// <summary>Orders with lines and history.</summary>
    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();

    /// <summary>Saga bookkeeping per order.</summary>
    public DbSet<OrderSagaState> SagaStates => Set<OrderSagaState>();

    /// <summary>Single-row failure switch.</summary>
    public DbSet<OrderSettings> Settings => Set<OrderSettings>();

    /// <summary>Messages already handled by Order's consumers.</summary>
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.ToTable("Orders", table =>
                table.HasCheckConstraint("CK_Orders_TotalAmount_NonNegative", "\"TotalAmount\" >= 0"));
            entity.HasKey(o => o.OrderId);
            entity.Property(o => o.OrderId).ValueGeneratedNever();
            entity.HasIndex(o => new { o.CustomerId, o.CreatedAt });
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            entity.Property(o => o.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
            entity.Property(o => o.CancellationReason).HasMaxLength(CustomerOrder.CancellationReasonMaxLength);
            entity.Property(o => o.Version).IsRowVersion();
            entity.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(o => o.History).WithOne().HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(o => o.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("OrderLines", table =>
                table.HasCheckConstraint("CK_OrderLines_Quantity_Positive", "\"Quantity\" > 0"));
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Id).ValueGeneratedNever();
            entity.Property(l => l.ProductName).HasMaxLength(OrderLine.ProductNameMaxLength).IsRequired();
            entity.Property(l => l.UnitPrice).HasPrecision(18, 2);
            entity.Ignore(l => l.LineTotal);
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.ToTable("OrderStatusHistory");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).ValueGeneratedNever();
            entity.Property(h => h.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(h => h.Reason).HasMaxLength(OrderStatusHistory.ReasonMaxLength);
            entity.HasIndex(h => new { h.OrderId, h.Sequence }).IsUnique();
        });

        modelBuilder.Entity<OrderSagaState>(entity =>
        {
            entity.ToTable("OrderSagaStates");
            entity.HasKey(s => s.OrderId);
            entity.Property(s => s.OrderId).ValueGeneratedNever();
            entity.Property(s => s.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(s => s.Step).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(s => s.FailureReason).HasMaxLength(OrderSagaState.FailureReasonMaxLength);
            entity.Property(s => s.Version).IsRowVersion();
            entity.Ignore(s => s.CompensationFinished);
            entity.HasOne<CustomerOrder>().WithOne().HasForeignKey<OrderSagaState>(s => s.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderSettings>(entity =>
        {
            entity.ToTable("OrderSettings", table =>
                table.HasCheckConstraint("CK_OrderSettings_SingleRow", $"\"Id\" = {OrderSettings.SingletonId}"));
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedNever();
            entity.Property(s => s.FailureMode).HasConversion<string>().HasMaxLength(20).IsRequired();
        });

        modelBuilder.AddProcessedMessages();
    }
}
