namespace HR.Employee.API.Domain.Helpdesk;

public enum TicketStatus : byte
{
    PendingApproval = 1, Open = 2, InProgress = 3, WaitingOnEmployee = 4, Resolved = 5, Closed = 6, Rejected = 7, Cancelled = 8
}

public enum TicketPriority : byte { Low = 1, Normal = 2, High = 3, Urgent = 4 }

public enum TicketActivityKind : byte
{
    Created = 1, Comment = 2, InternalNote = 3, Status = 4, Assigned = 5, Approved = 6, Rejected = 7, Rated = 8
}
