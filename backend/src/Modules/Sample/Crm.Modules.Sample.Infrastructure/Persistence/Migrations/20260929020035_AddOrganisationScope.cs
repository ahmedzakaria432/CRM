using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Modules.Sample.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "sample",
                table: "Samples",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                schema: "sample",
                table: "Samples",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "sample",
                table: "Samples",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Samples_TenantId_DepartmentId",
                schema: "sample",
                table: "Samples",
                columns: new[] { "TenantId", "DepartmentId" });

            // Story 06: rows created before scoping belong to the dev seed's Riyadh Customer Care department.
            migrationBuilder.Sql(
                "UPDATE [sample].[Samples] SET TenantId = '06000000-0000-0000-0000-000000000001', " +
                "BranchId = '06000000-0000-0000-0001-000000000001', DepartmentId = '06000000-0000-0000-0002-000000000001' " +
                "WHERE TenantId = '00000000-0000-0000-0000-000000000000';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Samples_TenantId_DepartmentId",
                schema: "sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "sample",
                table: "Samples");
        }
    }
}
