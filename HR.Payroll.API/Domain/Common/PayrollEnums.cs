namespace HR.Payroll.API.Domain.Common;

public enum PayFrequency : byte { Monthly = 1, SemiMonthly = 2, BiWeekly = 3, Weekly = 4 }

public enum ProrationMethod : byte { CalendarDays = 1, WorkingDays = 2, Fixed30 = 3 }

public enum ComponentType : byte { Earning = 1, Deduction = 2, EmployerContribution = 3, Informational = 4 }

public enum CalcType : byte { Fixed = 1, PercentOfComponent = 2, PercentOfGross = 3, Variable = 4, Remainder = 5 }

public enum SalaryBasis : byte { Annual = 1, Monthly = 2, Hourly = 3 }

public enum SalaryChangeReason : byte { Joining = 1, Increment = 2, Promotion = 3, Correction = 4, Other = 5 }

public enum TaxCalcMethod : byte { None = 0, Annualized = 1, PerPeriodFlat = 2 }

public enum ContributionBase : byte { BasicSalary = 1, Gross = 2, FixedAmount = 3 }

public enum InputSource : byte { Manual = 1, Import = 2, Attendance = 3 }

public enum LoanType : byte { Loan = 1, SalaryAdvance = 2 }

public enum LoanStatus : byte { Active = 1, Paused = 2, Closed = 3, Cancelled = 4 }

public enum PayPeriodStatus : byte { Open = 1, Locked = 2 }

public enum RunType : byte { Regular = 1, OffCycle = 2, FinalSettlement = 3 }

public enum RunStatus : byte { Draft = 1, Processing = 2, Calculated = 3, Approved = 4, Paid = 5, Cancelled = 6, Failed = 7 }

public enum PayslipStatus : byte { Calculated = 1, OnHold = 2, Paid = 3 }

public enum LineSource : byte { Template = 1, Override = 2, Input = 3, Proration = 4, Contribution = 5, Tax = 6, Loan = 7 }

public enum PaymentMethod : byte { BankTransfer = 1, Cash = 2, Cheque = 3 }

public enum PaymentStatus : byte { Pending = 1, Paid = 2, Failed = 3, Reversed = 4 }

public enum PaymentFileFormat : byte { GenericCsv = 1, WpsSif = 2 }

public static class PayFrequencyExtensions
{
    public static int PeriodsPerYear(this PayFrequency frequency) => frequency switch
    {
        PayFrequency.Monthly => 12,
        PayFrequency.SemiMonthly => 24,
        PayFrequency.BiWeekly => 26,
        PayFrequency.Weekly => 52,
        _ => throw new DomainException("Unknown pay frequency.")
    };
}
