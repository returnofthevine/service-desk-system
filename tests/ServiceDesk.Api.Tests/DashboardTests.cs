using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Api.Controllers;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using Xunit;

namespace ServiceDesk.Api.Tests;

public sealed class DashboardTests
{
    [Fact]
    public async Task ViewerCanReadTheSeededDashboard()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var viewer = await app.CreateSignedInClientAsync(viewer: true);

        var result = await ReadDashboardAsync(viewer, "2026-10-27", "2026-11-02");

        Assert.Equal(new DashboardController.DashboardMetrics(
            new DateOnly(2026, 10, 27), new DateOnly(2026, 11, 2), 20, 5, 15, 204), result);
    }

    [Fact]
    public async Task DashboardUsesTheResolutionCohortAndHalfOpenUtcIntervals()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
            var start = new DateTime(2027, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddDays(2);
            db.Tickets.AddRange(
                CreateTicket("Resolved exactly at start", start.AddMinutes(-30), start),
                CreateTicket("Resolved exactly at end", end.AddHours(-3), end),
                CreateTicket("Created before the range", start.AddHours(-1), start.AddMinutes(30)),
                CreateTicket("Open ticket created at start", start));
            await db.SaveChangesAsync();
        }

        using var technician = await app.CreateSignedInClientAsync();
        var result = await ReadDashboardAsync(technician, "2027-01-10", "2027-01-12");
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(2, result.ResolvedCount);
        Assert.Equal(16, result.OpenNowCount);
        Assert.Equal(60.0, result.AverageResolutionMinutes);

        var next = await ReadDashboardAsync(technician, "2027-01-12", "2027-01-13");
        Assert.Equal(0, next.CreatedCount);
        Assert.Equal(1, next.ResolvedCount);
        Assert.Equal(180.0, next.AverageResolutionMinutes);

        var empty = await ReadDashboardAsync(technician, "2027-02-01", "2027-02-02");
        Assert.Equal(0, empty.CreatedCount);
        Assert.Equal(0, empty.ResolvedCount);
        Assert.Equal(16, empty.OpenNowCount);
        Assert.Null(empty.AverageResolutionMinutes);
    }

    [Theory]
    [InlineData("?to=2027-01-10")]
    [InlineData("?from=2027-01-10&to=2027-01-10")]
    [InlineData("?from=2027-01-12&to=2027-01-10")]
    [InlineData("?from=2027-1-10&to=2027-01-12")]
    public async Task DashboardRejectsInvalidDateRanges(string query)
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var technician = await app.CreateSignedInClientAsync();

        using var response = await technician.GetAsync($"/api/dashboard{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<DashboardController.DashboardMetrics> ReadDashboardAsync(
        HttpClient client, string from, string to)
    {
        var result = await client.GetFromJsonAsync<DashboardController.DashboardMetrics>($"/api/dashboard?from={from}&to={to}");
        return Assert.IsType<DashboardController.DashboardMetrics>(result);
    }

    private static Ticket CreateTicket(string summary, DateTime created, DateTime? resolved = null) => new()
    {
        AssetId = 1,
        Summary = summary,
        Priority = "P2",
        Status = resolved is null ? TicketStatus.Open : TicketStatus.Resolved,
        CreatedAtUtc = created,
        ResolvedAtUtc = resolved,
    };
}
