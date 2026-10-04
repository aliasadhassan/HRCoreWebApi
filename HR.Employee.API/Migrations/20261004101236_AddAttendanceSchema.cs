using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "attendance");

            migrationBuilder.CreateTable(
                name: "AttendanceDevices",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyHash = table.Column<string>(type: "character varying(128)", unicode: false, maxLength: 128, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDevices_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "employee",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePolicies",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FullDayMinutes = table.Column<short>(type: "smallint", nullable: false),
                    HalfDayMinutes = table.Column<short>(type: "smallint", nullable: false),
                    LatesPerHalfDay = table.Column<byte>(type: "smallint", nullable: true),
                    AllowedMethods = table.Column<byte>(type: "smallint", nullable: false),
                    RequireGeofence = table.Column<bool>(type: "boolean", nullable: false),
                    GeoLatitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    GeoLongitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    GeoRadiusMeters = table.Column<short>(type: "smallint", nullable: true),
                    RequestApprover = table.Column<byte>(type: "smallint", nullable: false),
                    CorrectionWindowDays = table.Column<byte>(type: "smallint", nullable: false),
                    MaxCorrectionsPerMonth = table.Column<byte>(type: "smallint", nullable: true),
                    OvertimeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OvertimeMinMinutes = table.Column<short>(type: "smallint", nullable: false),
                    OvertimeMaxMinutesPerDay = table.Column<short>(type: "smallint", nullable: true),
                    OvertimeRequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    OvertimeRateWorkday = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    OvertimeRateWeeklyOff = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    OvertimeRateHoliday = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePolicies", x => x.Id);
                    table.CheckConstraint("CK_AP_DayMinutes", "\"HalfDayMinutes\" > 0 AND \"HalfDayMinutes\" < \"FullDayMinutes\"");
                    table.CheckConstraint("CK_AP_Geofence", "\"RequireGeofence\" = false OR (\"GeoLatitude\" IS NOT NULL AND \"GeoLongitude\" IS NOT NULL AND \"GeoRadiusMeters\" > 0)");
                    table.CheckConstraint("CK_AP_OtRates", "\"OvertimeRateWorkday\" >= 1 AND \"OvertimeRateWeeklyOff\" >= 1 AND \"OvertimeRateHoliday\" >= 1");
                    table.ForeignKey(
                        name: "FK_AttendancePolicies_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "employee",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Color = table.Column<string>(type: "character(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BreakMinutes = table.Column<short>(type: "smallint", nullable: false),
                    GraceInMinutes = table.Column<byte>(type: "smallint", nullable: false),
                    GraceOutMinutes = table.Column<byte>(type: "smallint", nullable: false),
                    IsFlexible = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.CheckConstraint("CK_Shift_Break", "\"BreakMinutes\" >= 0");
                    table.CheckConstraint("CK_Shift_Times", "\"StartTime\" <> \"EndTime\"");
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDays",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DayType = table.Column<byte>(type: "smallint", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledStart = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ScheduledEnd = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    FirstIn = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    LastOut = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    WorkedMinutes = table.Column<short>(type: "smallint", nullable: false),
                    LateMinutes = table.Column<short>(type: "smallint", nullable: false),
                    EarlyLeaveMinutes = table.Column<short>(type: "smallint", nullable: false),
                    OvertimeMinutes = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    LeaveRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsManuallyEdited = table.Column<bool>(type: "boolean", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDays", x => x.Id);
                    table.CheckConstraint("CK_AD_InOut", "\"FirstIn\" IS NULL OR \"LastOut\" IS NULL OR \"LastOut\" >= \"FirstIn\"");
                    table.CheckConstraint("CK_AD_Minutes", "\"WorkedMinutes\" >= 0 AND \"LateMinutes\" >= 0 AND \"EarlyLeaveMinutes\" >= 0 AND \"OvertimeMinutes\" >= 0");
                    table.CheckConstraint("CK_AD_Schedule", "\"ScheduledStart\" IS NULL OR \"ScheduledEnd\" IS NULL OR \"ScheduledEnd\" > \"ScheduledStart\"");
                    table.ForeignKey(
                        name: "FK_AttendanceDays_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDays_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalSchema: "employee",
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDays_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "attendance",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterEntries",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterEntries_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "attendance",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    WeeklyOffDays = table.Column<byte>(type: "smallint", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.CheckConstraint("CK_SA_Dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.CheckConstraint("CK_SA_OffDays", "\"WeeklyOffDays\" IS NULL OR \"WeeklyOffDays\" BETWEEN 0 AND 127");
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "attendance",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePunches",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    PunchedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    Direction = table.Column<byte>(type: "smallint", nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", unicode: false, maxLength: 45, nullable: true),
                    IsIgnored = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePunches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendancePunches_AttendanceDays_AttendanceDayId",
                        column: x => x.AttendanceDayId,
                        principalSchema: "attendance",
                        principalTable: "AttendanceDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendancePunches_AttendanceDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "attendance",
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRequests",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    AttendanceDayId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedIn = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    RequestedOut = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    OvertimeMinutes = table.Column<short>(type: "smallint", nullable: true),
                    ApprovedMinutes = table.Column<short>(type: "smallint", nullable: true),
                    OvertimeRate = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    ApproverType = table.Column<byte>(type: "smallint", nullable: false),
                    AssignedApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRequests", x => x.Id);
                    table.CheckConstraint("CK_AR_Approved", "\"ApprovedMinutes\" IS NULL OR (\"ApprovedMinutes\" > 0 AND \"ApprovedMinutes\" <= \"OvertimeMinutes\")");
                    table.CheckConstraint("CK_AR_InOut", "\"RequestedIn\" IS NULL OR \"RequestedOut\" IS NULL OR \"RequestedOut\" > \"RequestedIn\"");
                    table.CheckConstraint("CK_AR_Shape", "(\"Type\" = 4 AND \"OvertimeMinutes\" > 0 AND \"RequestedIn\" IS NULL AND \"RequestedOut\" IS NULL) OR (\"Type\" <> 4 AND \"OvertimeMinutes\" IS NULL AND (\"RequestedIn\" IS NOT NULL OR \"RequestedOut\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_AttendanceRequests_AttendanceDays_AttendanceDayId",
                        column: x => x.AttendanceDayId,
                        principalSchema: "attendance",
                        principalTable: "AttendanceDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_EmployeeId_WorkDate",
                schema: "attendance",
                table: "AttendanceDays",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_LeaveRequestId",
                schema: "attendance",
                table: "AttendanceDays",
                column: "LeaveRequestId",
                filter: "\"LeaveRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_ShiftId",
                schema: "attendance",
                table: "AttendanceDays",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_TenantId_WorkDate",
                schema: "attendance",
                table: "AttendanceDays",
                columns: new[] { "TenantId", "WorkDate" },
                filter: "\"IsDeleted\" = false")
                .Annotation("Npgsql:IndexInclude", new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_LocationId",
                schema: "attendance",
                table: "AttendanceDevices",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_TenantId_SerialNumber",
                schema: "attendance",
                table: "AttendanceDevices",
                columns: new[] { "TenantId", "SerialNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePolicies_LocationId",
                schema: "attendance",
                table: "AttendancePolicies",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePolicies_TenantId_LocationId",
                schema: "attendance",
                table: "AttendancePolicies",
                columns: new[] { "TenantId", "LocationId" },
                unique: true,
                filter: "\"IsActive\" = true AND \"IsDeleted\" = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_AttendanceDayId_PunchedAt",
                schema: "attendance",
                table: "AttendancePunches",
                columns: new[] { "AttendanceDayId", "PunchedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_DeviceId",
                schema: "attendance",
                table: "AttendancePunches",
                column: "DeviceId",
                filter: "\"DeviceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRequests_AssignedApproverId",
                schema: "attendance",
                table: "AttendanceRequests",
                column: "AssignedApproverId",
                filter: "\"Status\" = 1 AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRequests_AttendanceDayId",
                schema: "attendance",
                table: "AttendanceRequests",
                column: "AttendanceDayId",
                filter: "\"AttendanceDayId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRequests_EmployeeId_WorkDate",
                schema: "attendance",
                table: "AttendanceRequests",
                columns: new[] { "EmployeeId", "WorkDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRequests_EmployeeId_WorkDate_Type",
                schema: "attendance",
                table: "AttendanceRequests",
                columns: new[] { "EmployeeId", "WorkDate", "Type" },
                unique: true,
                filter: "\"Status\" IN (1, 2) AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRequests_TenantId_Type_Status",
                schema: "attendance",
                table: "AttendanceRequests",
                columns: new[] { "TenantId", "Type", "Status" })
                .Annotation("Npgsql:IndexInclude", new[] { "EmployeeId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_EmployeeId_WorkDate",
                schema: "attendance",
                table: "RosterEntries",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_ShiftId",
                schema: "attendance",
                table: "RosterEntries",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterEntries_TenantId_WorkDate",
                schema: "attendance",
                table: "RosterEntries",
                columns: new[] { "TenantId", "WorkDate" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_EmployeeId_EffectiveFrom",
                schema: "attendance",
                table: "ShiftAssignments",
                columns: new[] { "EmployeeId", "EffectiveFrom" },
                descending: new[] { false, true },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftId",
                schema: "attendance",
                table: "ShiftAssignments",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId_Code",
                schema: "attendance",
                table: "Shifts",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendancePolicies",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendancePunches",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendanceRequests",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "RosterEntries",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "ShiftAssignments",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendanceDevices",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendanceDays",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "Shifts",
                schema: "attendance");
        }
    }
}
