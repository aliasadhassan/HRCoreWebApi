namespace HR.Employee.API.Domain.Employees;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Organization;

/// <summary>
/// Aggregate root. Salary yahan NAHI (Payroll service).
/// Har job change (department, designation, manager, status) khud JobHistory row banata hai.
/// </summary>
public sealed class Employee : AuditableEntity
{
    private const int MaxEmergencyContacts = 5;

    private readonly List<EmergencyContact> _emergencyContacts = new();
    private readonly List<JobHistoryEntry> _jobHistory = new();
    private readonly List<EmployeeDocument> _documents = new();

    public string EmployeeCode { get; private set; } = default!;
    public Guid? UserId { get; private set; }                    // Identity user (logical link)

    // Personal
    public string FirstName { get; private set; } = default!;
    public string? MiddleName { get; private set; }
    public string LastName { get; private set; } = default!;
    public Gender? Gender { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public MaritalStatus? MaritalStatus { get; private set; }
    public string? NationalityCode { get; private set; }
    public string? NationalIdNumber { get; private set; }        // encrypted value (application layer)
    public string? PhotoStorageKey { get; private set; }

    // Contact
    public string WorkEmail { get; private set; } = default!;
    public string? PersonalEmail { get; private set; }
    public string? WorkPhone { get; private set; }
    public string? PersonalPhone { get; private set; }
    public Address? Address { get; private set; }

    // Job (current snapshot)
    public Guid LocationId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid DesignationId { get; private set; }
    public Guid? ManagerId { get; private set; }
    public EmploymentType EmploymentType { get; private set; }
    public EmploymentStatus EmploymentStatus { get; private set; }
    public DateOnly JoiningDate { get; private set; }
    public DateOnly? ProbationEndDate { get; private set; }
    public DateOnly? ConfirmationDate { get; private set; }
    public short? NoticePeriodDays { get; private set; }
    public DateOnly? ExitDate { get; private set; }
    public string? ExitReason { get; private set; }

    // Read-only navigations (sirf queries/projections ke liye)
    public Location Location { get; private set; } = default!;
    public Department Department { get; private set; } = default!;
    public Designation Designation { get; private set; } = default!;
    public Employee? Manager { get; private set; }

    public IReadOnlyCollection<EmergencyContact> EmergencyContacts => _emergencyContacts.AsReadOnly();
    public IReadOnlyCollection<JobHistoryEntry> JobHistory => _jobHistory.AsReadOnly();
    public IReadOnlyCollection<EmployeeDocument> Documents => _documents.AsReadOnly();

    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";

    public bool HasExited => EmploymentStatus == EmploymentStatus.Exited;

    private Employee() { }

    // ─────────────────────────────── Create ───────────────────────────────
    public static Employee Create(
        Guid tenantId, string employeeCode,
        string firstName, string? middleName, string lastName, string workEmail,
        Guid locationId, Guid departmentId, Guid designationId, Guid? managerId,
        EmploymentType employmentType, DateOnly joiningDate, DateOnly? probationEndDate)
    {
        if (employmentType is not (EmploymentType.FullTime or EmploymentType.PartTime or EmploymentType.Contract or EmploymentType.Intern))
            throw new DomainException("Select exactly one employment type.");

        if (probationEndDate is not null && probationEndDate <= joiningDate)
            throw new DomainException("Probation end date must be after the joining date.");

        var employee = new Employee
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeCode = Guard.Required(employeeCode, "Employee code", 20).ToUpperInvariant(),
            WorkEmail = Guard.Email(workEmail, "Work email"),
            LocationId = Guard.NotEmpty(locationId, "Location"),
            DepartmentId = Guard.NotEmpty(departmentId, "Department"),
            DesignationId = Guard.NotEmpty(designationId, "Designation"),
            ManagerId = managerId,
            EmploymentType = employmentType,
            EmploymentStatus = probationEndDate is null ? EmploymentStatus.Active : EmploymentStatus.Probation,
            JoiningDate = joiningDate,
            ProbationEndDate = probationEndDate
        };

        employee.SetName(firstName, middleName, lastName);
        employee.RecordHistory(JobChangeType.Joined, joiningDate, null);
        employee.Raise(new EmployeeCreatedDomainEvent(employee));
        return employee;
    }

    // ─────────────────────────────── Profile ──────────────────────────────
    public void UpdatePersonalInfo(
        string firstName, string? middleName, string lastName,
        Gender? gender, DateOnly? dateOfBirth, MaritalStatus? maritalStatus, string? nationalityCode)
    {
        EnsureNotExited();

        if (dateOfBirth is not null && dateOfBirth >= JoiningDate)
            throw new DomainException("Date of birth must be before the joining date.");

        SetName(firstName, middleName, lastName);
        Gender = gender;
        DateOfBirth = dateOfBirth;
        MaritalStatus = maritalStatus;
        NationalityCode = string.IsNullOrWhiteSpace(nationalityCode) ? null : Guard.CountryCode(nationalityCode, "Nationality");
    }

    public void UpdateContactInfo(string? personalEmail, string? workPhone, string? personalPhone, Address? address)
    {
        EnsureNotExited();

        PersonalEmail = string.IsNullOrWhiteSpace(personalEmail) ? null : Guard.Email(personalEmail, "Personal email");
        WorkPhone = Guard.Optional(workPhone, "Work phone", 50);
        PersonalPhone = Guard.Optional(personalPhone, "Personal phone", 50);
        Address = address;
    }

    /// <summary>Value pehle se encrypted aani chahiye (application layer).</summary>
    public void SetNationalId(string? encryptedValue) => NationalIdNumber = Guard.Optional(encryptedValue, "National ID", 256);

