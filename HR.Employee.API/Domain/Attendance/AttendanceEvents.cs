namespace HR.Employee.API.Domain.Attendance;

using HR.Employee.API.Domain.Common;

public sealed record AttendanceRequestSubmittedDomainEvent(AttendanceRequest Request) : IDomainEvent;

/// <summary>Correction → AttendanceDay recalculation. Overtime → payroll ko integration event.</summary>
public sealed record AttendanceRequestApprovedDomainEvent(AttendanceRequest Request) : IDomainEvent;

public sealed record AttendanceRequestRejectedDomainEvent(AttendanceRequest Request) : IDomainEvent;
