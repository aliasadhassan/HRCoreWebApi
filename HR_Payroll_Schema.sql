/* =====================================================================================
   HR Cloud — HR_Payroll_Db  (reference schema, v1)
   -------------------------------------------------------------------------------------
   Faisle:
   - Tax GENERIC + table-driven: TaxRegimes/TaxSlabs/ContributionRules. Naya mulk = data, code nahi.
     TenantId NULL wale regimes = platform-provided (hum maintain karte hain), tenant apne bhi bana sakta hai.
   - Salary: Grade → Template (default components) → Employee override. Effective-dated (increment history).
   - Pay frequency: PayGroup level pe (Monthly / SemiMonthly / BiWeekly / Weekly). Run = PayGroup + PayPeriod.
   - Employee data Employee API ke events se LOCAL copy (PayrollEmployees). Run mein koi HTTP call nahi.
   - Unpaid leave: LeaveRequestApproved/Cancelled events → EmployeeUnpaidLeaveDays.
   - Retry-safe: har level pe natural-key unique index (run / payslip / payment / loan repayment).
   - Calculation ≠ Payment: Run status machine se lock. Approved ke baad regenerate nahi.

   EF model source of truth hoga (Employee_Db ki tarah). Tenant tables pe standard audit columns:
   CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion (yahan "AUDIT" likha hai).
   MassTransit InboxState/OutboxMessage/OutboxState bhi isi DB mein.

   Amounts: DECIMAL(18,2)  |  Rates/percent: DECIMAL(9,4)  |  Currency: CHAR(3) ISO 4217
   ===================================================================================== */

CREATE DATABASE HR_Payroll_Db;
GO
USE HR_Payroll_Db;
GO

/* ─────────────────────────────── 1. TENANT SETTINGS ─────────────────────────────── */

CREATE TABLE PayrollSettings (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    BaseCurrency          CHAR(3)          NOT NULL,           -- reporting currency
    ProrationMethod       TINYINT          NOT NULL DEFAULT 1, -- 1 CalendarDays, 2 WorkingDays, 3 Fixed30
    RoundingDecimals      TINYINT          NOT NULL DEFAULT 2,
    PayslipNumberPrefix   NVARCHAR(10)     NOT NULL DEFAULT 'PS',
    RequireApproval       BIT              NOT NULL DEFAULT 1, -- Calculated → Approved step lazmi?
    -- AUDIT
    CONSTRAINT UQ_PayrollSettings_Tenant UNIQUE (TenantId)
);

/* ─────────────────────────── 2. LOCAL EMPLOYEE READ MODEL ───────────────────────── */
-- Employee API ke events se bharta hai (EmployeeCreated / JobChanged / Exited).
-- Bank + tax identity Payroll ki apni (sensitive) — Employee_Db mein nahi.

CREATE TABLE PayrollEmployees (
    EmployeeId            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,  -- SAME Guid as Employee API
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    EmployeeCode          NVARCHAR(20)     NOT NULL,
    FullName              NVARCHAR(300)    NOT NULL,
    WorkEmail             NVARCHAR(256)    NOT NULL,
    DepartmentId          UNIQUEIDENTIFIER NOT NULL,
    DepartmentName        NVARCHAR(150)    NULL,
    DesignationTitle      NVARCHAR(150)    NULL,
    LocationId            UNIQUEIDENTIFIER NOT NULL,
    EmploymentType        TINYINT          NOT NULL,              -- same enum as Employee API
    JoiningDate           DATE             NOT NULL,
    ExitDate              DATE             NULL,
    IsActive              BIT              NOT NULL DEFAULT 1,

    PayGroupId            UNIQUEIDENTIFIER NULL,                  -- HR assign karega; NULL = payroll mein nahi
    IsTaxExempt           BIT              NOT NULL DEFAULT 0,

    -- Encrypted at application layer (Data Protection). Payslip pe sirf masked.
    BankName              NVARCHAR(100)    NULL,
    BankAccountTitle      NVARCHAR(150)    NULL,
    BankAccountNumber     NVARCHAR(256)    NULL,
    Iban                  NVARCHAR(256)    NULL,
    TaxIdentifier         NVARCHAR(256)    NULL,                  -- NTN / CNIC / TRN / SSN

    LastSyncedAt          DATETIME2(3)     NOT NULL,
    -- AUDIT
);
CREATE UNIQUE INDEX UX_PayrollEmployees_Code ON PayrollEmployees (TenantId, EmployeeCode) WHERE IsDeleted = 0;
CREATE INDEX IX_PayrollEmployees_PayGroup ON PayrollEmployees (TenantId, PayGroupId) INCLUDE (IsActive, JoiningDate, ExitDate);

