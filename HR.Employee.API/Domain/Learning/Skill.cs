namespace HR.Employee.API.Domain.Learning;

using HR.Employee.API.Domain.Common;

/// <summary>Company ki skill library (C#, Negotiation, Excel...). Levels 1-5 har jagah same scale.</summary>
public sealed class Skill : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public SkillCategory Category { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Skill() { }

    public static Skill Create(Guid tenantId, string name, SkillCategory category, string? description)
    {
        var skill = new Skill { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        skill.Update(name, category, description, true);
        return skill;
    }

    public void Update(string name, SkillCategory category, string? description, bool isActive)
    {
        if (!Enum.IsDefined(category))
            throw new DomainException("Select a valid category.");
        Name = Guard.Required(name, "Name", 100);
        Category = category;
        Description = Guard.Optional(description, "Description", 500);
        IsActive = isActive;
    }

    internal static byte EnsureLevel(byte level)
        => level is >= 1 and <= Course.MaxLevel ? level : throw new DomainException("Level must be between 1 and 5.");
}

/// <summary>Employee ka ek skill par level. Ek row per (employee, skill); aakhri rating jeet-ti hai, source yaad rehta hai.</summary>
public sealed class EmployeeSkill : TenantChildEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid SkillId { get; private set; }
    public byte Level { get; private set; }
    public SkillSource Source { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private EmployeeSkill() { }

    public static EmployeeSkill Create(Guid employeeId, Guid skillId, byte level, SkillSource source, Guid? byUserId, DateTime now)
    {
        var row = new EmployeeSkill { EmployeeId = Guard.NotEmpty(employeeId, "Employee"), SkillId = Guard.NotEmpty(skillId, "Skill") };
        row.Rate(level, source, byUserId, now);
        return row;
    }

    public void Rate(byte level, SkillSource source, Guid? byUserId, DateTime now)
    {
        if (!Enum.IsDefined(source))
            throw new DomainException("Select a valid source.");
        Level = Skill.EnsureLevel(level);
        Source = source;
        ByUserId = byUserId;
        UpdatedAt = now;
    }

    /// <summary>Course mukammal: level sirf barhta hai, kabhi kam nahi hota. True = badla.</summary>
    public bool RaiseTo(byte level, Guid? byUserId, DateTime now)
    {
        if (Skill.EnsureLevel(level) <= Level)
            return false;
        Rate(level, SkillSource.Course, byUserId, now);
        return true;
    }
}

/// <summary>Career path: kisi designation ke liye kaun si skill kis level par chahiye.</summary>
public sealed class RoleSkill : TenantChildEntity
{
    public Guid DesignationId { get; private set; }
    public Guid SkillId { get; private set; }
    public byte RequiredLevel { get; private set; }

    private RoleSkill() { }

    public static RoleSkill Create(Guid designationId, Guid skillId, byte requiredLevel) => new()
    {
        DesignationId = Guard.NotEmpty(designationId, "Role"),
        SkillId = Guard.NotEmpty(skillId, "Skill"),
        RequiredLevel = Skill.EnsureLevel(requiredLevel)
    };

    public void SetLevel(byte requiredLevel) => RequiredLevel = Skill.EnsureLevel(requiredLevel);
}

/// <summary>Employee ka agla role (ek waqt mein ek). Readiness = target role ki skills jahan level poora hai.</summary>
public sealed class CareerGoal : TenantChildEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid TargetDesignationId { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public string? Notes { get; private set; }
    public CareerGoalStatus Status { get; private set; }
    public DateOnly? AchievedOn { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CareerGoal() { }

    public static CareerGoal Create(Guid employeeId) => new() { EmployeeId = Guard.NotEmpty(employeeId, "Employee") };

    /// <summary>Naya ya badla hua target; achieved goal par dobara set karna naya safar shuru karta hai.</summary>
    public void Set(Guid targetDesignationId, Guid? currentDesignationId, DateOnly? targetDate, string? notes, Guid? byUserId, DateOnly today, DateTime now)
    {
        Guard.NotEmpty(targetDesignationId, "Target role");
        if (targetDesignationId == currentDesignationId)
            throw new DomainException("Pick a role other than the current one.");
        if (targetDate is { } d && d < today)
            throw new DomainException("Target date cannot be in the past.");

        TargetDesignationId = targetDesignationId;
        TargetDate = targetDate;
        Notes = Guard.Optional(notes, "Notes", 1000);
        Status = CareerGoalStatus.Active;
        AchievedOn = null;
        ByUserId = byUserId;
        UpdatedAt = now;
    }

    public void MarkAchieved(DateOnly today, Guid? byUserId, DateTime now)
    {
        if (Status != CareerGoalStatus.Active)
            throw new DomainException("This goal is already achieved.");
        Status = CareerGoalStatus.Achieved;
        AchievedOn = today;
        ByUserId = byUserId;
        UpdatedAt = now;
    }
}
