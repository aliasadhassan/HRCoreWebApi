namespace HR.Employee.API.Domain.Leaves;

public enum AccrualMethod : byte { Upfront = 1, Monthly = 2 }

public enum ApproverType : byte { LineManager = 1, DepartmentHead = 2, HR = 3 }

public enum LeaveRequestStatus : byte { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }

public enum ApprovalDecision : byte { Pending = 0, Approved = 1, Rejected = 2, Skipped = 3 }

public enum HalfDayPeriod : byte { FirstHalf = 1, SecondHalf = 2 }
