using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Controllers;

/// <summary>Shared base for controllers that need to resolve the current authenticated user.</summary>
public abstract class ApiControllerBase(UserManager<User> userManager) : ControllerBase
{
    protected readonly UserManager<User> UserManager = userManager;

    protected Task<User?> GetCurrentUserAsync() => UserManager.GetUserAsync(User);
}
