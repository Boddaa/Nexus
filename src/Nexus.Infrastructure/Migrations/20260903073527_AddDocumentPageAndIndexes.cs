using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentPageAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_WorkspaceId",
                table: "Documents");

            migrationBuilder.AddColumn<Guid>(
                name: "PageId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_PageId",
                table: "Documents",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WorkspaceId_CreatedAtUtc",
                table: "Documents",
                columns: new[] { "WorkspaceId", "CreatedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Pages_PageId",
                table: "Documents",
                column: "PageId",
                principalTable: "Pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Pages_PageId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_PageId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_WorkspaceId_CreatedAtUtc",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "PageId",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WorkspaceId",
                table: "Documents",
                column: "WorkspaceId");
        }
    }
}
