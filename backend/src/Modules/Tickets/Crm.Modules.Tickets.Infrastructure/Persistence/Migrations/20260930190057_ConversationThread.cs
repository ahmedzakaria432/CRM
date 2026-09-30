using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Modules.Tickets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConversationThread : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketActivities_TicketId",
                schema: "tickets",
                table: "TicketActivities");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                schema: "tickets",
                table: "TicketActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Story 18: number the Story 15 activities 1..n per ticket (the order they were recorded), then make sure
            // Revision - the next Sequence source - is never below the highest one. Data only; the schema is EF's.
            migrationBuilder.Sql("""
                UPDATE a SET a.[Sequence] = x.[rn]
                FROM [tickets].[TicketActivities] a
                JOIN (SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [TicketId] ORDER BY [OccurredAtUtc], [Id]) AS [rn]
                      FROM [tickets].[TicketActivities]) x ON x.[Id] = a.[Id];

                UPDATE t SET t.[Revision] = s.[MaxSequence]
                FROM [tickets].[Tickets] t
                JOIN (SELECT [TicketId], MAX([Sequence]) AS [MaxSequence] FROM [tickets].[TicketActivities] GROUP BY [TicketId]) s
                  ON s.[TicketId] = t.[Id]
                WHERE t.[Revision] < s.[MaxSequence];
                """);

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                schema: "tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketMessages_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "tickets",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketNotes",
                schema: "tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketNotes_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "tickets",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_TicketId_Sequence",
                schema: "tickets",
                table: "TicketActivities",
                columns: new[] { "TicketId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId_Sequence",
                schema: "tickets",
                table: "TicketMessages",
                columns: new[] { "TicketId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketNotes_TicketId_Sequence",
                schema: "tickets",
                table: "TicketNotes",
                columns: new[] { "TicketId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketMessages",
                schema: "tickets");

            migrationBuilder.DropTable(
                name: "TicketNotes",
                schema: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketActivities_TicketId_Sequence",
                schema: "tickets",
                table: "TicketActivities");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "tickets",
                table: "TicketActivities");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_TicketId",
                schema: "tickets",
                table: "TicketActivities",
                column: "TicketId");
        }
    }
}
