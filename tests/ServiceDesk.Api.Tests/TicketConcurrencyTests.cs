using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using Xunit;

namespace ServiceDesk.Api.Tests;

public sealed class TicketConcurrencyTests
{
    // Separate contexts simulate two requests updating the same original row version.
    [Fact]
    public async Task SaveChanges_WithStaleStatus_LeavesOnlyTheWinningEvent()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();

        await using var firstScope = app.Services.CreateAsyncScope();
        await using var secondScope = app.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();

        var first = await firstDb.Tickets.SingleAsync(ticket => ticket.Id == 1);
        var second = await secondDb.Tickets.SingleAsync(ticket => ticket.Id == 1);

        StartWork(first, "First technician");
        StartWork(second, "Second technician");

        await firstDb.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());

        await using var readScope = app.Services.CreateAsyncScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        var saved = await readDb.Tickets.AsNoTracking()
            .Include(ticket => ticket.History)
            .SingleAsync(ticket => ticket.Id == 1);

        Assert.Equal(TicketStatus.InProgress, saved.Status);
        Assert.Equal("First technician", saved.AssignedTechnician);
        Assert.Equal("First technician", Assert.Single(saved.History).Technician);
    }

    private static void StartWork(Ticket ticket, string technician)
    {
        var previousStatus = ticket.Status;
        ticket.Status = TicketStatus.InProgress;
        ticket.AssignedTechnician = technician;
        ticket.History.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            PreviousStatus = previousStatus,
            NewStatus = ticket.Status,
            Technician = technician,
            OccurredAtUtc = DateTime.UtcNow,
        });
    }
}
