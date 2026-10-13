namespace HR.Employee.API.Domain.Safety;

public enum IncidentType : byte
{
    Injury = 1, NearMiss = 2, Illness = 3, PropertyDamage = 4, Hazard = 5, Environmental = 6, Vehicle = 7, Other = 8
}

public enum SafetySeverity : byte { Low = 1, Medium = 2, High = 3, Critical = 4 }

/// <summary>Reported → Investigating → ActionPending (kaam baqi) → Closed.</summary>
public enum IncidentStatus : byte { Reported = 1, Investigating = 2, ActionPending = 3, Closed = 4 }

/// <summary>Zakhmi ko kya ilaj mila. MedicalTreatment se upar "recordable" maana jata hai.</summary>
public enum IncidentTreatment : byte { None = 1, FirstAid = 2, MedicalTreatment = 3, Hospitalised = 4, Fatal = 5 }

public enum RootCauseCategory : byte
{
    UnsafeAct = 1, UnsafeCondition = 2, Equipment = 3, Training = 4, Procedure = 5, Environment = 6, Other = 7
}

public enum IncidentEntryKind : byte
{
    Created = 1, Note = 2, StatusChanged = 3, Updated = 4, InvestigatorChanged = 5, ActionAdded = 6, ActionCompleted = 7,
    AuthorityNotified = 8, Closed = 9, Reopened = 10
}

/// <summary>Open → InProgress → Done (assignee) → Verified (safety team). Cancelled = zaroorat nahi rahi.</summary>
public enum ActionStatus : byte { Open = 1, InProgress = 2, Done = 3, Verified = 4, Cancelled = 5 }

public enum InspectionStatus : byte { Scheduled = 1, Completed = 2, Cancelled = 3 }

public enum InspectionResult : byte { Pass = 1, Fail = 2, NotApplicable = 3 }
