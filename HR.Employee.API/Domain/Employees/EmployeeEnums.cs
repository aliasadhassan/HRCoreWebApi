namespace HR.Employee.API.Domain.Employees;

public enum Gender : byte { Male = 1, Female = 2, Other = 3 }

public enum MaritalStatus : byte { Single = 1, Married = 2, Divorced = 3, Widowed = 4 }

/// <summary>Flags: leave policy rules "FullTime | Contract" jaisa combination store kar sakti hain.</summary>
[Flags]
public enum EmploymentType : byte { FullTime = 1, PartTime = 2, Contract = 4, Intern = 8 }

public enum EmploymentStatus : byte { Active = 1, Probation = 2, OnNotice = 3, Suspended = 4, Exited = 5 }

public enum JobChangeType : byte { Joined = 1, DesignationChange = 2, Transfer = 3, ManagerChange = 4, StatusChange = 5, Exit = 6 }

public enum DocumentType : byte { Contract = 1, NationalId = 2, Passport = 3, Visa = 4, Certificate = 5, Resume = 6, Other = 99 }
