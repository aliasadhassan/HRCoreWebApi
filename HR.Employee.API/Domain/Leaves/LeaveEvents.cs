namespace HR.Employee.API.Domain.Leaves;

using HR.Employee.API.Domain.Common;

public sealed record LeaveRequestSubmittedDomainEvent(LeaveRequest LeaveRequest) : IDomainEvent;

public sealed record LeaveRequestApprovedDomainEvent(LeaveRequest LeaveRequest) : IDomainEvent;

public sealed record LeaveRequestRejectedDomainEvent(LeaveRequest LeaveRequest) : IDomainEvent;

public sealed record LeaveRequestCancelledDomainEvent(LeaveRequest LeaveRequest, bool WasApproved) : IDomainEvent;
