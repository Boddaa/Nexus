using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("Pages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Icon)
            .HasMaxLength(10);

        builder.Property(p => p.CoverImageUrl)
            .HasMaxLength(500);

        builder.HasOne(p => p.ParentPage)
            .WithMany(p => p.SubPages)
            .HasForeignKey(p => p.ParentPageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Tags)
            .WithMany(t => t.Pages);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("Notes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.ContentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(n => n.Page)
            .WithMany(p => p.Notes)
            .HasForeignKey(n => n.PageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(n => n.Tags)
            .WithMany(t => t.Notes);

        builder.HasQueryFilter(n => !n.IsDeleted);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.ColorHex)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(t => new { t.WorkspaceId, t.Name })
            .IsUnique();

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
