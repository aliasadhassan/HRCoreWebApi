/* =====================================================================
   HR_Employee_Db  —  Target Schema (v1)
   Owns: Organization (Locations, Departments, Designations),
         Employees (profile, contacts, documents, job history),
         Leaves (types, location-wise policies, balances, requests,
                 configurable multi-level approvals, holidays)
   NOT here: Salary (Payroll_Db), Attendance (future HR.Attendance.API)
   Conventions (same as Identity):
     - PK: UNIQUEIDENTIFIER (EF sequential Guid)
     - Every tenant-owned row has TenantId (no cross-DB FK)
     - Audit: CreatedAt/By, UpdatedAt/By | Soft delete: IsDeleted
     - Concurrency: RowVersion
     - MassTransit tables (InboxState, OutboxMessage, OutboxState) unchanged
   ===================================================================== */

USE HR_Employee_Db;
GO

/* =====================================================================
   ORGANIZATION
   ===================================================================== */

/* 1. Locations — office/branch. Country, timezone aur work week yahin se.
      Leave days ginna, holidays aur (aage) payroll tax rules isi pe depend. */
CREATE TABLE dbo.Locations (
    Id              UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(150)    NOT NULL,          -- "Lahore HQ", "Dubai Office"
    Code            NVARCHAR(20)     NOT NULL,          -- "LHR-HQ"
    CountryCode     CHAR(2)          NOT NULL,          -- ISO 3166-1: PK, AE, GB
    City            NVARCHAR(100)    NULL,
    AddressLine     NVARCHAR(300)    NULL,
    TimeZone        NVARCHAR(64)     NOT NULL,          -- IANA: "Asia/Karachi"
    WorkWeekDays    TINYINT          NOT NULL CONSTRAINT DF_Loc_WorkWeek DEFAULT 31, -- bitmask Mon=1..Sun=64
    IsHeadOffice    BIT              NOT NULL CONSTRAINT DF_Loc_IsHQ DEFAULT 0,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Loc_IsActive DEFAULT 1,
    CreatedAt       DATETIME2(3)     NOT NULL CONSTRAINT DF_Loc_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy       UNIQUEIDENTIFIER NULL,
    UpdatedAt       DATETIME2(3)     NULL,
    UpdatedBy       UNIQUEIDENTIFIER NULL,
    IsDeleted       BIT              NOT NULL CONSTRAINT DF_Loc_IsDeleted DEFAULT 0,
    RowVersion      ROWVERSION       NOT NULL,
    CONSTRAINT PK_Locations PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX UX_Locations_Tenant_Code ON dbo.Locations (TenantId, Code) WHERE IsDeleted = 0;
GO

/* 2. Departments — hierarchy (Parent) + head. HeadEmployeeId ka FK Employees banne ke baad. */
CREATE TABLE dbo.Departments (
    Id                  UNIQUEIDENTIFIER NOT NULL,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(150)    NOT NULL,
    Code                NVARCHAR(20)     NOT NULL,
    Description         NVARCHAR(500)    NULL,
    ParentDepartmentId  UNIQUEIDENTIFIER NULL,
    HeadEmployeeId      UNIQUEIDENTIFIER NULL,
    IsActive            BIT              NOT NULL CONSTRAINT DF_Dept_IsActive DEFAULT 1,
    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_Dept_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NULL,
    UpdatedAt           DATETIME2(3)     NULL,
    UpdatedBy           UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL CONSTRAINT DF_Dept_IsDeleted DEFAULT 0,
    RowVersion          ROWVERSION       NOT NULL,
    CONSTRAINT PK_Departments PRIMARY KEY (Id),
    CONSTRAINT FK_Departments_Parent FOREIGN KEY (ParentDepartmentId) REFERENCES dbo.Departments (Id)
);
CREATE UNIQUE INDEX UX_Departments_Tenant_Code ON dbo.Departments (TenantId, Code) WHERE IsDeleted = 0;
CREATE INDEX IX_Departments_Parent ON dbo.Departments (ParentDepartmentId);
GO

/* 3. Designations — job titles + seniority level (org chart / payroll grades). */
CREATE TABLE dbo.Designations (
    Id              UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Title           NVARCHAR(150)    NOT NULL,          -- "Senior Developer"
    Level           TINYINT          NULL,              -- 1 = entry ... 10 = C-level
    Description     NVARCHAR(500)    NULL,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Desg_IsActive DEFAULT 1,
    CreatedAt       DATETIME2(3)     NOT NULL CONSTRAINT DF_Desg_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy       UNIQUEIDENTIFIER NULL,
    UpdatedAt       DATETIME2(3)     NULL,
    UpdatedBy       UNIQUEIDENTIFIER NULL,
    IsDeleted       BIT              NOT NULL CONSTRAINT DF_Desg_IsDeleted DEFAULT 0,
    RowVersion      ROWVERSION       NOT NULL,
    CONSTRAINT PK_Designations PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX UX_Designations_Tenant_Title ON dbo.Designations (TenantId, Title) WHERE IsDeleted = 0;
GO

/* =====================================================================
   EMPLOYEES
   ===================================================================== */

/* 4. Employees — HR profile. Salary yahan NAHI (Payroll_Db).
      NationalIdNumber: app layer pe encrypt karke save (ciphertext ke liye 256). */
CREATE TABLE dbo.Employees (
    Id                  UNIQUEIDENTIFIER NOT NULL,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    EmployeeCode        NVARCHAR(20)     NOT NULL,      -- "EMP-0001" (tenant-wise unique)
    UserId              UNIQUEIDENTIFIER NULL,          -- Identity user (logical link, no FK)

    -- Personal
    FirstName           NVARCHAR(100)    NOT NULL,
    MiddleName          NVARCHAR(100)    NULL,
    LastName            NVARCHAR(100)    NOT NULL,
    Gender              TINYINT          NULL,          -- 1 Male, 2 Female, 3 Other (leave eligibility: maternity/paternity)
    DateOfBirth         DATE             NULL,
    MaritalStatus       TINYINT          NULL,
    NationalityCode     CHAR(2)          NULL,
    NationalIdNumber    NVARCHAR(256)    NULL,          -- ENCRYPTED at app layer
    PhotoStorageKey     NVARCHAR(500)    NULL,

    -- Contact
    WorkEmail           NVARCHAR(256)    NOT NULL,
    PersonalEmail       NVARCHAR(256)    NULL,
    WorkPhone           NVARCHAR(50)     NULL,
    PersonalPhone       NVARCHAR(50)     NULL,
    AddressLine1        NVARCHAR(300)    NULL,
    AddressLine2        NVARCHAR(300)    NULL,
    City                NVARCHAR(100)    NULL,
    State               NVARCHAR(100)    NULL,
    PostalCode          NVARCHAR(20)     NULL,
    CountryCode         CHAR(2)          NULL,

    -- Job (current snapshot; history EmployeeJobHistory mein)
    LocationId          UNIQUEIDENTIFIER NOT NULL,
    DepartmentId        UNIQUEIDENTIFIER NOT NULL,
    DesignationId       UNIQUEIDENTIFIER NOT NULL,
    ManagerId           UNIQUEIDENTIFIER NULL,
    EmploymentType      TINYINT          NOT NULL,      -- 1 FullTime, 2 PartTime, 4 Contract, 8 Intern (bitmask values)
    EmploymentStatus    TINYINT          NOT NULL CONSTRAINT DF_Emp_Status DEFAULT 1, -- 1 Active, 2 Probation, 3 OnNotice, 4 Suspended, 5 Exited
    JoiningDate         DATE             NOT NULL,
    ProbationEndDate    DATE             NULL,
    ConfirmationDate    DATE             NULL,
    NoticePeriodDays    SMALLINT         NULL,
    ExitDate            DATE             NULL,
    ExitReason          NVARCHAR(500)    NULL,

    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_Emp_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NULL,
    UpdatedAt           DATETIME2(3)     NULL,
    UpdatedBy           UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL CONSTRAINT DF_Emp_IsDeleted DEFAULT 0,
    RowVersion          ROWVERSION       NOT NULL,
    CONSTRAINT PK_Employees PRIMARY KEY (Id),
    CONSTRAINT FK_Employees_Location    FOREIGN KEY (LocationId)    REFERENCES dbo.Locations (Id),
    CONSTRAINT FK_Employees_Department  FOREIGN KEY (DepartmentId)  REFERENCES dbo.Departments (Id),
    CONSTRAINT FK_Employees_Designation FOREIGN KEY (DesignationId) REFERENCES dbo.Designations (Id),
    CONSTRAINT FK_Employees_Manager     FOREIGN KEY (ManagerId)     REFERENCES dbo.Employees (Id),
    CONSTRAINT CK_Employees_ExitAfterJoin CHECK (ExitDate IS NULL OR ExitDate >= JoiningDate)
);
CREATE UNIQUE INDEX UX_Employees_Tenant_Code      ON dbo.Employees (TenantId, EmployeeCode) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Employees_Tenant_WorkEmail ON dbo.Employees (TenantId, WorkEmail)    WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Employees_Tenant_UserId    ON dbo.Employees (TenantId, UserId)       WHERE UserId IS NOT NULL AND IsDeleted = 0;
CREATE INDEX IX_Employees_Tenant_Status ON dbo.Employees (TenantId, EmploymentStatus) INCLUDE (DepartmentId, LocationId);
CREATE INDEX IX_Employees_Department    ON dbo.Employees (DepartmentId);
CREATE INDEX IX_Employees_Manager       ON dbo.Employees (ManagerId);
CREATE INDEX IX_Employees_Location      ON dbo.Employees (LocationId);
GO

-- Circular FK: Department head bhi employee hai
ALTER TABLE dbo.Departments
    ADD CONSTRAINT FK_Departments_Head FOREIGN KEY (HeadEmployeeId) REFERENCES dbo.Employees (Id);
GO

/* 5. EmployeeEmergencyContacts */
CREATE TABLE dbo.EmployeeEmergencyContacts (
    Id              UNIQUEIDENTIFIER NOT NULL,
    EmployeeId      UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    Relationship    NVARCHAR(50)     NOT NULL,          -- Spouse, Father, Sibling...
    Phone           NVARCHAR(50)     NOT NULL,
    AlternatePhone  NVARCHAR(50)     NULL,
    IsPrimary       BIT              NOT NULL CONSTRAINT DF_EEC_IsPrimary DEFAULT 0,
    CONSTRAINT PK_EmployeeEmergencyContacts PRIMARY KEY (Id),
    CONSTRAINT FK_EEC_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id) ON DELETE CASCADE
);
CREATE INDEX IX_EEC_Employee ON dbo.EmployeeEmergencyContacts (EmployeeId);
GO

/* 6. EmployeeDocuments — file Blob Storage mein, yahan sirf metadata + private key.
      ExpiryDate: passport/visa/contract expiry alerts. */
CREATE TABLE dbo.EmployeeDocuments (
    Id              UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    EmployeeId      UNIQUEIDENTIFIER NOT NULL,
    DocumentType    TINYINT          NOT NULL,          -- 1 Contract, 2 NationalId, 3 Passport, 4 Visa, 5 Certificate, 6 Resume, 99 Other
    Title           NVARCHAR(200)    NOT NULL,
    FileName        NVARCHAR(260)    NOT NULL,
    ContentType     NVARCHAR(100)    NOT NULL,
    SizeBytes       BIGINT           NOT NULL,
    StorageKey      NVARCHAR(500)    NOT NULL,          -- blob path (public URL kabhi nahi; SAS on demand)
    IssueDate       DATE             NULL,
    ExpiryDate      DATE             NULL,
    UploadedAt      DATETIME2(3)     NOT NULL CONSTRAINT DF_EDoc_UploadedAt DEFAULT SYSUTCDATETIME(),
    UploadedBy      UNIQUEIDENTIFIER NULL,
    IsDeleted       BIT              NOT NULL CONSTRAINT DF_EDoc_IsDeleted DEFAULT 0,
    CONSTRAINT PK_EmployeeDocuments PRIMARY KEY (Id),
    CONSTRAINT FK_EDoc_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id)
);
CREATE INDEX IX_EDoc_Employee ON dbo.EmployeeDocuments (EmployeeId) WHERE IsDeleted = 0;
CREATE INDEX IX_EDoc_Expiry   ON dbo.EmployeeDocuments (TenantId, ExpiryDate) WHERE ExpiryDate IS NOT NULL AND IsDeleted = 0;
GO

