namespace ServiceDesk.Api.Contracts;

// Staff contracts include assignment, endpoint metadata, and audit details.
public sealed record CreateTicketRequest(int AssetId, string? Summary, string? Priority);

public sealed record TicketRow(
    int Id, string Summary, string Priority, string Status,
    string? AssignedTechnician, DateTime CreatedAtUtc, string? AssetTag, string? Hostname,
    string Category, string? RequesterName, string? AssignedTechnicianId);

public sealed record AssetSummary(
    int Id, string AssetTag, string Hostname, string BitLockerState,
    string TpmState, DateTime LastCheckedAtUtc);

public sealed record TicketEventItem(
    int Id, string PreviousStatus, string NewStatus, string Technician,
    string? Note, DateTime OccurredAtUtc);

public sealed record TicketDetail(
    int Id, string Summary, string Priority, string Status, string? AssignedTechnician,
    DateTime CreatedAtUtc, DateTime? ResolvedAtUtc, AssetSummary? Asset,
    IReadOnlyList<TicketEventItem> History, string? Description, string Category,
    string? WorkstationDetails, string? RequesterName, string? AssignedTechnicianId, int Version,
    IReadOnlyList<StaffReplyItem> Replies, IReadOnlyList<TicketTriageEventItem> TriageHistory);

public sealed record ChangeTicketStatusRequest(string? ExpectedStatus, string? Status, string? Note, int? ExpectedVersion);

// The queue returns a bounded page and the total matching rows for navigation.
public sealed record TicketPage(
    IReadOnlyList<TicketRow> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record UpdateTicketTriageRequest(int? ExpectedVersion, string? Priority, string? AssignedTechnicianId, int? AssetId);
public sealed record AddStaffReplyRequest(string? Body, bool IsInternal);

public sealed record StaffReplyItem(int Id, string Body, string Author, DateTime OccurredAtUtc, bool IsInternal);
public sealed record TicketTriageEventItem(
    int Id, string Field, string? PreviousValue, string? NewValue, string Technician, DateTime OccurredAtUtc);

public sealed record TechnicianOption(string Id, string UserName);
public sealed record AssetOption(int Id, string AssetTag, string Hostname);
public sealed record TicketOptions(IReadOnlyList<TechnicianOption> Technicians, IReadOnlyList<AssetOption> Assets);

// Employee contracts expose the public conversation and status changes for one requester.
public sealed record CreateEmployeeTicketRequest(string? Summary, string? Description, string? Category, string? WorkstationDetails);
public sealed record AddEmployeeReplyRequest(string? Body);

public sealed record EmployeeTicketRow(int Id, string Summary, string Category, string Status,
    DateTime CreatedAtUtc, DateTime? ResolvedAtUtc);

public sealed record EmployeeReplyItem(int Id, string Body, string Author, DateTime OccurredAtUtc);

public sealed record EmployeeStatusItem(
    int Id, string PreviousStatus, string NewStatus, DateTime OccurredAtUtc, string? ResolutionNote);

public sealed record EmployeeTicketDetail(
    int Id, string Summary, string? Description, string Category, string? WorkstationDetails,
    string Status, DateTime CreatedAtUtc, DateTime? ResolvedAtUtc,
    IReadOnlyList<EmployeeReplyItem> Replies, IReadOnlyList<EmployeeStatusItem> StatusHistory);
