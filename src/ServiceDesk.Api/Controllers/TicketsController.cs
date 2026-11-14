using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize(Roles = "Technician,Viewer")]
public sealed class TicketsController(ServiceDeskDbContext db, UserManager<IdentityUser> users) : ControllerBase
{
    // Keep the original queue endpoint for existing clients alongside the paginated workspace.
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
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? category = null, [FromQuery] string? assigneeId = null)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return Problem(statusCode: 400,
                detail: "Use a page of at least 1 and a pageSize between 1 and 100.");
        }

        var (query, error) = FilterTickets(status, priority, category, assigneeId);
        if (error is not null)
        {
            return Problem(statusCode: 400, detail: error);
        }

        // Count the filtered set and avoid narrowing a large offset until it fits the result size.
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
        var ticket = await StaffTickets().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, ct);

        if (ticket is null)
        {
            return Problem(statusCode: 404, detail: $"Ticket #{id} was not found.");
        }

        return Ok(ToDetail(ticket));
    }

    [HttpGet("options")]
    public async Task<ActionResult<TicketOptions>> GetOptions(CancellationToken ct)
    {
        // Populate filters and triage controls with stable technician and asset IDs.
        var technicians = (await users.GetUsersInRoleAsync("Technician"))
            .OrderBy(user => user.UserName)
            .Select(user => new TechnicianOption(user.Id, user.UserName!)).ToArray();
        var assets = await db.Assets.AsNoTracking().OrderBy(item => item.AssetTag)
            .Select(item => new AssetOption(item.Id, item.AssetTag, item.Hostname)).ToArrayAsync(ct);
        return Ok(new TicketOptions(technicians, assets));
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

        var ticket = await StaffTickets()
            .SingleOrDefaultAsync(item => item.Id == id, ct);

        if (ticket is null)
        {
            return Problem(statusCode: 404, detail: $"Ticket #{id} was not found.");
        }

        // Original clients provide the expected status. The workspace also sends the ticket version.
        if (ticket.Status != expectedStatus ||
            request.ExpectedVersion is int expectedVersion && expectedVersion != ticket.Version)
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

        var technicianId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var technician = User.Identity!.Name!;
        var previousStatus = ticket.Status;
        var now = DateTime.UtcNow;
        ticket.Status = nextStatus;

        // Starting work claims an unassigned ticket and preserves any existing assignment.
        if (nextStatus == TicketStatus.InProgress && ticket.AssignedTechnician is null)
        {
            ticket.AssignedTechnicianId = technicianId;
            ticket.AssignedTechnician = technician;
            RecordTriageChange(ticket, "assignedTechnician", null, technician, technicianId, technician, now);
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
            TechnicianId = technicianId,
            Note = string.IsNullOrEmpty(note) ? null : note,
            OccurredAtUtc = now,
        });

        return await SaveTicketChanges(ticket, ct);
    }

    [Authorize(Roles = "Technician")]
    [HttpPatch("{id:int}/triage")]
    public async Task<ActionResult<TicketDetail>> UpdateTriage(int id, UpdateTicketTriageRequest request, CancellationToken ct)
    {
        var priority = request.Priority?.Trim().ToUpperInvariant();
        if (request.ExpectedVersion is not int expectedVersion || expectedVersion < 0 ||
            priority is null || !IsPriority(priority) || request.AssetId is <= 0)
        {
            return Problem(statusCode: 400, detail: "Provide a ticket version, a valid priority, and a positive asset ID or no asset.");
        }

        var ticket = await StaffTickets().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null)
        {
            return Problem(statusCode: 404, detail: $"Ticket #{id} was not found.");
        }
        if (ticket.Version != expectedVersion)
        {
            return Problem(statusCode: 409,
                detail: "This ticket changed while you were editing. Reload it and review the latest details.");
        }

        var assigneeId = request.AssignedTechnicianId;
        string? assignee = null;
        // A legacy assignment can be preserved when editing other fields, or replaced with a current account.
        if (assigneeId == "legacy" && ticket.AssignedTechnicianId is null && ticket.AssignedTechnician is not null)
        {
            assigneeId = null;
            assignee = ticket.AssignedTechnician;
        }
        else if (assigneeId is not null)
        {
            var assigneeUser = await users.FindByIdAsync(assigneeId);
            if (assigneeUser is null || !await users.IsInRoleAsync(assigneeUser, "Technician"))
            {
                return Problem(statusCode: 400, detail: "Select a technician from the assignee list.");
            }
            assignee = assigneeUser.UserName;
        }

        var asset = ticket.Asset;
        if (request.AssetId != ticket.AssetId)
        {
            asset = request.AssetId is int assetId
                ? await db.Assets.SingleOrDefaultAsync(item => item.Id == assetId, ct)
                : null;
            if (request.AssetId is not null && asset is null)
            {
                return Problem(statusCode: 404, detail: $"Asset #{request.AssetId} was not found.");
            }
        }

        // Record each changed triage field with the acting technician and a common timestamp.
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var actor = User.Identity!.Name!;
        var now = DateTime.UtcNow;
        if (ticket.Priority != priority)
        {
            RecordTriageChange(ticket, "priority", ticket.Priority, priority, actorId, actor, now);
            ticket.Priority = priority;
        }
        if (ticket.AssignedTechnicianId != assigneeId || ticket.AssignedTechnician != assignee)
        {
            RecordTriageChange(ticket, "assignedTechnician", ticket.AssignedTechnician, assignee, actorId, actor, now);
            ticket.AssignedTechnician = assignee;
            ticket.AssignedTechnicianId = assigneeId;
        }
        if (ticket.AssetId != request.AssetId)
        {
            RecordTriageChange(ticket, "asset", ticket.Asset?.AssetTag, asset?.AssetTag, actorId, actor, now);
            ticket.Asset = asset;
            ticket.AssetId = request.AssetId;
        }
        return db.ChangeTracker.HasChanges() ? await SaveTicketChanges(ticket, ct) : Ok(ToDetail(ticket));
    }

    [Authorize(Roles = "Technician")]
    [HttpPost("{id:int}/replies")]
    public async Task<ActionResult<TicketDetail>> AddReply(int id, AddStaffReplyRequest request, CancellationToken ct)
    {
        var body = request.Body?.Trim();
        if (string.IsNullOrEmpty(body) || body.Length > 2000)
        {
            return Problem(statusCode: 400, detail: "Enter a reply of 1 to 2000 characters.");
        }

        var ticket = await StaffTickets().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null)
        {
            return Problem(statusCode: 404, detail: $"Ticket #{id} was not found.");
        }

        // Reply writes append independent rows and leave the status and triage version intact.
        ticket.Replies.Add(new TicketReply
        {
            TicketId = ticket.Id,
            AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            Author = User.Identity!.Name!,
            Body = body,
            IsInternal = request.IsInternal,
            OccurredAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Ok(ToDetail(ticket));
    }

    // Load each history collection separately to avoid multiplying rows across collections.
    private IQueryable<Ticket> StaffTickets() => db.Tickets.AsSplitQuery()
        .Include(item => item.Asset)
        .Include(item => item.Requester)
        .Include(item => item.History)
        .Include(item => item.Replies)
        .Include(item => item.TriageHistory);

    private async Task<ActionResult<TicketDetail>> SaveTicketChanges(Ticket ticket, CancellationToken ct)
    {
        // EF saves the ticket and its audit rows together and detects a competing update by version.
        ticket.Version++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(statusCode: 409,
                detail: "This ticket changed while you were saving. Reload it and review the latest details.");
        }

        return Ok(ToDetail(ticket));
    }

    private static void RecordTriageChange(Ticket ticket, string field, string? previousValue,
        string? newValue, string actorId, string actor, DateTime now) => ticket.TriageHistory.Add(new TicketTriageEvent
    {
        TicketId = ticket.Id,
        Field = field,
        PreviousValue = previousValue,
        NewValue = newValue,
        TechnicianId = actorId,
        Technician = actor,
        OccurredAtUtc = now,
    });

    private (IQueryable<Ticket> Query, string? Error) FilterTickets(
        string? status, string? priority, string? category = null, string? assigneeId = null)
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

        var categoryFilter = category?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(categoryFilter))
        {
            if (!TicketCategory.IsKnown(categoryFilter))
            {
                return (query, "Choose a known ticket category.");
            }
            query = query.Where(ticket => ticket.Category == categoryFilter);
        }

        if (!string.IsNullOrWhiteSpace(assigneeId))
        {
            query = assigneeId == "unassigned"
                ? query.Where(ticket => ticket.AssignedTechnician == null && ticket.AssignedTechnicianId == null)
                : query.Where(ticket => ticket.AssignedTechnicianId == assigneeId);
        }

        return (query, null);
    }

    // The final ID sort keeps page boundaries stable when tickets share a priority and timestamp.
    private static IOrderedQueryable<Ticket> OrderTickets(IQueryable<Ticket> query) =>
        query.OrderBy(ticket => ticket.Priority)
            .ThenByDescending(ticket => ticket.CreatedAtUtc)
            .ThenByDescending(ticket => ticket.Id);

    private static IQueryable<TicketRow> SelectRows(IQueryable<Ticket> query) =>
        query.Select(ticket => new TicketRow(
            ticket.Id, ticket.Summary, ticket.Priority, ticket.Status,
            ticket.AssignedTechnician, ticket.CreatedAtUtc,
            ticket.Asset == null ? null : ticket.Asset.AssetTag,
            ticket.Asset == null ? null : ticket.Asset.Hostname, ticket.Category,
            ticket.Requester == null ? null : ticket.Requester.UserName, ticket.AssignedTechnicianId));

    private static bool IsPriority(string priority) =>
        priority is "P1" or "P2" or "P3" or "P4";

    private static TicketDetail ToDetail(Ticket ticket)
    {
        var asset = ticket.Asset is { } workstation ? new AssetSummary(
            workstation.Id, workstation.AssetTag, workstation.Hostname,
            workstation.BitLockerState, workstation.TpmState, workstation.LastCheckedAtUtc) : null;

        var history = ticket.History
            .OrderBy(entry => entry.OccurredAtUtc)
            .ThenBy(entry => entry.Id)
            .Select(entry => new TicketEventItem(
                entry.Id, entry.PreviousStatus, entry.NewStatus, entry.Technician,
                entry.Note, entry.OccurredAtUtc))
            .ToArray();

        var replies = ticket.Replies.OrderBy(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id)
            .Select(entry => new StaffReplyItem(entry.Id, entry.Body, entry.Author, entry.OccurredAtUtc, entry.IsInternal))
            .ToArray();
        var triageHistory = ticket.TriageHistory.OrderBy(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id)
            .Select(entry => new TicketTriageEventItem(entry.Id, entry.Field, entry.PreviousValue,
                entry.NewValue, entry.Technician, entry.OccurredAtUtc))
            .ToArray();

        return new TicketDetail(
            ticket.Id, ticket.Summary, ticket.Priority, ticket.Status, ticket.AssignedTechnician,
            ticket.CreatedAtUtc, ticket.ResolvedAtUtc, asset, history, ticket.Description, ticket.Category,
            ticket.WorkstationDetails, ticket.Requester?.UserName, ticket.AssignedTechnicianId, ticket.Version,
            replies, triageHistory);
    }
}