/* 7. EmployeeJobHistory — append-only: joining, promotion, transfer, manager change, exit */
CREATE TABLE dbo.EmployeeJobHistory (
    Id                  UNIQUEIDENTIFIER NOT NULL,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    EmployeeId          UNIQUEIDENTIFIER NOT NULL,
    EffectiveDate       DATE             NOT NULL,
    ChangeType          TINYINT          NOT NULL,      -- 1 Joined, 2 Promotion, 3 Transfer, 4 ManagerChange, 5 StatusChange, 6 Exit
    LocationId          UNIQUEIDENTIFIER NOT NULL,
    DepartmentId        UNIQUEIDENTIFIER NOT NULL,
    DesignationId       UNIQUEIDENTIFIER NOT NULL,
    ManagerId           UNIQUEIDENTIFIER NULL,
    EmploymentStatus    TINYINT          NOT NULL,
    Remarks             NVARCHAR(500)    NULL,
    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_EJH_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_EmployeeJobHistory PRIMARY KEY (Id),
    CONSTRAINT FK_EJH_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id)
);
CREATE INDEX IX_EJH_Employee_Date ON dbo.EmployeeJobHistory (EmployeeId, EffectiveDate DESC);
GO

/* =====================================================================
   LEAVES
   ===================================================================== */

