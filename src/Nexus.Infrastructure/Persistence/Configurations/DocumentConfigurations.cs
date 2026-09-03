using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.StoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.Checksum)
            .HasMaxLength(128);

        builder.HasOne(d => d.Page)
            .WithMany(p => p.Documents)
            .HasForeignKey(d => d.PageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => new { d.WorkspaceId, d.CreatedAtUtc });
        builder.HasIndex(d => d.PageId);

        builder.HasMany(d => d.Chunks)
            .WithOne(c => c.Document)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("DocumentChunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Text)
            .IsRequired();

        builder.Property(c => c.EmbeddingModel)
            .HasMaxLength(100);

        builder.Property(c => c.EmbeddingStatus)
            .IsRequired();

        builder.Property(c => c.EmbeddingVector)
            .HasColumnType("varbinary(max)");

        builder.HasOne(c => c.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Workspace)
            .WithMany()
            .HasForeignKey(c => c.WorkspaceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(c => new { c.WorkspaceId, c.DocumentId });
        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex });
        builder.HasIndex(c => new { c.WorkspaceId, c.EmbeddingStatus });

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
