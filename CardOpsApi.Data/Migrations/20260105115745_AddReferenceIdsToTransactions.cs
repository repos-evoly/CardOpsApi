using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardOpsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceIdsToTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                table: "Transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReverseRefId",
                table: "Transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReverseRefId",
                table: "Transactions");
        }
    }
}
