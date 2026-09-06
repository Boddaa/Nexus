using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<BoardItem> BoardItems => Set<BoardItem>();
    public DbSet<MindMap> MindMaps => Set<MindMap>();
    public DbSet<MindMapNode> MindMapNodes => Set<MindMapNode>();
    public DbSet<MindMapEdge> MindMapEdges => Set<MindMapEdge>();
    public DbSet<KnowledgeConcept> KnowledgeConcepts => Set<KnowledgeConcept>();
    public DbSet<KnowledgeRelation> KnowledgeRelations => Set<KnowledgeRelation>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<SourceReference> SourceReferences => Set<SourceReference>();
    public DbSet<AiGeneration> AiGenerations => Set<AiGeneration>();
    public DbSet<AiGenerationSource> AiGenerationSources => Set<AiGenerationSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService?.UserId?.ToString();
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = currentUserId ?? "System";
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = currentUserId ?? "System";
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