/* ─────────────────────────────── 3. PAY GROUPS & PERIODS ────────────────────────── */

CREATE TABLE PayGroups (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    Name                  NVARCHAR(100)    NOT NULL,             -- "Pakistan Monthly", "UAE Monthly", "Hourly Weekly"
    Code                  NVARCHAR(20)     NOT NULL,
    PayFrequency          TINYINT          NOT NULL,             -- 1 Monthly, 2 SemiMonthly, 3 BiWeekly, 4 Weekly
    CountryCode           CHAR(2)          NOT NULL,             -- tax jurisdiction isi se
    CurrencyCode          CHAR(3)          NOT NULL,
    AnchorDate            DATE             NOT NULL,             -- weekly/bi-weekly cycle ka pehla din
    PayDayOffset          SMALLINT         NOT NULL DEFAULT 0,   -- PeriodEnd + N din = PayDate
    IsActive              BIT              NOT NULL DEFAULT 1,
    -- AUDIT
    CONSTRAINT CK_PayGroups_Frequency CHECK (PayFrequency BETWEEN 1 AND 4)
);
CREATE UNIQUE INDEX UX_PayGroups_Code ON PayGroups (TenantId, Code) WHERE IsDeleted = 0;

-- System aage ke periods generate karta hai (e.g. 3 mahine advance).
CREATE TABLE PayPeriods (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayGroupId            UNIQUEIDENTIFIER NOT NULL REFERENCES PayGroups(Id),
    PeriodStart           DATE             NOT NULL,
    PeriodEnd             DATE             NOT NULL,
    PayDate               DATE             NOT NULL,
    FiscalYear            SMALLINT         NOT NULL,
    PeriodNumber          TINYINT          NOT NULL,             -- 1..12 / 1..24 / 1..27 / 1..53
    Status                TINYINT          NOT NULL DEFAULT 1,   -- 1 Open, 2 Locked (regular run Approved/Paid)
    -- AUDIT
    CONSTRAINT CK_PayPeriods_Dates CHECK (PeriodEnd >= PeriodStart AND PayDate >= PeriodStart)
);
CREATE UNIQUE INDEX UX_PayPeriods_Start ON PayPeriods (PayGroupId, PeriodStart) WHERE IsDeleted = 0;

/* ──────────────────────────────── 4. PAY COMPONENTS ─────────────────────────────── */
-- Salary ke "building blocks". SystemCode wale engine khud use karta hai (BASIC, INCOME_TAX, UNPAID_LEAVE...).

