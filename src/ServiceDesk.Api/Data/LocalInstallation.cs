using System.Net;
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

        // Retain the original application-data folder so updates reopen the existing database.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(appData))
        {
            throw new InvalidOperationException("Set ServiceDesk__DataDirectory to a writable folder for this user.");
        }

        return Path.Combine(appData, "EndpointServiceDesk");
    }

    internal static LocalListener GetListener(IConfiguration configuration)
    {
        var urls = configuration["urls"] ?? "http://127.0.0.1:5087";
        if (!Uri.TryCreate(urls, UriKind.Absolute, out var url) ||
            url.Port <= 0 ||
            url.PathAndQuery != "/" || url.Fragment.Length != 0 || url.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("Choose one application URL without a path, such as --urls http://127.0.0.1:5087.");
        }

        if (url.Scheme == Uri.UriSchemeHttp && url.Host == "127.0.0.1")
        {
            return new LocalListener(IPAddress.Loopback, url.Port, "127.0.0.1", null, null);
        }

        // Shared mode serves passwords and tickets over HTTPS and accepts only the configured Host.
        var networkHost = configuration["ServiceDesk:NetworkHost"];
        var certificatePath = configuration["ServiceDesk:Tls:CertificatePath"];
        if (url.Scheme != Uri.UriSchemeHttps || !IPAddress.TryParse(url.Host, out var address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || IPAddress.IsLoopback(address) ||
            string.IsNullOrEmpty(networkHost) ||
            Uri.CheckHostName(networkHost) is not (UriHostNameType.Dns or UriHostNameType.IPv4) ||
            string.IsNullOrEmpty(certificatePath) || !Path.IsPathFullyQualified(certificatePath) || !File.Exists(certificatePath))
        {
            throw new InvalidOperationException("Shared access requires an HTTPS IPv4 listen URL, ServiceDesk:NetworkHost, and an absolute ServiceDesk:Tls:CertificatePath to a PFX file.");
        }

        return new LocalListener(address, url.Port, networkHost, certificatePath,
            configuration["ServiceDesk:Tls:CertificatePassword"]);
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

    internal static async Task PrepareAsync(IServiceProvider services, bool shared)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        await db.Database.MigrateAsync();

        // Only loopback may bootstrap the first technician. Shared mode requires an existing account.
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if ((await users.GetUsersInRoleAsync("Technician")).Count == 0)
        {
            if (shared)
            {
                throw new InvalidOperationException("Start on http://127.0.0.1:5087 and create the first technician before enabling shared access.");
            }

            var setup = services.GetRequiredService<FirstRunSetup>();
            Console.WriteLine($"Setup code: {setup.Begin()}");
        }
    }

    internal sealed record LocalListener(IPAddress Address, int Port, string Host,
        string? CertificatePath, string? CertificatePassword)
    {
        internal bool Shared => CertificatePath is not null;
    }
}
