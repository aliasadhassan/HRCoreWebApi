namespace HR.Employee.API.Domain.Attendance;

public enum AttendanceStatus : byte
{
    Present = 1,
    Absent = 2,
    HalfDay = 3,
    OnLeave = 4,
    Holiday = 5,
    WeeklyOff = 6,
    Incomplete = 7    // punch missing (in hai, out nahi) — correction request se theek hota hai
}

/// <summary>Din ki qisam — overtime rate isi se chunta hai.</summary>
public enum DayType : byte { Workday = 1, WeeklyOff = 2, Holiday = 3 }

public enum PunchDirection : byte { In = 1, Out = 2, Unknown = 3 }   // biometric aksar direction nahi bhejta

public enum PunchSource : byte { Web = 1, Mobile = 2, Biometric = 3, Manual = 4, Request = 5 }

/// <summary>Policy mein allowed clock-in tareeqay (bitmask).</summary>
[Flags]
public enum ClockInMethods : byte { Web = 1, Mobile = 2, Biometric = 4 }

public enum AttendanceRequestType : byte { Correction = 1, WorkFromHome = 2, OnDuty = 3, Overtime = 4 }

public enum AttendanceRequestStatus : byte { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }
