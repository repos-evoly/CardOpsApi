using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardOpsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class updatedefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FisBankAccount",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "0010838890434",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "0015798000001");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FisBankAccount",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "0015798000001",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "0010838890434");
        }
    }
}
