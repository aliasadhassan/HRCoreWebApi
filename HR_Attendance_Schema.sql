/* =====================================================================================
   HR Cloud — Attendance / Time tracking / Shifts  (Postgres, schema "attendance")
   -------------------------------------------------------------------------------------
   Generated from EF migrations (HR.Employee.API): UseXminRowVersion + AddAttendanceSchema.
   Idempotent: Supabase SQL editor mein dobara chalane se kuch nahi tootega.
     dotnet ef migrations script InitialPostgresMigration AddAttendanceSchema --idempotent

   Faisle:
   - Attendance HR.Employee.API ke andar, lekin apne "attendance" schema mein. Leave, holiday,
     location aur employee ek hi DB transaction mein mil jate hain; kabhi HR.Attendance.API
     alag karni ho to tables pehle se alag hain.
   - Ek page = ek group of tables (sidebar consolidation ke saath):
       Attendance page        Timesheet tab  -> AttendanceDays + AttendancePunches
                              Roster tab     -> ShiftAssignments + RosterEntries
                              Overtime tab   -> AttendanceRequests (Type = 4 Overtime)
                              Requests tab   -> AttendanceRequests (Correction / WFH / On duty)
       Attendance setup page  Shifts tab     -> Shifts
                              Policies tab   -> AttendancePolicies (day rules, clock-in, geofence, overtime)
                              Devices tab    -> AttendanceDevices (biometric)
   - Enums smallint mein (API pe string): AttendanceStatus 1 Present, 2 Absent, 3 HalfDay,
     4 OnLeave, 5 Holiday, 6 WeeklyOff, 7 Incomplete | DayType 1 Workday, 2 WeeklyOff, 3 Holiday |
     AttendanceRequestType 1 Correction, 2 WorkFromHome, 3 OnDuty, 4 Overtime |
     AttendanceRequestStatus 1 Pending, 2 Approved, 3 Rejected, 4 Cancelled.
   - Timestamps UTC (timestamptz); WorkDate local date. Raat ki shift: EndTime <= StartTime.
   - Concurrency: Postgres xmin (purana bytea RowVersion har INSERT pe NOT NULL se fail hota tha).
   ===================================================================================== */

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."Departments" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."Designations" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."EmployeeDocuments" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."EmployeeJobHistory" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."Employees" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."Holidays" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."LeaveApprovalSettings" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."LeaveBalances" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."LeavePolicies" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."LeaveRequests" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."LeaveTypes" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    ALTER TABLE employee."Locations" DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101133_UseXminRowVersion') THEN
    INSERT INTO employee."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004101133_UseXminRowVersion', '8.0.26');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'attendance') THEN
            CREATE SCHEMA attendance;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE TABLE attendance."AttendanceDevices" (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "SerialNumber" character varying(50) NOT NULL,
        "Vendor" character varying(50),
        "LocationId" uuid NOT NULL,
        "ApiKeyHash" character varying(128),
        "LastSyncedAt" timestamp(3) with time zone,
        "IsActive" boolean NOT NULL,
        "TenantId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        CONSTRAINT "PK_AttendanceDevices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AttendanceDevices_Locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES employee."Locations" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE TABLE attendance."AttendancePolicies" (
        "Id" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "LocationId" uuid,
        "IsActive" boolean NOT NULL,
        "FullDayMinutes" smallint NOT NULL,
        "HalfDayMinutes" smallint NOT NULL,
        "LatesPerHalfDay" smallint,
        "AllowedMethods" smallint NOT NULL,
        "RequireGeofence" boolean NOT NULL,
        "GeoLatitude" numeric(9,6),
        "GeoLongitude" numeric(9,6),
        "GeoRadiusMeters" smallint,
        "RequestApprover" smallint NOT NULL,
        "CorrectionWindowDays" smallint NOT NULL,
        "MaxCorrectionsPerMonth" smallint,
        "OvertimeEnabled" boolean NOT NULL,
        "OvertimeMinMinutes" smallint NOT NULL,
        "OvertimeMaxMinutesPerDay" smallint,
        "OvertimeRequiresApproval" boolean NOT NULL,
        "OvertimeRateWorkday" numeric(4,2) NOT NULL,
        "OvertimeRateWeeklyOff" numeric(4,2) NOT NULL,
        "OvertimeRateHoliday" numeric(4,2) NOT NULL,
        "TenantId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        CONSTRAINT "PK_AttendancePolicies" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_AP_DayMinutes" CHECK ("HalfDayMinutes" > 0 AND "HalfDayMinutes" < "FullDayMinutes"),
        CONSTRAINT "CK_AP_Geofence" CHECK ("RequireGeofence" = false OR ("GeoLatitude" IS NOT NULL AND "GeoLongitude" IS NOT NULL AND "GeoRadiusMeters" > 0)),
        CONSTRAINT "CK_AP_OtRates" CHECK ("OvertimeRateWorkday" >= 1 AND "OvertimeRateWeeklyOff" >= 1 AND "OvertimeRateHoliday" >= 1),
        CONSTRAINT "FK_AttendancePolicies_Locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES employee."Locations" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE TABLE attendance."Shifts" (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Code" character varying(20) NOT NULL,
        "Color" character(7),
        "StartTime" time without time zone NOT NULL,
        "EndTime" time without time zone NOT NULL,
        "BreakMinutes" smallint NOT NULL,
        "GraceInMinutes" smallint NOT NULL,
        "GraceOutMinutes" smallint NOT NULL,
        "IsFlexible" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "TenantId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        CONSTRAINT "PK_Shifts" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Shift_Break" CHECK ("BreakMinutes" >= 0),
        CONSTRAINT "CK_Shift_Times" CHECK ("StartTime" <> "EndTime")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE TABLE attendance."AttendanceDays" (
        "Id" uuid NOT NULL,
        "EmployeeId" uuid NOT NULL,
        "WorkDate" date NOT NULL,
        "DayType" smallint NOT NULL,
        "ShiftId" uuid,
        "ScheduledStart" timestamp(3) with time zone,
        "ScheduledEnd" timestamp(3) with time zone,
        "FirstIn" timestamp(3) with time zone,
        "LastOut" timestamp(3) with time zone,
        "WorkedMinutes" smallint NOT NULL,
        "LateMinutes" smallint NOT NULL,
        "EarlyLeaveMinutes" smallint NOT NULL,
        "OvertimeMinutes" smallint NOT NULL,
        "Status" smallint NOT NULL,
        "LeaveRequestId" uuid,
        "IsManuallyEdited" boolean NOT NULL,
        "Remarks" character varying(500),
        "TenantId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        CONSTRAINT "PK_AttendanceDays" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_AD_InOut" CHECK ("FirstIn" IS NULL OR "LastOut" IS NULL OR "LastOut" >= "FirstIn"),
        CONSTRAINT "CK_AD_Minutes" CHECK ("WorkedMinutes" >= 0 AND "LateMinutes" >= 0 AND "EarlyLeaveMinutes" >= 0 AND "OvertimeMinutes" >= 0),
        CONSTRAINT "CK_AD_Schedule" CHECK ("ScheduledStart" IS NULL OR "ScheduledEnd" IS NULL OR "ScheduledEnd" > "ScheduledStart"),
        CONSTRAINT "FK_AttendanceDays_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES employee."Employees" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AttendanceDays_LeaveRequests_LeaveRequestId" FOREIGN KEY ("LeaveRequestId") REFERENCES employee."LeaveRequests" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AttendanceDays_Shifts_ShiftId" FOREIGN KEY ("ShiftId") REFERENCES attendance."Shifts" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE TABLE attendance."RosterEntries" (
        "Id" uuid NOT NULL,
        "EmployeeId" uuid NOT NULL,
        "WorkDate" date NOT NULL,
        "ShiftId" uuid,
        "Note" character varying(200),
        "TenantId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        CONSTRAINT "PK_RosterEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RosterEntries_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES employee."Employees" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RosterEntries_Shifts_ShiftId" FOREIGN KEY ("ShiftId") REFERENCES attendance."Shifts" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE TABLE attendance."ShiftAssignments" (
        "Id" uuid NOT NULL,
        "EmployeeId" uuid NOT NULL,
        "ShiftId" uuid NOT NULL,
        "EffectiveFrom" date NOT NULL,
        "EffectiveTo" date,
        "WeeklyOffDays" smallint,
        "TenantId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        CONSTRAINT "PK_ShiftAssignments" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_SA_Dates" CHECK ("EffectiveTo" IS NULL OR "EffectiveTo" >= "EffectiveFrom"),
        CONSTRAINT "CK_SA_OffDays" CHECK ("WeeklyOffDays" IS NULL OR "WeeklyOffDays" BETWEEN 0 AND 127),
        CONSTRAINT "FK_ShiftAssignments_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES employee."Employees" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ShiftAssignments_Shifts_ShiftId" FOREIGN KEY ("ShiftId") REFERENCES attendance."Shifts" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE TABLE attendance."AttendancePunches" (
        "Id" uuid NOT NULL,
        "AttendanceDayId" uuid NOT NULL,
        "PunchedAt" timestamp(3) with time zone NOT NULL,
        "Direction" smallint NOT NULL,
        "Source" smallint NOT NULL,
        "DeviceId" uuid,
        "Latitude" numeric(9,6),
        "Longitude" numeric(9,6),
        "IpAddress" character varying(45),
        "IsIgnored" boolean NOT NULL,
        "Note" character varying(200),
        "RecordedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_AttendancePunches" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AttendancePunches_AttendanceDays_AttendanceDayId" FOREIGN KEY ("AttendanceDayId") REFERENCES attendance."AttendanceDays" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AttendancePunches_AttendanceDevices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES attendance."AttendanceDevices" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE TABLE attendance."AttendanceRequests" (
        "Id" uuid NOT NULL,
        "EmployeeId" uuid NOT NULL,
        "WorkDate" date NOT NULL,
        "Type" smallint NOT NULL,
        "AttendanceDayId" uuid,
        "RequestedIn" timestamp(3) with time zone,
        "RequestedOut" timestamp(3) with time zone,
        "OvertimeMinutes" smallint,
        "ApprovedMinutes" smallint,
        "OvertimeRate" numeric(4,2),
        "Reason" character varying(1000),
        "Status" smallint NOT NULL,
        "ApproverType" smallint NOT NULL,
        "AssignedApproverId" uuid,
        "DecidedByEmployeeId" uuid,
        "DecidedAt" timestamp(3) with time zone,
        "DecisionComment" character varying(500),
        "TenantId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        CONSTRAINT "PK_AttendanceRequests" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_AR_Approved" CHECK ("ApprovedMinutes" IS NULL OR ("ApprovedMinutes" > 0 AND "ApprovedMinutes" <= "OvertimeMinutes")),
        CONSTRAINT "CK_AR_InOut" CHECK ("RequestedIn" IS NULL OR "RequestedOut" IS NULL OR "RequestedOut" > "RequestedIn"),
        CONSTRAINT "CK_AR_Shape" CHECK (("Type" = 4 AND "OvertimeMinutes" > 0 AND "RequestedIn" IS NULL AND "RequestedOut" IS NULL) OR ("Type" <> 4 AND "OvertimeMinutes" IS NULL AND ("RequestedIn" IS NOT NULL OR "RequestedOut" IS NOT NULL))),
        CONSTRAINT "FK_AttendanceRequests_AttendanceDays_AttendanceDayId" FOREIGN KEY ("AttendanceDayId") REFERENCES attendance."AttendanceDays" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AttendanceRequests_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES employee."Employees" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE UNIQUE INDEX "IX_AttendanceDays_EmployeeId_WorkDate" ON attendance."AttendanceDays" ("EmployeeId", "WorkDate") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendanceDays_LeaveRequestId" ON attendance."AttendanceDays" ("LeaveRequestId") WHERE "LeaveRequestId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendanceDays_ShiftId" ON attendance."AttendanceDays" ("ShiftId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendanceDays_TenantId_WorkDate" ON attendance."AttendanceDays" ("TenantId", "WorkDate") INCLUDE ("EmployeeId", "Status") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendanceDevices_LocationId" ON attendance."AttendanceDevices" ("LocationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE UNIQUE INDEX "IX_AttendanceDevices_TenantId_SerialNumber" ON attendance."AttendanceDevices" ("TenantId", "SerialNumber") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendancePolicies_LocationId" ON attendance."AttendancePolicies" ("LocationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE UNIQUE INDEX "IX_AttendancePolicies_TenantId_LocationId" ON attendance."AttendancePolicies" ("TenantId", "LocationId") NULLS NOT DISTINCT WHERE "IsActive" = true AND "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE UNIQUE INDEX "IX_AttendancePunches_AttendanceDayId_PunchedAt" ON attendance."AttendancePunches" ("AttendanceDayId", "PunchedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendancePunches_DeviceId" ON attendance."AttendancePunches" ("DeviceId") WHERE "DeviceId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendanceRequests_AssignedApproverId" ON attendance."AttendanceRequests" ("AssignedApproverId") WHERE "Status" = 1 AND "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendanceRequests_AttendanceDayId" ON attendance."AttendanceRequests" ("AttendanceDayId") WHERE "AttendanceDayId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendanceRequests_EmployeeId_WorkDate" ON attendance."AttendanceRequests" ("EmployeeId", "WorkDate" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE UNIQUE INDEX "IX_AttendanceRequests_EmployeeId_WorkDate_Type" ON attendance."AttendanceRequests" ("EmployeeId", "WorkDate", "Type") WHERE "Status" IN (1, 2) AND "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_AttendanceRequests_TenantId_Type_Status" ON attendance."AttendanceRequests" ("TenantId", "Type", "Status") INCLUDE ("EmployeeId", "WorkDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE UNIQUE INDEX "IX_RosterEntries_EmployeeId_WorkDate" ON attendance."RosterEntries" ("EmployeeId", "WorkDate") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_RosterEntries_ShiftId" ON attendance."RosterEntries" ("ShiftId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_RosterEntries_TenantId_WorkDate" ON attendance."RosterEntries" ("TenantId", "WorkDate") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_ShiftAssignments_EmployeeId_EffectiveFrom" ON attendance."ShiftAssignments" ("EmployeeId", "EffectiveFrom" DESC) WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE INDEX "IX_ShiftAssignments_ShiftId" ON attendance."ShiftAssignments" ("ShiftId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    CREATE UNIQUE INDEX "IX_Shifts_TenantId_Code" ON attendance."Shifts" ("TenantId", "Code") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM employee."__EFMigrationsHistory" WHERE "MigrationId" = '20261004101236_AddAttendanceSchema') THEN
    INSERT INTO employee."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004101236_AddAttendanceSchema', '8.0.26');
    END IF;
END $EF$;
COMMIT;

