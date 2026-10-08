namespace HR.Employee.API.Domain.Recruitment;

public enum JobStatus : byte { Draft = 1, Submitted = 2, Open = 3, OnHold = 4, Filled = 5, Cancelled = 6 }

public enum JobReason : byte { NewPosition = 1, Replacement = 2 }

public enum CandidateSource : byte { Website = 1, Referral = 2, JobBoard = 3, LinkedIn = 4, Agency = 5, Direct = 6, Other = 7 }

public enum ApplicationStage : byte { Applied = 1, Screening = 2, Interview = 3, Offer = 4, Hired = 5, Rejected = 6, Withdrawn = 7 }

public enum OfferStatus : byte { Pending = 1, Accepted = 2, Declined = 3 }

public enum ApplicationEventKind : byte { Applied = 1, Stage = 2, Note = 3, Offer = 4, OfferResponse = 5, Hired = 6 }

public enum InterviewMode : byte { InPerson = 1, Video = 2, Phone = 3 }

public enum InterviewStatus : byte { Scheduled = 1, Completed = 2, Cancelled = 3, NoShow = 4 }

public enum InterviewRecommendation : byte { StrongNo = 1, No = 2, Yes = 3, StrongYes = 4 }
