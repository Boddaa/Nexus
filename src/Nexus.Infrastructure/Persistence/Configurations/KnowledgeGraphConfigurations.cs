using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class KnowledgeConceptConfiguration : IEntityTypeConfiguration<KnowledgeConcept>
{
    public void Configure(EntityTypeBuilder<KnowledgeConcept> builder)
    {
        builder.ToTable("KnowledgeConcepts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.NormalizedName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.HasIndex(c => new { c.WorkspaceId, c.NormalizedName });

        builder.HasMany(c => c.OutgoingRelations)
            .WithOne(r => r.SourceConcept)
            .HasForeignKey(r => r.SourceConceptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.IncomingRelations)
            .WithOne(r => r.TargetConcept)
            .HasForeignKey(r => r.TargetConceptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class KnowledgeRelationConfiguration : IEntityTypeConfiguration<KnowledgeRelation>
{
    public void Configure(EntityTypeBuilder<KnowledgeRelation> builder)
    {
        builder.ToTable("KnowledgeRelations");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.WorkspaceId, r.SourceConceptId, r.TargetConceptId });

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
