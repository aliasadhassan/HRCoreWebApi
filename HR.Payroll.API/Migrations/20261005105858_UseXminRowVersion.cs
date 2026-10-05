using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Payroll.API.Migrations
{
    /// <summary>
    /// bytea "RowVersion" Postgres mein khud generate nahi hota → har INSERT NOT NULL violation pe fail hota tha.
    /// Ab concurrency token Postgres ka xmin system column hai, isliye purane columns sirf drop hote hain
    /// (xmin har table mein pehle se hota hai, banana nahi padta). Employee API ka 20261004101133_UseXminRowVersion jaisa.
    /// </summary>
    public partial class UseXminRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "ContributionRules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "EmployeeLoans");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "EmployeeSalaries");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "EmployeeTaxOpeningBalances");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "LoanRepayments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "PayComponents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "PayGroups");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "PayPeriods");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "PaymentBatches");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "PayrollInputs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "SalaryGrades");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "SalaryTemplates");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payroll",
                table: "TaxRegimes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "ContributionRules",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "EmployeeLoans",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "EmployeeSalaries",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "EmployeeTaxOpeningBalances",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "LoanRepayments",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "PayComponents",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "PayGroups",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "PayPeriods",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "PaymentBatches",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "Payments",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "PayrollEmployees",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "PayrollInputs",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "PayrollRuns",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "PayrollSettings",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "Payslips",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "SalaryGrades",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "SalaryTemplates",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payroll",
                table: "TaxRegimes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
