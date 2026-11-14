using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ServiceDesk.Api.Tests;

public sealed class TicketApiTests
{
    // Check that a newly created ticket can be retrieved from the URL returned by the API.
    [Fact]
    public async Task CreateTicket_CanBeReadFromItsLocation()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var client = await app.CreateSignedInClientAsync();

        using var created = await TestApp.SendJsonAsync(client, HttpMethod.Post, "/api/tickets", new
        {
            assetId = 2,
            summary = "  Confirm workstation TPM readiness  ",
            priority = "p3",
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = Assert.IsType<Uri>(created.Headers.Location);

        using var readBack = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, readBack.StatusCode);

        var ticket = await readBack.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Confirm workstation TPM readiness", ticket.GetProperty("summary").GetString());
        Assert.Equal("P3", ticket.GetProperty("priority").GetString());
        Assert.Equal("open", ticket.GetProperty("status").GetString());
        Assert.Equal(2, ticket.GetProperty("asset").GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, ticket.GetProperty("resolvedAtUtc").ValueKind);
        Assert.Empty(ticket.GetProperty("history").EnumerateArray());
    }

    [Fact]
    public async Task Resolve_ValidatesStatusAndNote_AndPersistsHistory()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var client = await app.CreateSignedInClientAsync();
        const string path = "/api/tickets/1/status";
        const string note = "Recorded workstation follow-up.";

        using var start = await TestApp.SendJsonAsync(client, HttpMethod.Patch, path, new
        {
            expectedStatus = "open",
            status = "in-progress",
        });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using var emptyNote = await TestApp.SendJsonAsync(client, HttpMethod.Patch, path, new
        {
            expectedStatus = "in-progress",
            status = "resolved",
            note = "   ",
        });
        Assert.Equal(HttpStatusCode.BadRequest, emptyNote.StatusCode);

        using var stale = await TestApp.SendJsonAsync(client, HttpMethod.Patch, path, new
        {
            expectedStatus = "open",
            status = "in-progress",
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/tickets/1");
        Assert.Equal("in-progress", before.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("resolvedAtUtc").ValueKind);
        Assert.Single(before.GetProperty("history").EnumerateArray());

        var resolution = new { expectedStatus = "in-progress", status = "resolved", note = $"  {note}  " };
        using var resolved = await TestApp.SendJsonAsync(client, HttpMethod.Patch, path, resolution);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        using var replay = await TestApp.SendJsonAsync(client, HttpMethod.Patch, path, resolution);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);

        using var freshClient = await app.CreateSignedInClientAsync();
        var after = await freshClient.GetFromJsonAsync<JsonElement>("/api/tickets/1");
        Assert.Equal("resolved", after.GetProperty("status").GetString());
        Assert.Equal("demo-tech", after.GetProperty("assignedTechnician").GetString());

        var history = after.GetProperty("history");
        Assert.Equal(2, history.GetArrayLength());
        var entry = history[1];
        Assert.Equal("in-progress", entry.GetProperty("previousStatus").GetString());
        Assert.Equal("resolved", entry.GetProperty("newStatus").GetString());
        Assert.Equal("demo-tech", entry.GetProperty("technician").GetString());
        Assert.Equal(note, entry.GetProperty("note").GetString());

        var resolvedAt = after.GetProperty("resolvedAtUtc").GetString();
        Assert.NotNull(resolvedAt);
        Assert.EndsWith("Z", resolvedAt);
        Assert.Equal(resolvedAt, entry.GetProperty("occurredAtUtc").GetString());
    }
}
