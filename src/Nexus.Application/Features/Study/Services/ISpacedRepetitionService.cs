using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Study.Services;

public record SpacedRepetitionResult(
    int Repetitions,
    int IntervalDays,
    double EaseFactor,
    DateTime NextReviewDateUtc,
    FlashcardState State
);

public interface ISpacedRepetitionService
{
    SpacedRepetitionResult CalculateNextReview(Flashcard card, ReviewRating rating, DateTime? nowUtc = null);
}
