using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class StudySessionConfiguration : IEntityTypeConfiguration<StudySession>
{
    public void Configure(EntityTypeBuilder<StudySession> builder)
    {
        builder.ToTable("StudySessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(150);

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

        builder.HasOne(f => f.Concept)
            .WithMany(c => c.Flashcards)
            .HasForeignKey(f => f.ConceptId)
            .OnDelete(DeleteBehavior.SetNull);

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

        builder.HasMany(q => q.Questions)
            .WithOne(qq => qq.Quiz)
            .HasForeignKey(qq => qq.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Attempts)
            .WithOne(qa => qa.Quiz)
            .HasForeignKey(qa => qa.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

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

        builder.HasQueryFilter(qa => !qa.IsDeleted);
    }
}