    public void SetPhoto(string? storageKey) => PhotoStorageKey = Guard.Optional(storageKey, "Photo", 500);

    public void SetNoticePeriod(short? days)
    {
        if (days is < 0 or > 365)
            throw new DomainException("Notice period must be between 0 and 365 days.");
        NoticePeriodDays = days;
    }

    /// <summary>Identity mein user banne ke baad (UserCreatedEvent) link hota hai.</summary>
    public void LinkUser(Guid userId)
    {
        Guard.NotEmpty(userId, "User");
        if (UserId is not null && UserId != userId)
            throw new DomainException("This employee is already linked to another user account.");
        UserId = userId;
    }

    // ─────────────────────────────── Job ──────────────────────────────────
    /// <summary>Department / location / designation / manager — jo badla uski ek history row.</summary>
    public void ChangeJob(Guid departmentId, Guid locationId, Guid designationId, Guid? managerId, DateOnly effectiveDate, string? remarks)
    {
        EnsureNotExited();

        if (managerId == Id)
            throw new DomainException("An employee cannot be their own manager.");
        if (effectiveDate < JoiningDate)
            throw new DomainException("Effective date cannot be before the joining date.");

        var designationChanged = designationId != DesignationId;
        var transferred = departmentId != DepartmentId || locationId != LocationId;
        var managerChanged = managerId != ManagerId;

        if (!designationChanged && !transferred && !managerChanged)
            return;

        DepartmentId = Guard.NotEmpty(departmentId, "Department");
        LocationId = Guard.NotEmpty(locationId, "Location");
        DesignationId = Guard.NotEmpty(designationId, "Designation");
        ManagerId = managerId;

        var changeType = designationChanged ? JobChangeType.DesignationChange
                       : transferred ? JobChangeType.Transfer
                       : JobChangeType.ManagerChange;

        RecordHistory(changeType, effectiveDate, remarks);
    }

    public void ConfirmProbation(DateOnly confirmationDate)
    {
        EnsureNotExited();

        if (EmploymentStatus != EmploymentStatus.Probation)
            throw new DomainException("Only employees on probation can be confirmed.");
        if (confirmationDate < JoiningDate)
            throw new DomainException("Confirmation date cannot be before the joining date.");

        EmploymentStatus = EmploymentStatus.Active;
        ConfirmationDate = confirmationDate;
        RecordHistory(JobChangeType.StatusChange, confirmationDate, "Probation confirmed");
    }

    public void Exit(DateOnly exitDate, string reason)
    {
        EnsureNotExited();

        if (exitDate < JoiningDate)
            throw new DomainException("Exit date cannot be before the joining date.");

        ExitDate = exitDate;
        ExitReason = Guard.Required(reason, "Exit reason", 500);
        EmploymentStatus = EmploymentStatus.Exited;

        RecordHistory(JobChangeType.Exit, exitDate, ExitReason);
        Raise(new EmployeeExitedDomainEvent(this));
    }

    // ─────────────────────────── Emergency contacts ───────────────────────
    public EmergencyContact AddEmergencyContact(string name, string relationship, string phone, string? alternatePhone, bool isPrimary)
    {
        if (_emergencyContacts.Count >= MaxEmergencyContacts)
            throw new DomainException($"An employee can have at most {MaxEmergencyContacts} emergency contacts.");

        var contact = new EmergencyContact(name, relationship, phone, alternatePhone);
        _emergencyContacts.Add(contact);

        if (isPrimary || _emergencyContacts.Count == 1)
            MakePrimary(contact);

        return contact;
    }

    public void UpdateEmergencyContact(Guid contactId, string name, string relationship, string phone, string? alternatePhone, bool isPrimary)
    {
        var contact = FindContact(contactId);
        contact.Update(name, relationship, phone, alternatePhone);
        if (isPrimary)
            MakePrimary(contact);
    }

    public void RemoveEmergencyContact(Guid contactId)
    {
        var contact = FindContact(contactId);
        _emergencyContacts.Remove(contact);

        if (contact.IsPrimary && _emergencyContacts.Count > 0)
            MakePrimary(_emergencyContacts[0]);
    }

    // ─────────────────────────────── Documents ────────────────────────────
    public EmployeeDocument AddDocument(
        DocumentType type, string title, string fileName, string contentType, long sizeBytes,
        string storageKey, DateOnly? issueDate, DateOnly? expiryDate)
    {
        var document = EmployeeDocument.Create(TenantId, type, title, fileName, contentType, sizeBytes, storageKey, issueDate, expiryDate);
        _documents.Add(document);
        return document;
    }

    // ─────────────────────────────── Helpers ──────────────────────────────
    private void SetName(string firstName, string? middleName, string lastName)
    {
        FirstName = Guard.Required(firstName, "First name", 100);
        MiddleName = Guard.Optional(middleName, "Middle name", 100);
        LastName = Guard.Required(lastName, "Last name", 100);
    }

    private void MakePrimary(EmergencyContact primary)
    {
        foreach (var contact in _emergencyContacts)
            contact.SetPrimary(contact == primary);
    }

    private EmergencyContact FindContact(Guid contactId)
        => _emergencyContacts.FirstOrDefault(c => c.Id == contactId)
           ?? throw new DomainException("Emergency contact not found.");

    private void EnsureNotExited()
    {
        if (HasExited)
            throw new DomainException("This employee has exited; the record is read-only.");
    }

    private void RecordHistory(JobChangeType type, DateOnly effectiveDate, string? remarks)
        => _jobHistory.Add(JobHistoryEntry.Snapshot(this, type, effectiveDate, remarks));
}
