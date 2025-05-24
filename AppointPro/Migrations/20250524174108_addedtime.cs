using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointPro.Migrations
{
    /// <inheritdoc />
    public partial class addedtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndTime",
                table: "Doctors",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "SlotDurationMinutes",
                table: "Doctors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SlotsPerDay",
                table: "Doctors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartTime",
                table: "Doctors",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "SlotDurationMinutes",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "SlotsPerDay",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Doctors");
        }
    }
}
