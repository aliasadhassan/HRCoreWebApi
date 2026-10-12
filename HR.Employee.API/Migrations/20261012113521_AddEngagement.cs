using System;
using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recognitions",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<byte>(type: "smallint", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_Recognitions", x => x.Id);
                    table.CheckConstraint("CK_Recognitions_NotSelf", "\"FromEmployeeId\" <> \"ToEmployeeId\"");
                    table.CheckConstraint("CK_Recognitions_Value", "\"Value\" BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_Recognitions_Employees_FromEmployeeId",
                        column: x => x.FromEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recognitions_Employees_ToEmployeeId",
                        column: x => x.ToEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Surveys",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    Audience = table.Column<byte>(type: "smallint", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosesOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_Surveys", x => x.Id);
                    table.CheckConstraint("CK_Surveys_Audience", "(\"Audience\" = 1 AND \"DepartmentId\" IS NULL) OR (\"Audience\" = 2 AND \"DepartmentId\" IS NOT NULL)");
                    table.CheckConstraint("CK_Surveys_Published", "\"Status\" = 1 OR \"PublishedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_Surveys_Status", "\"Status\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Surveys_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "employee",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecognitionCheers",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecognitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecognitionCheers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecognitionCheers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecognitionCheers_Recognitions_RecognitionId",
                        column: x => x.RecognitionId,
                        principalSchema: "employee",
                        principalTable: "Recognitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyParticipations",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyParticipations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyParticipations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SurveyParticipations_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalSchema: "employee",
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyQuestions",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<short>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    Options = table.Column<string>(type: "character varying(1100)", maxLength: 1100, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyQuestions", x => x.Id);
                    table.CheckConstraint("CK_SurveyQuestions_Options", "(\"Type\" = 4) = (\"Options\" IS NOT NULL)");
                    table.CheckConstraint("CK_SurveyQuestions_Type", "\"Type\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_SurveyQuestions_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalSchema: "employee",
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyResponses",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyResponses_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "employee",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurveyResponses_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SurveyResponses_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalSchema: "employee",
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyAnswers",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<short>(type: "smallint", nullable: true),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyAnswers", x => x.Id);
                    table.CheckConstraint("CK_SurveyAnswers_Present", "\"Value\" IS NOT NULL OR \"Text\" IS NOT NULL");
                    table.CheckConstraint("CK_SurveyAnswers_Value", "\"Value\" IS NULL OR \"Value\" BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "FK_SurveyAnswers_SurveyQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "employee",
                        principalTable: "SurveyQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SurveyAnswers_SurveyResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalSchema: "employee",
                        principalTable: "SurveyResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecognitionCheers_EmployeeId",
                schema: "employee",
                table: "RecognitionCheers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "UX_RecognitionCheers_Recognition_Employee",
                schema: "employee",
                table: "RecognitionCheers",
                columns: new[] { "RecognitionId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recognitions_FromEmployeeId",
                schema: "employee",
                table: "Recognitions",
                column: "FromEmployeeId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Recognitions_TenantId_CreatedAt",
                schema: "employee",
                table: "Recognitions",
                columns: new[] { "TenantId", "CreatedAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Recognitions_ToEmployeeId",
                schema: "employee",
                table: "Recognitions",
                column: "ToEmployeeId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyAnswers_QuestionId",
                schema: "employee",
                table: "SurveyAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "UX_SurveyAnswers_Response_Question",
                schema: "employee",
                table: "SurveyAnswers",
                columns: new[] { "ResponseId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyParticipations_EmployeeId",
                schema: "employee",
                table: "SurveyParticipations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "UX_SurveyParticipations_Survey_Employee",
                schema: "employee",
                table: "SurveyParticipations",
                columns: new[] { "SurveyId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SurveyQuestions_Survey_Order",
                schema: "employee",
                table: "SurveyQuestions",
                columns: new[] { "SurveyId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_DepartmentId",
                schema: "employee",
                table: "SurveyResponses",
                column: "DepartmentId",
                filter: "\"DepartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_EmployeeId",
                schema: "employee",
                table: "SurveyResponses",
                column: "EmployeeId",
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_SurveyId",
                schema: "employee",
                table: "SurveyResponses",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_Surveys_DepartmentId",
                schema: "employee",
                table: "Surveys",
                column: "DepartmentId",
                filter: "\"DepartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Surveys_TenantId_Status",
                schema: "employee",
                table: "Surveys",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = false");

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa) — HR.Shared.Library TenantMigrations
            const string schema = "employee";
            migrationBuilder.AddTenantKey(schema, "Surveys");
            migrationBuilder.AddTenantKey(schema, "SurveyQuestions");
            migrationBuilder.AddTenantKey(schema, "SurveyResponses");
            migrationBuilder.AddTenantKey(schema, "Recognitions");
            migrationBuilder.AddTenantForeignKey(schema, "Surveys", "DepartmentId", "Departments");
            migrationBuilder.AddTenantForeignKey(schema, "SurveyQuestions", "SurveyId", "Surveys", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "SurveyParticipations", "SurveyId", "Surveys", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "SurveyParticipations", "EmployeeId", "Employees", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "SurveyResponses", "SurveyId", "Surveys", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "SurveyResponses", "EmployeeId", "Employees", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "SurveyResponses", "DepartmentId", "Departments");
            migrationBuilder.AddTenantForeignKey(schema, "SurveyAnswers", "ResponseId", "SurveyResponses", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "SurveyAnswers", "QuestionId", "SurveyQuestions", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "Recognitions", "FromEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "Recognitions", "ToEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "RecognitionCheers", "RecognitionId", "Recognitions", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "RecognitionCheers", "EmployeeId", "Employees", "CASCADE");
            migrationBuilder.EnableTenantIsolation(schema, "hr_employee_app",
                "Surveys", "SurveyQuestions", "SurveyParticipations", "SurveyResponses", "SurveyAnswers", "Recognitions", "RecognitionCheers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecognitionCheers",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "SurveyAnswers",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "SurveyParticipations",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Recognitions",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "SurveyQuestions",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "SurveyResponses",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Surveys",
                schema: "employee");
        }
    }
}
