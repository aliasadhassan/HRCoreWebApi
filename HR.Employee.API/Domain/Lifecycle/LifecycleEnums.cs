namespace HR.Employee.API.Domain.Lifecycle;

public enum LifecycleKind : byte { Onboarding = 1, Exit = 2 }

/// <summary>Task kis ka kaam hai (department / role). Assignee employee optional.</summary>
public enum TaskOwner : byte { Hr = 1, Manager = 2, It = 3, Finance = 4, Employee = 5, Admin = 6 }

public enum CaseStatus : byte { InProgress = 1, Completed = 2, Cancelled = 3 }

public enum LifecycleTaskStatus : byte { Open = 1, Done = 2, Skipped = 3 }

public enum ExitType : byte { Resignation = 1, Termination = 2, EndOfContract = 3, Retirement = 4, Other = 5 }
