using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("AiConversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.IsArchived)
            .HasDefaultValue(false);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.WorkspaceId, c.UserId });
        builder.HasIndex(c => new { c.WorkspaceId, c.CreatedAtUtc });

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("AiMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .IsRequired();

        builder.HasMany(m => m.SourceReferences)
            .WithOne(s => s.AiMessage)
            .HasForeignKey(s => s.AiMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAtUtc });

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}

public class SourceReferenceConfiguration : IEntityTypeConfiguration<SourceReference>
{
    public void Configure(EntityTypeBuilder<SourceReference> builder)
    {
        builder.ToTable("SourceReferences");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SourceType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.SourceTitle)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(s => s.Snippet)
            .IsRequired();

        builder.HasOne(s => s.DocumentChunk)
            .WithMany(c => c.SourceReferences)
            .HasForeignKey(s => s.DocumentChunkId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => s.AiMessageId);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

public class AiGenerationConfiguration : IEntityTypeConfiguration<AiGeneration>
{
    public void Configure(EntityTypeBuilder<AiGeneration> builder)
    {
        builder.ToTable("AiGenerations");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Operation)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(g => g.Content)
            .IsRequired();

        builder.Property(g => g.Model)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasMany(g => g.Sources)
            .WithOne(s => s.AiGeneration)
            .HasForeignKey(s => s.AiGenerationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.Workspace)
            .WithMany()
            .HasForeignKey(g => g.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => new { g.WorkspaceId, g.UserId, g.CreatedAtUtc });
        builder.HasIndex(g => new { g.WorkspaceId, g.Operation });

        builder.HasQueryFilter(g => !g.IsDeleted);
    }
}

public class AiGenerationSourceConfiguration : IEntityTypeConfiguration<AiGenerationSource>
{
    public void Configure(EntityTypeBuilder<AiGenerationSource> builder)
    {
        builder.ToTable("AiGenerationSources");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .HasMaxLength(250);

        builder.HasOne(s => s.DocumentChunk)
            .WithMany()
            .HasForeignKey(s => s.DocumentChunkId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => s.AiGenerationId);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
