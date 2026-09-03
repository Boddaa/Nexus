using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class KnowledgeConcept : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double MasteryLevel { get; set; } = 0.0; // 0.0 to 100.0%
    public DateTime? LastReviewedAtUtc { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<KnowledgeRelation> OutgoingRelations { get; set; } = new List<KnowledgeRelation>();
    public ICollection<KnowledgeRelation> IncomingRelations { get; set; } = new List<KnowledgeRelation>();
    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
}

public class KnowledgeRelation : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid SourceConceptId { get; set; }
    public Guid TargetConceptId { get; set; }
    public RelationType RelationType { get; set; } = RelationType.RelatedTo;
    public double Weight { get; set; } = 1.0;

    public Workspace Workspace { get; set; } = null!;
    public KnowledgeConcept SourceConcept { get; set; } = null!;
    public KnowledgeConcept TargetConcept { get; set; } = null!;
}
