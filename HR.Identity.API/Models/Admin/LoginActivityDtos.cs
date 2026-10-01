using HR.Identity.API.Models.Common;

namespace HR.Identity.API.Models.Admin;

public sealed record LoginActivityItemDto(
    long Id,
    DateTime OccurredAt,
    Guid? UserId,
    string? UserName,
    string EmailAttempted,
    LoginMethod Method,
    bool Succeeded,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent);

public sealed record LoginActivitySummaryDto(
    int Days,
    int SignIns,
    int Failed,
    int Lockouts,
    int UniqueUsers);
