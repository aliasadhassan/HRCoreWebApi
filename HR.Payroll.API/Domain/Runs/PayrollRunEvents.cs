namespace HR.Payroll.API.Domain.Runs;

using HR.Payroll.API.Domain.Common;

public sealed record PayrollRunStartedDomainEvent(PayrollRun Run) : IDomainEvent;      // handler: queue mein message (outbox)

public sealed record PayrollRunCalculatedDomainEvent(PayrollRun Run) : IDomainEvent;   // handler: HR ko notification

public sealed record PayrollRunApprovedDomainEvent(PayrollRun Run) : IDomainEvent;     // handler: period lock

public sealed record PayrollRunPaidDomainEvent(PayrollRun Run) : IDomainEvent;         // handler: payslips Paid + employees notify
