namespace HR.Employee.API.Application.Common.Interfaces;

using HR.Employee.API.Domain.Ai;
using HR.Employee.API.Domain.Assets;
using HR.Employee.API.Domain.Performance;
using HR.Employee.API.Domain.Helpdesk;
using HR.Employee.API.Domain.Policies;
using HR.Employee.API.Domain.Recruitment;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Engagement;
using HR.Employee.API.Domain.Relations;
using HR.Employee.API.Domain.Safety;
using HR.Employee.API.Domain.ReportBuilder;
using HR.Employee.API.Domain.Learning;
using HR.Employee.API.Domain.Leaves;
using HR.Employee.API.Domain.Lifecycle;
using HR.Employee.API.Domain.Organization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Repository + UnitOfWork ki jagah: DbContext khud dono hai.
/// Handlers is interface se kaam karte hain (tenant filter + soft delete automatic).
/// </summary>
public interface IAppDbContext
{
    DbSet<Location> Locations { get; }
    DbSet<Department> Departments { get; }
    DbSet<Designation> Designations { get; }

    DbSet<Employee> Employees { get; }
    DbSet<EmployeeDocument> EmployeeDocuments { get; }
    DbSet<JobHistoryEntry> EmployeeJobHistory { get; }

    DbSet<Holiday> Holidays { get; }
    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<LeavePolicy> LeavePolicies { get; }
    DbSet<LeaveApprovalSettings> LeaveApprovalSettings { get; }
    DbSet<LeaveBalance> LeaveBalances { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }

    DbSet<Shift> Shifts { get; }
    DbSet<ShiftAssignment> ShiftAssignments { get; }
    DbSet<RosterEntry> RosterEntries { get; }
    DbSet<AttendancePolicy> AttendancePolicies { get; }
    DbSet<AttendanceDevice> AttendanceDevices { get; }
    DbSet<AttendanceDay> AttendanceDays { get; }
    DbSet<AttendanceRequest> AttendanceRequests { get; }

    DbSet<ChecklistTemplate> ChecklistTemplates { get; }
    DbSet<LifecycleCase> LifecycleCases { get; }

    DbSet<AssetCategory> AssetCategories { get; }
    DbSet<Asset> Assets { get; }
    DbSet<AssetAssignment> AssetAssignments { get; }
    DbSet<AssetEvent> AssetEvents { get; }
    DbSet<ReviewCycle> ReviewCycles { get; }
    DbSet<PerformanceReview> PerformanceReviews { get; }
    DbSet<Goal> Goals { get; }
    DbSet<GoalCheckIn> GoalCheckIns { get; }
    DbSet<JobOpening> JobOpenings { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<JobApplication> JobApplications { get; }
    DbSet<ApplicationEvent> ApplicationEvents { get; }
    DbSet<Interview> Interviews { get; }
    DbSet<HelpdeskCategory> HelpdeskCategories { get; }
    DbSet<HelpdeskTicket> HelpdeskTickets { get; }
    DbSet<HR.Shared.Library.Persistence.AuditLog> AuditLogs { get; }
    DbSet<TicketActivity> TicketActivities { get; }
    DbSet<Policy> Policies { get; }
    DbSet<PolicyVersion> PolicyVersions { get; }
    DbSet<PolicyAcknowledgement> PolicyAcknowledgements { get; }
    DbSet<ComplianceObligation> ComplianceObligations { get; }
    DbSet<ComplianceFiling> ComplianceFilings { get; }
    DbSet<Skill> Skills { get; }
    DbSet<Course> Courses { get; }
    DbSet<CourseEnrollment> CourseEnrollments { get; }
    DbSet<EmployeeSkill> EmployeeSkills { get; }
    DbSet<RoleSkill> RoleSkills { get; }
    DbSet<CareerGoal> CareerGoals { get; }
    DbSet<Survey> Surveys { get; }
    DbSet<SurveyQuestion> SurveyQuestions { get; }
    DbSet<SurveyParticipation> SurveyParticipations { get; }
    DbSet<SurveyResponse> SurveyResponses { get; }
    DbSet<SurveyAnswer> SurveyAnswers { get; }
    DbSet<Recognition> Recognitions { get; }
    DbSet<RecognitionCheer> RecognitionCheers { get; }
    DbSet<CaseStage> CaseStages { get; }
    DbSet<ErCase> ErCases { get; }
    DbSet<CaseMember> CaseMembers { get; }
    DbSet<CaseParty> CaseParties { get; }
    DbSet<CaseEntry> CaseEntries { get; }
    DbSet<CaseEvidence> CaseEvidence { get; }
    DbSet<CaseAccessLog> CaseAccessLogs { get; }
    DbSet<SafetyIncident> SafetyIncidents { get; }
    DbSet<IncidentEntry> IncidentEntries { get; }
    DbSet<SafetyAction> SafetyActions { get; }
    DbSet<SafetyChecklist> SafetyChecklists { get; }
    DbSet<ChecklistItem> ChecklistItems { get; }
    DbSet<SafetyInspection> SafetyInspections { get; }
    DbSet<InspectionLine> InspectionLines { get; }
    DbSet<SavedReport> SavedReports { get; }
    DbSet<LetterTemplate> LetterTemplates { get; }
    DbSet<LetterTemplateVersion> LetterTemplateVersions { get; }
    DbSet<IssuedLetter> IssuedLetters { get; }
    DbSet<AiSettings> AiSettings { get; }
    DbSet<AiConversation> AiConversations { get; }
    DbSet<AiMessage> AiMessages { get; }
    DbSet<AiInsight> AiInsights { get; }
    DbSet<AiScreening> AiScreenings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
