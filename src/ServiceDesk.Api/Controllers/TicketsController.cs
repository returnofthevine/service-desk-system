using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize(Roles = "Technician,Viewer")]
public sealed class TicketsController(ServiceDeskDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TicketRow[]>> GetTickets(
        [FromQuery] string? status, [FromQuery] string? priority, CancellationToken ct)
    {
        var (query, error) = FilterTickets(status, priority);
        if (error is not null)
        {
            return Problem(statusCode: 400, detail: error);
        }

        return Ok(await SelectRows(OrderTickets(query)).ToArrayAsync(ct));
    }

    [HttpGet("/api/v2/tickets")]
    public async Task<ActionResult<TicketPage>> GetTicketPage(
        [FromQuery] string? status, [FromQuery] string? priority, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return Problem(statusCode: 400,
                detail: "Use a page of at least 1 and a pageSize between 1 and 100.");
        }

        var (query, error) = FilterTickets(status, priority);
        if (error is not null)
        {
            return Problem(statusCode: 400, detail: error);
        }

        var totalCount = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        TicketRow[] items = offset >= totalCount
            ? []
            : await SelectRows(OrderTickets(query).Skip((int)offset).Take(pageSize)).ToArrayAsync(ct);

        return Ok(new TicketPage(items, page, pageSize, totalCount, totalPages));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDetail>> GetTicket(int id, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .Include(item => item.Asset)
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.Id == id, ct);

        if (ticket is null)
        {
            return Problem(statusCode: 404, detail: $"Ticket #{id} was not found.");
        }

        return Ok(ToDetail(ticket));
    }

    [Authorize(Roles = "Technician")]
    [HttpPost]
    public async Task<ActionResult<TicketDetail>> CreateTicket(
        CreateTicketRequest request, CancellationToken ct)
    {
        var summary = request.Summary?.Trim();
        var priority = request.Priority?.Trim().ToUpperInvariant();

        if (request.AssetId <= 0)
        {
            return Problem(statusCode: 400, detail: "Provide a positive assetId.");
        }

        if (string.IsNullOrEmpty(summary) || summary.Length > 160)
        {
            return Problem(statusCode: 400, detail: "Provide a summary of 1 to 160 characters.");
        }

        if (priority is null || !IsPriority(priority))
        {
            return Problem(statusCode: 400, detail: "Use P1, P2, P3, or P4 for priority.");
        }

        var asset = await db.Assets.SingleOrDefaultAsync(item => item.Id == request.AssetId, ct);
        if (asset is null)
        {
            return Problem(statusCode: 404, detail: $"Asset #{request.AssetId} was not found.");
        }

        var ticket = new Ticket
        {
            AssetId = asset.Id,
            Asset = asset,
            Summary = summary,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ToDetail(ticket));
    }

    [Authorize(Roles = "Technician")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<TicketDetail>> ChangeStatus(
        int id, ChangeTicketStatusRequest request, CancellationToken ct)
    {
        var expectedStatus = request.ExpectedStatus?.Trim().ToLowerInvariant();
        var nextStatus = request.Status?.Trim().ToLowerInvariant();

        if (expectedStatus is null || nextStatus is null
            || !TicketStatus.IsKnown(expectedStatus) || !TicketStatus.IsKnown(nextStatus))
        {
            return Problem(statusCode: 400, detail: "Provide valid expectedStatus and status values.");
        }

        var ticket = await db.Tickets
            .Include(item => item.Asset)
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.Id == id, ct);

        if (ticket is null)
        {
            return Problem(statusCode: 404, detail: $"Ticket #{id} was not found.");
        }

        if (ticket.Status != expectedStatus)
        {
            return Problem(statusCode: 409,
                detail: $"This ticket is now {ticket.Status}. Reload it before changing its status.");
        }

        if (!TicketStatus.CanMove(ticket.Status, nextStatus))
        {
            return Problem(statusCode: 422,
                detail: $"A ticket cannot move from {ticket.Status} to {nextStatus}.");
        }

        var note = request.Note?.Trim();
        if (note?.Length > 500)
        {
            return Problem(statusCode: 400, detail: "Use 500 characters or fewer for the note.");
        }

        if (nextStatus == TicketStatus.Resolved && string.IsNullOrEmpty(note))
        {
            return Problem(statusCode: 400, detail: "Enter a resolution note before resolving this ticket.");
        }

        var technician = User.Identity!.Name!;
        var previousStatus = ticket.Status;
        var now = DateTime.UtcNow;
        ticket.Status = nextStatus;

        if (nextStatus == TicketStatus.InProgress)
        {
            ticket.AssignedTechnician = technician;
        }

        if (nextStatus == TicketStatus.Resolved)
        {
            ticket.ResolvedAtUtc = now;
        }

        ticket.History.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            PreviousStatus = previousStatus,
            NewStatus = nextStatus,
            Technician = technician,
            Note = string.IsNullOrEmpty(note) ? null : note,
            OccurredAtUtc = now,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(statusCode: 409,
                detail: "This ticket changed while you were saving. Reload it and review the latest status.");
        }

        return Ok(ToDetail(ticket));
    }

    private (IQueryable<Ticket> Query, string? Error) FilterTickets(string? status, string? priority)
    {
        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        var priorityFilter = string.IsNullOrWhiteSpace(priority) ? null : priority.Trim().ToUpperInvariant();
        var query = db.Tickets.AsNoTracking();

        if (statusFilter is not null)
        {
            if (!TicketStatus.IsKnown(statusFilter))
            {
                return (query, "Use open, in-progress, or resolved for status.");
            }

            query = query.Where(ticket => ticket.Status == statusFilter);
        }

        if (priorityFilter is not null)
        {
            if (!IsPriority(priorityFilter))
            {
                return (query, "Use P1, P2, P3, or P4 for priority.");
            }

            query = query.Where(ticket => ticket.Priority == priorityFilter);
        }

        return (query, null);
    }

    private static IOrderedQueryable<Ticket> OrderTickets(IQueryable<Ticket> query) =>
        query.OrderBy(ticket => ticket.Priority)
            .ThenByDescending(ticket => ticket.CreatedAtUtc)
            .ThenByDescending(ticket => ticket.Id);

    private static IQueryable<TicketRow> SelectRows(IQueryable<Ticket> query) =>
        query.Select(ticket => new TicketRow(
            ticket.Id, ticket.Summary, ticket.Priority, ticket.Status,
            ticket.AssignedTechnician, ticket.CreatedAtUtc,
            ticket.Asset.AssetTag, ticket.Asset.Hostname));

    private static bool IsPriority(string priority) =>
        priority is "P1" or "P2" or "P3" or "P4";

    private static TicketDetail ToDetail(Ticket ticket)
    {
        var asset = new AssetSummary(
            ticket.Asset.Id, ticket.Asset.AssetTag, ticket.Asset.Hostname,
            ticket.Asset.BitLockerState, ticket.Asset.TpmState, ticket.Asset.LastCheckedAtUtc);

        var history = ticket.History
            .OrderBy(entry => entry.OccurredAtUtc)
            .ThenBy(entry => entry.Id)
            .Select(entry => new TicketEventItem(
                entry.Id, entry.PreviousStatus, entry.NewStatus, entry.Technician,
                entry.Note, entry.OccurredAtUtc))
            .ToArray();

        return new TicketDetail(
            ticket.Id, ticket.Summary, ticket.Priority, ticket.Status, ticket.AssignedTechnician,
            ticket.CreatedAtUtc, ticket.ResolvedAtUtc, asset, history);
    }
}
