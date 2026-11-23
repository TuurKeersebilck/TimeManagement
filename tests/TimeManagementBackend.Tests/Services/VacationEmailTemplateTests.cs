using MimeKit;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// What the vacation emails actually say. The service tests only prove an email is asked for with
/// the right data; these check the message itself: subject, recipient, dates, totals, the review
/// link, and that names and notes typed by people can't inject HTML.
/// </summary>
public class VacationEmailTemplateTests
{
    private static readonly DateOnly Mon7Dec = new(2026, 12, 7);
    private static readonly DateOnly Fri11Dec = new(2026, 12, 11);
    private static readonly DateOnly Mon21Dec = new(2026, 12, 21);
    private const string Link = "https://logr.example.test/admin/vacation-requests";

    private static string Html(MimeMessage message) => Assert.IsType<TextPart>(message.Body).Text;

    // ── To the admin ──────────────────────────────────────────────────────────

    [Fact]
    public void Request_OnePeriod_NamesTheEmployeeDatesTypeAndDays()
    {
        var message = EmailService.BuildVacationRequestMessage("noreply@example.test", "admin@example.test",
            "Lotte Janssens", [new("Annual Leave", Mon7Dec, Fri11Dec, 5m, "Family trip")], Link);

        Assert.Equal("Vacation request from Lotte Janssens: 7 Dec – 11 Dec 2026", message.Subject);
        Assert.Equal("admin@example.test", message.To.Mailboxes.Single().Address);
        Assert.Equal("noreply@example.test", message.From.Mailboxes.Single().Address);
        var html = Html(message);
        Assert.Contains("<strong>Lotte Janssens</strong> requested", html);
        Assert.Contains("Monday 7 December to Friday 11 December 2026", html);
        Assert.Contains("Annual Leave · 5 days", html);
        Assert.Contains("Family trip", html);
        Assert.Contains($"href=\"{Link}\"", html);
        Assert.Contains("Review request\n", html.Replace("\r", ""));
    }

    [Fact]
    public void Request_SeveralPeriods_ListsEachAndTotalsThemInTheSubject()
    {
        var message = EmailService.BuildVacationRequestMessage("noreply@example.test", "admin@example.test", "Lotte Janssens",
        [
            new("Annual Leave", Mon7Dec, Fri11Dec, 5m, null),
            new("Annual Leave", Mon21Dec, Mon21Dec, 0.5m, null),
        ], Link);

        Assert.Equal("Vacation request from Lotte Janssens: 2 periods, 5.5 days", message.Subject);
        var html = Html(message);
        Assert.Contains("Monday 7 December to Friday 11 December 2026", html);
        Assert.Contains("Monday 21 December 2026", html);
        Assert.Contains("Annual Leave · 0.5 days", html);
        Assert.Contains("Review requests", html);
    }

    [Fact]
    public void Request_SingleDay_UsesTheSingularDay()
    {
        var message = EmailService.BuildVacationRequestMessage("noreply@example.test", "admin@example.test",
            "Lotte Janssens", [new("Annual Leave", Mon21Dec, Mon21Dec, 1m, null)], Link);

        Assert.Equal("Vacation request from Lotte Janssens: 21 Dec 2026", message.Subject);
        Assert.Contains("Annual Leave · 1 day<", Html(message));
    }

    [Fact]
    public void Request_EscapesNamesTypesAndNotes()
    {
        var html = Html(EmailService.BuildVacationRequestMessage("noreply@example.test", "admin@example.test",
            "<b>Lotte</b>", [new("Leave & <i>more</i>", Mon7Dec, Mon7Dec, 1m, "<script>alert(1)</script>")], Link));

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>Lotte</b>", html);
        Assert.Contains("&lt;b&gt;Lotte&lt;/b&gt;", html);
        Assert.Contains("Leave &amp; &lt;i&gt;more&lt;/i&gt;", html);
    }

    // ── To the employee ───────────────────────────────────────────────────────

    [Fact]
    public void Outcome_Approved_SaysSoWithoutANote()
    {
        var message = EmailService.BuildVacationRequestOutcomeMessage("noreply@example.test", "lotte@example.test",
            "Lotte Janssens", "Annual Leave", Mon7Dec, Fri11Dec, approved: true, reviewNote: null);

        Assert.Equal("Your vacation request for 7 Dec – 11 Dec 2026 has been approved", message.Subject);
        Assert.Equal("lotte@example.test", message.To.Mailboxes.Single().Address);
        var html = Html(message);
        Assert.Contains("Hi Lotte Janssens,", html);
        Assert.Contains("<strong>Annual Leave</strong>, Monday 7 December to Friday 11 December 2026", html);
        Assert.Contains(">approved</strong>", html);
        Assert.Contains("The days are now in your vacation overview.", html);
        Assert.DoesNotContain("Note from your admin", html);
    }

    [Fact]
    public void Outcome_Denied_IncludesTheAdminsReasonEscaped()
    {
        var message = EmailService.BuildVacationRequestOutcomeMessage("noreply@example.test", "lotte@example.test",
            "Lotte Janssens", "Annual Leave", Mon21Dec, Mon21Dec, approved: false,
            reviewNote: "Too busy <that> week");

        Assert.Equal("Your vacation request for 21 Dec 2026 has been denied", message.Subject);
        var html = Html(message);
        Assert.Contains(">denied</strong>", html);
        Assert.Contains("No days were booked.", html);
        Assert.Contains("<strong>Note from your admin:</strong> Too busy &lt;that&gt; week", html);
    }
}
