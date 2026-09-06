using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("Boards");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Description)
            .HasMaxLength(1000);

        builder.HasOne(b => b.Workspace)
            .WithMany(w => w.Boards)
            .HasForeignKey(b => b.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.User)
            .WithMany(u => u.Boards)
            .HasForeignKey(b => b.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Columns)
            .WithOne(c => c.Board)
            .HasForeignKey(c => c.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Items)
            .WithOne(i => i.Board)
            .HasForeignKey(i => i.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.WorkspaceId, b.IsDeleted });
        builder.HasIndex(b => new { b.WorkspaceId, b.UserId, b.IsDeleted });

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}

public class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        builder.ToTable("BoardColumns");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasOne(c => c.Board)
            .WithMany(b => b.Columns)
            .HasForeignKey(c => c.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Column)
            .HasForeignKey(i => i.BoardColumnId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => new { c.BoardId, c.IsDeleted });

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class BoardItemConfiguration : IEntityTypeConfiguration<BoardItem>
{
    public void Configure(EntityTypeBuilder<BoardItem> builder)
    {
        builder.ToTable("BoardItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(i => i.Description)
            .HasMaxLength(2000);

        builder.Property(i => i.Content)
            .HasMaxLength(10000);

        builder.Property(i => i.LinkedEntityType)
            .HasMaxLength(50);

        builder.Property(i => i.ColorHex)
            .HasMaxLength(30);

        builder.HasOne(i => i.Board)
            .WithMany(b => b.Items)
            .HasForeignKey(i => i.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Column)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.BoardColumnId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => new { i.BoardId, i.IsDeleted });
        builder.HasIndex(i => new { i.BoardId, i.ZIndex });
        builder.HasIndex(i => new { i.LinkedEntityType, i.LinkedEntityId });

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
