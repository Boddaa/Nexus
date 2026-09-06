using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class StudyTopicConfiguration : IEntityTypeConfiguration<StudyTopic>
{
    public void Configure(EntityTypeBuilder<StudyTopic> builder)
    {
        builder.ToTable("StudyTopics");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasOne(t => t.Workspace)
            .WithMany(w => w.StudyTopics)
            .HasForeignKey(t => t.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.User)
            .WithMany(u => u.StudyTopics)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Flashcards)
            .WithOne(f => f.Topic)
            .HasForeignKey(f => f.StudyTopicId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Quizzes)
            .WithOne(q => q.Topic)
            .HasForeignKey(q => q.StudyTopicId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.StudySessions)
            .WithOne(s => s.Topic)
            .HasForeignKey(s => s.StudyTopicId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.WorkspaceId, t.UserId });

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

public class StudySessionConfiguration : IEntityTypeConfiguration<StudySession>
{
    public void Configure(EntityTypeBuilder<StudySession> builder)
    {
        builder.ToTable("StudySessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasOne(s => s.Workspace)
            .WithMany(w => w.StudySessions)
            .HasForeignKey(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.User)
            .WithMany(u => u.StudySessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.WorkspaceId, s.UserId });
        builder.HasIndex(s => s.StudyTopicId);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

public class FlashcardConfiguration : IEntityTypeConfiguration<Flashcard>
{
    public void Configure(EntityTypeBuilder<Flashcard> builder)
    {
        builder.ToTable("Flashcards");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FrontText)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(f => f.BackText)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasOne(f => f.Workspace)
            .WithMany(w => w.Flashcards)
            .HasForeignKey(f => f.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.User)
            .WithMany(u => u.Flashcards)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Concept)
            .WithMany(c => c.Flashcards)
            .HasForeignKey(f => f.ConceptId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(f => new { f.WorkspaceId, f.UserId });
        builder.HasIndex(f => f.StudyTopicId);
        builder.HasIndex(f => f.NextReviewDateUtc);

        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("Quizzes");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(q => q.DifficultyLevel)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(q => q.Workspace)
            .WithMany(w => w.Quizzes)
            .HasForeignKey(q => q.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.User)
            .WithMany(u => u.Quizzes)
            .HasForeignKey(q => q.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Questions)
            .WithOne(qq => qq.Quiz)
            .HasForeignKey(qq => qq.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Attempts)
            .WithOne(qa => qa.Quiz)
            .HasForeignKey(qa => qa.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => new { q.WorkspaceId, q.UserId });
        builder.HasIndex(q => q.StudyTopicId);

        builder.HasQueryFilter(q => !q.IsDeleted);
    }
}

public class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("QuizQuestions");

        builder.HasKey(qq => qq.Id);

        builder.Property(qq => qq.QuestionText)
            .IsRequired();

        builder.Property(qq => qq.CorrectAnswer)
            .IsRequired();

        builder.HasIndex(qq => qq.QuizId);

        builder.HasQueryFilter(qq => !qq.IsDeleted);
    }
}

public class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("QuizAttempts");

        builder.HasKey(qa => qa.Id);

        builder.HasOne(qa => qa.User)
            .WithMany(u => u.QuizAttempts)
            .HasForeignKey(qa => qa.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(qa => qa.Answers)
            .WithOne(a => a.QuizAttempt)
            .HasForeignKey(a => a.QuizAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(qa => new { qa.WorkspaceId, qa.UserId });
        builder.HasIndex(qa => qa.QuizId);

        builder.HasQueryFilter(qa => !qa.IsDeleted);
    }
}

public class QuizAnswerConfiguration : IEntityTypeConfiguration<QuizAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAnswer> builder)
    {
        builder.ToTable("QuizAnswers");

        builder.HasKey(qa => qa.Id);

        builder.Property(qa => qa.SubmittedAnswer)
            .IsRequired();

        builder.HasOne(qa => qa.QuizQuestion)
            .WithMany()
            .HasForeignKey(qa => qa.QuizQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(qa => qa.QuizAttemptId);

        builder.HasQueryFilter(qa => !qa.IsDeleted);
    }
}
