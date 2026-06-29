using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardOpsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManualReverseDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ManualReverseDate",
                table: "ManualReverse",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualReverseDate",
                table: "ManualReverse");
        }
    }
}
