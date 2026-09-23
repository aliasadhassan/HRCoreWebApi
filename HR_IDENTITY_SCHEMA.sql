/* =====================================================================
   HR_Identity_Db  —  Target Schema (v1)
   Owns: Tenants, Users, Roles/Permissions (RBAC), SSO logins,
         Refresh tokens, Tenant-level settings, Login audit
   Conventions:
     - PK: UNIQUEIDENTIFIER (app generates sequential Guid / NEWSEQUENTIALID)
     - Every tenant-owned row has TenantId
     - Audit: CreatedAt/By, UpdatedAt/By  |  Soft delete: IsDeleted
     - Concurrency: RowVersion
   ===================================================================== */

USE HR_Identity_Db;
GO

/* ---------------------------------------------------------------------
   1. Tenants  (one row per customer company)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Tenants (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Tenants_Id DEFAULT NEWSEQUENTIALID(),
    Name                NVARCHAR(200)    NOT NULL,
    Slug                NVARCHAR(100)    NOT NULL,          -- e.g. "acme" -> acme.hr-cloud.online
    LegalName           NVARCHAR(300)    NULL,
    LogoUrl             NVARCHAR(500)    NULL,
    PrimaryEmail        NVARCHAR(256)    NULL,
    Phone               NVARCHAR(50)     NULL,
    EntraTenantId       UNIQUEIDENTIFIER NULL,              -- Microsoft SSO tenant mapping
    SsoEnabled          BIT              NOT NULL CONSTRAINT DF_Tenants_SsoEnabled DEFAULT 0,
    [Plan]              NVARCHAR(50)     NOT NULL CONSTRAINT DF_Tenants_Plan DEFAULT N'Free',
    Status              TINYINT          NOT NULL CONSTRAINT DF_Tenants_Status DEFAULT 1, -- 1 Active, 2 Suspended, 3 Cancelled
    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_Tenants_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NULL,
    UpdatedAt           DATETIME2(3)     NULL,
    UpdatedBy           UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL CONSTRAINT DF_Tenants_IsDeleted DEFAULT 0,
    RowVersion          ROWVERSION       NOT NULL,
    CONSTRAINT PK_Tenants PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX UX_Tenants_Slug          ON dbo.Tenants (Slug) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Tenants_EntraTenantId ON dbo.Tenants (EntraTenantId) WHERE EntraTenantId IS NOT NULL;
GO

/* ---------------------------------------------------------------------
   2. TenantSettings  (1:1 with Tenant — "Settings > General" page)
      Leave/Payroll specific settings apni apni DB mein rahengi.
   --------------------------------------------------------------------- */
CREATE TABLE dbo.TenantSettings (
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    TimeZone                NVARCHAR(64)     NOT NULL CONSTRAINT DF_TS_TimeZone DEFAULT N'Asia/Karachi',
    Currency                CHAR(3)          NOT NULL CONSTRAINT DF_TS_Currency DEFAULT 'PKR',
    DateFormat              NVARCHAR(20)     NOT NULL CONSTRAINT DF_TS_DateFormat DEFAULT N'dd-MMM-yyyy',
    Locale                  NVARCHAR(10)     NOT NULL CONSTRAINT DF_TS_Locale DEFAULT N'en-US',
    FiscalYearStartMonth    TINYINT          NOT NULL CONSTRAINT DF_TS_FYStart DEFAULT 7,
    WorkWeekDays            TINYINT          NOT NULL CONSTRAINT DF_TS_WorkWeek DEFAULT 31,  -- bitmask Mon=1..Sun=64 (31 = Mon-Fri)
    PasswordMinLength       TINYINT          NOT NULL CONSTRAINT DF_TS_PwdLen DEFAULT 8,
    MaxFailedLoginAttempts  TINYINT          NOT NULL CONSTRAINT DF_TS_MaxFail DEFAULT 5,
    SessionTimeoutMinutes   SMALLINT         NOT NULL CONSTRAINT DF_TS_Session DEFAULT 60,
    UpdatedAt               DATETIME2(3)     NULL,
    UpdatedBy               UNIQUEIDENTIFIER NULL,
    RowVersion              ROWVERSION       NOT NULL,
    CONSTRAINT PK_TenantSettings PRIMARY KEY (TenantId),
    CONSTRAINT FK_TenantSettings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id),
    CONSTRAINT CK_TS_FYStart CHECK (FiscalYearStartMonth BETWEEN 1 AND 12)
);
GO

