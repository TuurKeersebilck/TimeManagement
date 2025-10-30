using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Data;

public class AppDbContext : IdentityDbContext<User>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<TimeLog> TimeLogs => Set<TimeLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ensure Identity key columns are VARCHAR(255) for MySQL/MariaDB compatibility
        builder.Entity<IdentityRole>(entity =>
        {
            entity.Property(e => e.Id).HasColumnType("varchar(255)");
        });
        builder.Entity<User>(entity =>
        {
            entity.Property(e => e.Id).HasColumnType("varchar(255)");
        });
        builder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasColumnType("varchar(255)");
            entity.Property(e => e.RoleId).HasColumnType("varchar(255)");
        });
        builder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasColumnType("varchar(255)");
            entity.Property(e => e.LoginProvider).HasColumnType("varchar(255)");
            entity.Property(e => e.ProviderKey).HasColumnType("varchar(255)");
        });
        builder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasColumnType("varchar(255)");
            entity.Property(e => e.LoginProvider).HasColumnType("varchar(255)");
            entity.Property(e => e.Name).HasColumnType("varchar(255)");
        });
        builder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasColumnType("varchar(255)");
        });
        builder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.Property(e => e.RoleId).HasColumnType("varchar(255)");
        });
    }
}
