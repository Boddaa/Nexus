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

        builder.Property(c => c.Content)
            .IsRequired();

        builder.Property(c => c.VectorStoreId)
            .HasMaxLength(100);

        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex });

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
