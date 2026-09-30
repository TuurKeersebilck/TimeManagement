using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// Holidays come from an external provider and drive both the payroll export and everyone's
/// target hours, so a bad fetch must never be allowed to quietly wipe the stored set.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PublicHolidayServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TwoBelgianHolidays = """
        [
          { "date": "2026-01-01", "localName": "Nieuwjaar", "name": "New Year's Day", "types": ["Public"] },
          { "date": "2026-07-21", "localName": "Nationale feestdag", "name": "National Day", "types": ["Public"] }
        ]
        """;

    private PublicHolidayService NewService(StubHttpMessageHandler handler) =>
        new(NewContext(), handler.CreateClient(), NullLogger<PublicHolidayService>.Instance);

    private async Task ArrangeCountryAsync(string countryCode = "BE")
    {
        Db.AppConfigurations.Add(new TimeManagementBackend.Models.AppConfiguration { CountryCode = countryCode });
        await Db.SaveChangesAsync();
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Configuration_DefaultsBothEmailTogglesOnWhenNoRowExists()
    {
        var config = await NewService(StubHttpMessageHandler.Failing()).GetConfigurationAsync();

        Assert.True(config.EnableAdjustmentRequestEmails);
        Assert.True(config.EnableMissedClockInEmails);
        Assert.Null(config.CountryCode);
    }

    [Fact]
    public async Task NotificationEmail_IsNormalisedToLowercase()
    {
        var config = await NewService(StubHttpMessageHandler.Failing())
            .SetNotificationEmailAsync("  Admin@Example.TEST  ");

        Assert.Equal("admin@example.test", config.NotificationEmail);
    }

    [Fact]
    public async Task NotificationEmail_CanBeClearedWithBlankInput()
    {
        var service = NewService(StubHttpMessageHandler.Failing());
        await service.SetNotificationEmailAsync("admin@example.test");

        var config = await NewService(StubHttpMessageHandler.Failing()).SetNotificationEmailAsync("   ");

        Assert.Null(config.NotificationEmail);
    }

    [Fact]
    public async Task Country_IsNormalisedToUppercase()
    {
        var config = await NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays))
            .SetCountryAsync(" be ");

        Assert.Equal("BE", config.CountryCode);
    }

    [Fact]
    public async Task SettingTheCountry_SucceedsEvenWhenTheProviderIsDown()
    {
        // Losing the eager holiday prefetch must not stop an admin configuring the country.
        var config = await NewService(StubHttpMessageHandler.Failing()).SetCountryAsync("BE");

        Assert.Equal("BE", config.CountryCode);
    }

    [Fact]
    public async Task NotificationToggles_AreStored()
    {
        var config = await NewService(StubHttpMessageHandler.Failing())
            .SetNotificationTogglesAsync(enableAdjustmentRequestEmails: false, enableMissedClockInEmails: true);

        Assert.False(config.EnableAdjustmentRequestEmails);
        Assert.True(config.EnableMissedClockInEmails);
    }

    [Fact]
    public async Task SettlementEmails_DefaultOnAndCanBeSwitchedOff()
    {
        Assert.True((await NewService(StubHttpMessageHandler.Failing()).GetConfigurationAsync()).EnableSettlementEmails);

        var config = await NewService(StubHttpMessageHandler.Failing())
            .SetNotificationTogglesAsync(true, true, enableSettlementEmails: false);

        Assert.False(config.EnableSettlementEmails);
    }

    [Fact]
    public async Task SettlementEmails_AreLeftAloneByAClientThatDoesNotSendTheToggle()
    {
        // An older frontend only knows the first two toggles; saving them must not flip this one.
        await NewService(StubHttpMessageHandler.Failing())
            .SetNotificationTogglesAsync(true, true, enableSettlementEmails: false);

        var config = await NewService(StubHttpMessageHandler.Failing())
            .SetNotificationTogglesAsync(enableAdjustmentRequestEmails: false, enableMissedClockInEmails: true);

        Assert.False(config.EnableSettlementEmails);
    }

    [Fact]
    public async Task MinimumBreakMinutes_CanBeSetAndCleared()
    {
        Assert.Equal(30, (await NewService(StubHttpMessageHandler.Failing())
            .SetMinimumBreakMinutesAsync(30)).MinimumBreakMinutes);

        Assert.Null((await NewService(StubHttpMessageHandler.Failing())
            .SetMinimumBreakMinutesAsync(null)).MinimumBreakMinutes);
    }

    // ── Fetching ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Holidays_AreEmptyUntilACountryIsConfigured()
    {
        var holidays = await NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays))
            .GetHolidaysAsync(2026);

        Assert.Empty(holidays);
    }

    [Fact]
    public async Task Holidays_AreFetchedOnFirstReadForAYear()
    {
        await ArrangeCountryAsync();
        var handler = StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays);

        var holidays = (await NewService(handler).GetHolidaysAsync(2026)).ToList();

        Assert.Equal(2, holidays.Count);
        Assert.Equal("Nieuwjaar", holidays[0].Name); // the local name, ordered by date
        Assert.Contains("/PublicHolidays/2026/BE", Assert.Single(handler.RequestedUris));
    }

    [Fact]
    public async Task Holidays_AreServedFromTheDatabaseOnLaterReads()
    {
        await ArrangeCountryAsync();
        await NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays)).GetHolidaysAsync(2026);

        var handler = StubHttpMessageHandler.Failing();
        var holidays = await NewService(handler).GetHolidaysAsync(2026);

        Assert.Equal(2, holidays.Count());
        Assert.Empty(handler.RequestedUris); // no second call to the provider
    }

    [Fact]
    public async Task NonPublicHolidayTypes_DefaultToWorkingDays()
    {
        // Bank and Optional days are on the calendar but the company still works them.
        await ArrangeCountryAsync();
        var handler = StubHttpMessageHandler.RespondingWithJson("""
            [
              { "date": "2026-01-01", "localName": "Nieuwjaar", "name": "New Year", "types": ["Public"] },
              { "date": "2026-05-15", "localName": "Bank day", "name": "Bank day", "types": ["Bank"] }
            ]
            """);

        var holidays = (await NewService(handler).GetHolidaysAsync(2026)).ToList();

        Assert.False(holidays.Single(h => h.Date == "2026-01-01").IsWorkingDay);
        Assert.True(holidays.Single(h => h.Date == "2026-05-15").IsWorkingDay);
    }

    [Fact]
    public async Task AFailedFetch_LeavesTheStoredHolidaysIntact()
    {
        await ArrangeCountryAsync();
        await NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays)).GetHolidaysAsync(2026);

        // A provider outage during an auto-fetch must not empty the calendar.
        var holidays = await NewService(StubHttpMessageHandler.Failing()).GetHolidaysAsync(2026);

        Assert.Equal(2, holidays.Count());
    }

    [Fact]
    public async Task Refresh_SurfacesAProviderFailureToTheAdmin()
    {
        // Unlike auto-fetch, an explicit refresh that silently did nothing would be misleading.
        await ArrangeCountryAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService(StubHttpMessageHandler.Failing()).RefreshHolidaysAsync(2026));

        Assert.Contains("Couldn't reach the holiday provider", ex.Message);
    }

    [Fact]
    public async Task Refresh_TreatsAnEmptyResponseAsAFailedFetch()
    {
        // No country has zero holidays; an empty list means the provider misbehaved, and
        // accepting it would delete every stored holiday with nothing to put back.
        await ArrangeCountryAsync();
        await NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays)).GetHolidaysAsync(2026);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService(StubHttpMessageHandler.RespondingWithJson("[]")).RefreshHolidaysAsync(2026));

        Assert.Contains("returned no holidays", ex.Message);
        Assert.Equal(2, await NewContext().PublicHolidays.CountAsync());
    }

    [Fact]
    public async Task Refresh_RejectsAnUnconfiguredCountry()
        => await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays)).RefreshHolidaysAsync(2026));

    [Fact]
    public async Task AutoFetch_PreservesAnAdminsWorkingDayOverride()
    {
        await ArrangeCountryAsync();
        var service = NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays));
        var holidays = (await service.GetHolidaysAsync(2026)).ToList();
        var newYear = holidays.Single(h => h.Date == "2026-01-01");
        await NewService(StubHttpMessageHandler.Failing()).SetIsWorkingDayAsync(newYear.Id, true);

        // Clear the non-custom rows so the next read triggers a fresh auto-fetch.
        var context = NewContext();
        // An incidental re-fetch must not silently undo a deliberate admin decision.
        var refreshed = await NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays))
            .GetHolidaysAsync(2026);

        Assert.True(refreshed.Single(h => h.Date == "2026-01-01").IsWorkingDay);
        Assert.NotNull(context);
    }

    [Fact]
    public async Task ExplicitRefresh_ResetsWorkingDayBackToTheProvidersDefault()
    {
        await ArrangeCountryAsync();
        var holidays = (await NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays))
            .GetHolidaysAsync(2026)).ToList();
        await NewService(StubHttpMessageHandler.Failing())
            .SetIsWorkingDayAsync(holidays.Single(h => h.Date == "2026-01-01").Id, true);

        var refreshed = await NewService(StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays))
            .RefreshHolidaysAsync(2026);

        Assert.False(refreshed.Single(h => h.Date == "2026-01-01").IsWorkingDay);
    }

    // ── Custom holidays ───────────────────────────────────────────────────────

    [Fact]
    public async Task CustomHoliday_IsStoredAgainstTheConfiguredCountry()
    {
        await ArrangeCountryAsync();

        var holiday = await NewService(StubHttpMessageHandler.Failing())
            .AddCustomHolidayAsync(new CreateHolidayDto { Date = "2026-06-01", Name = "  Company Day  " });

        Assert.True(holiday.IsCustom);
        Assert.Equal("Company Day", holiday.Name);
        Assert.Equal("2026-06-01", holiday.Date);
    }

    [Fact]
    public async Task CustomHoliday_RejectsAnUnparseableDate()
    {
        await ArrangeCountryAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewService(StubHttpMessageHandler.Failing())
                .AddCustomHolidayAsync(new CreateHolidayDto { Date = "not-a-date", Name = "Company Day" }));
    }

    [Fact]
    public async Task CustomHoliday_AcceptsNonIsoDatesDespiteTheErrorMessage()
    {
        // Documents current behaviour, not desired behaviour. The guard uses DateOnly.TryParse,
        // which honours the server's culture, so "01/06/2026" is accepted even though the
        // message promises YYYY-MM-DD — and it means 1 June under en-GB but 6 January under
        // en-US. The frontend only ever sends ISO dates, so nothing is broken today; tightening
        // this to TryParseExact("yyyy-MM-dd", InvariantCulture) would remove the ambiguity.
        await ArrangeCountryAsync();

        var holiday = await NewService(StubHttpMessageHandler.Failing())
            .AddCustomHolidayAsync(new CreateHolidayDto { Date = "01/06/2026", Name = "Company Day" });

        Assert.Equal(2026, DateOnly.Parse(holiday.Date).Year);
    }

    [Fact]
    public async Task ACustomHolidayAloneStillAllowsTheAutoFetch()
    {
        // Only non-custom rows count as "already fetched", or one custom entry would
        // block the real holidays from ever loading for that year.
        await ArrangeCountryAsync();
        await NewService(StubHttpMessageHandler.Failing())
            .AddCustomHolidayAsync(new CreateHolidayDto { Date = "2026-06-01", Name = "Company Day" });

        var handler = StubHttpMessageHandler.RespondingWithJson(TwoBelgianHolidays);
        var holidays = await NewService(handler).GetHolidaysAsync(2026);

        Assert.Equal(3, holidays.Count());
        Assert.NotEmpty(handler.RequestedUris);
    }

    [Fact]
    public async Task Holiday_CanBeDeleted()
    {
        await ArrangeCountryAsync();
        var holiday = await NewService(StubHttpMessageHandler.Failing())
            .AddCustomHolidayAsync(new CreateHolidayDto { Date = "2026-06-01", Name = "Company Day" });

        await NewService(StubHttpMessageHandler.Failing()).DeleteHolidayAsync(holiday.Id);

        Assert.Empty(await NewContext().PublicHolidays.ToListAsync());
    }

    [Fact]
    public async Task Holiday_DeleteAndToggleRejectUnknownIds()
    {
        var service = NewService(StubHttpMessageHandler.Failing());

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.DeleteHolidayAsync(999));
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.SetIsWorkingDayAsync(999, true));
    }

    // ── Global workday targets ────────────────────────────────────────────────

    [Fact]
    public async Task GlobalWorkdayTargets_StartFromTheSeededMondayToFridaySchedule()
    {
        var targets = (await NewService(StubHttpMessageHandler.Failing()).GetGlobalWorkdayTargetsAsync()).ToList();

        Assert.Equal(7, targets.Count);
        Assert.Equal(8m, targets.Single(t => t.DayOfWeek == DayOfWeek.Monday).Hours);
        Assert.Equal(0m, targets.Single(t => t.DayOfWeek == DayOfWeek.Saturday).Hours);
    }

    [Fact]
    public async Task GlobalWorkdayTargets_CanBeReplaced()
    {
        var targets = await NewService(StubHttpMessageHandler.Failing()).SetGlobalWorkdayTargetsAsync(
        [
            new WorkdayTargetDto { DayOfWeek = DayOfWeek.Monday, Hours = 6m },
            new WorkdayTargetDto { DayOfWeek = DayOfWeek.Tuesday, Hours = 6m },
        ]);

        Assert.Equal(2, targets.Count());
        Assert.All(targets, t => Assert.Equal(6m, t.Hours));
    }

    // ── Available countries ───────────────────────────────────────────────────

    [Fact]
    public async Task AvailableCountries_AreSortedByName()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson("""
            [
              { "countryCode": "NL", "name": "Netherlands" },
              { "countryCode": "BE", "name": "Belgium" }
            ]
            """);

        var countries = (await NewService(handler).GetAvailableCountriesAsync()).ToList();

        Assert.Equal(["Belgium", "Netherlands"], countries.Select(c => c.Name));
    }

    [Fact]
    public async Task AvailableCountries_AreEmptyWhenTheProviderFails()
    {
        // The country picker degrades to empty rather than breaking the settings page.
        Assert.Empty(await NewService(StubHttpMessageHandler.Failing()).GetAvailableCountriesAsync());
        Assert.Empty(await NewService(StubHttpMessageHandler.RespondingWith(HttpStatusCode.InternalServerError))
            .GetAvailableCountriesAsync());
    }
}
