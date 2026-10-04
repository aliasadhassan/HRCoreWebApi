using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <summary>
    /// bytea "RowVersion" Postgres mein khud generate nahi hota → har INSERT NOT NULL violation pe fail hota tha.
    /// Ab concurrency token Postgres ka xmin system column hai, isliye purane columns sirf drop hote hain
    /// (xmin har table mein pehle se hota hai, banana nahi padta).
    /// </summary>
    public partial class UseXminRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "Designations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "EmployeeJobHistory");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "LeaveApprovalSettings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "LeavePolicies");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "employee",
                table: "Locations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "Departments",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "Designations",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "EmployeeDocuments",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "EmployeeJobHistory",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "Employees",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "Holidays",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "LeaveApprovalSettings",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "LeaveBalances",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "LeavePolicies",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "LeaveRequests",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "LeaveTypes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "employee",
                table: "Locations",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
