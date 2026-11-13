using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;

var packaged = File.Exists(Path.Combine(AppContext.BaseDirectory, "service-desk-installed"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = packaged ? AppContext.BaseDirectory : null,
});

string? dataDirectory = null;
if (packaged)
{
    if (!File.Exists(Path.Combine(builder.Environment.WebRootPath ??
        Path.Combine(AppContext.BaseDirectory, "wwwroot"), "index.html")))
    {
        throw new InvalidOperationException("The web files are missing. Extract the full download before starting the app.");
    }

    dataDirectory = LocalInstallation.GetDataDirectory(builder.Configuration);
    var port = LocalInstallation.GetPort(builder.Configuration);
    builder.Configuration["AllowedHosts"] = "127.0.0.1";
    builder.WebHost.ConfigureKestrel(options =>
    {
        // This distribution accepts HTTP only on loopback, including when the environment supplies Kestrel settings.
        options.Configure(new ConfigurationBuilder().Build(), reloadOnChange: false);
        options.Listen(IPAddress.Loopback, port);
    });
    builder.Services.AddDataProtection()
        .SetApplicationName("EndpointServiceDesk")
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));
}

using var installationLock = dataDirectory is null ? null : LocalInstallation.Lock(dataDirectory);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ServiceDeskDbContext>();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<FirstRunSetup>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = packaged || builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddDbContext<ServiceDeskDbContext>((services, options) =>
{
    if (dataDirectory is not null)
    {
        var localDatabase = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "servicedesk.db"),
            ForeignKeys = true,
        };
        options.UseSqlite(localDatabase.ToString());
        return;
    }

    var configuration = services.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("ServiceDesk")
        ?? throw new InvalidOperationException("Add the ServiceDesk connection string.");

    var sqlite = new SqliteConnectionStringBuilder(connectionString);
    var contentRoot = services.GetRequiredService<IHostEnvironment>().ContentRootPath;
    sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, contentRoot);
    options.UseSqlite(sqlite.ToString());
});

var app = builder.Build();

if (dataDirectory is not null)
{
    await LocalInstallation.PrepareAsync(app.Services);
    app.Logger.LogInformation("Application data directory: {DataDirectory}", dataDirectory);
}
else if (app.Environment.IsDevelopment() &&
    (app.Configuration[DemoAccounts.TechnicianPasswordKey] is not null ||
     app.Configuration[DemoAccounts.ViewerPasswordKey] is not null))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DemoAccounts.InitializeAsync(
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
        app.Configuration);
}

app.UseExceptionHandler();
if (packaged)
{
    app.UseStaticFiles();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
if (packaged)
{
    app.Map("/api/{**path}", () => Results.NotFound());
    app.MapFallbackToFile("index.html");
}

await app.RunAsync();

public partial class Program { }
