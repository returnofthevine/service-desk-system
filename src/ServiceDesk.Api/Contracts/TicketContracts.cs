namespace ServiceDesk.Api.Contracts;

public sealed record CreateTicketRequest(int AssetId, string? Summary, string? Priority);

public sealed record TicketRow(
    int Id, string Summary, string Priority, string Status,
    string? AssignedTechnician, DateTime CreatedAtUtc, string AssetTag, string Hostname);

public sealed record AssetSummary(
    int Id, string AssetTag, string Hostname, string BitLockerState,
    string TpmState, DateTime LastCheckedAtUtc);

public sealed record TicketEventItem(
    int Id, string PreviousStatus, string NewStatus, string Technician,
    string? Note, DateTime OccurredAtUtc);

public sealed record TicketDetail(
    int Id, string Summary, string Priority, string Status, string? AssignedTechnician,
    DateTime CreatedAtUtc, DateTime? ResolvedAtUtc, AssetSummary Asset,
    IReadOnlyList<TicketEventItem> History);

public sealed record ChangeTicketStatusRequest(string? ExpectedStatus, string? Status, string? Note);

public sealed record TicketPage(
    IReadOnlyList<TicketRow> Items, int Page, int PageSize, int TotalCount, int TotalPages);
