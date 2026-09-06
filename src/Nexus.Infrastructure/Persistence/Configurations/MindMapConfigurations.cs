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
            .HasMaxLength(200);

        builder.Property(m => m.Description)
            .HasMaxLength(1000);

        builder.HasOne(m => m.Workspace)
            .WithMany(w => w.MindMaps)
            .HasForeignKey(m => m.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany(u => u.MindMaps)
            .HasForeignKey(m => m.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Nodes)
            .WithOne(n => n.MindMap)
            .HasForeignKey(n => n.MindMapId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Edges)
            .WithOne(e => e.MindMap)
            .HasForeignKey(e => e.MindMapId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.WorkspaceId, m.IsDeleted });
        builder.HasIndex(m => new { m.WorkspaceId, m.UserId, m.IsDeleted });

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}

public class MindMapNodeConfiguration : IEntityTypeConfiguration<MindMapNode>
{
    public void Configure(EntityTypeBuilder<MindMapNode> builder)
    {
        builder.ToTable("MindMapNodes");

        builder.HasKey(n => n.Id);

        // Ignore computed helpers so EF maps PositionX/PositionY directly
        builder.Ignore(n => n.X);
        builder.Ignore(n => n.Y);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Description)
            .HasMaxLength(2000);

        builder.Property(n => n.ColorHex)
            .HasMaxLength(30);

        builder.Property(n => n.Shape)
            .HasMaxLength(50);

        builder.Property(n => n.LinkedEntityType)
            .HasMaxLength(50);

        builder.HasOne(n => n.ParentNode)
            .WithMany(p => p.Children)
            .HasForeignKey(n => n.ParentNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.MindMapId, n.IsDeleted });
        builder.HasIndex(n => new { n.LinkedEntityType, n.LinkedEntityId });

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
            .HasMaxLength(500);

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

        builder.HasIndex(e => new { e.MindMapId, e.IsDeleted });
        builder.HasIndex(e => new { e.SourceNodeId, e.TargetNodeId });

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