CREATE TABLE PayComponents (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    Code                  NVARCHAR(30)     NOT NULL,             -- BASIC, HRA, TRANSPORT, OT, BONUS, LOAN, EOBI_EE
    Name                  NVARCHAR(100)    NOT NULL,
    SystemCode            NVARCHAR(30)     NULL,                 -- engine hooks; NULL = normal component
    ComponentType         TINYINT          NOT NULL,             -- 1 Earning, 2 Deduction, 3 EmployerContribution, 4 Informational
    DefaultCalcType       TINYINT          NOT NULL,             -- 1 Fixed, 2 PercentOfComponent, 3 PercentOfGross, 4 Variable (per-run input)
    DefaultBaseComponentId UNIQUEIDENTIFIER NULL REFERENCES PayComponents(Id),
    IsTaxable             BIT              NOT NULL DEFAULT 1,
    IsProrated            BIT              NOT NULL DEFAULT 1,   -- joining/exit/unpaid leave pe kaatega?
    IsRecurring           BIT              NOT NULL DEFAULT 1,   -- har period (vs one-time bonus)
    ShowOnPayslip         BIT              NOT NULL DEFAULT 1,
    SortOrder             SMALLINT         NOT NULL DEFAULT 0,
    IsActive              BIT              NOT NULL DEFAULT 1,
    -- AUDIT
    CONSTRAINT CK_PayComponents_Type CHECK (ComponentType BETWEEN 1 AND 4),
    CONSTRAINT CK_PayComponents_Calc CHECK (DefaultCalcType BETWEEN 1 AND 4)
);
CREATE UNIQUE INDEX UX_PayComponents_Code ON PayComponents (TenantId, Code) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_PayComponents_System ON PayComponents (TenantId, SystemCode) WHERE SystemCode IS NOT NULL AND IsDeleted = 0;

/* ─────────────────────────────── 5. GRADES & TEMPLATES ──────────────────────────── */

CREATE TABLE SalaryGrades (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    Code                  NVARCHAR(20)     NOT NULL,             -- G1, G2, M1 ...
    Name                  NVARCHAR(100)    NOT NULL,
    CurrencyCode          CHAR(3)          NOT NULL,
    MinAnnual             DECIMAL(18,2)    NULL,                 -- band — increment is se bahar ho to warning
    MaxAnnual             DECIMAL(18,2)    NULL,
    IsActive              BIT              NOT NULL DEFAULT 1,
    -- AUDIT
    CONSTRAINT CK_SalaryGrades_Band CHECK (MinAnnual IS NULL OR MaxAnnual IS NULL OR MaxAnnual >= MinAnnual)
);
CREATE UNIQUE INDEX UX_SalaryGrades_Code ON SalaryGrades (TenantId, Code) WHERE IsDeleted = 0;

CREATE TABLE SalaryTemplates (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    Name                  NVARCHAR(100)    NOT NULL,             -- "Standard PK", "G3 Engineering"
    SalaryGradeId         UNIQUEIDENTIFIER NULL REFERENCES SalaryGrades(Id),
    IsActive              BIT              NOT NULL DEFAULT 1,
    -- AUDIT
);

-- Template ka "recipe": e.g. BASIC = 60% of CTC_GROSS, HRA = 40% of BASIC, TRANSPORT = 5000 fixed
CREATE TABLE SalaryTemplateLines (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    SalaryTemplateId      UNIQUEIDENTIFIER NOT NULL REFERENCES SalaryTemplates(Id) ON DELETE CASCADE,
    PayComponentId        UNIQUEIDENTIFIER NOT NULL REFERENCES PayComponents(Id),
    CalcType              TINYINT          NOT NULL,             -- 1 Fixed, 2 PercentOfComponent, 3 PercentOfGross
    Amount                DECIMAL(18,2)    NULL,                 -- Fixed (per PERIOD of pay group)
    Percentage            DECIMAL(9,4)     NULL,
    BaseComponentId       UNIQUEIDENTIFIER NULL REFERENCES PayComponents(Id),
    CONSTRAINT UQ_TemplateLine UNIQUE (SalaryTemplateId, PayComponentId),
    CONSTRAINT CK_TemplateLine_Value CHECK (
        (CalcType = 1 AND Amount IS NOT NULL) OR
        (CalcType = 2 AND Percentage IS NOT NULL AND BaseComponentId IS NOT NULL) OR
        (CalcType = 3 AND Percentage IS NOT NULL))
);

/* ──────────────────────────────── 6. EMPLOYEE SALARY ────────────────────────────── */
-- Effective-dated: har increment = nayi row, purani ka EffectiveTo set. History kabhi overwrite nahi.