/* ---------------------------------------------------------------------
   3. Users  (login identity only — HR profile Employee_Db mein hai)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Users (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_Id DEFAULT NEWSEQUENTIALID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Email               NVARCHAR(256)    NOT NULL,
    NormalizedEmail     NVARCHAR(256)    NOT NULL,
    PasswordHash        NVARCHAR(500)    NULL,              -- NULL = SSO-only user
    DisplayName         NVARCHAR(200)    NOT NULL,
    AvatarUrl           NVARCHAR(500)    NULL,
    EmployeeId          UNIQUEIDENTIFIER NULL,              -- logical link to Employee_Db.Employees (no FK, cross-service)
    EmailConfirmed      BIT              NOT NULL CONSTRAINT DF_Users_EmailConfirmed DEFAULT 0,
    IsActive            BIT              NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    MustChangePassword  BIT              NOT NULL CONSTRAINT DF_Users_MustChange DEFAULT 0,
    AccessFailedCount   TINYINT          NOT NULL CONSTRAINT DF_Users_Failed DEFAULT 0,
    LockoutEnd          DATETIME2(3)     NULL,
    LastLoginAt         DATETIME2(3)     NULL,
    SecurityStamp       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_SecStamp DEFAULT NEWID(), -- change => sab tokens invalid
    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NULL,
    UpdatedAt           DATETIME2(3)     NULL,
    UpdatedBy           UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT 0,
    RowVersion          ROWVERSION       NOT NULL,
    CONSTRAINT PK_Users PRIMARY KEY (Id),
    CONSTRAINT FK_Users_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id)
);
CREATE UNIQUE INDEX UX_Users_Tenant_Email    ON dbo.Users (TenantId, NormalizedEmail) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Users_Tenant_Employee ON dbo.Users (TenantId, EmployeeId) WHERE EmployeeId IS NOT NULL AND IsDeleted = 0;
GO

/* ---------------------------------------------------------------------
   4. UserExternalLogins  (Microsoft Entra SSO — oid mapping)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.UserExternalLogins (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UEL_Id DEFAULT NEWSEQUENTIALID(),
    UserId              UNIQUEIDENTIFIER NOT NULL,
    Provider            NVARCHAR(50)     NOT NULL,          -- 'Microsoft'
    ProviderKey         NVARCHAR(200)    NOT NULL,          -- Entra 'oid' claim
    ProviderTenantId    NVARCHAR(100)    NULL,              -- Entra 'tid' claim
    LinkedAt            DATETIME2(3)     NOT NULL CONSTRAINT DF_UEL_LinkedAt DEFAULT SYSUTCDATETIME(),
    LastUsedAt          DATETIME2(3)     NULL,
    CONSTRAINT PK_UserExternalLogins PRIMARY KEY (Id),
    CONSTRAINT FK_UEL_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_UEL_Provider_Key ON dbo.UserExternalLogins (Provider, ProviderKey);
CREATE INDEX IX_UEL_UserId ON dbo.UserExternalLogins (UserId);
GO

/* ---------------------------------------------------------------------
   5. Roles  (TenantId NULL = system role shared by all tenants)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Roles (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Roles_Id DEFAULT NEWSEQUENTIALID(),
    TenantId            UNIQUEIDENTIFIER NULL,
    Name                NVARCHAR(100)    NOT NULL,
    NormalizedName      NVARCHAR(100)    NOT NULL,
    Description         NVARCHAR(500)    NULL,
    IsSystem            BIT              NOT NULL CONSTRAINT DF_Roles_IsSystem DEFAULT 0, -- system roles edit/delete nahi hongay
    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NULL,
    UpdatedAt           DATETIME2(3)     NULL,
    UpdatedBy           UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL CONSTRAINT DF_Roles_IsDeleted DEFAULT 0,
    RowVersion          ROWVERSION       NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (Id),
    CONSTRAINT FK_Roles_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id)
);
CREATE UNIQUE INDEX UX_Roles_Tenant_Name ON dbo.Roles (TenantId, NormalizedName) WHERE IsDeleted = 0;
GO

/* ---------------------------------------------------------------------
   6. Permissions  (global catalogue, seeded — sidebar/page visibility isi se)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Permissions (
    Id                  INT              NOT NULL IDENTITY(1,1),
    Code                NVARCHAR(100)    NOT NULL,          -- 'employees.view'
    Module              NVARCHAR(50)     NOT NULL,          -- 'Employees'
    Description         NVARCHAR(300)    NULL,
    SortOrder           SMALLINT         NOT NULL CONSTRAINT DF_Perm_Sort DEFAULT 0,
    CONSTRAINT PK_Permissions PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX UX_Permissions_Code ON dbo.Permissions (Code);
GO

/* ---------------------------------------------------------------------
   7. RolePermissions  /  8. UserRoles
   --------------------------------------------------------------------- */
