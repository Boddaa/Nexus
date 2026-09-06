using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Study;
using Nexus.Application.Features.Study.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class StudyServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly User _alice;
    private readonly User _bob;
    private readonly Workspace _workspaceAlice;
    private readonly Workspace _workspaceBob;

    private class MockLlmService : ILLMService
    {
        public string ResponseContent { get; set; } = string.Empty;

        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMResponse(ResponseContent, "mock-model", 50, 50, 100));
        }
    }

    private class MockRagService : IRagService
    {
        public Task<Result<RagAnswerResult>> AnswerQuestionAsync(
            Guid workspaceId,
            string question,
            IReadOnlyList<LLMChatMessage>? conversationHistory = null,
            CancellationToken cancellationToken = default)
        {
            var sources = new List<ChatSourceDto>
            {
                new(Guid.NewGuid(), null, null, null, null, "Domain Spec", "Document", 0.95, 1, "Snippet content")
            };
            return Task.FromResult(Result.Success(new RagAnswerResult("RAG tutor response", sources, 10, 10, 20)));
        }

        public Task<RagResponse> AnswerQuestionAsync(RagRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagResponse("Answer", Array.Empty<SourceReferenceDto>(), 0, 0));
        }
    }

    public StudyServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "StudyTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(dbOptions, _currentUserService);

        _alice = new User { Email = "alice@nexus.ai", FullName = "Alice" };
        _bob = new User { Email = "bob@nexus.ai", FullName = "Bob" };
        _context.Users.AddRange(_alice, _bob);

        _workspaceAlice = new Workspace { Name = "Alice Workspace", OwnerId = _alice.Id };
        _workspaceBob = new Workspace { Name = "Bob Workspace", OwnerId = _bob.Id };
        _context.Workspaces.AddRange(_workspaceAlice, _workspaceBob);
        _context.SaveChanges();

        _currentUserService.UserId = _alice.Id;
        _currentUserService.Email = _alice.Email;
    }

    // ==================== 1. Spaced Repetition (SM-2) Tests ====================

    [Fact]
    public void SpacedRepetition_AgainRating_ResetsRepetitionsAndDecreasesEase()
    {
        var service = new SpacedRepetitionService();
        var card = new Flashcard
        {
            EaseFactor = 2.5,
            Repetitions = 3,
            IntervalDays = 10,
            State = FlashcardState.Review
        };

        var now = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        var result = service.CalculateNextReview(card, ReviewRating.Again, now);

        Assert.Equal(0, result.Repetitions);
        Assert.Equal(1, result.IntervalDays);
        Assert.Equal(2.3, result.EaseFactor);
        Assert.Equal(FlashcardState.Learning, result.State);
        Assert.Equal(now.AddDays(1), result.NextReviewDateUtc);
    }

    [Fact]
    public void SpacedRepetition_EaseFactor_NeverDropsBelowMinimum()
    {
        var service = new SpacedRepetitionService();
        var card = new Flashcard
        {
            EaseFactor = 1.35,
            Repetitions = 1,
            IntervalDays = 1
        };

        var result = service.CalculateNextReview(card, ReviewRating.Again);

        Assert.Equal(1.3, result.EaseFactor); // Min bound clamp
    }

    [Fact]
    public void SpacedRepetition_HardRating_IncrementsRepetitionsAndAdjustsInterval()
    {
        var service = new SpacedRepetitionService();
        var card = new Flashcard
        {
            EaseFactor = 2.5,
            Repetitions = 1,
            IntervalDays = 1,
            State = FlashcardState.Review
        };

        var result = service.CalculateNextReview(card, ReviewRating.Hard);

        Assert.Equal(2, result.Repetitions);
        Assert.Equal(3, result.IntervalDays);
        Assert.Equal(2.35, result.EaseFactor);
        Assert.Equal(FlashcardState.Review, result.State);
    }

    [Fact]
    public void SpacedRepetition_GoodRating_ScalesIntervalByEaseFactor()
    {
        var service = new SpacedRepetitionService();
        var card = new Flashcard
        {
            EaseFactor = 2.5,
            Repetitions = 1,
            IntervalDays = 1
        };

        var result = service.CalculateNextReview(card, ReviewRating.Good);

        Assert.Equal(2, result.Repetitions);
        Assert.Equal(6, result.IntervalDays);
        Assert.Equal(2.5, result.EaseFactor);
        Assert.Equal(FlashcardState.Review, result.State);

        // Next review with Good rating
        card.Repetitions = result.Repetitions;
        card.IntervalDays = result.IntervalDays;
        var nextResult = service.CalculateNextReview(card, ReviewRating.Good);

        Assert.Equal(3, nextResult.Repetitions);
        Assert.Equal((int)Math.Round(6 * 2.5), nextResult.IntervalDays); // 15 days
    }

    [Fact]
    public void SpacedRepetition_EasyRating_IncreasesEaseAndAcceleratesInterval()
    {
        var service = new SpacedRepetitionService();
        var card = new Flashcard
        {
            EaseFactor = 2.5,
            Repetitions = 2,
            IntervalDays = 6
        };

        var result = service.CalculateNextReview(card, ReviewRating.Easy);

        Assert.Equal(3, result.Repetitions);
        Assert.True(result.IntervalDays > 6);
        Assert.Equal(2.65, result.EaseFactor);
        Assert.Equal(FlashcardState.Mastered, result.State);
    }

    // ==================== 2. Study Topics Service Tests ====================

    [Fact]
    public async Task StudyTopicService_CreateAndGetTopics_WorksWithWorkspaceIsolation()
    {
        var service = new StudyTopicService(_context, _currentUserService, NullLogger<StudyTopicService>.Instance);

        var createRes = await service.CreateTopicAsync(_workspaceAlice.Id, new CreateStudyTopicRequest("Distributed Systems", "Consensus algorithms"));
        Assert.True(createRes.IsSuccess);
        Assert.Equal("Distributed Systems", createRes.Value.Title);

        var aliceTopics = await service.GetTopicsAsync(_workspaceAlice.Id);
        Assert.True(aliceTopics.IsSuccess);
        Assert.Single(aliceTopics.Value);

        // Bob attempting to access Alice's workspace topics is rejected
        _currentUserService.UserId = _bob.Id;
        _currentUserService.Email = _bob.Email;

        var unauthorizedAccess = await service.GetTopicsAsync(_workspaceAlice.Id);
        Assert.False(unauthorizedAccess.IsSuccess);
        Assert.Contains("AccessDenied", unauthorizedAccess.Error.Code);
    }

    [Fact]
    public async Task StudyTopicService_UpdateAndDelete_SoftDeletesTopic()
    {
        var service = new StudyTopicService(_context, _currentUserService, NullLogger<StudyTopicService>.Instance);

        var created = await service.CreateTopicAsync(_workspaceAlice.Id, new CreateStudyTopicRequest("Algorithms"));
        Assert.True(created.IsSuccess);

        var updated = await service.UpdateTopicAsync(_workspaceAlice.Id, created.Value.Id, new UpdateStudyTopicRequest("Advanced Algorithms", "Graphs and DP"));
        Assert.True(updated.IsSuccess);
        Assert.Equal("Advanced Algorithms", updated.Value.Title);

        var deleted = await service.DeleteTopicAsync(_workspaceAlice.Id, created.Value.Id);
        Assert.True(deleted.IsSuccess);

        var list = await service.GetTopicsAsync(_workspaceAlice.Id);
        Assert.True(list.IsSuccess);
        Assert.Empty(list.Value);
    }

    // ==================== 3. Study Session Service Tests ====================

    [Fact]
    public async Task StudySessionService_Lifecycle_StartsAndCompletesSession()
    {
        var service = new StudySessionService(_context, _currentUserService, NullLogger<StudySessionService>.Instance);

        var startRes = await service.StartSessionAsync(_workspaceAlice.Id, null, new StartStudySessionRequest("Review Session 1", "Focusing on DB"));
        Assert.True(startRes.IsSuccess);
        Assert.Equal(StudySessionStatus.InProgress, startRes.Value.Status);

        var completeRes = await service.CompleteSessionAsync(_workspaceAlice.Id, startRes.Value.Id, new CompleteStudySessionRequest(DurationMinutes: 25, ItemsAttempted: 10, ItemsCompleted: 9));
        Assert.True(completeRes.IsSuccess);
        Assert.Equal(StudySessionStatus.Completed, completeRes.Value.Status);
        Assert.Equal(25, completeRes.Value.DurationMinutes);
        Assert.Equal(10, completeRes.Value.ItemsAttempted);
        Assert.Equal(9, completeRes.Value.ItemsCompleted);
    }

    // ==================== 4. Flashcard Service Tests ====================

    [Fact]
    public async Task FlashcardService_CreateReviewAndDueQuery_TracksCorrectly()
    {
        var spacedRep = new SpacedRepetitionService();
        var service = new FlashcardService(_context, _currentUserService, spacedRep, Options.Create(new StudyOptions()), NullLogger<FlashcardService>.Instance);

        var createRes = await service.CreateFlashcardAsync(_workspaceAlice.Id, null, new CreateFlashcardRequest("What is CAP theorem?", "Consistency, Availability, Partition tolerance"));
        Assert.True(createRes.IsSuccess);
        Assert.Equal(0, createRes.Value.ReviewCount);

        // Due flashcards should include this card since NextReviewDateUtc is <= now
        var due = await service.GetDueFlashcardsAsync(_workspaceAlice.Id);
        Assert.True(due.IsSuccess);
        Assert.Single(due.Value);

        // Review with Good rating
        var reviewRes = await service.ReviewFlashcardAsync(_workspaceAlice.Id, createRes.Value.Id, new ReviewFlashcardRequest(ReviewRating.Good));
        Assert.True(reviewRes.IsSuccess);
        Assert.Equal(1, reviewRes.Value.ReviewCount);
        Assert.Equal(1, reviewRes.Value.CorrectCount);
        Assert.Equal(0, reviewRes.Value.WrongCount);

        // Card should now have future review date and not appear in due list
        var dueAfter = await service.GetDueFlashcardsAsync(_workspaceAlice.Id);
        Assert.True(dueAfter.IsSuccess);
        Assert.Empty(dueAfter.Value);
    }

    [Fact]
    public async Task FlashcardService_GenerateFlashcards_ParsesJsonDefensively()
    {
        var spacedRep = new SpacedRepetitionService();
        var mockLlm = new MockLlmService
        {
            ResponseContent = "```json\n[\n  {\"front\": \"What is 2PC?\", \"back\": \"Two-phase commit protocol.\", \"difficulty\": \"Hard\"}\n]\n```"
        };

        var doc = new Document { WorkspaceId = _workspaceAlice.Id, Title = "Distributed Transactions", ExtractedText = "Two phase commit is a protocol." };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var service = new FlashcardService(_context, _currentUserService, spacedRep, Options.Create(new StudyOptions()), NullLogger<FlashcardService>.Instance, mockLlm);

        var genRes = await service.GenerateFlashcardsAsync(_workspaceAlice.Id, null, new GenerateFlashcardsRequest("Document", doc.Id, 1, "Hard"));
        Assert.True(genRes.IsSuccess);
        Assert.Single(genRes.Value);
        Assert.Equal("What is 2PC?", genRes.Value[0].FrontText);
        Assert.Equal("Two-phase commit protocol.", genRes.Value[0].BackText);
        Assert.Equal(doc.Id, genRes.Value[0].SourceDocumentId);
    }

    // ==================== 5. Quiz Service & Deterministic Scoring Tests ====================

    [Fact]
    public async Task QuizService_SafeQuestionRetrieval_OmitsAnswerKeys()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Architecture Quiz" };
        var qn = new QuizQuestion
        {
            Quiz = quiz,
            QuestionText = "Which pattern decouples producers and consumers?",
            QuestionType = QuestionType.MultipleChoice,
            OptionsJson = JsonSerializer.Serialize(new[] { "Publisher-Subscriber", "Singleton", "Adapter" }),
            CorrectAnswer = "Publisher-Subscriber",
            Explanation = "Pub/Sub decouples senders and receivers via an event bus.",
            OrderIndex = 0
        };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.Add(qn);
        await _context.SaveChangesAsync();

        // Safe query (for quiz taker)
        var safeRes = await service.GetSafeQuizByIdAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(safeRes.IsSuccess);
        Assert.Single(safeRes.Value.Questions);
        Assert.Equal("Which pattern decouples producers and consumers?", safeRes.Value.Questions[0].QuestionText);

        // SafeQuestionDto type does not have CorrectAnswer or Explanation properties!
        var safeQn = safeRes.Value.Questions[0];
        Assert.Equal("MultipleChoice", safeQn.QuestionType);
        Assert.Equal(3, safeQn.Options.Count);

        // Full query (for author / review)
        var fullRes = await service.GetQuizByIdAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(fullRes.IsSuccess);
        Assert.Equal("Publisher-Subscriber", fullRes.Value.Questions[0].CorrectAnswer);
        Assert.NotNull(fullRes.Value.Questions[0].Explanation);
    }

    [Fact]
    public async Task QuizService_AttemptLifecycleAndScoring_CalculatesDeterministically()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Microservices Quiz" };
        var q1 = new QuizQuestion
        {
            Quiz = quiz,
            QuestionText = "Question 1",
            QuestionType = QuestionType.MultipleChoice,
            OptionsJson = "[\"A\", \"B\"]",
            CorrectAnswer = "A",
            OrderIndex = 0
        };
        var q2 = new QuizQuestion
        {
            Quiz = quiz,
            QuestionText = "Question 2",
            QuestionType = QuestionType.MultipleChoice,
            OptionsJson = "[\"A\", \"B\"]",
            CorrectAnswer = "B",
            OrderIndex = 1
        };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.AddRange(q1, q2);
        await _context.SaveChangesAsync();

        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(startRes.IsSuccess);
        Assert.False(startRes.Value.IsCompleted);

        // Submit attempt with 1 correct and 1 incorrect answer
        var answers = new List<SubmitQuizAnswerDto>
        {
            new(q1.Id, "A"), // Correct
            new(q2.Id, "A")  // Incorrect (Correct is B)
        };

        var submitRes = await service.SubmitQuizAttemptAsync(_workspaceAlice.Id, startRes.Value.AttemptId, new SubmitQuizAttemptRequest(answers));
        Assert.True(submitRes.IsSuccess);
        Assert.True(submitRes.Value.IsCompleted);
        Assert.Equal(2, submitRes.Value.TotalQuestions);
        Assert.Equal(1, submitRes.Value.CorrectAnswers);
        Assert.Equal(1.0, submitRes.Value.Score);
        Assert.Equal(50.0, submitRes.Value.ScorePercentage);

        // Attempting to submit again is rejected (already completed)
        var duplicateRes = await service.SubmitQuizAttemptAsync(_workspaceAlice.Id, startRes.Value.AttemptId, new SubmitQuizAttemptRequest(answers));
        Assert.False(duplicateRes.IsSuccess);
        Assert.Contains("AlreadyCompleted", duplicateRes.Error.Code);
    }

    // ==================== 6. Knowledge Assessment & Dashboard Tests ====================

    [Fact]
    public async Task KnowledgeAssessmentService_CalculatesMasteryLevelsAccurately()
    {
        var assessmentService = new KnowledgeAssessmentService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<KnowledgeAssessmentService>.Instance);

        var topicStrong = new StudyTopic { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Strong Topic" };
        var topicWeak = new StudyTopic { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Weak Topic" };
        _context.StudyTopics.AddRange(topicStrong, topicWeak);

        // Strong topic: 10 reviews, 9 correct -> 90% accuracy
        var strongCard = new Flashcard
        {
            WorkspaceId = _workspaceAlice.Id,
            UserId = _alice.Id,
            StudyTopicId = topicStrong.Id,
            FrontText = "Q",
            BackText = "A",
            ReviewCount = 10,
            CorrectCount = 9,
            WrongCount = 1
        };

        // Weak topic: 10 reviews, 3 correct -> 30% accuracy
        var weakCard = new Flashcard
        {
            WorkspaceId = _workspaceAlice.Id,
            UserId = _alice.Id,
            StudyTopicId = topicWeak.Id,
            FrontText = "Q",
            BackText = "A",
            ReviewCount = 10,
            CorrectCount = 3,
            WrongCount = 7
        };

        _context.Flashcards.AddRange(strongCard, weakCard);
        await _context.SaveChangesAsync();

        var assessment = await assessmentService.GetAssessmentAsync(_workspaceAlice.Id);
        Assert.True(assessment.IsSuccess);
        Assert.Contains(assessment.Value.StrongAreas, s => s.TopicTitle == "Strong Topic" && s.MasteryLevel == "Strong");
        Assert.Contains(assessment.Value.WeakAreas, w => w.TopicTitle == "Weak Topic" && w.MasteryLevel == "Weak");

        var dash = await assessmentService.GetDashboardAsync(_workspaceAlice.Id);
        Assert.True(dash.IsSuccess);
        Assert.Equal(2, dash.Value.TotalTopics);
        Assert.Equal(2, dash.Value.TotalFlashcards);
    }

    // ==================== 7. AI Tutor Service Tests ====================

    [Fact]
    public async Task AiTutorService_ChatAndGrounding_ReusesStudyConversation()
    {
        var assessmentService = new KnowledgeAssessmentService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<KnowledgeAssessmentService>.Instance);
        var mockLlm = new MockLlmService
        {
            ResponseContent = "Understanding event consistency is vital.\n---KEY_TAKEAWAYS---\n[\"Events are eventual\", \"State is distributed\"]\n---FOLLOW_UP_SUGGESTIONS---\n[\"Would you like an example?\"]"
        };
        var mockRag = new MockRagService();

        var tutorService = new AiTutorService(_context, _currentUserService, assessmentService, Options.Create(new StudyOptions()), NullLogger<AiTutorService>.Instance, mockLlm, mockRag);

        var topic = new StudyTopic { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Event Driven Architecture" };
        _context.StudyTopics.Add(topic);
        await _context.SaveChangesAsync();

        var chatRes = await tutorService.ChatWithTutorAsync(_workspaceAlice.Id, new TutorChatRequest("Can you explain eventual consistency?", topic.Id));
        Assert.True(chatRes.IsSuccess);
        Assert.Contains("event consistency is vital", chatRes.Value.AssistantMessage);
        Assert.NotEmpty(chatRes.Value.KeyTakeaways);
        Assert.NotEmpty(chatRes.Value.FollowUpSuggestions);

        // Verify conversation was persisted with ContextType.Study
        var savedConv = await _context.AiConversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == chatRes.Value.ConversationId);

        Assert.NotNull(savedConv);
        Assert.Equal(ContextType.Study, savedConv.ContextType);
        Assert.Equal(topic.Id, savedConv.ContextEntityId);
        Assert.Equal(2, savedConv.Messages.Count); // 1 User + 1 Assistant
    }

    [Fact]
    public async Task AiTutorService_HintAndWrongAnswerExplanation_ReturnPedagogicalAssistance()
    {
        var assessmentService = new KnowledgeAssessmentService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<KnowledgeAssessmentService>.Instance);
        var mockLlm = new MockLlmService
        {
            ResponseContent = "{\"explanation\": \"You confused latency with throughput.\", \"remedialSteps\": [\"Review network definitions\", \"Retake performance quiz\"]}"
        };

        var tutorService = new AiTutorService(_context, _currentUserService, assessmentService, Options.Create(new StudyOptions()), NullLogger<AiTutorService>.Instance, mockLlm);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Network Quiz" };
        var qn = new QuizQuestion
        {
            Quiz = quiz,
            QuestionText = "What measures the delay before transfer begins?",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "Latency",
            Explanation = "Latency is delay."
        };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.Add(qn);
        await _context.SaveChangesAsync();

        // Test Wrong Answer Explanation
        var explainRes = await tutorService.ExplainWrongAnswerAsync(_workspaceAlice.Id, new TutorExplainWrongAnswerRequest(qn.Id, "Throughput"));
        Assert.True(explainRes.IsSuccess);
        Assert.Contains("throughput", explainRes.Value.Explanation);
        Assert.Equal(2, explainRes.Value.RemedialSteps.Count);
    }
}
