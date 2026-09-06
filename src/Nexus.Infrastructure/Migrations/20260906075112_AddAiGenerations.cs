using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourcePageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StructuredContentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiGenerations_AiConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiGenerations_Documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiGenerations_Notes_SourceNoteId",
                        column: x => x.SourceNoteId,
                        principalTable: "Notes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiGenerations_Pages_SourcePageId",
                        column: x => x.SourcePageId,
                        principalTable: "Pages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiGenerations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiGenerations_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiGenerationSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiGenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelevanceScore = table.Column<double>(type: "float", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Snippet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PageNumber = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGenerationSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiGenerationSources_AiGenerations_AiGenerationId",
                        column: x => x.AiGenerationId,
                        principalTable: "AiGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiGenerationSources_DocumentChunks_DocumentChunkId",
                        column: x => x.DocumentChunkId,
                        principalTable: "DocumentChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiGenerationSources_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiGenerationSources_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiGenerationSources_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_ConversationId",
                table: "AiGenerations",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_SourceDocumentId",
                table: "AiGenerations",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_SourceNoteId",
                table: "AiGenerations",
                column: "SourceNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_SourcePageId",
                table: "AiGenerations",
                column: "SourcePageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_UserId",
                table: "AiGenerations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_WorkspaceId_Operation",
                table: "AiGenerations",
                columns: new[] { "WorkspaceId", "Operation" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerations_WorkspaceId_UserId_CreatedAtUtc",
                table: "AiGenerations",
                columns: new[] { "WorkspaceId", "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationSources_AiGenerationId",
                table: "AiGenerationSources",
                column: "AiGenerationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationSources_DocumentChunkId",
                table: "AiGenerationSources",
                column: "DocumentChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationSources_DocumentId",
                table: "AiGenerationSources",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationSources_NoteId",
                table: "AiGenerationSources",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationSources_PageId",
                table: "AiGenerationSources",
                column: "PageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiGenerationSources");

            migrationBuilder.DropTable(
                name: "AiGenerations");
        }
    }
}