CREATE TABLE EmployeeSalaries (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    EmployeeId            UNIQUEIDENTIFIER NOT NULL REFERENCES PayrollEmployees(EmployeeId),
    SalaryTemplateId      UNIQUEIDENTIFIER NOT NULL REFERENCES SalaryTemplates(Id),
    SalaryGradeId         UNIQUEIDENTIFIER NULL REFERENCES SalaryGrades(Id),
    CurrencyCode          CHAR(3)          NOT NULL,
    SalaryBasis           TINYINT          NOT NULL,             -- 1 Annual, 2 Monthly, 3 Hourly
    BasisAmount           DECIMAL(18,2)    NOT NULL,             -- Annual CTC / Monthly gross / Hourly rate
    EffectiveFrom         DATE             NOT NULL,
    EffectiveTo           DATE             NULL,                 -- NULL = current
    ChangeReason          TINYINT          NOT NULL,             -- 1 Joining, 2 Increment, 3 Promotion, 4 Correction, 5 Other
    Remarks               NVARCHAR(500)    NULL,
    -- AUDIT
    CONSTRAINT CK_EmpSalary_Amount CHECK (BasisAmount > 0),
    CONSTRAINT CK_EmpSalary_Dates  CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
);
-- Ek employee ki ek hi CURRENT salary
CREATE UNIQUE INDEX UX_EmpSalary_Current ON EmployeeSalaries (EmployeeId) WHERE EffectiveTo IS NULL AND IsDeleted = 0;
CREATE INDEX IX_EmpSalary_Effective ON EmployeeSalaries (EmployeeId, EffectiveFrom DESC);

-- Template par employee-level override (amount badlo, component add karo, ya template line hatao)
CREATE TABLE EmployeeSalaryComponents (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeSalaryId      UNIQUEIDENTIFIER NOT NULL REFERENCES EmployeeSalaries(Id) ON DELETE CASCADE,
    PayComponentId        UNIQUEIDENTIFIER NOT NULL REFERENCES PayComponents(Id),
    IsExcluded            BIT              NOT NULL DEFAULT 0,   -- 1 = template ki ye line is employee pe nahi
    CalcType              TINYINT          NULL,
    Amount                DECIMAL(18,2)    NULL,
    Percentage            DECIMAL(9,4)     NULL,
    BaseComponentId       UNIQUEIDENTIFIER NULL REFERENCES PayComponents(Id),
    CONSTRAINT UQ_EmpSalaryComponent UNIQUE (EmployeeSalaryId, PayComponentId)
);

/* ──────────────────────────────── 7. TAX & STATUTORY ────────────────────────────── */
-- TenantId NULL = platform regime (sab tenants dekh sakte hain). Query filter: TenantId = @t OR TenantId IS NULL.

CREATE TABLE TaxRegimes (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NULL,
    CountryCode           CHAR(2)          NOT NULL,
    Name                  NVARCHAR(150)    NOT NULL,             -- "Pakistan Salaried FY2026-27", "UAE (no PIT)"
    TaxYearStartMonth     TINYINT          NOT NULL,             -- PK = 7 (July), UAE/US = 1
    CalcMethod            TINYINT          NOT NULL,             -- 0 None, 1 Annualized (YTD-based), 2 PerPeriodFlat
    EffectiveFrom         DATE             NOT NULL,
    EffectiveTo           DATE             NULL,
    IsActive              BIT              NOT NULL DEFAULT 1,
    -- AUDIT
    CONSTRAINT CK_TaxRegimes_Month CHECK (TaxYearStartMonth BETWEEN 1 AND 12)
);
CREATE INDEX IX_TaxRegimes_Lookup ON TaxRegimes (CountryCode, EffectiveFrom) INCLUDE (TenantId, EffectiveTo, IsActive);

