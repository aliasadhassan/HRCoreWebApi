namespace HR.Employee.API.Domain.Learning;

public enum CourseCategory : byte { Technical = 1, Leadership, Compliance, SoftSkills, Onboarding, Product, Safety, Other }
public enum CourseFormat : byte { Online = 1, Classroom, Workshop, Video, Reading, Mentoring }
public enum EnrollmentSource : byte { Assigned = 1, Self }
public enum EnrollmentStatus : byte { NotStarted = 1, InProgress, Completed, Cancelled }
public enum SkillCategory : byte { Technical = 1, Leadership, Communication, Domain, Tools, Language, Other }
/// <summary>Level kis ne diya: employee khud, manager/HR ka assessment, ya course mukammal karne par.</summary>
public enum SkillSource : byte { Self = 1, Assessed, Course }
public enum CareerGoalStatus : byte { Active = 1, Achieved }
