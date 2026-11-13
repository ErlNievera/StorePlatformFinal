using Microsoft.EntityFrameworkCore;
using Store.Contracts.Events;

namespace Store.Messaging;

/// <summary>EF Core helpers for the <c>ProcessedMessages</c> idempotency table.</summary>
public static class ProcessedMessageExtensions
{
    /// <summary>
    /// Maps <see cref="ProcessedMessage"/> to the <c>ProcessedMessages</c> table with the composite
    /// key (MessageId, Consumer). Call from the service DbContext's <c>OnModelCreating</c>.
    /// </summary>
    public static ModelBuilder AddProcessedMessages(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.ToTable("ProcessedMessages");
            entity.HasKey(m => new { m.MessageId, m.Consumer });
            entity.Property(m => m.Consumer).HasMaxLength(ProcessedMessage.ConsumerMaxLength).IsRequired();
            entity.Property(m => m.EventType).HasMaxLength(100).IsRequired();
            entity.Property(m => m.ProcessedAt).IsRequired();
        });

        return modelBuilder;
    }

    /// <summary>Returns whether <paramref name="consumer"/> already handled the message.</summary>
    public static Task<bool> HasProcessedMessageAsync(
        this DbContext dbContext,
        Guid messageId,
        string consumer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return dbContext.Set<ProcessedMessage>()
            .AsNoTracking()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == consumer, cancellationToken);
    }

    /// <summary>
    /// Adds a <see cref="ProcessedMessage"/> row to the change tracker. It is written by the next
    /// <c>SaveChangesAsync</c>, together with the business change.
    /// </summary>
    public static void MarkMessageProcessed(
        this DbContext dbContext,
        IIntegrationEvent message,
        string consumer,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        dbContext.Set<ProcessedMessage>().Add(new ProcessedMessage
        {
            MessageId = message.MessageId,
            Consumer = consumer,
            EventType = message.GetType().Name,
            ProcessedAt = (timeProvider ?? TimeProvider.System).GetUtcNow(),
        });
    }

    /// <summary>
    /// Runs <paramref name="businessChange"/> exactly once per message and consumer:
    /// in one database transaction it checks <c>ProcessedMessages</c>, applies the change,
    /// records the message id and commits.
    /// </summary>
    /// <param name="dbContext">The consuming service's DbContext.</param>
    /// <param name="message">The event being handled.</param>
    /// <param name="consumer">Consumer name (normally the queue name).</param>
    /// <param name="businessChange">
    /// Loads and modifies entities on <paramref name="dbContext"/>; must not call
    /// <c>SaveChanges</c> itself. It runs inside the transaction and may run again if the
    /// configured execution strategy retries, so it must load what it needs itself.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the change was applied now; <see langword="false"/> when the
    /// message had already been processed and nothing was changed.
    /// </returns>
    /// <remarks>
    /// If two deliveries of the same message race, the second insert violates the primary key,
    /// its transaction rolls back and the exception propagates; the consumer then retries and
    /// sees the message as already processed.
    /// </remarks>
    public static Task<bool> ExecuteIdempotentAsync<TContext>(
        this TContext dbContext,
        IIntegrationEvent message,
        string consumer,
        Func<CancellationToken, Task> businessChange,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(businessChange);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async ct =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            if (await dbContext.HasProcessedMessageAsync(message.MessageId, consumer, ct))
            {
                return false;
            }

            await businessChange(ct);
            dbContext.MarkMessageProcessed(message, consumer);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }, cancellationToken);
    }
}
