using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Study.Services;

public class SpacedRepetitionService : ISpacedRepetitionService
{
    private const double MinEaseFactor = 1.3;
    private const double DefaultEaseFactor = 2.5;

    public SpacedRepetitionResult CalculateNextReview(Flashcard card, ReviewRating rating, DateTime? nowUtc = null)
    {
        var currentEf = card.EaseFactor > 0 ? card.EaseFactor : DefaultEaseFactor;
        var now = nowUtc ?? DateTime.UtcNow;

        int newRepetitions;
        int newIntervalDays;
        double newEaseFactor;
        FlashcardState newState;

        switch (rating)
        {
            case ReviewRating.Again:
                newRepetitions = 0;
                newIntervalDays = 1;
                newEaseFactor = Math.Max(MinEaseFactor, Math.Round(currentEf - 0.2, 2));
                newState = FlashcardState.Learning;
                break;

            case ReviewRating.Hard:
                newRepetitions = card.Repetitions + 1;
                newIntervalDays = card.Repetitions switch
                {
                    0 => 1,
                    1 => 3,
                    _ => Math.Max(1, (int)Math.Floor(card.IntervalDays * 1.2))
                };
                newEaseFactor = Math.Max(MinEaseFactor, Math.Round(currentEf - 0.15, 2));
                newState = FlashcardState.Review;
                break;

            case ReviewRating.Good:
                newRepetitions = card.Repetitions + 1;
                newIntervalDays = card.Repetitions switch
                {
                    0 => 1,
                    1 => 6,
                    _ => Math.Max(1, (int)Math.Round(card.IntervalDays * currentEf))
                };
                newEaseFactor = Math.Round(currentEf, 2);
                newState = FlashcardState.Review;
                break;

            case ReviewRating.Easy:
                newRepetitions = card.Repetitions + 1;
                newIntervalDays = card.Repetitions switch
                {
                    0 => 3,
                    1 => 8,
                    _ => Math.Max(1, (int)Math.Round(card.IntervalDays * currentEf * 1.3))
                };
                newEaseFactor = Math.Round(currentEf + 0.15, 2);
                newState = newRepetitions >= 3 ? FlashcardState.Mastered : FlashcardState.Review;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(rating), $"Unsupported review rating: {rating}");
        }

        var nextReviewDateUtc = now.AddDays(newIntervalDays);

        return new SpacedRepetitionResult(
            Repetitions: newRepetitions,
            IntervalDays: newIntervalDays,
            EaseFactor: newEaseFactor,
            NextReviewDateUtc: nextReviewDateUtc,
            State: newState
        );
    }
}
