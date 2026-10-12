namespace HR.Employee.API.Domain.Relations;

/// <summary>Har stage kis marhale ka hissa hai. Stage ka naam company badal sakti hai, phase fixed hai (logic isi par chalta hai).</summary>
public enum CasePhase : byte { Open = 1, Review = 2, Investigation = 3, Resolution = 4, Closed = 5 }

public enum CaseType : byte
{
    Complaint = 1, Grievance = 2, Harassment = 3, Discrimination = 4, Misconduct = 5, Disciplinary = 6, Whistleblowing = 7, Other = 8
}

public enum CaseSeverity : byte { Low = 1, Medium = 2, High = 3, Critical = 4 }

/// <summary>Standard = relations team sab dekhti hai. Restricted = sirf case ke members (owner, investigators, viewers).</summary>
public enum CaseConfidentiality : byte { Standard = 1, Restricted = 2 }

/// <summary>Case kahan se aaya.</summary>
public enum CaseSource : byte { Employee = 1, Hr = 2, Manager = 3, Hotline = 4, Other = 5 }

/// <summary>Owner = sab kuch (members, band karna). Investigator = notes, evidence, stage. Viewer = sirf parhna (restricted evidence nahi).</summary>
public enum CaseMemberRole : byte { Owner = 1, Investigator = 2, Viewer = 3 }

public enum CasePartyRole : byte { Complainant = 1, Respondent = 2, Witness = 3, Other = 4 }

public enum CaseOutcome : byte { Substantiated = 1, PartlySubstantiated = 2, NotSubstantiated = 3, ResolvedInformally = 4, Withdrawn = 5 }

public enum CaseAction : byte
{
    None = 1, Coaching = 2, VerbalWarning = 3, WrittenWarning = 4, FinalWarning = 5, Suspension = 6, Termination = 7, Other = 8
}

public enum CaseEntryKind : byte
{
    Created = 1, Note = 2, Message = 3, ReporterMessage = 4, StageChanged = 5, MemberAdded = 6, MemberRemoved = 7, OwnerChanged = 8,
    PartyAdded = 9, PartyRemoved = 10, EvidenceAdded = 11, EvidenceRemoved = 12, DeadlineChanged = 13, Escalated = 14,
    Closed = 15, Reopened = 16, BreakGlass = 17, Updated = 18
}

public enum CaseAccessAction : byte { Viewed = 1, BreakGlass = 2, ViewedAccessLog = 3 }
