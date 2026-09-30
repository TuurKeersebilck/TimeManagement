using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// Leave booking. The balance arithmetic is what stops an employee overdrawing their allowance,
/// and the year scoping is deliberately taken from the booked date rather than the server clock.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class VacationServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly Monday = new(2026, 3, 2);
    private static readonly DateOnly Tuesday = new(2026, 3, 3);
    private static readonly DateOnly Saturday = new(2026, 3, 7);

    private VacationService NewService() => new(NewContext(), Mapper);

    private async Task<(User User, VacationType Type)> ArrangeAsync(decimal yearlyBalance = 20m)
    {
        var user = Db.AddUser();
        var type = Db.AddVacationType();
        await Db.SaveChangesAsync();
        Db.AddBalance(user.Id, type, yearlyBalance);
        await Db.SaveChangesAsync();
        return (user, type);
    }

    // ── Amount validation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.75)]
    [InlineData(2.0)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task OnlyHalfAndFullDaysAreAccepted(decimal amount)
    {
        var (user, type) = await ArrangeAsync();

        await Assert.ThrowsAsync<InvalidVacationAmountException>(() =>
            NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
            {
                VacationTypeId = type.Id, Date = Monday, Amount = amount,
            }));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public async Task HalfAndFullDaysAreBothBookable(decimal amount)
    {
        var (user, type) = await ArrangeAsync();

        var dto = await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = amount,
        });

        Assert.Equal(amount, dto.Amount);
    }

    // ── Creating a single day ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateDay_PersistsTheBookingWithItsTypeDetails()
    {
        var (user, type) = await ArrangeAsync();

        var dto = await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m, Note = "  long weekend  ",
        });

        Assert.Equal("Annual Leave", dto.VacationTypeName);
        Assert.Equal("long weekend", dto.Note); // trimmed
        var saved = await NewContext().VacationDays.SingleAsync();
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(Monday, saved.Date);
    }

    [Fact]
    public async Task CreateDay_RequiresTheTypeToBeAssignedToTheEmployee()
    {
        var user = Db.AddUser();
        var type = Db.AddVacationType();
        await Db.SaveChangesAsync(); // deliberately no balance row

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
            {
                VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
            }));
    }

    [Fact]
    public async Task CreateDay_RefusesToOverdrawTheYearlyBalance()
    {
        var (user, type) = await ArrangeAsync(yearlyBalance: 1.5m);
        await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        var ex = await Assert.ThrowsAsync<InsufficientVacationBalanceException>(() =>
            NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
            {
                VacationTypeId = type.Id, Date = Tuesday, Amount = 1.0m,
            }));

        Assert.Contains("0.5", ex.Message);
        Assert.Equal(1, await NewContext().VacationDays.CountAsync());
    }

    [Fact]
    public async Task CreateDay_AllowsSpendingTheBalanceExactly()
    {
        var (user, type) = await ArrangeAsync(yearlyBalance: 1.0m);

        await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        Assert.Equal(1, await NewContext().VacationDays.CountAsync());
    }

    [Fact]
    public async Task CreateDay_ScopesTheBalanceToTheBookedYearNotTheCurrentOne()
    {
        // Booking next January must not be refused because this year's allowance is spent.
        var (user, type) = await ArrangeAsync(yearlyBalance: 1.0m);
        await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = new DateOnly(2027, 1, 5), Amount = 1.0m,
        });

        Assert.Equal(2, await NewContext().VacationDays.CountAsync());
    }

    [Fact]
    public async Task CreateDay_RefusesANonWorkingPublicHoliday()
    {
        var (user, type) = await ArrangeAsync();
        Db.AddHoliday(Monday, "Easter Monday");
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
            {
                VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
            }));

        Assert.Contains("public holiday", ex.Message);
    }

    [Fact]
    public async Task CreateDay_AllowsAHolidayTheCompanyWorksThrough()
    {
        var (user, type) = await ArrangeAsync();
        Db.AddHoliday(Monday, "Worked holiday", isWorkingDay: true);
        await Db.SaveChangesAsync();

        await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        Assert.Equal(1, await NewContext().VacationDays.CountAsync());
    }

    // ── Updating ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDay_DoesNotCountTheRowBeingEditedAgainstTheBalance()
    {
        // Otherwise editing the note on the last available day would fail as "insufficient".
        var (user, type) = await ArrangeAsync(yearlyBalance: 1.0m);
        var created = await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        var updated = await NewService().UpdateVacationDayAsync(user.Id, created.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Tuesday, Amount = 1.0m, Note = "moved",
        });

        Assert.Equal(Tuesday, updated.Date);
        Assert.Equal("moved", updated.Note);
    }

    [Fact]
    public async Task UpdateDay_ReflectsAChangeOfType()
    {
        var (user, annual) = await ArrangeAsync();
        var sick = Db.AddVacationType("Sick Leave", "#FF0000");
        await Db.SaveChangesAsync();
        Db.AddBalance(user.Id, sick, 5m);
        await Db.SaveChangesAsync();

        var created = await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = annual.Id, Date = Monday, Amount = 1.0m,
        });

        var updated = await NewService().UpdateVacationDayAsync(user.Id, created.Id, new CreateVacationDayDto
        {
            VacationTypeId = sick.Id, Date = Monday, Amount = 1.0m,
        });

        Assert.Equal("Sick Leave", updated.VacationTypeName);
        Assert.Equal("#FF0000", updated.VacationTypeColor);
    }

    [Fact]
    public async Task UpdateDay_CannotReachAnotherEmployeesBooking()
    {
        var (owner, type) = await ArrangeAsync();
        var intruder = Db.AddUser("Ivan Intruder");
        await Db.SaveChangesAsync();
        var created = await NewService().CreateVacationDayAsync(owner.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService().UpdateVacationDayAsync(intruder.Id, created.Id, new CreateVacationDayDto
            {
                VacationTypeId = type.Id, Date = Tuesday, Amount = 1.0m,
            }));
    }

    // ── Deleting ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDay_RemovesTheBooking()
    {
        var (user, type) = await ArrangeAsync();
        var created = await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        await NewService().DeleteVacationDayAsync(user.Id, created.Id);

        Assert.Empty(await NewContext().VacationDays.ToListAsync());
    }

    [Fact]
    public async Task DeleteDay_CannotReachAnotherEmployeesBooking()
    {
        var (owner, type) = await ArrangeAsync();
        var intruder = Db.AddUser("Ivan Intruder");
        await Db.SaveChangesAsync();
        var created = await NewService().CreateVacationDayAsync(owner.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService().DeleteVacationDayAsync(intruder.Id, created.Id));

        Assert.Equal(1, await NewContext().VacationDays.CountAsync());
    }

    // ── Booking a range ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateRange_SkipsWeekendsAndHolidaysAndReportsWhatItSkipped()
    {
        var (user, type) = await ArrangeAsync();
        Db.AddHoliday(new DateOnly(2026, 3, 4), "Mid-week holiday");
        await Db.SaveChangesAsync();

        // Mon 2 – Sun 8: five weekdays, one of which is a holiday, plus two weekend days.
        var result = await NewService().CreateVacationRangeAsync(user.Id, new CreateVacationRangeDto
        {
            VacationTypeId = type.Id,
            StartDate = Monday,
            EndDate = new DateOnly(2026, 3, 8),
            Amount = 1.0m,
        });

        Assert.Equal(4, result.Created.Count());
        Assert.Equal(2, result.SkippedWeekends);
        Assert.Equal(1, result.SkippedHolidays);
        Assert.Equal(0, result.SkippedExisting);
        Assert.DoesNotContain(result.Created, d => d.Date == Saturday);
    }

    [Fact]
    public async Task CreateRange_SkipsDaysAlreadyBookedForThatType()
    {
        var (user, type) = await ArrangeAsync();
        await NewService().CreateVacationDayAsync(user.Id, new CreateVacationDayDto
        {
            VacationTypeId = type.Id, Date = Monday, Amount = 1.0m,
        });

        var result = await NewService().CreateVacationRangeAsync(user.Id, new CreateVacationRangeDto
        {
            VacationTypeId = type.Id, StartDate = Monday, EndDate = Tuesday, Amount = 1.0m,
        });

        Assert.Single(result.Created);
        Assert.Equal(1, result.SkippedExisting);
    }

    [Fact]
    public async Task CreateRange_RejectsAnInvertedRange()
    {
        var (user, type) = await ArrangeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewService().CreateVacationRangeAsync(user.Id, new CreateVacationRangeDto
            {
                VacationTypeId = type.Id, StartDate = Tuesday, EndDate = Monday, Amount = 1.0m,
            }));
    }

    [Fact]
    public async Task CreateRange_RefusesWhenTheWholeRangeWouldOverdrawTheBalance()
    {
        var (user, type) = await ArrangeAsync(yearlyBalance: 2m);

        var ex = await Assert.ThrowsAsync<InsufficientVacationBalanceException>(() =>
            NewService().CreateVacationRangeAsync(user.Id, new CreateVacationRangeDto
            {
                VacationTypeId = type.Id,
                StartDate = Monday,
                EndDate = new DateOnly(2026, 3, 6), // five weekdays against a 2 day balance
                Amount = 1.0m,
            }));

        Assert.Contains("needed: 5", ex.Message);
        // Nothing may be written when the range as a whole is refused.
        Assert.Empty(await NewContext().VacationDays.ToListAsync());
    }

    [Fact]
    public async Task CreateRange_ThatIsEntirelyWeekendCreatesNothing()
    {
        var (user, type) = await ArrangeAsync();

        var result = await NewService().CreateVacationRangeAsync(user.Id, new CreateVacationRangeDto
        {
            VacationTypeId = type.Id,
            StartDate = Saturday,
            EndDate = new DateOnly(2026, 3, 8),
            Amount = 1.0m,
        });

        Assert.Empty(result.Created);
        Assert.Equal(2, result.SkippedWeekends);
    }

    [Fact]
    public async Task CreateRange_AppliesHalfDaysAcrossTheWholeRange()
    {
        var (user, type) = await ArrangeAsync();

        var result = await NewService().CreateVacationRangeAsync(user.Id, new CreateVacationRangeDto
        {
            VacationTypeId = type.Id, StartDate = Monday, EndDate = Tuesday, Amount = 0.5m,
        });

        Assert.All(result.Created, d => Assert.Equal(0.5m, d.Amount));
    }

    [Fact]
    public async Task CreateRange_AcrossNewYear_OnlyChecksTheStartYearsBalance()
    {
        // KNOWN GAP, documented rather than asserted as correct — see technical-debt.md
        // ("Vacation range balance only checks start year") and dev/testing-plan.md.
        //
        // The range below spans 28 Dec 2026 to 3 Jan 2027: five working days, four of them in
        // 2026 and one on 1 Jan 2027. The balance check counts only the four 2026 days against
        // the 4-day allowance, so it passes, and the 2027 day is created without 2027's balance
        // ever being consulted. Booking the same single day on its own would be refused.
        //
        // When this is fixed, this test should start failing on the final assertion and be
        // rewritten to expect InsufficientVacationBalanceException.
        var (user, type) = await ArrangeAsync(yearlyBalance: 4m);

        var result = await NewService().CreateVacationRangeAsync(user.Id, new CreateVacationRangeDto
        {
            VacationTypeId = type.Id,
            StartDate = new DateOnly(2026, 12, 28),
            EndDate = new DateOnly(2027, 1, 3),
            Amount = 1.0m,
        });

        var created = result.Created.ToList();
        Assert.Equal(5, created.Count);
        Assert.Equal(4, created.Count(d => d.Date.Year == 2026));

        // The 2027 day exists even though 2027 has no allowance assigned at all.
        var leaked = Assert.Single(created, d => d.Date.Year == 2027);
        Assert.Equal(new DateOnly(2027, 1, 1), leaked.Date);
        Assert.Equal(1.0m, (await NewService().GetMyBalancesAsync(user.Id, 2027)).Single().UsedDays);
    }

    // ── Balances and reads ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalances_ReportsUsedAndRemainingPerType()
    {
        var (user, annual) = await ArrangeAsync(yearlyBalance: 20m);
        var sick = Db.AddVacationType("Sick Leave");
        await Db.SaveChangesAsync();
        Db.AddBalance(user.Id, sick, 5m);
        Db.AddVacationDay(user.Id, annual, Monday, 1.0m);
        Db.AddVacationDay(user.Id, annual, Tuesday, 0.5m);
        await Db.SaveChangesAsync();

        var balances = (await NewService().GetMyBalancesAsync(user.Id, 2026)).ToList();

        var annualBalance = balances.Single(b => b.VacationTypeName == "Annual Leave");
        Assert.Equal(1.5m, annualBalance.UsedDays);
        Assert.Equal(18.5m, annualBalance.RemainingDays);
        Assert.Equal(0m, balances.Single(b => b.VacationTypeName == "Sick Leave").UsedDays);
    }

    [Fact]
    public async Task GetBalances_CountsOnlyTheRequestedYear()
    {
        var (user, type) = await ArrangeAsync(yearlyBalance: 20m);
        Db.AddVacationDay(user.Id, type, Monday, 1.0m);
        Db.AddVacationDay(user.Id, type, new DateOnly(2025, 3, 3), 1.0m);
        await Db.SaveChangesAsync();

        var balance = Assert.Single(await NewService().GetMyBalancesAsync(user.Id, 2026));

        Assert.Equal(1.0m, balance.UsedDays);
    }

    [Fact]
    public async Task GetBalances_IsOrderedByTypeName()
    {
        var user = Db.AddUser();
        var zebra = Db.AddVacationType("Zebra Leave");
        var alpha = Db.AddVacationType("Alpha Leave");
        await Db.SaveChangesAsync();
        Db.AddBalance(user.Id, zebra, 5m);
        Db.AddBalance(user.Id, alpha, 5m);
        await Db.SaveChangesAsync();

        var balances = (await NewService().GetMyBalancesAsync(user.Id, 2026)).ToList();

        Assert.Equal(["Alpha Leave", "Zebra Leave"], balances.Select(b => b.VacationTypeName));
    }

    [Fact]
    public async Task GetBalances_ShowsNothingForAnotherEmployee()
    {
        var (_, type) = await ArrangeAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();

        Assert.Empty(await NewService().GetMyBalancesAsync(colleague.Id, 2026));
        Assert.NotNull(type);
    }

    [Fact]
    public async Task GetVacationDays_FiltersByDateAndIsNewestFirst()
    {
        var (user, type) = await ArrangeAsync();
        Db.AddVacationDay(user.Id, type, Monday);
        Db.AddVacationDay(user.Id, type, Tuesday);
        Db.AddVacationDay(user.Id, type, new DateOnly(2026, 5, 1));
        await Db.SaveChangesAsync();

        var days = (await NewService().GetMyVacationDaysAsync(
            user.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31))).ToList();

        Assert.Equal([Tuesday, Monday], days.Select(d => d.Date));
    }

    [Fact]
    public async Task GetVacationForDate_ReturnsTheBookingOrNull()
    {
        var (user, type) = await ArrangeAsync();
        Db.AddVacationDay(user.Id, type, Monday, 0.5m);
        await Db.SaveChangesAsync();

        Assert.Equal(0.5m, (await NewService().GetVacationForDateAsync(user.Id, Monday))!.Amount);
        Assert.Null(await NewService().GetVacationForDateAsync(user.Id, Tuesday));
    }

    [Fact]
    public async Task ExistsForDateAndType_DistinguishesTypes()
    {
        var (user, annual) = await ArrangeAsync();
        var sick = Db.AddVacationType("Sick Leave");
        await Db.SaveChangesAsync();
        Db.AddVacationDay(user.Id, annual, Monday);
        await Db.SaveChangesAsync();

        Assert.True(await NewService().ExistsForDateAndTypeAsync(user.Id, Monday, annual.Id));
        Assert.False(await NewService().ExistsForDateAndTypeAsync(user.Id, Monday, sick.Id));
    }
}
