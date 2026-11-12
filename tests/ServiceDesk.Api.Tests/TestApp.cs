using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Api.Data;

namespace ServiceDesk.Api.Tests;

internal sealed class TestApp : WebApplicationFactory<Program>
{
    private readonly string databasePath =
        Path.Combine(Path.GetTempPath(), $"service-desk-{Guid.NewGuid():N}.db");
    private readonly string technicianPassword = $"Test!7-{Guid.NewGuid():N}";
    private readonly string viewerPassword = $"Test!7-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Pooling = false,
        };

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ServiceDesk"] = connection.ToString(),
                ["DemoAccounts:TechnicianPassword"] = technicianPassword,
                ["DemoAccounts:ViewerPassword"] = viewerPassword,
            }));
    }

    public async Task PrepareDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<ServiceDeskDbContext>().Database.MigrateAsync();
        await DemoAccounts.InitializeAsync(
            services.GetRequiredService<RoleManager<IdentityRole>>(),
            services.GetRequiredService<UserManager<IdentityUser>>(),
            services.GetRequiredService<IConfiguration>());
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    public async Task<HttpClient> CreateSignedInClientAsync(bool viewer = false)
    {
        var client = CreateApiClient();
        try
        {
            using var response = await SendJsonAsync(client, HttpMethod.Post, "/api/auth/sign-in", new
            {
                userName = viewer ? "demo-viewer" : "demo-tech",
                password = viewer ? viewerPassword : technicianPassword,
            });
            response.EnsureSuccessStatusCode();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        return data.GetProperty("requestToken").GetString()!;
    }

    public static async Task<HttpResponseMessage> SendJsonAsync<T>(
        HttpClient client, HttpMethod method, string path, T body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        return await client.SendAsync(request);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(databasePath);
    }
}
