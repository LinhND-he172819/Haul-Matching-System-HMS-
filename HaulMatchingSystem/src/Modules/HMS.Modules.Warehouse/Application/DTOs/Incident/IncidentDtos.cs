namespace HMS.Modules.Warehouse.Application.DTOs.Incident;

// ─── Incident List Item (Admin/Staff view) ──────────────────────────

public sealed record IncidentListItem
{
    public Guid Id { get; init; }
    public string IncidentCode { get; init; } = string.Empty;
    public string IncidentType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    public string TripId { get; init; } = string.Empty;
    public string TripCode { get; init; } = string.Empty;

    public string DriverName { get; init; } = string.Empty;
    public string DriverId { get; init; } = string.Empty;

    public string? VehiclePlate { get; init; }
    public string? Route { get; init; }

    public DateTimeOffset ReportedAt { get; init; }
    public int EvidenceCount { get; init; }

    public string? AssignedToName { get; init; }
    public string? AssignedToId { get; init; }
}

// ─── Incident Detail (Admin/Staff view) ─────────────────────────────

public sealed record IncidentDetailDto
{
    public Guid Id { get; init; }
    public string IncidentCode { get; init; } = string.Empty;
    public string IncidentType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    public string TripId { get; init; } = string.Empty;
    public string TripCode { get; init; } = string.Empty;

    public Guid DriverId { get; init; }
    public string DriverName { get; init; } = string.Empty;

    public string? VehiclePlate { get; init; }
    public string? Route { get; init; }

    public DateTimeOffset ReportedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public Guid? AssignedToUserId { get; init; }
    public string? AssignedToName { get; init; }
    public DateTimeOffset? AssignedAt { get; init; }

    public string? ResolutionNote { get; init; }
    public Guid? ResolvedByUserId { get; init; }
    public string? ResolvedByName { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }

    public List<IncidentEvidenceDto> Evidence { get; init; } = new();

    public List<IncidentAllowedActions> AllowedActions { get; init; } = new();
}

// ─── Evidence DTO ───────────────────────────────────────────────────

public sealed record IncidentEvidenceDto
{
    public Guid Id { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
}

// ─── Allowed Actions for Admin/Staff ────────────────────────────────

public sealed record IncidentAllowedActions
{
    public bool CanTake { get; init; }      // Open → InProgress
    public bool CanResolve { get; init; }   // InProgress → Resolved
    public bool CanReject { get; init; }    // Open → Rejected
}

// ─── Status Change Request ──────────────────────────────────────────

public sealed record IncidentStatusChangeRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("note")]
    public string? Note { get; set; }
}

// ─── Incident History Item (Audit Log) ──────────────────────────────

public sealed record IncidentHistoryItem
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

// ─── Paged Result (reused from existing pattern) ────────────────────

public sealed record IncidentPagedResult
{
    public List<IncidentListItem> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
