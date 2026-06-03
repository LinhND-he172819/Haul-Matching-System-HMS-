using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Application.DTOs;

public sealed record ChangeTripStatusRequest(
    TripStatus Status,
    DateTimeOffset? OccurredAt);
