using System;
using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningGrowth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareerGoals",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetDesignationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    AchievedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerGoals", x => x.Id);
                    table.CheckConstraint("CK_CareerGoals_Achieved", "\"Status\" <> 2 OR \"AchievedOn\" IS NOT NULL");
                    table.CheckConstraint("CK_CareerGoals_Status", "\"Status\" BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_CareerGoals_Designations_TargetDesignationId",
                        column: x => x.TargetDesignationId,
                        principalSchema: "employee",
                        principalTable: "Designations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CareerGoals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.CheckConstraint("CK_Skills_Category", "\"Category\" BETWEEN 1 AND 7");
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    Format = table.Column<byte>(type: "smallint", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LinkUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DurationHours = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    SkillLevel = table.Column<byte>(type: "smallint", nullable: true),
                    AllowSelfEnrol = table.Column<bool>(type: "boolean", nullable: false),
                    CompleteWithinDays = table.Column<short>(type: "smallint", nullable: true),
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
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.CheckConstraint("CK_Courses_Category", "\"Category\" BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_Courses_CompleteWithin", "\"CompleteWithinDays\" IS NULL OR \"CompleteWithinDays\" BETWEEN 1 AND 365");
                    table.CheckConstraint("CK_Courses_Duration", "\"DurationHours\" IS NULL OR (\"DurationHours\" > 0 AND \"DurationHours\" <= 1000)");
                    table.CheckConstraint("CK_Courses_Format", "\"Format\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_Courses_SkillLevel", "\"SkillLevel\" IS NULL OR (\"SkillId\" IS NOT NULL AND \"SkillLevel\" BETWEEN 1 AND 5)");
                    table.ForeignKey(
                        name: "FK_Courses_Skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "employee",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSkills",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<byte>(type: "smallint", nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSkills", x => x.Id);
                    table.CheckConstraint("CK_EmployeeSkills_Level", "\"Level\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_EmployeeSkills_Source", "\"Source\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_EmployeeSkills_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "employee",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleSkills",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DesignationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredLevel = table.Column<byte>(type: "smallint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleSkills", x => x.Id);
                    table.CheckConstraint("CK_RoleSkills_Level", "\"RequiredLevel\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_RoleSkills_Designations_DesignationId",
                        column: x => x.DesignationId,
                        principalSchema: "employee",
                        principalTable: "Designations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "employee",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseEnrollments",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Progress = table.Column<byte>(type: "smallint", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Score = table.Column<byte>(type: "smallint", nullable: true),
                    Rating = table.Column<byte>(type: "smallint", nullable: true),
                    Feedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CertificateUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_CourseEnrollments", x => x.Id);
                    table.CheckConstraint("CK_CourseEnrollments_Completed", "\"Status\" <> 3 OR \"CompletedOn\" IS NOT NULL");
                    table.CheckConstraint("CK_CourseEnrollments_Progress", "\"Progress\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_CourseEnrollments_Rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_CourseEnrollments_Score", "\"Score\" IS NULL OR \"Score\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_CourseEnrollments_Source", "\"Source\" BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_CourseEnrollments_Status", "\"Status\" BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_CourseEnrollments_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "employee",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseEnrollments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerGoals_TargetDesignationId",
                schema: "employee",
                table: "CareerGoals",
                column: "TargetDesignationId");

            migrationBuilder.CreateIndex(
                name: "UX_CareerGoals_EmployeeId",
                schema: "employee",
                table: "CareerGoals",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_EmployeeId_Status",
                schema: "employee",
                table: "CourseEnrollments",
                columns: new[] { "EmployeeId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_TenantId_Status_DueOn",
                schema: "employee",
                table: "CourseEnrollments",
                columns: new[] { "TenantId", "Status", "DueOn" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_CourseEnrollments_Open",
                schema: "employee",
                table: "CourseEnrollments",
                columns: new[] { "CourseId", "EmployeeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_SkillId",
                schema: "employee",
                table: "Courses",
                column: "SkillId",
                filter: "\"SkillId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Courses_TenantId_Title",
                schema: "employee",
                table: "Courses",
                columns: new[] { "TenantId", "Title" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSkills_SkillId",
                schema: "employee",
                table: "EmployeeSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "UX_EmployeeSkills_Employee_Skill",
                schema: "employee",
                table: "EmployeeSkills",
                columns: new[] { "EmployeeId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleSkills_SkillId",
                schema: "employee",
                table: "RoleSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "UX_RoleSkills_Designation_Skill",
                schema: "employee",
                table: "RoleSkills",
                columns: new[] { "DesignationId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Skills_TenantId_Name",
                schema: "employee",
                table: "Skills",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa) — HR.Shared.Library TenantMigrations
            const string schema = "employee";
            migrationBuilder.AddTenantKey(schema, "Skills");
            migrationBuilder.AddTenantKey(schema, "Courses");
            migrationBuilder.AddTenantForeignKey(schema, "Courses", "SkillId", "Skills");
            migrationBuilder.AddTenantForeignKey(schema, "CourseEnrollments", "CourseId", "Courses");
            migrationBuilder.AddTenantForeignKey(schema, "CourseEnrollments", "EmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "EmployeeSkills", "EmployeeId", "Employees", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "EmployeeSkills", "SkillId", "Skills");
            migrationBuilder.AddTenantForeignKey(schema, "RoleSkills", "DesignationId", "Designations", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "RoleSkills", "SkillId", "Skills");
            migrationBuilder.AddTenantForeignKey(schema, "CareerGoals", "EmployeeId", "Employees", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "CareerGoals", "TargetDesignationId", "Designations");
            migrationBuilder.EnableTenantIsolation(schema, "hr_employee_app",
                "Skills", "Courses", "CourseEnrollments", "EmployeeSkills", "RoleSkills", "CareerGoals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerGoals",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "CourseEnrollments",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeeSkills",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "RoleSkills",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Courses",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Skills",
                schema: "employee");
        }
    }
}
