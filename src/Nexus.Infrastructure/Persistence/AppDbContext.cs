using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private InMemoryDbContextTransaction? _currentInMemoryTransaction;

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
    public DbSet<StudyTopic> StudyTopics => Set<StudyTopic>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
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
                _currentInMemoryTransaction?.TrackAdded(entry.Entity);
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = currentUserId ?? "System";
            }
        }

        if (_currentInMemoryTransaction != null)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added && !(entry.Entity is AuditableEntity))
                {
                    _currentInMemoryTransaction.TrackAdded(entry.Entity);
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    public virtual Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var tx = new InMemoryDbContextTransaction(this);
            _currentInMemoryTransaction = tx;
            return Task.FromResult<IDbContextTransaction>(tx);
        }

        return Database.BeginTransactionAsync(cancellationToken);
    }

    internal Task<int> BaseSaveChangesAsync(CancellationToken cancellationToken)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    internal void ClearCurrentInMemoryTransaction(InMemoryDbContextTransaction tx)
    {
        if (_currentInMemoryTransaction == tx)
        {
            _currentInMemoryTransaction = null;
        }
    }
}

public class InMemoryDbContextTransaction : IDbContextTransaction
{
    private readonly AppDbContext _context;
    private readonly List<object> _addedEntities = new();
    private bool _isCommitted;
    private bool _isRolledBack;

    public InMemoryDbContextTransaction(AppDbContext context)
    {
        _context = context;
        TransactionId = Guid.NewGuid();
    }

    public Guid TransactionId { get; }

    public void TrackAdded(object entity)
    {
        if (!_isCommitted && !_isRolledBack)
        {
            _addedEntities.Add(entity);
        }
    }

    public void Commit()
    {
        _isCommitted = true;
        _addedEntities.Clear();
        _context.ClearCurrentInMemoryTransaction(this);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Commit();
        return Task.CompletedTask;
    }

    public void Rollback()
    {
        if (_isRolledBack || _isCommitted) return;
        _isRolledBack = true;

        _context.ChangeTracker.Clear();
        if (_addedEntities.Count > 0)
        {
            foreach (var entity in _addedEntities)
            {
                try
                {
                    _context.Remove(entity);
                }
                catch
                {
                }
            }
            try
            {
                _context.BaseSaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (DbUpdateConcurrencyException)
            {
            }
            _context.ChangeTracker.Clear();
            _addedEntities.Clear();
        }
        _context.ClearCurrentInMemoryTransaction(this);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledBack || _isCommitted) return;
        _isRolledBack = true;

        _context.ChangeTracker.Clear();
        if (_addedEntities.Count > 0)
        {
            foreach (var entity in _addedEntities)
            {
                try
                {
                    _context.Remove(entity);
                }
                catch
                {
                }
            }
            try
            {
                await _context.BaseSaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateConcurrencyException)
            {
            }
            _context.ChangeTracker.Clear();
            _addedEntities.Clear();
        }
        _context.ClearCurrentInMemoryTransaction(this);
    }

    public void Dispose()
    {
        if (!_isCommitted && !_isRolledBack)
        {
            Rollback();
        }
        _context.ClearCurrentInMemoryTransaction(this);
    }

    public ValueTask DisposeAsync()
    {
        if (!_isCommitted && !_isRolledBack)
        {
            Rollback();
        }
        _context.ClearCurrentInMemoryTransaction(this);
        return ValueTask.CompletedTask;
    }
}
