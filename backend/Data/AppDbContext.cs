using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Data;

public class AppDbContext : IdentityUserContext<User>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ClockEvent> ClockEvents => Set<ClockEvent>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();
    public DbSet<BreakRecord> BreakRecords => Set<BreakRecord>();
    public DbSet<WorkDay> WorkDays => Set<WorkDay>();
    public DbSet<TimeAdjustmentRequest> TimeAdjustmentRequests => Set<TimeAdjustmentRequest>();
    public DbSet<VacationType> VacationTypes => Set<VacationType>();
    public DbSet<EmployeeVacationBalance> EmployeeVacationBalances => Set<EmployeeVacationBalance>();
    public DbSet<VacationDay> VacationDays => Set<VacationDay>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmployeeInvite> EmployeeInvites => Set<EmployeeInvite>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();
    public DbSet<EmployeeTarget> EmployeeTargets => Set<EmployeeTarget>();
    public DbSet<WorkdayTarget> WorkdayTargets => Set<WorkdayTarget>();
    public DbSet<TimeBankAdjustment> TimeBankAdjustments => Set<TimeBankAdjustment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MonthlySettlement> MonthlySettlements => Set<MonthlySettlement>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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
            entity.HasIndex(e => e.CalendarTokenHash)
                  .IsUnique()
                  .HasFilter("\"CalendarTokenHash\" IS NOT NULL");
        });

        builder.Entity<ClockEvent>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.HasIndex(e => new { e.UserId, e.Date, e.Type }).IsUnique();
        });

        builder.Entity<WorkSession>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Date });
            // Enforce one open session per user at the DB level
            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasFilter("\"Status\" = 0")
                .HasDatabaseName("IX_WorkSessions_UserId_Open");
        });

        builder.Entity<BreakRecord>(entity =>
        {
            entity.HasIndex(e => e.WorkSessionId);
            // Enforce one open break per session at the DB level
            entity.HasIndex(e => e.WorkSessionId)
                .IsUnique()
                .HasFilter("\"BreakEnd\" IS NULL")
                .HasDatabaseName("IX_BreakRecords_WorkSessionId_Open");
        });

        builder.Entity<WorkDay>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();
        });

        builder.Entity<AppConfiguration>(entity =>
        {
            entity.Property(e => e.MaxSessionHours).HasDefaultValue(10m);
        });

        builder.Entity<WorkdayTarget>(entity =>
        {
            // One global row per weekday (UserId IS NULL)
            entity.HasIndex(e => e.DayOfWeek)
                .IsUnique()
                .HasFilter("\"UserId\" IS NULL")
                .HasDatabaseName("IX_WorkdayTargets_DayOfWeek_Global");

            // One per-employee row per weekday (UserId IS NOT NULL)
            entity.HasIndex(e => new { e.UserId, e.DayOfWeek })
                .IsUnique()
                .HasFilter("\"UserId\" IS NOT NULL")
                .HasDatabaseName("IX_WorkdayTargets_UserId_DayOfWeek");

            // Seed global defaults: Mon–Fri = 8h, Sat–Sun = 0h
            entity.HasData(
                new WorkdayTarget { Id = 1, DayOfWeek = DayOfWeek.Monday,    Hours = 8m },
                new WorkdayTarget { Id = 2, DayOfWeek = DayOfWeek.Tuesday,   Hours = 8m },
                new WorkdayTarget { Id = 3, DayOfWeek = DayOfWeek.Wednesday, Hours = 8m },
                new WorkdayTarget { Id = 4, DayOfWeek = DayOfWeek.Thursday,  Hours = 8m },
                new WorkdayTarget { Id = 5, DayOfWeek = DayOfWeek.Friday,    Hours = 8m },
                new WorkdayTarget { Id = 6, DayOfWeek = DayOfWeek.Saturday,  Hours = 0m },
                new WorkdayTarget { Id = 7, DayOfWeek = DayOfWeek.Sunday,    Hours = 0m }
            );
        });

        builder.Entity<TimeBankAdjustment>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.EffectiveDate });
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
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

        builder.Entity<EmployeeInvite>(entity =>
        {
            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => e.Email);
        });

        builder.Entity<MonthlySettlement>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Year, e.Month }).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.ReviewedByUser)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
