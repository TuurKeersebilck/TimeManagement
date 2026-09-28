using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Tests.Infrastructure;

/// <summary>Arrange helpers. Every factory sets only what the schema requires, so a test that
/// cares about a value sets it explicitly and the intent stays visible at the call site.</summary>
internal static class TestData
{
    public static User AddUser(
        this AppDbContext db,
        string fullName = "Emma Employee",
        UserRole role = UserRole.Employee,
        string? email = null,
        bool isDisabled = false)
    {
        email ??= $"{Guid.NewGuid():N}@example.test";
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            FullName = fullName,
            Role = role,
            IsDisabled = isDisabled,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        db.Users.Add(user);
        return user;
    }

    /// <summary>A closed session on <paramref name="date"/> running local-clock <paramref name="from"/>
    /// to <paramref name="to"/> in UTC, optionally with one break.</summary>
    public static WorkSession AddClosedSession(
        this AppDbContext db,
        string userId,
        DateOnly date,
        TimeSpan from,
        TimeSpan to,
        (TimeSpan Start, TimeSpan End)? aBreak = null)
    {
        var clockIn = At(date, from);
        var session = new WorkSession
        {
            UserId = userId,
            Date = date,
            ClockIn = clockIn,
            ClockInServerStamp = clockIn,
            ClockOut = At(date, to),
            ClockOutServerStamp = At(date, to),
            Status = WorkSessionStatus.Closed,
        };

        if (aBreak is { } b)
        {
            session.Breaks.Add(new BreakRecord
            {
                BreakStart = At(date, b.Start),
                BreakStartServerStamp = At(date, b.Start),
                BreakEnd = At(date, b.End),
                BreakEndServerStamp = At(date, b.End),
            });
        }

        db.WorkSessions.Add(session);
        return session;
    }

    public static WorkSession AddOpenSession(this AppDbContext db, string userId, DateOnly date, TimeSpan from)
    {
        var clockIn = At(date, from);
        var session = new WorkSession
        {
            UserId = userId,
            Date = date,
            ClockIn = clockIn,
            ClockInServerStamp = clockIn,
            Status = WorkSessionStatus.Open,
        };
        db.WorkSessions.Add(session);
        return session;
    }

    public static VacationType AddVacationType(this AppDbContext db, string name = "Annual Leave", string color = "#3366FF")
    {
        var type = new VacationType { Name = name, Color = color };
        db.VacationTypes.Add(type);
        return type;
    }

    public static EmployeeVacationBalance AddBalance(
        this AppDbContext db, string userId, VacationType type, decimal yearlyBalance)
    {
        var balance = new EmployeeVacationBalance
        {
            UserId = userId,
            VacationType = type,
            YearlyBalance = yearlyBalance,
        };
        db.EmployeeVacationBalances.Add(balance);
        return balance;
    }

    public static VacationDay AddVacationDay(
        this AppDbContext db, string userId, VacationType type, DateOnly date,
        decimal amount = 1.0m, string? note = null)
    {
        var day = new VacationDay
        {
            UserId = userId, VacationType = type, Date = date, Amount = amount, Note = note,
        };
        db.VacationDays.Add(day);
        return day;
    }

    public static AppConfiguration AddConfiguration(
        this AppDbContext db,
        int? minimumBreakMinutes = null,
        decimal? dailyAllowance = null,
        decimal? weeklyAllowance = null,
        string? notificationEmail = null,
        bool enableAdjustmentRequestEmails = true)
    {
        var config = new AppConfiguration
        {
            MinimumBreakMinutes = minimumBreakMinutes,
            DefaultDailyOvertimeAllowanceHours = dailyAllowance,
            DefaultWeeklyOvertimeAllowanceHours = weeklyAllowance,
            NotificationEmail = notificationEmail,
            EnableAdjustmentRequestEmails = enableAdjustmentRequestEmails,
        };
        db.AppConfigurations.Add(config);
        return config;
    }

    public static MonthlySettlement AddSettlement(
        this AppDbContext db,
        string userId,
        int year,
        int month,
        SettlementStatus status = SettlementStatus.Settled,
        decimal netBalanceHours = 0m)
    {
        var settlement = new MonthlySettlement
        {
            UserId = userId,
            Year = year,
            Month = month,
            Status = status,
            NetBalanceHours = netBalanceHours,
            OvertimeHours = netBalanceHours > 0 ? netBalanceHours : 0m,
            DeficitHours = netBalanceHours < 0 ? -netBalanceHours : 0m,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
        db.MonthlySettlements.Add(settlement);
        return settlement;
    }

    /// <summary>
    /// Rewrites the seeded global schedule so only the listed weekdays have hours. Tests that care
    /// about a single day set just that day, which keeps the expected monthly balance small enough
    /// to state directly instead of deriving it from 22 working days.
    /// </summary>
    public static async Task SetGlobalScheduleAsync(this AppDbContext db, params (DayOfWeek Day, decimal Hours)[] schedule)
    {
        var rows = await db.WorkdayTargets.Where(t => t.UserId == null).ToListAsync();
        foreach (var row in rows)
            row.Hours = schedule.FirstOrDefault(s => s.Day == row.DayOfWeek).Hours;
        await db.SaveChangesAsync();
    }

    /// <summary>Gives one employee a per-weekday override, leaving the global rows untouched.</summary>
    public static void AddWorkdayOverride(this AppDbContext db, string userId, DayOfWeek day, decimal hours)
        => db.WorkdayTargets.Add(new WorkdayTarget { UserId = userId, DayOfWeek = day, Hours = hours });

    public static PublicHoliday AddHoliday(
        this AppDbContext db, DateOnly date, string name = "Test Holiday", bool isWorkingDay = false)
    {
        var holiday = new PublicHoliday
        {
            Date = date,
            Name = name,
            CountryCode = "BE",
            Year = date.Year,
            IsWorkingDay = isWorkingDay,
        };
        db.PublicHolidays.Add(holiday);
        return holiday;
    }

    public static TimeBankAdjustment AddAdjustment(
        this AppDbContext db, string userId, DateOnly effectiveDate, decimal hours, string reason = "manual")
    {
        var adjustment = new TimeBankAdjustment
        {
            UserId = userId,
            EffectiveDate = effectiveDate,
            Hours = hours,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.TimeBankAdjustments.Add(adjustment);
        return adjustment;
    }

    public static DateTimeOffset At(DateOnly date, TimeSpan time) =>
        new(date.ToDateTime(TimeOnly.MinValue).Add(time), TimeSpan.Zero);
}