CREATE TABLE dbo.RolePermissions (
    RoleId              UNIQUEIDENTIFIER NOT NULL,
    PermissionId        INT              NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
    CONSTRAINT FK_RP_Roles       FOREIGN KEY (RoleId)       REFERENCES dbo.Roles (Id) ON DELETE CASCADE,
    CONSTRAINT FK_RP_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (Id) ON DELETE CASCADE
);

CREATE TABLE dbo.UserRoles (
    UserId              UNIQUEIDENTIFIER NOT NULL,
    RoleId              UNIQUEIDENTIFIER NOT NULL,
    AssignedAt          DATETIME2(3)     NOT NULL CONSTRAINT DF_UR_AssignedAt DEFAULT SYSUTCDATETIME(),
    AssignedBy          UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UR_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UR_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id)
);
CREATE INDEX IX_UserRoles_RoleId ON dbo.UserRoles (RoleId);
GO

/* ---------------------------------------------------------------------
   9. RefreshTokens  (hash store karo, raw token kabhi nahi; rotation chain)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.RefreshTokens (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RT_Id DEFAULT NEWSEQUENTIALID(),
    UserId              UNIQUEIDENTIFIER NOT NULL,
    TokenHash           VARBINARY(32)    NOT NULL,          -- SHA-256
    FamilyId            UNIQUEIDENTIFIER NOT NULL,          -- reuse detect => poori family revoke
    ExpiresAt           DATETIME2(3)     NOT NULL,
    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_RT_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedByIp         VARCHAR(45)      NULL,
    UserAgent           NVARCHAR(300)    NULL,
    RevokedAt           DATETIME2(3)     NULL,
    RevokedByIp         VARCHAR(45)      NULL,
    RevokedReason       NVARCHAR(100)    NULL,
    ReplacedByTokenId   UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_RefreshTokens PRIMARY KEY (Id),
    CONSTRAINT FK_RT_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_RT_TokenHash ON dbo.RefreshTokens (TokenHash);
CREATE INDEX IX_RT_User_Active ON dbo.RefreshTokens (UserId) INCLUDE (ExpiresAt) WHERE RevokedAt IS NULL;
CREATE INDEX IX_RT_FamilyId ON dbo.RefreshTokens (FamilyId);
GO

/* ---------------------------------------------------------------------
   10. UserTokens  (one-time tokens: password reset, email confirm, invite)
       Users.PasswordResetToken / ResetTokenExpires ki jagah — hashed
   --------------------------------------------------------------------- */
