using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class MindMapConfiguration : IEntityTypeConfiguration<MindMap>
{
    public void Configure(EntityTypeBuilder<MindMap> builder)
    {
        builder.ToTable("MindMaps");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(m => m.Description)
            .HasMaxLength(500);

        builder.HasMany(m => m.Nodes)
            .WithOne(n => n.MindMap)
            .HasForeignKey(n => n.MindMapId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Edges)
            .WithOne(e => e.MindMap)
            .HasForeignKey(e => e.MindMapId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}

public class MindMapNodeConfiguration : IEntityTypeConfiguration<MindMapNode>
{
    public void Configure(EntityTypeBuilder<MindMapNode> builder)
    {
        builder.ToTable("MindMapNodes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(n => n.Description)
            .HasMaxLength(500);

        builder.Property(n => n.ColorHex)
            .HasMaxLength(20);

        builder.Property(n => n.Shape)
            .HasMaxLength(50);

        builder.Property(n => n.LinkedEntityType)
            .HasMaxLength(50);

        builder.HasOne(n => n.ParentNode)
            .WithMany(p => p.Children)
            .HasForeignKey(n => n.ParentNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(n => !n.IsDeleted);
    }
}

public class MindMapEdgeConfiguration : IEntityTypeConfiguration<MindMapEdge>
{
    public void Configure(EntityTypeBuilder<MindMapEdge> builder)
    {
        builder.ToTable("MindMapEdges");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Label)
            .HasMaxLength(100);

        builder.Property(e => e.RelationType)
            .HasMaxLength(50);

        builder.Property(e => e.Style)
            .HasMaxLength(50);

        builder.HasOne(e => e.SourceNode)
            .WithMany(n => n.SourceEdges)
            .HasForeignKey(e => e.SourceNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TargetNode)
            .WithMany(n => n.TargetEdges)
            .HasForeignKey(e => e.TargetNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