-- Annual slabs. Tax = FixedAmount + (Income - FromAmount) * Rate/100
CREATE TABLE TaxSlabs (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TaxRegimeId           UNIQUEIDENTIFIER NOT NULL REFERENCES TaxRegimes(Id) ON DELETE CASCADE,
    FromAmount            DECIMAL(18,2)    NOT NULL,
    ToAmount              DECIMAL(18,2)    NULL,                 -- NULL = upar koi limit nahi
    FixedAmount           DECIMAL(18,2)    NOT NULL DEFAULT 0,
    RatePercent           DECIMAL(9,4)     NOT NULL,
    CONSTRAINT UQ_TaxSlab UNIQUE (TaxRegimeId, FromAmount),
    CONSTRAINT CK_TaxSlab CHECK (ToAmount IS NULL OR ToAmount > FromAmount)
);

-- EOBI (PK), GPSSA (UAE nationals), GOSI (KSA), Provident Fund — sab ek hi shape
CREATE TABLE ContributionRules (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NULL,                 -- NULL = platform rule
    CountryCode           CHAR(2)          NOT NULL,
    Code                  NVARCHAR(30)     NOT NULL,             -- EOBI, GOSI, PF
    Name                  NVARCHAR(150)    NOT NULL,
    BaseType              TINYINT          NOT NULL,             -- 1 BasicSalary, 2 Gross, 3 FixedAmount
    EmployeeRatePercent   DECIMAL(9,4)     NULL,
    EmployerRatePercent   DECIMAL(9,4)     NULL,
    EmployeeFixedAmount   DECIMAL(18,2)    NULL,
    EmployerFixedAmount   DECIMAL(18,2)    NULL,
    WageCeiling           DECIMAL(18,2)    NULL,                 -- base is se upar ho to ceiling pe calculate
    EmployeeComponentId   UNIQUEIDENTIFIER NULL REFERENCES PayComponents(Id),  -- tenant apne component se map
    EmployerComponentId   UNIQUEIDENTIFIER NULL REFERENCES PayComponents(Id),
    IsOptIn               BIT              NOT NULL DEFAULT 0,   -- PF jaisi voluntary scheme
    EffectiveFrom         DATE             NOT NULL,
    EffectiveTo           DATE             NULL,
    -- AUDIT
);

-- Saal ke beech join kiya: pichhle employer ki income/tax (annualized tax ke liye)
CREATE TABLE EmployeeTaxOpeningBalances (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    EmployeeId            UNIQUEIDENTIFIER NOT NULL REFERENCES PayrollEmployees(EmployeeId),
    TaxYearStart          DATE             NOT NULL,
    PriorTaxableIncome    DECIMAL(18,2)    NOT NULL DEFAULT 0,
    PriorTaxPaid          DECIMAL(18,2)    NOT NULL DEFAULT 0,
    -- AUDIT
);
CREATE UNIQUE INDEX UX_TaxOpening ON EmployeeTaxOpeningBalances (EmployeeId, TaxYearStart) WHERE IsDeleted = 0;

/* ──────────────────────────────── 8. VARIABLE INPUTS ────────────────────────────── */

-- Overtime, bonus, commission, manual deduction — per period
CREATE TABLE PayrollInputs (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayPeriodId           UNIQUEIDENTIFIER NOT NULL REFERENCES PayPeriods(Id),
    EmployeeId            UNIQUEIDENTIFIER NOT NULL REFERENCES PayrollEmployees(EmployeeId),
    PayComponentId        UNIQUEIDENTIFIER NOT NULL REFERENCES PayComponents(Id),
    Amount                DECIMAL(18,2)    NULL,
    Quantity              DECIMAL(9,2)     NULL,                 -- hours (hourly/OT) — Amount = Qty × rate
    Source                TINYINT          NOT NULL,             -- 1 Manual, 2 Import, 3 Attendance (future)
    SourceReference       NVARCHAR(100)    NOT NULL DEFAULT '',  -- import batch id / attendance id
    Remarks               NVARCHAR(500)    NULL,
    -- AUDIT
    CONSTRAINT CK_PayrollInputs_Value CHECK (Amount IS NOT NULL OR Quantity IS NOT NULL)
);
-- Same import dobara chale to duplicate nahi
CREATE UNIQUE INDEX UX_PayrollInputs ON PayrollInputs (PayPeriodId, EmployeeId, PayComponentId, Source, SourceReference) WHERE IsDeleted = 0;

