namespace HR.Employee.API.Domain.Performance;

public enum CycleStatus : byte { Draft = 1, Active = 2, Closed = 3 }

/// <summary>SelfReview → ManagerReview → Shared (employee dekh sakta hai) → Acknowledged.</summary>
public enum ReviewStatus : byte { SelfReview = 1, ManagerReview = 2, Shared = 3, Acknowledged = 4 }

public enum GoalStatus : byte { NotStarted = 1, OnTrack = 2, AtRisk = 3, OffTrack = 4, Completed = 5, Cancelled = 6 }
