using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardOpsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManualReverse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualReverse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestinationAccount = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TransferStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransferReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualReverse", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualReverse");
        }
    }
}