-- Employee API: LeaveRequestApproved (unpaid type) → har din ki row; Cancelled → rows delete
CREATE TABLE EmployeeUnpaidLeaveDays (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    EmployeeId            UNIQUEIDENTIFIER NOT NULL REFERENCES PayrollEmployees(EmployeeId),
    LeaveRequestId        UNIQUEIDENTIFIER NOT NULL,             -- Employee_Db (logical)
    LeaveDate             DATE             NOT NULL,
    DayFraction           DECIMAL(3,2)     NOT NULL,             -- 1.00 / 0.50
    ReceivedAt            DATETIME2(3)     NOT NULL,
    CONSTRAINT UQ_UnpaidLeaveDay UNIQUE (LeaveRequestId, LeaveDate),    -- event redelivery safe
    CONSTRAINT CK_UnpaidLeaveDay CHECK (DayFraction IN (0.50, 1.00))
);
CREATE INDEX IX_UnpaidLeave_Period ON EmployeeUnpaidLeaveDays (EmployeeId, LeaveDate);

/* ───────────────────────────────── 9. LOANS / ADVANCES ──────────────────────────── */

CREATE TABLE EmployeeLoans (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    EmployeeId            UNIQUEIDENTIFIER NOT NULL REFERENCES PayrollEmployees(EmployeeId),
    LoanType              TINYINT          NOT NULL,             -- 1 Loan, 2 SalaryAdvance
    CurrencyCode          CHAR(3)          NOT NULL,
    PrincipalAmount       DECIMAL(18,2)    NOT NULL,
    InstallmentAmount     DECIMAL(18,2)    NOT NULL,
    OutstandingAmount     DECIMAL(18,2)    NOT NULL,
    StartDate             DATE             NOT NULL,             -- is date ke baad wale period se katauti
    DeductionComponentId  UNIQUEIDENTIFIER NOT NULL REFERENCES PayComponents(Id),
    Status                TINYINT          NOT NULL DEFAULT 1,   -- 1 Active, 2 Paused, 3 Closed, 4 Cancelled
    Remarks               NVARCHAR(500)    NULL,
    -- AUDIT
    CONSTRAINT CK_Loans_Amounts CHECK (PrincipalAmount > 0 AND InstallmentAmount > 0
                                       AND OutstandingAmount BETWEEN 0 AND PrincipalAmount)
);
CREATE INDEX IX_Loans_Active ON EmployeeLoans (EmployeeId) WHERE Status = 1 AND IsDeleted = 0;

/* ──────────────────────────────── 10. PAYROLL RUNS ──────────────────────────────── */

CREATE TABLE PayrollRuns (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayGroupId            UNIQUEIDENTIFIER NOT NULL REFERENCES PayGroups(Id),
    PayPeriodId           UNIQUEIDENTIFIER NOT NULL REFERENCES PayPeriods(Id),
    RunType               TINYINT          NOT NULL,             -- 1 Regular, 2 OffCycle (bonus), 3 FinalSettlement
    Status                TINYINT          NOT NULL,             -- 1 Draft, 2 Processing, 3 Calculated, 4 Approved, 5 Paid, 6 Cancelled, 7 Failed
    CurrencyCode          CHAR(3)          NOT NULL,
    TotalEmployees        INT              NOT NULL DEFAULT 0,
    ProcessedEmployees    INT              NOT NULL DEFAULT 0,   -- progress bar (polling / SignalR)
    TotalGross            DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TotalDeductions       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TotalNet              DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TotalEmployerCost     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    StartedAt             DATETIME2(3)     NULL,
    CalculatedAt          DATETIME2(3)     NULL,
    ApprovedBy            UNIQUEIDENTIFIER NULL,
    ApprovedAt            DATETIME2(3)     NULL,
    PaidAt                DATETIME2(3)     NULL,
    FailureReason         NVARCHAR(1000)   NULL,
    -- AUDIT
    CONSTRAINT CK_Runs_Status CHECK (Status BETWEEN 1 AND 7)
);
-- ⭐ Ek period ka ek hi regular run (cancelled chhod kar) — "salary 2X" yahin rukti hai
CREATE UNIQUE INDEX UX_Runs_RegularPerPeriod ON PayrollRuns (PayPeriodId)
    WHERE RunType = 1 AND Status <> 6 AND IsDeleted = 0;
