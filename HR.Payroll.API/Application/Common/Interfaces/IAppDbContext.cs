namespace HR.Payroll.API.Application.Common.Interfaces;

using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Domain.Rewards;
using HR.Payroll.API.Domain.Expenses;
using HR.Payroll.API.Domain.Inputs;
using HR.Payroll.API.Domain.Payments;
using HR.Payroll.API.Domain.Runs;
using HR.Payroll.API.Domain.Salaries;
using HR.Payroll.API.Domain.Setup;
using HR.Payroll.API.Domain.Tax;
using Microsoft.EntityFrameworkCore;

public interface IAppDbContext
{
    DbSet<PayrollSettings> PayrollSettings { get; }
    DbSet<PayGroup> PayGroups { get; }
    DbSet<PayPeriod> PayPeriods { get; }
    DbSet<PayComponent> PayComponents { get; }

    DbSet<PayrollEmployee> PayrollEmployees { get; }

    DbSet<SalaryGrade> SalaryGrades { get; }
    DbSet<SalaryTemplate> SalaryTemplates { get; }
    DbSet<EmployeeSalary> EmployeeSalaries { get; }

    DbSet<TaxRegime> TaxRegimes { get; }
    DbSet<ContributionRule> ContributionRules { get; }
    DbSet<EmployeeTaxOpeningBalance> EmployeeTaxOpeningBalances { get; }

    DbSet<PayrollInput> PayrollInputs { get; }
    DbSet<UnpaidLeaveDay> UnpaidLeaveDays { get; }
    DbSet<EmployeeLoan> EmployeeLoans { get; }
    DbSet<LoanRequest> LoanRequests { get; }
    DbSet<LoanPolicy> LoanPolicies { get; }

    DbSet<ExpensePolicy> ExpensePolicies { get; }
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<ExpenseClaim> ExpenseClaims { get; }
    DbSet<TravelRequest> TravelRequests { get; }
    DbSet<BenefitPlan> BenefitPlans { get; }
    DbSet<BenefitEnrolment> BenefitEnrolments { get; }
    DbSet<SalaryRevision> SalaryRevisions { get; }
    DbSet<BonusAward> BonusAwards { get; }

    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<Payslip> Payslips { get; }
    DbSet<LoanRepayment> LoanRepayments { get; }

    DbSet<PaymentBatch> PaymentBatches { get; }
    DbSet<Payment> Payments { get; }

    DbSet<HR.Shared.Library.Persistence.AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Jab ek kaam mein do SaveChanges chahiyein (e.g. purani salary band → nayi salary),
    /// dono ek transaction mein. Retry strategy ke saath safe.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
