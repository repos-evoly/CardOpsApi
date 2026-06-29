using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardOpsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class OriginalReferenceAndRnnReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalReference",
                table: "ManualReverse",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RrnReference",
                table: "ManualReverse",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalReference",
                table: "ManualReverse");

            migrationBuilder.DropColumn(
                name: "RrnReference",
                table: "ManualReverse");
        }
    }
}
