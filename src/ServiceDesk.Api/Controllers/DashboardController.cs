using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = "Technician,Viewer")]
[Route("api/dashboard")]
public sealed class DashboardController(ServiceDeskDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardMetrics>> Get(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        const string format = "yyyy-MM-dd";
        if (!DateOnly.TryParseExact(from, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !DateOnly.TryParseExact(to, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) ||
            end <= start)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                detail: "Supply two UTC dates in yyyy-MM-dd format. The end date must be after the start date.");
        }

        // Use a half-open UTC interval so adjacent reports do not double-count midnight events.
        var fromUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var tickets = db.Tickets.AsNoTracking();

        var createdCount = await tickets.CountAsync(
            ticket => ticket.CreatedAtUtc >= fromUtc && ticket.CreatedAtUtc < toUtc, cancellationToken);
        var openNowCount = await tickets.CountAsync(
            ticket => ticket.Status == TicketStatus.Open || ticket.Status == TicketStatus.InProgress, cancellationToken);
        var resolved = await tickets
            .Where(ticket => ticket.ResolvedAtUtc >= fromUtc && ticket.ResolvedAtUtc < toUtc)
            .Select(ticket => new { ticket.CreatedAtUtc, ResolvedAtUtc = ticket.ResolvedAtUtc!.Value })
            .ToListAsync(cancellationToken);

        // Compute duration after fetching the resolution cohort, including tickets created before the range.
        double? averageResolutionMinutes = resolved.Count == 0
            ? null
            : resolved.Average(ticket => (ticket.ResolvedAtUtc - ticket.CreatedAtUtc).TotalMinutes);

        return new DashboardMetrics(start, end, createdCount, resolved.Count, openNowCount, averageResolutionMinutes);
    }

    public sealed record DashboardMetrics(
        DateOnly From,
        DateOnly To,
        int CreatedCount,
        int ResolvedCount,
        int OpenNowCount,
        double? AverageResolutionMinutes);
}