CREATE INDEX IX_Runs_Tenant ON PayrollRuns (TenantId, Status) INCLUDE (PayGroupId, PayPeriodId);

CREATE TABLE Payslips (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayrollRunId          UNIQUEIDENTIFIER NOT NULL REFERENCES PayrollRuns(Id),
    EmployeeId            UNIQUEIDENTIFIER NOT NULL REFERENCES PayrollEmployees(EmployeeId),
    PayslipNumber         NVARCHAR(30)     NOT NULL,

    -- Snapshot (baad mein employee ka naam/dept badle, purani payslip na badle)
    EmployeeCode          NVARCHAR(20)     NOT NULL,
    EmployeeName          NVARCHAR(300)    NOT NULL,
    DepartmentName        NVARCHAR(150)    NULL,
    DesignationTitle      NVARCHAR(150)    NULL,
    BankAccountMasked     NVARCHAR(50)     NULL,                 -- ****1234
    CurrencyCode          CHAR(3)          NOT NULL,
    PeriodStart           DATE             NOT NULL,
    PeriodEnd             DATE             NOT NULL,

    -- Proration
    PeriodDays            DECIMAL(5,2)     NOT NULL,             -- method ke hisaab se (calendar/working/30)
    PayableDays           DECIMAL(5,2)     NOT NULL,             -- joining/exit/unpaid leave ke baad
    UnpaidLeaveDays       DECIMAL(5,2)     NOT NULL DEFAULT 0,

    GrossEarnings         DECIMAL(18,2)    NOT NULL,
    TotalDeductions       DECIMAL(18,2)    NOT NULL,
    TaxAmount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    NetPay                DECIMAL(18,2)    NOT NULL,
    EmployerContributions DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TaxableIncome         DECIMAL(18,2)    NOT NULL DEFAULT 0,   -- YTD annualized tax ke liye

    Status                TINYINT          NOT NULL,             -- 1 Calculated, 2 OnHold, 3 Paid
    CalculatedAt          DATETIME2(3)     NOT NULL,
    -- AUDIT
    CONSTRAINT CK_Payslips_Net CHECK (NetPay = GrossEarnings - TotalDeductions)
);
-- ⭐ Ek run mein ek employee ki ek payslip — retry continue karta hai, duplicate nahi banata
CREATE UNIQUE INDEX UX_Payslips_RunEmployee ON Payslips (PayrollRunId, EmployeeId) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Payslips_Number ON Payslips (TenantId, PayslipNumber);
CREATE INDEX IX_Payslips_EmployeeHistory ON Payslips (EmployeeId, PeriodStart DESC) INCLUDE (NetPay, Status);

