namespace HR.Shared.Library.Authorization;

/// <summary>
/// Permission codes — Identity DB ke catalogue (IdentitySeeder) ke saath 1:1.
/// Har service inhi constants se check kare, string literal nahi.
/// </summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";

    public const string EmployeesView = "employees.view";
    public const string EmployeesCreate = "employees.create";
    public const string EmployeesEdit = "employees.edit";
    public const string EmployeesDelete = "employees.delete";

    public const string LeavesViewOwn = "leaves.view.own";
    public const string LeavesApply = "leaves.apply";
    public const string LeavesViewAll = "leaves.view.all";
    public const string LeavesApprove = "leaves.approve";

    public const string PayrollViewOwn = "payroll.view.own";
    public const string PayrollViewAll = "payroll.view.all";
    public const string PayrollRun = "payroll.run";
    public const string PayrollApprove = "payroll.approve";

    /// <summary>Employee relations cases (complaints, grievances, disciplinary). HR Manager ko khud nahi milta — har shakhs ko soch kar diya jaye.</summary>
    public const string RelationsManage = "relations.manage";

    /// <summary>Health &amp; safety: incidents, corrective actions, inspections aur checklists.</summary>
    public const string SafetyManage = "safety.manage";

    /// <summary>Report builder: table reports save karna aur letter templates banana / publish karna.</summary>
    public const string ReportsBuild = "reports.build";

    /// <summary>Published template se letter / certificate jari karna (aur jari shuda letters dekhna, void karna).</summary>
    public const string LettersIssue = "letters.issue";

    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
}
