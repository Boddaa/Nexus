using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisualThinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardItems_BoardColumns_BoardColumnId",
                table: "BoardItems");

            migrationBuilder.DropIndex(
                name: "IX_MindMaps_WorkspaceId",
                table: "MindMaps");

            migrationBuilder.DropIndex(
                name: "IX_MindMapNodes_MindMapId",
                table: "MindMapNodes");

            migrationBuilder.DropIndex(
                name: "IX_MindMapEdges_MindMapId",
                table: "MindMapEdges");

            migrationBuilder.DropIndex(
                name: "IX_MindMapEdges_SourceNodeId",
                table: "MindMapEdges");

            migrationBuilder.DropIndex(
                name: "IX_Boards_WorkspaceId",
                table: "Boards");

            migrationBuilder.DropIndex(
                name: "IX_BoardColumns_BoardId",
                table: "BoardColumns");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "MindMaps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MindMaps",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "MindMaps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "MindMapNodes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MindMapNodes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ColorHex",
                table: "MindMapNodes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<double>(
                name: "Height",
                table: "MindMapNodes",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "NodeType",
                table: "MindMapNodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Width",
                table: "MindMapNodes",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "MindMapEdges",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EdgeType",
                table: "MindMapEdges",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Boards",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Boards",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Boards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "BoardItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ColorHex",
                table: "BoardItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardColumnId",
                table: "BoardItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "BoardId",
                table: "BoardItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "BoardItems",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Height",
                table: "BoardItems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Rotation",
                table: "BoardItems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "BoardItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Width",
                table: "BoardItems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "X",
                table: "BoardItems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Y",
                table: "BoardItems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ZIndex",
                table: "BoardItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MindMaps_UserId",
                table: "MindMaps",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MindMaps_WorkspaceId_IsDeleted",
                table: "MindMaps",
                columns: new[] { "WorkspaceId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MindMaps_WorkspaceId_UserId_IsDeleted",
                table: "MindMaps",
                columns: new[] { "WorkspaceId", "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MindMapNodes_LinkedEntityType_LinkedEntityId",
                table: "MindMapNodes",
                columns: new[] { "LinkedEntityType", "LinkedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_MindMapNodes_MindMapId_IsDeleted",
                table: "MindMapNodes",
                columns: new[] { "MindMapId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MindMapEdges_MindMapId_IsDeleted",
                table: "MindMapEdges",
                columns: new[] { "MindMapId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MindMapEdges_SourceNodeId_TargetNodeId",
                table: "MindMapEdges",
                columns: new[] { "SourceNodeId", "TargetNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Boards_UserId",
                table: "Boards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_WorkspaceId_IsDeleted",
                table: "Boards",
                columns: new[] { "WorkspaceId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Boards_WorkspaceId_UserId_IsDeleted",
                table: "Boards",
                columns: new[] { "WorkspaceId", "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardItems_BoardId_IsDeleted",
                table: "BoardItems",
                columns: new[] { "BoardId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardItems_BoardId_ZIndex",
                table: "BoardItems",
                columns: new[] { "BoardId", "ZIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardItems_LinkedEntityType_LinkedEntityId",
                table: "BoardItems",
                columns: new[] { "LinkedEntityType", "LinkedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_BoardId_IsDeleted",
                table: "BoardColumns",
                columns: new[] { "BoardId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_BoardItems_BoardColumns_BoardColumnId",
                table: "BoardItems",
                column: "BoardColumnId",
                principalTable: "BoardColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardItems_Boards_BoardId",
                table: "BoardItems",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Users_UserId",
                table: "Boards",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MindMaps_Users_UserId",
                table: "MindMaps",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardItems_BoardColumns_BoardColumnId",
                table: "BoardItems");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardItems_Boards_BoardId",
                table: "BoardItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Boards_Users_UserId",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_MindMaps_Users_UserId",
                table: "MindMaps");

            migrationBuilder.DropIndex(
                name: "IX_MindMaps_UserId",
                table: "MindMaps");

            migrationBuilder.DropIndex(
                name: "IX_MindMaps_WorkspaceId_IsDeleted",
                table: "MindMaps");

            migrationBuilder.DropIndex(
                name: "IX_MindMaps_WorkspaceId_UserId_IsDeleted",
                table: "MindMaps");

            migrationBuilder.DropIndex(
                name: "IX_MindMapNodes_LinkedEntityType_LinkedEntityId",
                table: "MindMapNodes");

            migrationBuilder.DropIndex(
                name: "IX_MindMapNodes_MindMapId_IsDeleted",
                table: "MindMapNodes");

            migrationBuilder.DropIndex(
                name: "IX_MindMapEdges_MindMapId_IsDeleted",
                table: "MindMapEdges");

            migrationBuilder.DropIndex(
                name: "IX_MindMapEdges_SourceNodeId_TargetNodeId",
                table: "MindMapEdges");

            migrationBuilder.DropIndex(
                name: "IX_Boards_UserId",
                table: "Boards");

            migrationBuilder.DropIndex(
                name: "IX_Boards_WorkspaceId_IsDeleted",
                table: "Boards");

            migrationBuilder.DropIndex(
                name: "IX_Boards_WorkspaceId_UserId_IsDeleted",
                table: "Boards");

            migrationBuilder.DropIndex(
                name: "IX_BoardItems_BoardId_IsDeleted",
                table: "BoardItems");

            migrationBuilder.DropIndex(
                name: "IX_BoardItems_BoardId_ZIndex",
                table: "BoardItems");

            migrationBuilder.DropIndex(
                name: "IX_BoardItems_LinkedEntityType_LinkedEntityId",
                table: "BoardItems");

            migrationBuilder.DropIndex(
                name: "IX_BoardColumns_BoardId_IsDeleted",
                table: "BoardColumns");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MindMaps");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "MindMapNodes");

            migrationBuilder.DropColumn(
                name: "NodeType",
                table: "MindMapNodes");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "MindMapNodes");

            migrationBuilder.DropColumn(
                name: "EdgeType",
                table: "MindMapEdges");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Boards");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "BoardItems");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "BoardItems");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "BoardItems");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "BoardItems");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "BoardItems");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "BoardItems");

            migrationBuilder.DropColumn(
                name: "X",
                table: "BoardItems");

            migrationBuilder.DropColumn(
                name: "Y",
                table: "BoardItems");

            migrationBuilder.DropColumn(
                name: "ZIndex",
                table: "BoardItems");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "MindMaps",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MindMaps",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "MindMapNodes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MindMapNodes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ColorHex",
                table: "MindMapNodes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "MindMapEdges",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Boards",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Boards",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "BoardItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ColorHex",
                table: "BoardItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardColumnId",
                table: "BoardItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MindMaps_WorkspaceId",
                table: "MindMaps",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_MindMapNodes_MindMapId",
                table: "MindMapNodes",
                column: "MindMapId");

            migrationBuilder.CreateIndex(
                name: "IX_MindMapEdges_MindMapId",
                table: "MindMapEdges",
                column: "MindMapId");

            migrationBuilder.CreateIndex(
                name: "IX_MindMapEdges_SourceNodeId",
                table: "MindMapEdges",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_WorkspaceId",
                table: "Boards",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_BoardId",
                table: "BoardColumns",
                column: "BoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardItems_BoardColumns_BoardColumnId",
                table: "BoardItems",
                column: "BoardColumnId",
                principalTable: "BoardColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
