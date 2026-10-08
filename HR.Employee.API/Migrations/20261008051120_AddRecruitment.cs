using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candidates",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrentCompany = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CurrentTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ExperienceYears = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
                    ReferredByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResumeUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                    table.CheckConstraint("CK_Candidates_Experience", "\"ExperienceYears\" IS NULL OR \"ExperienceYears\" BETWEEN 0 AND 60");
                    table.CheckConstraint("CK_Candidates_Referral", "\"ReferredByEmployeeId\" IS NULL OR \"Source\" = 2");
                    table.CheckConstraint("CK_Candidates_Source", "\"Source\" BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_Candidates_Employees_ReferredByEmployeeId",
                        column: x => x.ReferredByEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobOpenings",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DesignationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    HiringManagerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmploymentType = table.Column<byte>(type: "smallint", nullable: false),
                    Openings = table.Column<short>(type: "smallint", nullable: false),
                    Reason = table.Column<byte>(type: "smallint", nullable: false),
                    ReplacesEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SalaryMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Requirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    CloseNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_JobOpenings", x => x.Id);
                    table.CheckConstraint("CK_JobOpenings_EmploymentType", "\"EmploymentType\" IN (1, 2, 4, 8)");
                    table.CheckConstraint("CK_JobOpenings_Openings", "\"Openings\" BETWEEN 1 AND 500");
                    table.CheckConstraint("CK_JobOpenings_Reason", "\"Reason\" BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_JobOpenings_Salary", "(\"SalaryMin\" IS NULL OR \"SalaryMin\" >= 0) AND (\"SalaryMax\" IS NULL OR \"SalaryMax\" >= 0) AND (\"SalaryMin\" IS NULL OR \"SalaryMax\" IS NULL OR \"SalaryMax\" >= \"SalaryMin\")");
                    table.CheckConstraint("CK_JobOpenings_Status", "\"Status\" BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_JobOpenings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "employee",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOpenings_Designations_DesignationId",
                        column: x => x.DesignationId,
                        principalSchema: "employee",
                        principalTable: "Designations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOpenings_Employees_HiringManagerEmployeeId",
                        column: x => x.HiringManagerEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOpenings_Employees_ReplacesEmployeeId",
                        column: x => x.ReplacesEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOpenings_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "employee",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobOpeningId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<byte>(type: "smallint", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    StageChangedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    Rating = table.Column<byte>(type: "smallint", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OfferSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OfferStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OfferExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    OfferStatus = table.Column<byte>(type: "smallint", nullable: true),
                    OfferedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    HiredAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    HiredEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.CheckConstraint("CK_JobApplications_Hired", "(\"Stage\" = 5) = (\"HiredEmployeeId\" IS NOT NULL AND \"HiredAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_JobApplications_OfferSalary", "\"OfferSalary\" IS NULL OR \"OfferSalary\" >= 0");
                    table.CheckConstraint("CK_JobApplications_OfferStatus", "\"OfferStatus\" IS NULL OR \"OfferStatus\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_JobApplications_Rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_JobApplications_Stage", "\"Stage\" BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_JobApplications_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "employee",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_Employees_HiredEmployeeId",
                        column: x => x.HiredEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_JobOpenings_JobOpeningId",
                        column: x => x.JobOpeningId,
                        principalSchema: "employee",
                        principalTable: "JobOpenings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationEvents",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    FromStage = table.Column<byte>(type: "smallint", nullable: true),
                    ToStage = table.Column<byte>(type: "smallint", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationEvents", x => x.Id);
                    table.CheckConstraint("CK_ApplicationEvents_Kind", "\"Kind\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_ApplicationEvents_Stage", "\"ToStage\" BETWEEN 1 AND 7 AND (\"FromStage\" IS NULL OR \"FromStage\" BETWEEN 1 AND 7)");
                    table.ForeignKey(
                        name: "FK_ApplicationEvents_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalSchema: "employee",
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Interviews",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    DurationMinutes = table.Column<short>(type: "smallint", nullable: false),
                    Mode = table.Column<byte>(type: "smallint", nullable: false),
                    LocationOrLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InterviewerEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Rating = table.Column<byte>(type: "smallint", nullable: true),
                    Recommendation = table.Column<byte>(type: "smallint", nullable: true),
                    Feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FeedbackAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_Interviews", x => x.Id);
                    table.CheckConstraint("CK_Interviews_Duration", "\"DurationMinutes\" BETWEEN 5 AND 480");
                    table.CheckConstraint("CK_Interviews_Mode", "\"Mode\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_Interviews_Rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Interviews_Recommendation", "\"Recommendation\" IS NULL OR \"Recommendation\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_Interviews_Status", "\"Status\" BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_Interviews_Employees_InterviewerEmployeeId",
                        column: x => x.InterviewerEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Interviews_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalSchema: "employee",
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_JobApplicationId_At",
                schema: "employee",
                table: "ApplicationEvents",
                columns: new[] { "JobApplicationId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_TenantId_At",
                schema: "employee",
                table: "ApplicationEvents",
                columns: new[] { "TenantId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_ReferredByEmployeeId",
                schema: "employee",
                table: "Candidates",
                column: "ReferredByEmployeeId",
                filter: "\"ReferredByEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Candidates_TenantId_Email",
                schema: "employee",
                table: "Candidates",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_InterviewerEmployeeId",
                schema: "employee",
                table: "Interviews",
                column: "InterviewerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_JobApplicationId",
                schema: "employee",
                table: "Interviews",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_TenantId_InterviewerEmployeeId_ScheduledAt",
                schema: "employee",
                table: "Interviews",
                columns: new[] { "TenantId", "InterviewerEmployeeId", "ScheduledAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_TenantId_ScheduledAt",
                schema: "employee",
                table: "Interviews",
                columns: new[] { "TenantId", "ScheduledAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_CandidateId",
                schema: "employee",
                table: "JobApplications",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_HiredEmployeeId",
                schema: "employee",
                table: "JobApplications",
                column: "HiredEmployeeId",
                filter: "\"HiredEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobOpeningId",
                schema: "employee",
                table: "JobApplications",
                column: "JobOpeningId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_TenantId_Stage",
                schema: "employee",
                table: "JobApplications",
                columns: new[] { "TenantId", "Stage" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_JobApplications_Job_Candidate",
                schema: "employee",
                table: "JobApplications",
                columns: new[] { "TenantId", "JobOpeningId", "CandidateId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_JobOpenings_DepartmentId",
                schema: "employee",
                table: "JobOpenings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOpenings_DesignationId",
                schema: "employee",
                table: "JobOpenings",
                column: "DesignationId",
                filter: "\"DesignationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobOpenings_HiringManagerEmployeeId",
                schema: "employee",
                table: "JobOpenings",
                column: "HiringManagerEmployeeId",
                filter: "\"HiringManagerEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobOpenings_LocationId",
                schema: "employee",
                table: "JobOpenings",
                column: "LocationId",
                filter: "\"LocationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobOpenings_ReplacesEmployeeId",
                schema: "employee",
                table: "JobOpenings",
                column: "ReplacesEmployeeId",
                filter: "\"ReplacesEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobOpenings_TenantId_Code",
                schema: "employee",
                table: "JobOpenings",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_JobOpenings_TenantId_Status",
                schema: "employee",
                table: "JobOpenings",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = false");

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa)
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_JobOpenings_TenantId_Id"" ON employee.""JobOpenings"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Candidates_TenantId_Id"" ON employee.""Candidates"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_JobApplications_TenantId_Id"" ON employee.""JobApplications"" (""TenantId"", ""Id"");
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JobOpenings_DepartmentId_Tenant' AND conrelid = 'employee.""JobOpenings""'::regclass) THEN
  ALTER TABLE employee.""JobOpenings"" ADD CONSTRAINT ""FK_JobOpenings_DepartmentId_Tenant"" FOREIGN KEY (""TenantId"", ""DepartmentId"") REFERENCES employee.""Departments"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JobOpenings_DesignationId_Tenant' AND conrelid = 'employee.""JobOpenings""'::regclass) THEN
  ALTER TABLE employee.""JobOpenings"" ADD CONSTRAINT ""FK_JobOpenings_DesignationId_Tenant"" FOREIGN KEY (""TenantId"", ""DesignationId"") REFERENCES employee.""Designations"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JobOpenings_LocationId_Tenant' AND conrelid = 'employee.""JobOpenings""'::regclass) THEN
  ALTER TABLE employee.""JobOpenings"" ADD CONSTRAINT ""FK_JobOpenings_LocationId_Tenant"" FOREIGN KEY (""TenantId"", ""LocationId"") REFERENCES employee.""Locations"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JobOpenings_HiringManagerEmployeeId_Tenant' AND conrelid = 'employee.""JobOpenings""'::regclass) THEN
  ALTER TABLE employee.""JobOpenings"" ADD CONSTRAINT ""FK_JobOpenings_HiringManagerEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""HiringManagerEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JobOpenings_ReplacesEmployeeId_Tenant' AND conrelid = 'employee.""JobOpenings""'::regclass) THEN
  ALTER TABLE employee.""JobOpenings"" ADD CONSTRAINT ""FK_JobOpenings_ReplacesEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""ReplacesEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Candidates_ReferredByEmployeeId_Tenant' AND conrelid = 'employee.""Candidates""'::regclass) THEN
  ALTER TABLE employee.""Candidates"" ADD CONSTRAINT ""FK_Candidates_ReferredByEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""ReferredByEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JobApplications_JobOpeningId_Tenant' AND conrelid = 'employee.""JobApplications""'::regclass) THEN
  ALTER TABLE employee.""JobApplications"" ADD CONSTRAINT ""FK_JobApplications_JobOpeningId_Tenant"" FOREIGN KEY (""TenantId"", ""JobOpeningId"") REFERENCES employee.""JobOpenings"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JobApplications_CandidateId_Tenant' AND conrelid = 'employee.""JobApplications""'::regclass) THEN
  ALTER TABLE employee.""JobApplications"" ADD CONSTRAINT ""FK_JobApplications_CandidateId_Tenant"" FOREIGN KEY (""TenantId"", ""CandidateId"") REFERENCES employee.""Candidates"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JobApplications_HiredEmployeeId_Tenant' AND conrelid = 'employee.""JobApplications""'::regclass) THEN
  ALTER TABLE employee.""JobApplications"" ADD CONSTRAINT ""FK_JobApplications_HiredEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""HiredEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ApplicationEvents_JobApplicationId_Tenant' AND conrelid = 'employee.""ApplicationEvents""'::regclass) THEN
  ALTER TABLE employee.""ApplicationEvents"" ADD CONSTRAINT ""FK_ApplicationEvents_JobApplicationId_Tenant"" FOREIGN KEY (""TenantId"", ""JobApplicationId"") REFERENCES employee.""JobApplications"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Interviews_JobApplicationId_Tenant' AND conrelid = 'employee.""Interviews""'::regclass) THEN
  ALTER TABLE employee.""Interviews"" ADD CONSTRAINT ""FK_Interviews_JobApplicationId_Tenant"" FOREIGN KEY (""TenantId"", ""JobApplicationId"") REFERENCES employee.""JobApplications"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Interviews_InterviewerEmployeeId_Tenant' AND conrelid = 'employee.""Interviews""'::regclass) THEN
  ALTER TABLE employee.""Interviews"" ADD CONSTRAINT ""FK_Interviews_InterviewerEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""InterviewerEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON employee.""JobOpenings"", employee.""Candidates"", employee.""JobApplications"", employee.""ApplicationEvents"", employee.""Interviews"" TO hr_employee_app;
ALTER TABLE employee.""JobOpenings"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""JobOpenings"";
CREATE POLICY tenant_isolation ON employee.""JobOpenings"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Candidates"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Candidates"";
CREATE POLICY tenant_isolation ON employee.""Candidates"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""JobApplications"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""JobApplications"";
CREATE POLICY tenant_isolation ON employee.""JobApplications"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""ApplicationEvents"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""ApplicationEvents"";
CREATE POLICY tenant_isolation ON employee.""ApplicationEvents"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Interviews"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Interviews"";
CREATE POLICY tenant_isolation ON employee.""Interviews"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationEvents",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Interviews",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "JobApplications",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Candidates",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "JobOpenings",
                schema: "employee");
        }
    }
}