-- Recalculation (run Approved se pehle) pe lines poori replace hoti hain
CREATE TABLE PayslipLines (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    PayslipId             UNIQUEIDENTIFIER NOT NULL REFERENCES Payslips(Id) ON DELETE CASCADE,
    PayComponentId        UNIQUEIDENTIFIER NOT NULL REFERENCES PayComponents(Id),
    ComponentCode         NVARCHAR(30)     NOT NULL,             -- snapshot
    ComponentName         NVARCHAR(100)    NOT NULL,             -- snapshot
    ComponentType         TINYINT          NOT NULL,
    Amount                DECIMAL(18,2)    NOT NULL,
    Quantity              DECIMAL(9,2)     NULL,
    Rate                  DECIMAL(18,4)    NULL,
    IsTaxable             BIT              NOT NULL,
    Source                TINYINT          NOT NULL,             -- 1 Template, 2 Override, 3 Input, 4 Proration, 5 Contribution, 6 Tax, 7 Loan
    SourceReference       UNIQUEIDENTIFIER NULL,                 -- LoanId / PayrollInputId
    SortOrder             SMALLINT         NOT NULL DEFAULT 0
);
CREATE INDEX IX_PayslipLines_Payslip ON PayslipLines (PayslipId);

-- ⭐ Ek installment ek payslip se ek hi dafa
CREATE TABLE LoanRepayments (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    EmployeeLoanId        UNIQUEIDENTIFIER NOT NULL REFERENCES EmployeeLoans(Id),
    PayslipId             UNIQUEIDENTIFIER NOT NULL REFERENCES Payslips(Id),
    Amount                DECIMAL(18,2)    NOT NULL,
    CONSTRAINT UQ_LoanRepayment UNIQUE (EmployeeLoanId, PayslipId),
    CONSTRAINT CK_LoanRepayment CHECK (Amount > 0)
);

/* ─────────────────────────────────── 11. PAYMENTS ───────────────────────────────── */

-- Bank file (generic CSV ab; WPS SIF / bank-specific formats baad mein)
CREATE TABLE PaymentBatches (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayrollRunId          UNIQUEIDENTIFIER NOT NULL REFERENCES PayrollRuns(Id),
    FileFormat            TINYINT          NOT NULL,             -- 1 GenericCsv, 2 WpsSif, ...
    FileStorageKey        NVARCHAR(500)    NULL,                 -- Blob (private)
    TotalAmount           DECIMAL(18,2)    NOT NULL,
    PaymentCount          INT              NOT NULL,
    GeneratedAt           DATETIME2(3)     NOT NULL,
    -- AUDIT
);

CREATE TABLE Payments (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayslipId             UNIQUEIDENTIFIER NOT NULL REFERENCES Payslips(Id),
    PaymentBatchId        UNIQUEIDENTIFIER NULL REFERENCES PaymentBatches(Id),
    Method                TINYINT          NOT NULL,             -- 1 BankTransfer, 2 Cash, 3 Cheque
    Amount                DECIMAL(18,2)    NOT NULL,
    CurrencyCode          CHAR(3)          NOT NULL,
    Status                TINYINT          NOT NULL,             -- 1 Pending, 2 Paid, 3 Failed, 4 Reversed
    Reference             NVARCHAR(100)    NULL,                 -- bank txn ref / cheque no
    PaidAt                DATETIME2(3)     NULL,
    -- AUDIT
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0)
);
-- ⭐ Ek payslip ka ek hi LIVE payment (Failed/Reversed ke baad naya ban sakta hai)
CREATE UNIQUE INDEX UX_Payments_Payslip ON Payments (PayslipId) WHERE Status IN (1, 2) AND IsDeleted = 0;

/* =====================================================================================
   CALCULATION ORDER (per employee, per run) — engine isi tarteeb se chalega:
     1. Current EmployeeSalary (period mein effective) + Template lines + employee overrides
     2. Period amount: Annual ÷ (12 | 24 | 26 | 52), Monthly × (1 | ½ | 12/26 | 12/52), Hourly × input hours
     3. Proration (IsProrated components): joining / exit / unpaid leave days
     4. PayrollInputs (OT, bonus, manual)
     5. ContributionRules → employee deduction + employer contribution (ceiling ke saath)
     6. Tax: regime (country + date) → Annualized = (YTD taxable + projected) → slab → minus YTD tax paid
     7. Loan installments (min(installment, outstanding))
     8. Net = Gross − Deductions  (negative ho to payslip OnHold + warning)
   ===================================================================================== */