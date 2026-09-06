using Microsoft.EntityFrameworkCore;
using Nexus.Domain.Entities;

namespace Nexus.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Workspace> Workspaces { get; }
    DbSet<WorkspaceMember> WorkspaceMembers { get; }
    DbSet<Page> Pages { get; }
    DbSet<Note> Notes { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentChunk> DocumentChunks { get; }
    DbSet<Tag> Tags { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<Board> Boards { get; }
    DbSet<BoardColumn> BoardColumns { get; }
    DbSet<BoardItem> BoardItems { get; }
    DbSet<MindMap> MindMaps { get; }
    DbSet<MindMapNode> MindMapNodes { get; }
    DbSet<MindMapEdge> MindMapEdges { get; }
    DbSet<KnowledgeConcept> KnowledgeConcepts { get; }
    DbSet<KnowledgeRelation> KnowledgeRelations { get; }
    DbSet<StudySession> StudySessions { get; }
    DbSet<Flashcard> Flashcards { get; }
    DbSet<Quiz> Quizzes { get; }
    DbSet<QuizQuestion> QuizQuestions { get; }
    DbSet<QuizAttempt> QuizAttempts { get; }
    DbSet<AiConversation> AiConversations { get; }
    DbSet<AiMessage> AiMessages { get; }
    DbSet<SourceReference> SourceReferences { get; }
    DbSet<AiGeneration> AiGenerations { get; }
    DbSet<AiGenerationSource> AiGenerationSources { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
