using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardOpsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFisBankFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FisBankFileId",
                table: "FisBankRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FisBankFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ImportedCount = table.Column<int>(type: "int", nullable: false),
                    FirstRecordId = table.Column<int>(type: "int", nullable: false),
                    LastRecordId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FisBankFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FisBankRecords_FisBankFileId",
                table: "FisBankRecords",
                column: "FisBankFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_FisBankRecords_FisBankFiles_FisBankFileId",
                table: "FisBankRecords",
                column: "FisBankFileId",
                principalTable: "FisBankFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FisBankRecords_FisBankFiles_FisBankFileId",
                table: "FisBankRecords");

            migrationBuilder.DropTable(
                name: "FisBankFiles");

            migrationBuilder.DropIndex(
                name: "IX_FisBankRecords_FisBankFileId",
                table: "FisBankRecords");

            migrationBuilder.DropColumn(
                name: "FisBankFileId",
                table: "FisBankRecords");
        }
    }
}
