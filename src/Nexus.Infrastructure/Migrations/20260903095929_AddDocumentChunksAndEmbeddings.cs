using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentChunksAndEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VectorStoreId",
                table: "DocumentChunks",
                newName: "EmbeddingModel");

            migrationBuilder.RenameColumn(
                name: "TokenCount",
                table: "DocumentChunks",
                newName: "StartPosition");

            migrationBuilder.RenameColumn(
                name: "StartCharOffset",
                table: "DocumentChunks",
                newName: "EndPosition");

            migrationBuilder.RenameColumn(
                name: "EndCharOffset",
                table: "DocumentChunks",
                newName: "EmbeddingStatus");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "DocumentChunks",
                newName: "Text");

            migrationBuilder.AlterColumn<int>(
                name: "PageNumber",
                table: "DocumentChunks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingDimensions",
                table: "DocumentChunks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "EmbeddingVector",
                table: "DocumentChunks",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "DocumentChunks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_WorkspaceId_DocumentId",
                table: "DocumentChunks",
                columns: new[] { "WorkspaceId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_WorkspaceId_EmbeddingStatus",
                table: "DocumentChunks",
                columns: new[] { "WorkspaceId", "EmbeddingStatus" });

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentChunks_Workspaces_WorkspaceId",
                table: "DocumentChunks",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentChunks_Workspaces_WorkspaceId",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_WorkspaceId_DocumentId",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_WorkspaceId_EmbeddingStatus",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingDimensions",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingVector",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "DocumentChunks");

            migrationBuilder.RenameColumn(
                name: "Text",
                table: "DocumentChunks",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "StartPosition",
                table: "DocumentChunks",
                newName: "TokenCount");

            migrationBuilder.RenameColumn(
                name: "EndPosition",
                table: "DocumentChunks",
                newName: "StartCharOffset");

            migrationBuilder.RenameColumn(
                name: "EmbeddingStatus",
                table: "DocumentChunks",
                newName: "EndCharOffset");

            migrationBuilder.RenameColumn(
                name: "EmbeddingModel",
                table: "DocumentChunks",
                newName: "VectorStoreId");

            migrationBuilder.AlterColumn<int>(
                name: "PageNumber",
                table: "DocumentChunks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
