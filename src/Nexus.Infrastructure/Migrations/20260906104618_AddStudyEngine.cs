using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudySessions_WorkspaceId",
                table: "StudySessions");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_WorkspaceId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_Flashcards_WorkspaceId",
                table: "Flashcards");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "StudySessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemsAttempted",
                table: "StudySessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ItemsCompleted",
                table: "StudySessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "StudySessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "StudyTopicId",
                table: "StudySessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "StudySessions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiGenerationId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceNoteId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePageId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudyTopicId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "QuizQuestions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "QuizQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceNoteId",
                table: "QuizQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePageId",
                table: "QuizQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorrectAnswers",
                table: "QuizAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "QuizAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                table: "QuizAttempts",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "QuizAttempts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TotalQuestions",
                table: "QuizAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "QuizAttempts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiGenerationId",
                table: "Flashcards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorrectCount",
                table: "Flashcards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Flashcards",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewedAtUtc",
                table: "Flashcards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextReviewAtUtc",
                table: "Flashcards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "Flashcards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentChunkId",
                table: "Flashcards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "Flashcards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceNoteId",
                table: "Flashcards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePageId",
                table: "Flashcards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudyTopicId",
                table: "Flashcards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Flashcards",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "WrongCount",
                table: "Flashcards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QuizAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizAnswers_QuizAttempts_QuizAttemptId",
                        column: x => x.QuizAttemptId,
                        principalTable: "QuizAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizAnswers_QuizQuestions_QuizQuestionId",
                        column: x => x.QuizQuestionId,
                        principalTable: "QuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourcePageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyTopics_Documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudyTopics_Notes_SourceNoteId",
                        column: x => x.SourceNoteId,
                        principalTable: "Notes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudyTopics_Pages_SourcePageId",
                        column: x => x.SourcePageId,
                        principalTable: "Pages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudyTopics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyTopics_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_StudyTopicId",
                table: "StudySessions",
                column: "StudyTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_UserId",
                table: "StudySessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_WorkspaceId_UserId",
                table: "StudySessions",
                columns: new[] { "WorkspaceId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_StudyTopicId",
                table: "Quizzes",
                column: "StudyTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_UserId",
                table: "Quizzes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_WorkspaceId_UserId",
                table: "Quizzes",
                columns: new[] { "WorkspaceId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_WorkspaceId_UserId",
                table: "QuizAttempts",
                columns: new[] { "WorkspaceId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_NextReviewDateUtc",
                table: "Flashcards",
                column: "NextReviewDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_StudyTopicId",
                table: "Flashcards",
                column: "StudyTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_UserId",
                table: "Flashcards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_WorkspaceId_UserId",
                table: "Flashcards",
                columns: new[] { "WorkspaceId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAnswers_QuizAttemptId",
                table: "QuizAnswers",
                column: "QuizAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAnswers_QuizQuestionId",
                table: "QuizAnswers",
                column: "QuizQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyTopics_SourceDocumentId",
                table: "StudyTopics",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyTopics_SourceNoteId",
                table: "StudyTopics",
                column: "SourceNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyTopics_SourcePageId",
                table: "StudyTopics",
                column: "SourcePageId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyTopics_UserId",
                table: "StudyTopics",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyTopics_WorkspaceId_UserId",
                table: "StudyTopics",
                columns: new[] { "WorkspaceId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Flashcards_StudyTopics_StudyTopicId",
                table: "Flashcards",
                column: "StudyTopicId",
                principalTable: "StudyTopics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Flashcards_Users_UserId",
                table: "Flashcards",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_StudyTopics_StudyTopicId",
                table: "Quizzes",
                column: "StudyTopicId",
                principalTable: "StudyTopics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Users_UserId",
                table: "Quizzes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudySessions_StudyTopics_StudyTopicId",
                table: "StudySessions",
                column: "StudyTopicId",
                principalTable: "StudyTopics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StudySessions_Users_UserId",
                table: "StudySessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flashcards_StudyTopics_StudyTopicId",
                table: "Flashcards");

            migrationBuilder.DropForeignKey(
                name: "FK_Flashcards_Users_UserId",
                table: "Flashcards");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_StudyTopics_StudyTopicId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Users_UserId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_StudySessions_StudyTopics_StudyTopicId",
                table: "StudySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_StudySessions_Users_UserId",
                table: "StudySessions");

            migrationBuilder.DropTable(
                name: "QuizAnswers");

            migrationBuilder.DropTable(
                name: "StudyTopics");

            migrationBuilder.DropIndex(
                name: "IX_StudySessions_StudyTopicId",
                table: "StudySessions");

            migrationBuilder.DropIndex(
                name: "IX_StudySessions_UserId",
                table: "StudySessions");

            migrationBuilder.DropIndex(
                name: "IX_StudySessions_WorkspaceId_UserId",
                table: "StudySessions");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_StudyTopicId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_UserId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_WorkspaceId_UserId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_WorkspaceId_UserId",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Flashcards_NextReviewDateUtc",
                table: "Flashcards");

            migrationBuilder.DropIndex(
                name: "IX_Flashcards_StudyTopicId",
                table: "Flashcards");

            migrationBuilder.DropIndex(
                name: "IX_Flashcards_UserId",
                table: "Flashcards");

            migrationBuilder.DropIndex(
                name: "IX_Flashcards_WorkspaceId_UserId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "ItemsAttempted",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "ItemsCompleted",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "StudyTopicId",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "AiGenerationId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "SourceNoteId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "SourcePageId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "StudyTopicId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "SourceNoteId",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "SourcePageId",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "CorrectAnswers",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "TotalQuestions",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "AiGenerationId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "CorrectCount",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "LastReviewedAtUtc",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "NextReviewAtUtc",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "SourceDocumentChunkId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "SourceNoteId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "SourcePageId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "StudyTopicId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "WrongCount",
                table: "Flashcards");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_WorkspaceId",
                table: "StudySessions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_WorkspaceId",
                table: "Quizzes",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_WorkspaceId",
                table: "Flashcards",
                column: "WorkspaceId");
        }
    }
}
