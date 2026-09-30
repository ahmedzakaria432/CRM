using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortalAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalAccounts",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreferredLocale = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FailedSignInCount = table.Column<int>(type: "int", nullable: false),
                    LockedOutUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSignInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortalSessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortalRefreshTokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RotatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalRefreshTokens_PortalSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "identity",
                        principalTable: "PortalSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccounts_ContactId",
                schema: "identity",
                table: "PortalAccounts",
                column: "ContactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccounts_CustomerId",
                schema: "identity",
                table: "PortalAccounts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccounts_Email",
                schema: "identity",
                table: "PortalAccounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccounts_TenantId",
                schema: "identity",
                table: "PortalAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalRefreshTokens_SessionId",
                schema: "identity",
                table: "PortalRefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalRefreshTokens_TokenHash",
                schema: "identity",
                table: "PortalRefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortalSessions_PortalAccountId",
                schema: "identity",
                table: "PortalSessions",
                column: "PortalAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalAccounts",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PortalRefreshTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PortalSessions",
                schema: "identity");
        }
    }
}
