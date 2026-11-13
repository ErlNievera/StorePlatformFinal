using Microsoft.EntityFrameworkCore;
using Payment.Api.Domain;
using Store.Messaging;

namespace Payment.Api.Data;

/// <summary>Payment's own database (database-per-service).</summary>
public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    /// <summary>One payment per order.</summary>
    public DbSet<OrderPayment> Payments => Set<OrderPayment>();

    /// <summary>Single-row failure switch.</summary>
    public DbSet<PaymentSettings> Settings => Set<PaymentSettings>();

    /// <summary>Messages already handled by Payment's consumers.</summary>
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderPayment>(entity =>
        {
            entity.ToTable("Payments", table =>
                table.HasCheckConstraint("CK_Payments_Amount_NonNegative", "\"Amount\" >= 0"));
            entity.HasKey(p => p.PaymentId);
            entity.Property(p => p.PaymentId).ValueGeneratedNever();
            entity.HasIndex(p => p.OrderId).IsUnique();
            entity.HasIndex(p => p.CreatedAt);
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(p => p.FailureReason).HasMaxLength(OrderPayment.ReasonMaxLength);
            entity.Property(p => p.RefundReason).HasMaxLength(OrderPayment.ReasonMaxLength);
            entity.Property(p => p.Version).IsRowVersion();
        });

        modelBuilder.Entity<PaymentSettings>(entity =>
        {
            entity.ToTable("PaymentSettings", table =>
                table.HasCheckConstraint("CK_PaymentSettings_SingleRow", $"\"Id\" = {PaymentSettings.SingletonId}"));
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedNever();
            entity.Property(s => s.FailureMode).HasConversion<string>().HasMaxLength(20).IsRequired();
        });

        modelBuilder.AddProcessedMessages();
    }
}
