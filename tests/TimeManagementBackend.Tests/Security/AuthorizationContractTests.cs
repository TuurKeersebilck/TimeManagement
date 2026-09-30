namespace TimeManagementBackend.Tests.Security;

/// <summary>
/// Locks down who can reach every endpoint. These tests fail whenever an endpoint is added,
/// or an existing one changes its authorization, which forces the decision to be made
/// deliberately rather than inherited by accident from the controller it was pasted into.
/// </summary>
public class AuthorizationContractTests
{
    /// <summary>The only endpoints reachable without a JWT. Everything here is either
    /// pre-authentication or guarded by a secret in the URL, and is called out below.</summary>
    private static readonly string[] ExpectedAnonymous =
    [
            "AuthController.ForgotPassword",
            "AuthController.Login",
            "AuthController.ResetPassword",
            "CalendarController.GetFeed",
            "InvitesController.AcceptInvite",
            "InvitesController.ValidateToken",
            "SetupController.Complete",
            "SetupController.GetStatus",
            "TimeAdjustmentRequestsController.Approve",
    ];

    /// <summary>Endpoints that require the Admin role.</summary>
    private static readonly string[] ExpectedAdminOnly =
    [
            "AdminController.AssignVacationType",
            "AdminController.CreateTimeBankAdjustment",
            "AdminController.CreateVacationType",
            "AdminController.DeleteEmployee",
            "AdminController.DeleteTimeBankAdjustment",
            "AdminController.DeleteVacationType",
            "AdminController.DisableEmployee",
            "AdminController.EnableEmployee",
            "AdminController.ExportDailyPayroll",
            "AdminController.ExportPayroll",
            "AdminController.GetAllTimeLogs",
            "AdminController.GetTimeLogSummary",
            "AdminController.GetAllVacationDays",
            "AdminController.GetEmployeeBalances",
            "AdminController.GetEmployeeOvertime",
            "AdminController.GetEmployees",
            "AdminController.GetEmployeeTarget",
            "AdminController.GetEmployeeWorkdayTargets",
            "AdminController.GetTimeBankAdjustments",
            "AdminController.GetVacationTypes",
            "AdminController.GetWeeklySummary",
            "AdminController.RemoveEmployeeVacationType",
            "AdminController.SetEmployeeTarget",
            "AdminController.SetEmployeeWorkdayTargets",
            "AdminController.UpdateEmployeeBalance",
            "AdminController.UpdateVacationType",
            "AdminSettingsController.AddHoliday",
            "AdminSettingsController.DeleteHoliday",
            "AdminSettingsController.GetAvailableCountries",
            "AdminSettingsController.GetConfiguration",
            "AdminSettingsController.GetDefaultTargets",
            "AdminSettingsController.GetHolidays",
            "AdminSettingsController.RefreshHolidays",
            "AdminSettingsController.SetCountry",
            "AdminSettingsController.SetDefaultTargets",
            "AdminSettingsController.SetIsWorkingDay",
            "AdminSettingsController.SetMinimumBreakMinutes",
            "AdminSettingsController.SetNotificationEmail",
            "AdminSettingsController.SetNotificationToggles",
            "InvitesController.CancelInvite",
            "InvitesController.CreateInvite",
            "InvitesController.GetPendingInvites",
            "SettlementsController.Confirm",
            "SettlementsController.Generate",
            "SettlementsController.GetAll",
            "SettlementsController.GetById",
            "SettlementsController.GetEmployeeHistory",
            "TimeAdjustmentRequestsController.ApproveById",
            "TimeAdjustmentRequestsController.EditDayAsAdmin",
            "TimeAdjustmentRequestsController.GetAll",
            "TimeAdjustmentRequestsController.Reject",
            "VacationsController.CreateEmployeeVacationDay",
            "VacationsController.CreateEmployeeVacationRange",
            "VacationsController.DeleteEmployeeVacationDay",
            "VacationsController.GetEmployeeBalances",
            "VacationsController.GetEmployeeVacationDays",
            "VacationsController.UpdateEmployeeVacationDay",
    ];

