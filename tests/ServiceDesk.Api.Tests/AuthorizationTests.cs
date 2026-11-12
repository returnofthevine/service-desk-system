using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ServiceDesk.Api.Tests;

public sealed class AuthorizationTests
{
    [Fact]
    public async Task TicketAccess_RequiresRoleAndCsrfForWrites()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var anonymous = app.CreateApiClient();
        using var viewer = await app.CreateSignedInClientAsync(viewer: true);
        using var technician = await app.CreateSignedInClientAsync();
        const string path = "/api/tickets/1/status";
        var start = new { expectedStatus = "open", status = "in-progress" };

        using var anonymousRead = await anonymous.GetAsync("/api/tickets/1");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousRead.StatusCode);

        using var viewerRead = await viewer.GetAsync("/api/tickets/1");
        Assert.Equal(HttpStatusCode.OK, viewerRead.StatusCode);
        using var viewerWrite = await TestApp.SendJsonAsync(viewer, HttpMethod.Patch, path, start);
        Assert.Equal(HttpStatusCode.Forbidden, viewerWrite.StatusCode);

        using var missingToken = await technician.PatchAsJsonAsync(path, start);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        using var technicianWrite = await TestApp.SendJsonAsync(technician, HttpMethod.Patch, path, start);
        Assert.Equal(HttpStatusCode.OK, technicianWrite.StatusCode);
        var ticket = await technicianWrite.Content.ReadFromJsonAsync<JsonElement>();
        var entry = Assert.Single(ticket.GetProperty("history").EnumerateArray());
        Assert.Equal("demo-tech", entry.GetProperty("technician").GetString());

        using var signedOut = await TestApp.SendJsonAsync(technician, HttpMethod.Post, "/api/auth/sign-out", new { });
        Assert.Equal(HttpStatusCode.OK, signedOut.StatusCode);
        using var afterSignOut = await technician.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterSignOut.StatusCode);
    }
}
