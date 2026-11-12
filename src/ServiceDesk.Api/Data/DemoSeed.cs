using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Domain;
using static ServiceDesk.Api.Domain.TicketStatus;

namespace ServiceDesk.Api.Data;

internal static class DemoSeed
{
    internal const string Technician = "Alex Morgan";

    private static readonly DateTime ReferenceUtc =
        new(2026, 11, 1, 14, 0, 0, DateTimeKind.Utc);

    private static readonly (string Hostname, string BitLocker, string Tpm)[] AssetRows =
    [
        ("FIN-WS-01", "suspended", "ready"),
        ("FIN-WS-02", "protected", "not-ready"),
        ("OPS-WS-01", "protected", "ready"),
        ("OPS-WS-02", "unknown", "unknown"),
        ("HR-WS-01", "protected", "ready"),
        ("HR-WS-02", "protected", "ready"),
        ("ENG-WS-01", "off", "ready"),
        ("ENG-WS-02", "protected", "ready"),
        ("SALES-WS-01", "protected", "ready"),
        ("DESIGN-WS-01", "protected", "ready"),
        ("SUPPORT-WS-01", "protected", "ready"),
        ("TRAINING-WS-01", "unknown", "missing"),
    ];

    private static readonly (int AssetId, string Summary, string Priority, string Status)[] TicketRows =
    [
        (1, "BitLocker protection suspended after restart", "P2", Open),
        (2, "TPM is not ready after firmware update", "P2", InProgress),
        (3, "Workstation cannot connect to the office network", "P1", Open),
        (4, "External monitor flickers after sign-in", "P3", Open),
        (5, "Requested headset replacement", "P4", Resolved),
        (6, "Shared printer missing after restart", "P3", InProgress),
        (7, "BitLocker check reports protection off", "P2", Open),
        (8, "USB dock drops network connection", "P2", Resolved),
        (9, "New starter needs a workstation", "P3", Open),
        (10, "Disk space alert on design workstation", "P2", InProgress),
        (11, "Video calls have no audio input", "P3", Resolved),
        (12, "Screen timeout setting requested", "P4", Open),
        (3, "Keyboard disconnects during sign-in", "P3", InProgress),
        (4, "Encryption check has not reported", "P2", Resolved),
        (5, "Update pending restart", "P4", Open),
        (6, "Workstation is stuck at the sign-in screen", "P1", InProgress),
        (7, "Install approved PDF reader", "P4", Resolved),
        (8, "Confirm replacement mouse delivery", "P4", InProgress),
        (9, "VPN connection drops on Wi-Fi", "P2", Open),
        (10, "Request for a second monitor", "P4", Open),
    ];

    internal static void Apply(ModelBuilder modelBuilder)
    {
        var assets = AssetRows.Select((row, index) => new Asset
        {
            Id = index + 1,
            AssetTag = $"WS-{index + 1:000}",
            Hostname = row.Hostname,
            BitLockerState = row.BitLocker,
            TpmState = row.Tpm,
            LastCheckedAtUtc = ReferenceUtc.AddMinutes(-15 * (index + 1)),
        }).ToArray();

        var tickets = TicketRows.Select((row, index) =>
        {
            var id = index + 1;
            var createdAt = ReferenceUtc.AddHours(-6 * id);
            return new
            {
                Id = id,
                AssetId = row.AssetId,
                Summary = row.Summary,
                Priority = row.Priority,
                Status = row.Status,
                AssignedTechnician = row.Status == Open ? null : Technician,
                CreatedAtUtc = createdAt,
                ResolvedAtUtc = row.Status == Resolved ? (DateTime?)createdAt.AddHours(2 + id % 4) : null,
            };
        }).ToArray();

        var history = new List<TicketEvent>();
        foreach (var ticket in tickets)
        {
            if (ticket.Status == Open)
            {
                continue;
            }

            history.Add(new TicketEvent
            {
                Id = history.Count + 1,
                TicketId = ticket.Id,
                PreviousStatus = Open,
                NewStatus = InProgress,
                Technician = Technician,
                OccurredAtUtc = ticket.CreatedAtUtc.AddHours(1),
            });

            if (ticket.ResolvedAtUtc is DateTime resolvedAt)
            {
                history.Add(new TicketEvent
                {
                    Id = history.Count + 1,
                    TicketId = ticket.Id,
                    PreviousStatus = InProgress,
                    NewStatus = Resolved,
                    Technician = Technician,
                    Note = "Verified the reported issue and recorded the outcome.",
                    OccurredAtUtc = resolvedAt,
                });
            }
        }

        modelBuilder.Entity<Asset>().HasData(assets);
        modelBuilder.Entity<Ticket>().HasData(tickets);
        modelBuilder.Entity<TicketEvent>().HasData(history);
    }
}
