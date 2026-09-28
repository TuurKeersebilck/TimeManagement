using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace TimeManagementBackend.Tests.Security;

internal sealed record Endpoint(string Controller, string Action, string Route, bool IsAnonymous, string[] Roles)
{
    public string Key => $"{Controller}.{Action}";
}

/// <summary>
/// Reads the authorization attributes off every MVC action by reflection, applying the same
/// precedence ASP.NET Core does: an [AllowAnonymous] anywhere in the chain wins, otherwise the
/// roles required are the union of the class-level and method-level [Authorize] attributes.
/// </summary>
internal static class EndpointInventory
{
    public static IReadOnlyList<Endpoint> All { get; } = Build();

    private static List<Endpoint> Build()
    {
        var assembly = typeof(TimeManagementBackend.Controllers.ApiControllerBase).Assembly;

        var controllers = assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name);

        var endpoints = new List<Endpoint>();

        foreach (var controller in controllers)
        {
            var classAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>() != null;
            var classAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>().ToArray();
            var routeTemplate = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? "";

            var actions = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .OrderBy(m => m.Name);

            foreach (var action in actions)
            {
                var methodAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() != null;
                var methodAuthorize = action.GetCustomAttributes<AuthorizeAttribute>().ToArray();

                var isAnonymous = classAnonymous
                    || methodAnonymous
                    || (classAuthorize.Length == 0 && methodAuthorize.Length == 0);

                var roles = classAuthorize.Concat(methodAuthorize)
                    .Select(a => a.Roles)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .SelectMany(r => r!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    .Distinct()
                    .OrderBy(r => r)
                    .ToArray();

                var httpAttribute = action.GetCustomAttributes<HttpMethodAttribute>().First();
                var verb = httpAttribute.HttpMethods.FirstOrDefault() ?? "GET";
                var path = string.IsNullOrEmpty(httpAttribute.Template)
                    ? routeTemplate
                    : $"{routeTemplate}/{httpAttribute.Template}";

                endpoints.Add(new Endpoint(
                    controller.Name, action.Name, $"{verb} /{path.Trim('/')}",
                    isAnonymous && !(methodAuthorize.Length > 0 && !methodAnonymous),
                    roles));
            }
        }

        return endpoints;
    }
}
