using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRagConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiMessages_ConversationId",
                table: "AiMessages");

            migrationBuilder.DropIndex(
                name: "IX_AiConversations_WorkspaceId",
                table: "AiConversations");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "SourceReferences",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "AiConversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AiConversations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_ConversationId_CreatedAtUtc",
                table: "AiMessages",
                columns: new[] { "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_WorkspaceId_CreatedAtUtc",
                table: "AiConversations",
                columns: new[] { "WorkspaceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_WorkspaceId_UserId",
                table: "AiConversations",
                columns: new[] { "WorkspaceId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiMessages_ConversationId_CreatedAtUtc",
                table: "AiMessages");

            migrationBuilder.DropIndex(
                name: "IX_AiConversations_WorkspaceId_CreatedAtUtc",
                table: "AiConversations");

            migrationBuilder.DropIndex(
                name: "IX_AiConversations_WorkspaceId_UserId",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "SourceReferences");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AiConversations");

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_ConversationId",
                table: "AiMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_WorkspaceId",
                table: "AiConversations",
                column: "WorkspaceId");
        }
    }
}
