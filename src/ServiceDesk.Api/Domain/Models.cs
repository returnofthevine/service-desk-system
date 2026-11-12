namespace ServiceDesk.Api.Domain;

public static class TicketStatus
{
    public const string Open = "open";
    public const string InProgress = "in-progress";
    public const string Resolved = "resolved";

    public static bool IsKnown(string? status) =>
        status is Open or InProgress or Resolved;

    public static bool CanMove(string from, string to) =>
        (from, to) is (Open, InProgress) or (InProgress, Resolved);
}

public sealed class Asset
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string BitLockerState { get; set; } = "";
    public string TpmState { get; set; } = "";
    public DateTime LastCheckedAtUtc { get; set; }
}

public sealed class Ticket
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public string Summary { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Status { get; set; } = TicketStatus.Open;
    public string? AssignedTechnician { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public List<TicketEvent> History { get; } = [];
}

public sealed class TicketEvent
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string PreviousStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public string Technician { get; set; } = "";
    public string? Note { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
