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

    public DbSet<ClockEvent> ClockEvents => Set<ClockEvent>();
    public DbSet<TimeAdjustmentRequest> TimeAdjustmentRequests => Set<TimeAdjustmentRequest>();
    public DbSet<VacationType> VacationTypes => Set<VacationType>();
    public DbSet<EmployeeVacationBalance> EmployeeVacationBalances => Set<EmployeeVacationBalance>();
    public DbSet<VacationDay> VacationDays => Set<VacationDay>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();
    public DbSet<EmployeeTarget> EmployeeTargets => Set<EmployeeTarget>();
    public DbSet<Notification> Notifications => Set<Notification>();

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

        builder.Entity<ClockEvent>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.HasIndex(e => new { e.UserId, e.Date, e.Type }).IsUnique();
        });

        builder.Entity<TimeAdjustmentRequest>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.HasIndex(e => e.ApprovalTokenHash);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.User)
                .WithMany(u => u.AdjustmentRequests)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ReviewedByUser)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
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

        builder.Entity<PublicHoliday>(entity =>
        {
            entity.HasIndex(h => new { h.CountryCode, h.Year });
            entity.HasIndex(h => h.Date);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => new { n.RecipientUserId, n.IsRead });
            entity.HasIndex(n => n.CreatedAt);
        });
    }
}
