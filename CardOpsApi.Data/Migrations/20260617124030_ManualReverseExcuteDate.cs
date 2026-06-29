using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardOpsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManualReverseExcuteDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ManualReverseExecuteDate",
                table: "ManualReverse",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualReverseExecuteDate",
                table: "ManualReverse");
        }
    }
}
