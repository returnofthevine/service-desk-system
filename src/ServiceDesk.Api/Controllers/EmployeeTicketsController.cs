using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/employee/tickets")]
[Authorize(Roles = "Employee")]
public sealed class EmployeeTicketsController(ServiceDeskDbContext db) : ControllerBase
{
    // The employee list and detail use the same requester-scoped query.
    [HttpGet]
    public async Task<ActionResult<EmployeeTicketRow[]>> GetTickets(CancellationToken ct) =>
        Ok(await OwnedTickets().AsNoTracking()
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ThenByDescending(ticket => ticket.Id)
            .Select(ticket => new EmployeeTicketRow(ticket.Id, ticket.Summary, ticket.Category,
                ticket.Status, ticket.CreatedAtUtc, ticket.ResolvedAtUtc))
            .ToArrayAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeTicketDetail>> GetTicket(int id, CancellationToken ct)
    {
        var ticket = await GetOwnedDetail(id, ct);
        return ticket is null ? TicketNotFound(id) : Ok(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeTicketDetail>> CreateTicket(CreateEmployeeTicketRequest request, CancellationToken ct)
    {
        // Intake accepts request details. The server chooses the requester, priority, and starting status.
        var summary = request.Summary?.Trim();
        var description = request.Description?.Trim();
        var category = request.Category?.Trim().ToLowerInvariant();
        var workstationDetails = request.WorkstationDetails?.Trim();

        if (string.IsNullOrEmpty(summary) || summary.Length > 160)
        {
            return Problem(statusCode: 400, detail: "Provide a summary of 1 to 160 characters.");
        }
        if (string.IsNullOrEmpty(description) || description.Length > 4000)
        {
            return Problem(statusCode: 400, detail: "Provide a description of 1 to 4000 characters.");
        }
        if (!TicketCategory.IsKnown(category))
        {
            return Problem(statusCode: 400, detail: "Choose a known ticket category.");
        }
        if (workstationDetails?.Length > 160)
        {
            return Problem(statusCode: 400, detail: "Use 160 characters or fewer for workstation details.");
        }

        var ticket = new Ticket
        {
            RequesterId = RequesterId,
            Summary = summary,
            Description = description,
            Category = category!,
            WorkstationDetails = string.IsNullOrEmpty(workstationDetails) ? null : workstationDetails,
            Priority = "P3",
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        var detail = new EmployeeTicketDetail(ticket.Id, ticket.Summary, ticket.Description, ticket.Category,
            ticket.WorkstationDetails, ticket.Status, ticket.CreatedAtUtc, ticket.ResolvedAtUtc, [], []);
        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, detail);
    }

    [HttpPost("{id:int}/replies")]
    public async Task<ActionResult<EmployeeTicketDetail>> AddReply(int id, AddEmployeeReplyRequest request, CancellationToken ct)
    {
        // Check ownership before writing a reply and use the same 404 for another employee's ticket.
        if (!await OwnedTickets().AnyAsync(ticket => ticket.Id == id, ct))
        {
            return TicketNotFound(id);
        }

        var body = request.Body?.Trim();
        if (string.IsNullOrEmpty(body) || body.Length > 2000)
        {
            return Problem(statusCode: 400, detail: "Enter a reply of 1 to 2000 characters.");
        }

        db.Set<TicketReply>().Add(new TicketReply
        {
            TicketId = id,
            AuthorId = RequesterId,
            Author = User.Identity!.Name!,
            Body = body,
            OccurredAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Ok(await GetOwnedDetail(id, ct));
    }

    private string RequesterId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The authenticated requester has no account identifier.");

    private IQueryable<Ticket> OwnedTickets() => db.Tickets.Where(ticket => ticket.RequesterId == RequesterId);

    // Project the employee view in the database so staff-only fields and notes stay out of the response.
    private Task<EmployeeTicketDetail?> GetOwnedDetail(int id, CancellationToken ct) =>
        OwnedTickets().AsNoTracking().AsSplitQuery().Where(ticket => ticket.Id == id)
            .Select(ticket => new EmployeeTicketDetail(ticket.Id, ticket.Summary, ticket.Description, ticket.Category,
                ticket.WorkstationDetails, ticket.Status, ticket.CreatedAtUtc, ticket.ResolvedAtUtc,
                ticket.Replies.Where(reply => !reply.IsInternal)
                    .OrderBy(reply => reply.OccurredAtUtc).ThenBy(reply => reply.Id)
                    .Select(reply => new EmployeeReplyItem(reply.Id, reply.Body, reply.Author, reply.OccurredAtUtc))
                    .ToArray(),
                ticket.History.OrderBy(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id)
                    .Select(entry => new EmployeeStatusItem(entry.Id, entry.PreviousStatus, entry.NewStatus,
                        entry.OccurredAtUtc, entry.NewStatus == TicketStatus.Resolved ? entry.Note : null))
                    .ToArray()))
            .SingleOrDefaultAsync(ct);

    private ObjectResult TicketNotFound(int id) => Problem(statusCode: 404, detail: $"Ticket #{id} was not found.");
}
