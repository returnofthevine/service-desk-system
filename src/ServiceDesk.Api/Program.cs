using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ServiceDeskDbContext>();
builder.Services.AddAuthorization();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddDbContext<ServiceDeskDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("ServiceDesk")
        ?? throw new InvalidOperationException("Add the ServiceDesk connection string.");

    var sqlite = new SqliteConnectionStringBuilder(connectionString);
    var contentRoot = services.GetRequiredService<IHostEnvironment>().ContentRootPath;
    sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, contentRoot);
    options.UseSqlite(sqlite.ToString());
});

var app = builder.Build();

if (app.Environment.IsDevelopment() &&
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program { }
