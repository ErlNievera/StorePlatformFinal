using Identity.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Data;

/// <summary>Identity's own database (database-per-service): users, roles and role memberships.</summary>
public sealed class StoreIdentityDbContext(DbContextOptions<StoreIdentityDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(AppUser.FullNameMaxLength).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
        });
    }
}
