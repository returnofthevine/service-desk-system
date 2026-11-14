using Microsoft.AspNetCore.Identity;

namespace ServiceDesk.Api.Data;

public static class DemoAccounts
{
    public const string TechnicianPasswordKey = "DemoAccounts:TechnicianPassword";
    public const string ViewerPasswordKey = "DemoAccounts:ViewerPassword";

    public static async Task InitializeAsync(
        RoleManager<IdentityRole> roles,
        UserManager<IdentityUser> users,
        IConfiguration configuration)
    {
        // Demo credentials come from developer configuration and are never part of the database seed.
        var technicianPassword = GetPassword(configuration, TechnicianPasswordKey);
        var viewerPassword = GetPassword(configuration, ViewerPasswordKey);

        await EnsureAccountAsync(roles, users, "Technician", "demo-tech", technicianPassword);
        await EnsureAccountAsync(roles, users, "Viewer", "demo-viewer", viewerPassword);
    }

    private static string GetPassword(IConfiguration configuration, string key)
    {
        var password = configuration[key];
        return !string.IsNullOrWhiteSpace(password)
            ? password
            : throw new InvalidOperationException($"Set the local development setting '{key}' to provision demo accounts.");
    }

    private static async Task EnsureAccountAsync(
        RoleManager<IdentityRole> roles,
        UserManager<IdentityUser> users,
        string role,
        string userName,
        string password)
    {
        if (!await roles.RoleExistsAsync(role))
        {
            EnsureSucceeded(await roles.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
        }

        // Keep existing demo credentials and fill only the missing account or role membership.
        var user = await users.FindByNameAsync(userName);
        if (user is null)
        {
            user = new IdentityUser(userName);
            EnsureSucceeded(await users.CreateAsync(user, password), $"create account '{userName}'");
        }

        if (!await users.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await users.AddToRoleAsync(user, role), $"assign role '{role}' to '{userName}'");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not {action}. {errors}");
        }
    }
}
