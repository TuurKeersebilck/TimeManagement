using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Data;

public class AppDbContext : IdentityUserContext<User>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<TimeLog> TimeLogs => Set<TimeLog>();
    public DbSet<VacationType> VacationTypes => Set<VacationType>();
    public DbSet<EmployeeVacationBalance> EmployeeVacationBalances => Set<EmployeeVacationBalance>();
    public DbSet<VacationDay> VacationDays => Set<VacationDay>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Remove unused Identity columns
        builder.Entity<User>(entity =>
        {
            entity.Ignore(e => e.PhoneNumber);
            entity.Ignore(e => e.PhoneNumberConfirmed);
            entity.Ignore(e => e.TwoFactorEnabled);
            entity.Ignore(e => e.LockoutEnd);
            entity.Ignore(e => e.LockoutEnabled);
            entity.Ignore(e => e.AccessFailedCount);
        });

        builder.Entity<TimeLog>(entity =>
        {
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.UserId, e.Date });
        });

        builder.Entity<VacationType>()
            .HasQueryFilter(v => !v.IsDeleted);

        builder.Entity<EmployeeVacationBalance>(entity =>
        {
            entity.Property(e => e.YearlyBalance).HasColumnType("numeric(5,1)");
            entity.HasIndex(e => new { e.UserId, e.VacationTypeId }).IsUnique();
        });

        builder.Entity<VacationDay>(entity =>
        {
            entity.Property(e => e.Amount).HasColumnType("numeric(3,1)");
            entity.HasIndex(d => new { d.UserId, d.Date });
            entity.HasIndex(d => d.VacationTypeId);
        });
    }
}