/* 8. Holidays — LocationId NULL = tenant ki saari locations pe lagu */
CREATE TABLE dbo.Holidays (
    Id              UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    LocationId      UNIQUEIDENTIFIER NULL,
    HolidayDate     DATE             NOT NULL,
    Name            NVARCHAR(150)    NOT NULL,
    IsOptional      BIT              NOT NULL CONSTRAINT DF_Hol_IsOptional DEFAULT 0,
    CONSTRAINT PK_Holidays PRIMARY KEY (Id),
    CONSTRAINT FK_Holidays_Location FOREIGN KEY (LocationId) REFERENCES dbo.Locations (Id)
);
CREATE UNIQUE INDEX UX_Holidays_Tenant_Loc_Date ON dbo.Holidays (TenantId, LocationId, HolidayDate);
GO

/* 9. LeaveTypes — kya leave hai (Annual, Sick, Casual, Maternity...). Quota yahan NAHI, policy mein. */
CREATE TABLE dbo.LeaveTypes (
    Id                   UNIQUEIDENTIFIER NOT NULL,
    TenantId             UNIQUEIDENTIFIER NOT NULL,
    Name                 NVARCHAR(100)    NOT NULL,
    Code                 NVARCHAR(20)     NOT NULL,     -- "AL", "SL", "CL"
    Color                CHAR(7)          NULL,         -- "#4F6F52" (calendar)
    IsPaid               BIT              NOT NULL CONSTRAINT DF_LT_IsPaid DEFAULT 1,  -- unpaid => payroll deduction
    RequiresAttachment   BIT              NOT NULL CONSTRAINT DF_LT_ReqAtt DEFAULT 0,  -- e.g. sick > 2 days
    AllowHalfDay         BIT              NOT NULL CONSTRAINT DF_LT_Half DEFAULT 1,
    AllowNegativeBalance BIT              NOT NULL CONSTRAINT DF_LT_Neg DEFAULT 0,
    SortOrder            SMALLINT         NOT NULL CONSTRAINT DF_LT_Sort DEFAULT 0,
    IsActive             BIT              NOT NULL CONSTRAINT DF_LT_IsActive DEFAULT 1,
    CreatedAt            DATETIME2(3)     NOT NULL CONSTRAINT DF_LT_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy            UNIQUEIDENTIFIER NULL,
    UpdatedAt            DATETIME2(3)     NULL,
    UpdatedBy            UNIQUEIDENTIFIER NULL,
    IsDeleted            BIT              NOT NULL CONSTRAINT DF_LT_IsDeleted DEFAULT 0,
    RowVersion           ROWVERSION       NOT NULL,
    CONSTRAINT PK_LeaveTypes PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX UX_LeaveTypes_Tenant_Code ON dbo.LeaveTypes (TenantId, Code) WHERE IsDeleted = 0;
GO

/* 10. LeavePolicies — location-wise. LocationId NULL = tenant default (jis location ki apni policy na ho). */
CREATE TABLE dbo.LeavePolicies (
    Id              UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(150)    NOT NULL,          -- "Pakistan Policy 2026"
    LocationId      UNIQUEIDENTIFIER NULL,
    EffectiveFrom   DATE             NOT NULL,
    IsActive        BIT              NOT NULL CONSTRAINT DF_LP_IsActive DEFAULT 1,
    CreatedAt       DATETIME2(3)     NOT NULL CONSTRAINT DF_LP_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy       UNIQUEIDENTIFIER NULL,
    UpdatedAt       DATETIME2(3)     NULL,
    UpdatedBy       UNIQUEIDENTIFIER NULL,
    IsDeleted       BIT              NOT NULL CONSTRAINT DF_LP_IsDeleted DEFAULT 0,
    RowVersion      ROWVERSION       NOT NULL,
    CONSTRAINT PK_LeavePolicies PRIMARY KEY (Id),
    CONSTRAINT FK_LP_Location FOREIGN KEY (LocationId) REFERENCES dbo.Locations (Id)
);
-- Har location (aur tenant default) ki sirf EK active policy
CREATE UNIQUE INDEX UX_LP_Tenant_Location_Active ON dbo.LeavePolicies (TenantId, LocationId)
    WHERE IsActive = 1 AND IsDeleted = 0;
GO

/* 11. LeavePolicyRules — policy ke andar har leave type ka quota aur rules */
CREATE TABLE dbo.LeavePolicyRules (
    Id                          UNIQUEIDENTIFIER NOT NULL,
    LeavePolicyId               UNIQUEIDENTIFIER NOT NULL,
    LeaveTypeId                 UNIQUEIDENTIFIER NOT NULL,
    AnnualEntitlement           DECIMAL(5,2)     NOT NULL,          -- 14.00 days
    AccrualMethod               TINYINT          NOT NULL CONSTRAINT DF_LPR_Accrual DEFAULT 1, -- 1 Upfront (Jan 1), 2 Monthly
    MaxCarryForward             DECIMAL(5,2)     NOT NULL CONSTRAINT DF_LPR_Carry DEFAULT 0,
    CarryForwardExpiryMonths    TINYINT          NULL,              -- carried days kab tak valid
    MinServiceDays              SMALLINT         NOT NULL CONSTRAINT DF_LPR_MinSvc DEFAULT 0,  -- e.g. 90 din baad eligible
    MaxConsecutiveDays          SMALLINT         NULL,
    ApplicableGender            TINYINT          NULL,              -- maternity/paternity
    ApplicableEmploymentTypes   TINYINT          NULL,              -- bitmask; NULL = sab
    CONSTRAINT PK_LeavePolicyRules PRIMARY KEY (Id),
    CONSTRAINT FK_LPR_Policy    FOREIGN KEY (LeavePolicyId) REFERENCES dbo.LeavePolicies (Id) ON DELETE CASCADE,
    CONSTRAINT FK_LPR_LeaveType FOREIGN KEY (LeaveTypeId)   REFERENCES dbo.LeaveTypes (Id),
    CONSTRAINT CK_LPR_Entitlement CHECK (AnnualEntitlement >= 0 AND MaxCarryForward >= 0)
);
CREATE UNIQUE INDEX UX_LPR_Policy_Type ON dbo.LeavePolicyRules (LeavePolicyId, LeaveTypeId);
GO

/* 12. LeaveApprovalSettings — tenant-configurable approval chain (1 row per tenant).
       Leave service ka apna setting; Identity ki TenantSettings pe runtime dependency nahi. */
CREATE TABLE dbo.LeaveApprovalSettings (
    TenantId                    UNIQUEIDENTIFIER NOT NULL,
    ApprovalLevels              TINYINT          NOT NULL CONSTRAINT DF_LAS_Levels DEFAULT 1,  -- 1 ya 2
    Level1Approver              TINYINT          NOT NULL CONSTRAINT DF_LAS_L1 DEFAULT 1,      -- 1 LineManager, 2 DepartmentHead
    Level2Approver              TINYINT          NULL,                                         -- 3 HR (permission: leaves.approve)
    AutoApproveAfterDays        TINYINT          NULL,                                         -- NULL = kabhi nahi
    AllowCancelAfterApproval    BIT              NOT NULL CONSTRAINT DF_LAS_Cancel DEFAULT 1,
    UpdatedAt                   DATETIME2(3)     NULL,
    UpdatedBy                   UNIQUEIDENTIFIER NULL,
    RowVersion                  ROWVERSION       NOT NULL,
    CONSTRAINT PK_LeaveApprovalSettings PRIMARY KEY (TenantId),
    CONSTRAINT CK_LAS_Levels CHECK (ApprovalLevels IN (1, 2)),
    CONSTRAINT CK_LAS_L2 CHECK ((ApprovalLevels = 1 AND Level2Approver IS NULL) OR (ApprovalLevels = 2 AND Level2Approver IS NOT NULL))
);
GO

/* 13. LeaveBalances — per employee, per leave type, per leave year.
       Available computed; RowVersion zaroori (do requests ek saath balance kaatein). */
CREATE TABLE dbo.LeaveBalances (
    Id              UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    EmployeeId      UNIQUEIDENTIFIER NOT NULL,
    LeaveTypeId     UNIQUEIDENTIFIER NOT NULL,
    LeaveYear       SMALLINT         NOT NULL,
    Entitled        DECIMAL(5,2)     NOT NULL CONSTRAINT DF_LB_Entitled DEFAULT 0,
    CarriedForward  DECIMAL(5,2)     NOT NULL CONSTRAINT DF_LB_Carry DEFAULT 0,
    Adjusted        DECIMAL(5,2)     NOT NULL CONSTRAINT DF_LB_Adjusted DEFAULT 0,  -- HR manual +/-
    Used            DECIMAL(5,2)     NOT NULL CONSTRAINT DF_LB_Used DEFAULT 0,
    Pending         DECIMAL(5,2)     NOT NULL CONSTRAINT DF_LB_Pending DEFAULT 0,   -- submitted, abhi approve nahi
    Available       AS (Entitled + CarriedForward + Adjusted - Used - Pending) PERSISTED,
    UpdatedAt       DATETIME2(3)     NULL,
    RowVersion      ROWVERSION       NOT NULL,
    CONSTRAINT PK_LeaveBalances PRIMARY KEY (Id),
    CONSTRAINT FK_LB_Employee  FOREIGN KEY (EmployeeId)  REFERENCES dbo.Employees (Id),
    CONSTRAINT FK_LB_LeaveType FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes (Id)
);
CREATE UNIQUE INDEX UX_LB_Emp_Type_Year ON dbo.LeaveBalances (EmployeeId, LeaveTypeId, LeaveYear);
GO

/* 14. LeaveRequests */
CREATE TABLE dbo.LeaveRequests (
    Id                      UNIQUEIDENTIFIER NOT NULL,
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    EmployeeId              UNIQUEIDENTIFIER NOT NULL,
    LeaveTypeId             UNIQUEIDENTIFIER NOT NULL,
    StartDate               DATE             NOT NULL,
    EndDate                 DATE             NOT NULL,
    IsHalfDay               BIT              NOT NULL CONSTRAINT DF_LR_Half DEFAULT 0,
    HalfDayPeriod           TINYINT          NULL,          -- 1 FirstHalf, 2 SecondHalf
    TotalDays               DECIMAL(5,2)     NOT NULL,      -- weekends + holidays nikaal kar
    Reason                  NVARCHAR(1000)   NULL,
    AttachmentDocumentId    UNIQUEIDENTIFIER NULL,          -- EmployeeDocuments (medical certificate)
    Status                  TINYINT          NOT NULL CONSTRAINT DF_LR_Status DEFAULT 1, -- 1 Pending, 2 Approved, 3 Rejected, 4 Cancelled
    CurrentApprovalLevel    TINYINT          NOT NULL CONSTRAINT DF_LR_Level DEFAULT 1,
    AppliedAt               DATETIME2(3)     NOT NULL CONSTRAINT DF_LR_AppliedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy               UNIQUEIDENTIFIER NULL,
    UpdatedAt               DATETIME2(3)     NULL,
    UpdatedBy               UNIQUEIDENTIFIER NULL,
    RowVersion              ROWVERSION       NOT NULL,
    CONSTRAINT PK_LeaveRequests PRIMARY KEY (Id),
    CONSTRAINT FK_LR_Employee   FOREIGN KEY (EmployeeId)  REFERENCES dbo.Employees (Id),
    CONSTRAINT FK_LR_LeaveType  FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes (Id),
    CONSTRAINT FK_LR_Attachment FOREIGN KEY (AttachmentDocumentId) REFERENCES dbo.EmployeeDocuments (Id),
    CONSTRAINT CK_LR_Dates CHECK (EndDate >= StartDate),
    CONSTRAINT CK_LR_HalfDay CHECK ((IsHalfDay = 0 AND HalfDayPeriod IS NULL) OR (IsHalfDay = 1 AND HalfDayPeriod IS NOT NULL AND StartDate = EndDate))
);
CREATE INDEX IX_LR_Employee_Start ON dbo.LeaveRequests (EmployeeId, StartDate DESC);
CREATE INDEX IX_LR_Tenant_Status  ON dbo.LeaveRequests (TenantId, Status) INCLUDE (EmployeeId, StartDate, EndDate);
CREATE INDEX IX_LR_Tenant_Dates   ON dbo.LeaveRequests (TenantId, StartDate, EndDate) WHERE Status IN (1, 2);  -- calendar / "On Leave today"
GO

/* 15. LeaveRequestApprovals — submit ke waqt settings ka SNAPSHOT: har level ki ek row.
       Baad mein tenant settings badlein to chalti hui requests pe asar nahi. */
CREATE TABLE dbo.LeaveRequestApprovals (
    Id                      UNIQUEIDENTIFIER NOT NULL,
    LeaveRequestId          UNIQUEIDENTIFIER NOT NULL,
    Level                   TINYINT          NOT NULL,
    ApproverType            TINYINT          NOT NULL,      -- 1 LineManager, 2 DepartmentHead, 3 HR
    AssignedApproverId      UNIQUEIDENTIFIER NULL,          -- manager/head resolve at submit; HR = NULL (role-based)
    Decision                TINYINT          NOT NULL CONSTRAINT DF_LRA_Decision DEFAULT 0, -- 0 Pending, 1 Approved, 2 Rejected, 3 Skipped
    DecidedByEmployeeId     UNIQUEIDENTIFIER NULL,
    DecidedAt               DATETIME2(3)     NULL,
    Comment                 NVARCHAR(500)    NULL,
    CONSTRAINT PK_LeaveRequestApprovals PRIMARY KEY (Id),
    CONSTRAINT FK_LRA_Request FOREIGN KEY (LeaveRequestId) REFERENCES dbo.LeaveRequests (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_LRA_Request_Level ON dbo.LeaveRequestApprovals (LeaveRequestId, Level);
CREATE INDEX IX_LRA_Approver_Pending ON dbo.LeaveRequestApprovals (AssignedApproverId) WHERE Decision = 0;  -- "My approvals" inbox
GO