using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningCoreWebApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNewAccessTokenColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewAccessToken",
                table: "RefreshTokenConfiguration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewAccessToken",
                table: "RefreshTokenConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
