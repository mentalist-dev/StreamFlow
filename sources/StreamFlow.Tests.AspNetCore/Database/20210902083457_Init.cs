using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StreamFlow.Tests.AspNetCore.Database
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "StreamFlow");

            migrationBuilder.CreateTable(
                name: "Locks",
                schema: "StreamFlow",
                columns: table => new
                {
                    OutboxLockId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locks", x => x.OutboxLockId);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "StreamFlow",
                columns: table => new
                {
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Body = table.Column<byte[]>(type: "bytea", nullable: false),
                    Options = table.Column<byte[]>(type: "bytea", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Scheduled = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Published = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.OutboxMessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX__Messages__Created__Scheduled__Published",
                schema: "StreamFlow",
                table: "Messages",
                columns: new[] { "Created", "Scheduled", "Published" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Locks",
                schema: "StreamFlow");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "StreamFlow");
        }
    }
}
