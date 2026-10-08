using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetCategories",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_AssetCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetTag = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Vendor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    WarrantyUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Condition = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.CheckConstraint("CK_Assets_Condition", "\"Condition\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Assets_PurchaseCost", "\"PurchaseCost\" IS NULL OR \"PurchaseCost\" >= 0");
                    table.CheckConstraint("CK_Assets_Status", "\"Status\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Assets_Warranty", "\"WarrantyUntil\" IS NULL OR \"PurchaseDate\" IS NULL OR \"WarrantyUntil\" >= \"PurchaseDate\"");
                    table.ForeignKey(
                        name: "FK_Assets_AssetCategories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalSchema: "employee",
                        principalTable: "AssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assets_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "employee",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetAssignments",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DueBack = table.Column<DateOnly>(type: "date", nullable: true),
                    ConditionOut = table.Column<byte>(type: "smallint", nullable: false),
                    AssignNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ReturnedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ConditionIn = table.Column<byte>(type: "smallint", nullable: true),
                    ReturnNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetAssignments", x => x.Id);
                    table.CheckConstraint("CK_AssetAssignments_ConditionIn", "\"ConditionIn\" IS NULL OR \"ConditionIn\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_AssetAssignments_ConditionOut", "\"ConditionOut\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_AssetAssignments_DueBack", "\"DueBack\" IS NULL OR \"DueBack\" >= \"AssignedOn\"");
                    table.CheckConstraint("CK_AssetAssignments_Returned", "(\"ReturnedOn\" IS NULL AND \"ConditionIn\" IS NULL) OR (\"ReturnedOn\" >= \"AssignedOn\" AND \"ConditionIn\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AssetAssignments_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "employee",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetEvents",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetEvents", x => x.Id);
                    table.CheckConstraint("CK_AssetEvents_Status", "\"Status\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_AssetEvents_Type", "\"Type\" BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_AssetEvents_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "employee",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetEvents_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetAssignments_AssetId_AssignedOn",
                schema: "employee",
                table: "AssetAssignments",
                columns: new[] { "AssetId", "AssignedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetAssignments_EmployeeId",
                schema: "employee",
                table: "AssetAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "UX_AssetAssignments_AssetId_Open",
                schema: "employee",
                table: "AssetAssignments",
                column: "AssetId",
                unique: true,
                filter: "\"ReturnedOn\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_TenantId_Name",
                schema: "employee",
                table: "AssetCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AssetEvents_AssetId_At",
                schema: "employee",
                table: "AssetEvents",
                columns: new[] { "AssetId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetEvents_EmployeeId",
                schema: "employee",
                table: "AssetEvents",
                column: "EmployeeId",
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssetEvents_TenantId_At",
                schema: "employee",
                table: "AssetEvents",
                columns: new[] { "TenantId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetCategoryId",
                schema: "employee",
                table: "Assets",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_LocationId",
                schema: "employee",
                table: "Assets",
                column: "LocationId",
                filter: "\"LocationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_TenantId_AssetTag",
                schema: "employee",
                table: "Assets",
                columns: new[] { "TenantId", "AssetTag" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_TenantId_SerialNumber",
                schema: "employee",
                table: "Assets",
                columns: new[] { "TenantId", "SerialNumber" },
                unique: true,
                filter: "\"SerialNumber\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_TenantId_Status",
                schema: "employee",
                table: "Assets",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = false");

            // Tenant safety (03/04 jaisa): composite tenant FKs, app role grants, RLS
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_AssetCategories_TenantId_Id"" ON employee.""AssetCategories"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Assets_TenantId_Id"" ON employee.""Assets"" (""TenantId"", ""Id"");
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Assets_AssetCategoryId_Tenant' AND conrelid = 'employee.""Assets""'::regclass) THEN
  ALTER TABLE employee.""Assets"" ADD CONSTRAINT ""FK_Assets_AssetCategoryId_Tenant"" FOREIGN KEY (""TenantId"", ""AssetCategoryId"") REFERENCES employee.""AssetCategories"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Assets_LocationId_Tenant' AND conrelid = 'employee.""Assets""'::regclass) THEN
  ALTER TABLE employee.""Assets"" ADD CONSTRAINT ""FK_Assets_LocationId_Tenant"" FOREIGN KEY (""TenantId"", ""LocationId"") REFERENCES employee.""Locations"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AssetAssignments_AssetId_Tenant' AND conrelid = 'employee.""AssetAssignments""'::regclass) THEN
  ALTER TABLE employee.""AssetAssignments"" ADD CONSTRAINT ""FK_AssetAssignments_AssetId_Tenant"" FOREIGN KEY (""TenantId"", ""AssetId"") REFERENCES employee.""Assets"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AssetAssignments_EmployeeId_Tenant' AND conrelid = 'employee.""AssetAssignments""'::regclass) THEN
  ALTER TABLE employee.""AssetAssignments"" ADD CONSTRAINT ""FK_AssetAssignments_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AssetEvents_AssetId_Tenant' AND conrelid = 'employee.""AssetEvents""'::regclass) THEN
  ALTER TABLE employee.""AssetEvents"" ADD CONSTRAINT ""FK_AssetEvents_AssetId_Tenant"" FOREIGN KEY (""TenantId"", ""AssetId"") REFERENCES employee.""Assets"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AssetEvents_EmployeeId_Tenant' AND conrelid = 'employee.""AssetEvents""'::regclass) THEN
  ALTER TABLE employee.""AssetEvents"" ADD CONSTRAINT ""FK_AssetEvents_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON employee.""AssetCategories"", employee.""Assets"", employee.""AssetAssignments"", employee.""AssetEvents"" TO hr_employee_app;
ALTER TABLE employee.""AssetCategories"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""AssetCategories"";
CREATE POLICY tenant_isolation ON employee.""AssetCategories"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Assets"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Assets"";
CREATE POLICY tenant_isolation ON employee.""Assets"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""AssetAssignments"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""AssetAssignments"";
CREATE POLICY tenant_isolation ON employee.""AssetAssignments"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""AssetEvents"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""AssetEvents"";
CREATE POLICY tenant_isolation ON employee.""AssetEvents"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetAssignments",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "AssetEvents",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Assets",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "AssetCategories",
                schema: "employee");
        }
    }
}