CREATE TABLE dbo.UserTokens (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UT_Id DEFAULT NEWSEQUENTIALID(),
    UserId              UNIQUEIDENTIFIER NOT NULL,
    Purpose             TINYINT          NOT NULL,          -- 1 PasswordReset, 2 EmailConfirm, 3 Invite
    TokenHash           VARBINARY(32)    NOT NULL,          -- SHA-256
    ExpiresAt           DATETIME2(3)     NOT NULL,
    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_UT_CreatedAt DEFAULT SYSUTCDATETIME(),
    UsedAt              DATETIME2(3)     NULL,              -- one-time use
    CONSTRAINT PK_UserTokens PRIMARY KEY (Id),
    CONSTRAINT FK_UT_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_UT_TokenHash ON dbo.UserTokens (TokenHash);
CREATE INDEX IX_UT_User_Purpose ON dbo.UserTokens (UserId, Purpose) WHERE UsedAt IS NULL;
GO

/* ---------------------------------------------------------------------
   11. LoginAudit  (Settings > Security page: "recent sign-ins")
   --------------------------------------------------------------------- */
CREATE TABLE dbo.LoginAudit (
    Id                  BIGINT           NOT NULL IDENTITY(1,1),
    TenantId            UNIQUEIDENTIFIER NULL,
    UserId              UNIQUEIDENTIFIER NULL,              -- NULL agar email hi ghalat tha
    EmailAttempted      NVARCHAR(256)    NOT NULL,
    Method              VARCHAR(20)      NOT NULL,          -- 'Password' | 'Microsoft' | 'Refresh'
    Succeeded           BIT              NOT NULL,
    FailureReason       NVARCHAR(100)    NULL,
    IpAddress           VARCHAR(45)      NULL,
    UserAgent           NVARCHAR(300)    NULL,
    OccurredAt          DATETIME2(3)     NOT NULL CONSTRAINT DF_LA_OccurredAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_LoginAudit PRIMARY KEY (Id)
);
CREATE INDEX IX_LoginAudit_User_Time   ON dbo.LoginAudit (UserId, OccurredAt DESC);
CREATE INDEX IX_LoginAudit_Tenant_Time ON dbo.LoginAudit (TenantId, OccurredAt DESC);
GO

/* =====================================================================
   SEED — Permissions + System Roles
   ===================================================================== */
INSERT INTO dbo.Permissions (Code, Module, Description, SortOrder) VALUES
 (N'dashboard.view',      N'Dashboard', N'View dashboard',                  10),
 (N'employees.view',      N'Employees', N'View employee directory',         20),
 (N'employees.create',    N'Employees', N'Add employees',                   21),
 (N'employees.edit',      N'Employees', N'Edit employee records',           22),
 (N'employees.delete',    N'Employees', N'Archive/delete employees',        23),
 (N'leaves.view.own',     N'Leaves',    N'View own leaves',                 30),
 (N'leaves.apply',        N'Leaves',    N'Apply for leave',                 31),
 (N'leaves.view.all',     N'Leaves',    N'View all leave requests',         32),
 (N'leaves.approve',      N'Leaves',    N'Approve/reject leave',            33),
 (N'payroll.view.own',    N'Payroll',   N'View own payslips',               40),
 (N'payroll.view.all',    N'Payroll',   N'View all payroll',                41),
 (N'payroll.run',         N'Payroll',   N'Process payroll runs',            42),
 (N'payroll.approve',     N'Payroll',   N'Approve/lock payroll runs',       43),
 (N'settings.view',       N'Settings',  N'View settings',                   50),
 (N'settings.manage',     N'Settings',  N'Change company settings',         51),
 (N'users.manage',        N'Settings',  N'Invite/deactivate users',         52),
 (N'roles.manage',        N'Settings',  N'Create roles, assign permissions',53);
GO

DECLARE @Admin    UNIQUEIDENTIFIER = NEWID(),
        @HR       UNIQUEIDENTIFIER = NEWID(),
        @Manager  UNIQUEIDENTIFIER = NEWID(),
        @Employee UNIQUEIDENTIFIER = NEWID();

INSERT INTO dbo.Roles (Id, TenantId, Name, NormalizedName, Description, IsSystem) VALUES
 (@Admin,    NULL, N'Tenant Admin', N'TENANT ADMIN', N'Full access within the company',        1),
 (@HR,       NULL, N'HR Manager',   N'HR MANAGER',   N'Employees, leaves and payroll',         1),
 (@Manager,  NULL, N'Line Manager', N'LINE MANAGER', N'Team view + leave approvals',           1),
 (@Employee, NULL, N'Employee',     N'EMPLOYEE',     N'Self-service: own leaves and payslips', 1);

-- Admin: sab kuch
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT @Admin, Id FROM dbo.Permissions;

-- HR: settings/users/roles manage ke ilawa sab
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT @HR, Id FROM dbo.Permissions
WHERE Code NOT IN (N'settings.manage', N'users.manage', N'roles.manage');

-- Line Manager
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT @Manager, Id FROM dbo.Permissions
WHERE Code IN (N'dashboard.view', N'employees.view', N'leaves.view.own', N'leaves.apply',
               N'leaves.view.all', N'leaves.approve', N'payroll.view.own');

-- Employee
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT @Employee, Id FROM dbo.Permissions
WHERE Code IN (N'dashboard.view', N'leaves.view.own', N'leaves.apply', N'payroll.view.own');
GO