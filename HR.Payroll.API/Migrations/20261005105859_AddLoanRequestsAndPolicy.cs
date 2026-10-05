using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Payroll.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanRequestsAndPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanPolicies",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoansEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxLoanAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxLoanSalaryMultiple = table.Column<byte>(type: "smallint", nullable: true),
                    MaxLoanInstallments = table.Column<short>(type: "smallint", nullable: false),
                    MinServiceMonths = table.Column<short>(type: "smallint", nullable: false),
                    AdvancesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxAdvancePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxAdvanceInstallments = table.Column<byte>(type: "smallint", nullable: false),
                    AllowMultipleActive = table.Column<bool>(type: "boolean", nullable: false),
                    LoanDeductionComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvanceDeductionComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanPolicies", x => x.Id);
                    table.CheckConstraint("CK_LoanPolicies_Advance", "\"MaxAdvancePercent\" > 0 AND \"MaxAdvancePercent\" <= 100");
                    table.CheckConstraint("CK_LoanPolicies_Installments", "\"MaxLoanInstallments\" BETWEEN 1 AND 120 AND \"MaxAdvanceInstallments\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_LoanPolicies_PayComponents_AdvanceDeductionComponentId",
                        column: x => x.AdvanceDeductionComponentId,
                        principalSchema: "payroll",
                        principalTable: "PayComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanPolicies_PayComponents_LoanDeductionComponentId",
                        column: x => x.LoanDeductionComponentId,
                        principalSchema: "payroll",
                        principalTable: "PayComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanRequests",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanType = table.Column<byte>(type: "smallint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedInstallments = table.Column<short>(type: "smallint", nullable: false),
                    PreferredStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedInstallmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EmployeeLoanId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanRequests", x => x.Id);
                    table.CheckConstraint("CK_LoanRequests_Amounts", "\"RequestedAmount\" > 0 AND \"RequestedInstallments\" BETWEEN 1 AND 120");
                    table.CheckConstraint("CK_LoanRequests_Approved", "\"Status\" <> 2 OR (\"EmployeeLoanId\" IS NOT NULL AND \"ApprovedAmount\" > 0 AND \"ApprovedInstallmentAmount\" > 0)");
                    table.ForeignKey(
                        name: "FK_LoanRequests_EmployeeLoans_EmployeeLoanId",
                        column: x => x.EmployeeLoanId,
                        principalSchema: "payroll",
                        principalTable: "EmployeeLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanRequests_PayrollEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "payroll",
                        principalTable: "PayrollEmployees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanPolicies_AdvanceDeductionComponentId",
                schema: "payroll",
                table: "LoanPolicies",
                column: "AdvanceDeductionComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanPolicies_LoanDeductionComponentId",
                schema: "payroll",
                table: "LoanPolicies",
                column: "LoanDeductionComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanPolicies_TenantId",
                schema: "payroll",
                table: "LoanPolicies",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LoanRequests_EmployeeId_CreatedAt",
                schema: "payroll",
                table: "LoanRequests",
                columns: new[] { "EmployeeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanRequests_EmployeeLoanId",
                schema: "payroll",
                table: "LoanRequests",
                column: "EmployeeLoanId",
                unique: true,
                filter: "\"EmployeeLoanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoanRequests_TenantId_Status",
                schema: "payroll",
                table: "LoanRequests",
                columns: new[] { "TenantId", "Status" },
                filter: "\"Status\" = 1 AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanPolicies",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "LoanRequests",
                schema: "payroll");
        }
    }
}