    /// <summary>Endpoints any authenticated user may call.</summary>
    private static readonly string[] ExpectedAuthenticated =
    [
            "AuthController.ChangePassword",
            "AuthController.GetCurrentUser",
            "AuthController.Logout",
            "AuthController.UpdateProfile",
            "CalendarController.GetToken",
            "CalendarController.RegenerateToken",
            "NotificationsController.GetNotifications",
            "NotificationsController.GetUnreadCount",
            "NotificationsController.MarkAllAsRead",
            "NotificationsController.MarkAsRead",
            "PublicHolidaysController.GetHolidays",
            "TimeAdjustmentRequestsController.Create",
            "TimeAdjustmentRequestsController.GetMine",
            "VacationsController.CreateVacationDay",
            "VacationsController.CreateVacationRange",
            "VacationsController.DeleteVacationDay",
            "VacationsController.GetBalances",
            "VacationsController.GetTeamVacationDays",
            "VacationsController.GetVacationDays",
            "VacationsController.GetVacationForDate",
            "VacationsController.GetVacationTypes",
            "VacationsController.UpdateVacationDay",
            "WorkSessionsController.ClockIn",
            "WorkSessionsController.ClockOut",
            "WorkSessionsController.EndBreak",
            "WorkSessionsController.GetMySchedule",
            "WorkSessionsController.GetOvertime",
            "WorkSessionsController.GetSummaries",
            "WorkSessionsController.GetToday",
            "WorkSessionsController.GetTodayLive",
            "WorkSessionsController.SetDefaultWfhWeekdays",
            "WorkSessionsController.StartBreak",
            "WorkSessionsController.UpdateDay",
    ];

    [Fact]
    public void EveryEndpointIsAccountedFor()
    {
        // Guards against an endpoint slipping in without landing in any bucket below.
        var expected = ExpectedAnonymous.Concat(ExpectedAdminOnly).Concat(ExpectedAuthenticated)
            .OrderBy(k => k).ToArray();
        var actual = EndpointInventory.All.Select(e => e.Key).OrderBy(k => k).ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OnlyTheExpectedEndpointsAreAnonymous()
    {
        // The important direction: an endpoint that should need a JWT but does not.
        var actual = EndpointInventory.All.Where(e => e.IsAnonymous)
            .Select(e => e.Key).OrderBy(k => k).ToArray();

        Assert.Equal(ExpectedAnonymous.OrderBy(k => k).ToArray(), actual);
    }

    [Fact]
    public void AdminOnlyEndpointsRequireTheAdminRole()
    {
        var actual = EndpointInventory.All.Where(e => e.Roles.Contains("Admin"))
            .Select(e => e.Key).OrderBy(k => k).ToArray();

        Assert.Equal(ExpectedAdminOnly.OrderBy(k => k).ToArray(), actual);
    }

    [Fact]
    public void EveryAdminRouteIsAdminOnly()
    {
        // A route under /api/admin that any signed-in employee could call would be a
        // privilege-escalation bug, so the route prefix and the role must agree.
        var offenders = EndpointInventory.All
            .Where(e => e.Route.Contains("/api/admin", StringComparison.OrdinalIgnoreCase)
                     || e.Route.Contains("/api/Admin", StringComparison.Ordinal))
            .Where(e => !e.Roles.Contains("Admin"))
            .Select(e => $"{e.Key} ({e.Route})")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoEndpointRequiresAnUndefinedRole()
    {
        // UserRole only defines Employee and Admin; a typo such as Roles = "Administrator"
        // silently locks everyone out instead of failing loudly at startup.
        var known = new[] { "Admin", "Employee" };
        var offenders = EndpointInventory.All
            .SelectMany(e => e.Roles.Select(r => (e.Key, Role: r)))
            .Where(x => !known.Contains(x.Role))
            .Select(x => $"{x.Key} -> {x.Role}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AnonymousEndpointsThatCarrySecretsAreTokenScoped()
    {
        // These three are anonymous only because the caller proves itself with a token in the
        // path instead of a JWT. If one ever loses its token segment, it becomes fully public.
        var tokenScoped = new Dictionary<string, string>
        {
            ["CalendarController.GetFeed"] = "{token}",
            ["TimeAdjustmentRequestsController.Approve"] = "{token}",
            ["InvitesController.ValidateToken"] = "validate",
        };

        foreach (var (key, marker) in tokenScoped)
        {
            var endpoint = Assert.Single(EndpointInventory.All, e => e.Key == key);
            Assert.True(endpoint.IsAnonymous, $"{key} is expected to be anonymous.");
            Assert.Contains(marker, endpoint.Route);
        }
    }
}
