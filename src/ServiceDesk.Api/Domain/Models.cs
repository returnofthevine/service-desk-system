using Microsoft.AspNetCore.Identity;

namespace ServiceDesk.Api.Domain;

// Status and category keys are persisted and shared by the two portal APIs.
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

public static class TicketCategory
{
    public const string Endpoint = "endpoint";

    public static bool IsKnown(string? category) =>
        category is Endpoint or "account-access" or "software" or "network" or "other";
}

// A workstation record is optional for requests such as account access or software help.
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
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }
    // Employee visibility follows this stable Identity ID. Legacy tickets have no requester.
    public string? RequesterId { get; set; }
    public IdentityUser? Requester { get; set; }
    public string Summary { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = TicketCategory.Endpoint;
    public string? WorkstationDetails { get; set; }
    public string Priority { get; set; } = "";
    public string Status { get; set; } = TicketStatus.Open;
    public string? AssignedTechnicianId { get; set; }
    // Retain the assigned name for display, including assignments from pre-Identity seed data.
    public string? AssignedTechnician { get; set; }
    // A version change invalidates open technician forms after a status or triage edit.
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public List<TicketEvent> History { get; } = [];
    public List<TicketReply> Replies { get; } = [];
    public List<TicketTriageEvent> TriageHistory { get; } = [];
}

// Status events retain the technician's display name and stable ID for the audit trail.
public sealed class TicketEvent
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string PreviousStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public string Technician { get; set; } = "";
    public string? TechnicianId { get; set; }
    public string? Note { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

// Replies use a visibility flag so the employee API can exclude staff-only notes.
public sealed class TicketReply
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string AuthorId { get; set; } = "";
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsInternal { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

// Triage history records the value before and after each priority, assignment, or asset edit.
public sealed class TicketTriageEvent
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Field { get; set; } = "";
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string TechnicianId { get; set; } = "";
    public string Technician { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
}
