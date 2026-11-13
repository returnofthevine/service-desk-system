using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ServiceDesk.Api.Data;

internal static class LocalInstallation
{
    internal static string GetDataDirectory(IConfiguration configuration)
    {
        var configured = configuration["ServiceDesk:DataDirectory"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!Path.IsPathFullyQualified(configured))
            {
                throw new InvalidOperationException("ServiceDesk:DataDirectory must be an absolute path.");
            }

            return Path.GetFullPath(configured);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(appData))
        {
            throw new InvalidOperationException("Set ServiceDesk__DataDirectory to a writable folder for this user.");
        }

        return Path.Combine(appData, "EndpointServiceDesk");
    }

    internal static int GetPort(IConfiguration configuration)
    {
        var urls = configuration["urls"] ?? "http://127.0.0.1:5087";
        if (!Uri.TryCreate(urls, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttp || url.Host != "127.0.0.1" || url.Port == 0 ||
            url.PathAndQuery != "/" || url.Fragment.Length != 0 || url.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("Choose a local URL such as --urls http://127.0.0.1:5087.");
        }

        return url.Port;
    }

    internal static FileStream Lock(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else
        {
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        try
        {
            return File.Open(Path.Combine(directory, "service-desk.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException error)
        {
            throw new InvalidOperationException($"Could not lock {directory}. Close any other copy using this data folder.", error);
        }
    }

    internal static async Task PrepareAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        await db.Database.MigrateAsync();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if ((await users.GetUsersInRoleAsync("Technician")).Count == 0)
        {
            var setup = services.GetRequiredService<FirstRunSetup>();
            Console.WriteLine($"Setup code: {setup.Begin()}");
        }
    }
}
