using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Study;
using Nexus.Application.Features.Conversations.Services;
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
        public LLMRequest? LastRequest { get; private set; }

        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
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
        var convService = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance, mockLlm, Options.Create(new AiKnowledgeOptions()));

        var tutorService = new AiTutorService(_context, _currentUserService, convService, assessmentService, Options.Create(new StudyOptions()), NullLogger<AiTutorService>.Instance, mockLlm, mockRag);

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
        var mockRag = new MockRagService();
        var convService = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance, mockLlm, Options.Create(new AiKnowledgeOptions()));

        var tutorService = new AiTutorService(_context, _currentUserService, convService, assessmentService, Options.Create(new StudyOptions()), NullLogger<AiTutorService>.Instance, mockLlm);

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

    // ==================== 8. Phase 6 Hardened Quiz Attempt Isolation Tests ====================

    [Fact]
    public async Task QuizService_AttemptUserIsolation_UserACannotSubmitUserBAttempt_InSameWorkspace()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        // Alice creates quiz and starts attempt in Alice's workspace
        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Isolation Quiz" };
        var qn = new QuizQuestion
        {
            Quiz = quiz,
            QuestionText = "Is user isolation enforced?",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "Yes",
            OptionsJson = "[\"Yes\", \"No\"]"
        };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.Add(qn);
        await _context.SaveChangesAsync();

        // Alice starts attempt
        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(startRes.IsSuccess);
        var attemptId = startRes.Value.AttemptId;

        // Bob is a member of Alice's workspace
        _context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = _workspaceAlice.Id, UserId = _bob.Id, Role = WorkspaceRole.Editor });
        await _context.SaveChangesAsync();

        // Switch active user to Bob
        _currentUserService.UserId = _bob.Id;
        _currentUserService.Email = _bob.Email;

        // Bob attempts to submit Alice's attempt -> Rejected!
        var submitRes = await service.SubmitQuizAttemptAsync(_workspaceAlice.Id, attemptId, new SubmitQuizAttemptRequest(new List<SubmitQuizAnswerDto>
        {
            new(qn.Id, "Yes")
        }));

        Assert.False(submitRes.IsSuccess);
        Assert.Equal("QuizAttempt.NotFound", submitRes.Error.Code);
    }

    [Fact]
    public async Task QuizService_AttemptUserIsolation_UserACannotReadUserBAttemptResult_InSameWorkspace()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Result Isolation Quiz" };
        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync();

        // Alice starts attempt
        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(startRes.IsSuccess);
        var attemptId = startRes.Value.AttemptId;

        // Bob is added to Alice's workspace
        _context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = _workspaceAlice.Id, UserId = _bob.Id, Role = WorkspaceRole.Editor });
        await _context.SaveChangesAsync();

        // Switch to Bob
        _currentUserService.UserId = _bob.Id;
        _currentUserService.Email = _bob.Email;

        // Bob tries to read Alice's attempt result -> Rejected!
        var readRes = await service.GetAttemptResultAsync(_workspaceAlice.Id, attemptId);
        Assert.False(readRes.IsSuccess);
        Assert.Equal("QuizAttempt.NotFound", readRes.Error.Code);
    }

    [Fact]
    public async Task QuizService_AttemptUserIsolation_CrossWorkspaceAttempt_IsInaccessible()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Cross WS Quiz" };
        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync();

        // Alice starts attempt in Workspace Alice
        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(startRes.IsSuccess);

        // Attempting to access Alice's attempt under Workspace Bob is rejected
        var crossResult = await service.GetAttemptResultAsync(_workspaceBob.Id, startRes.Value.AttemptId);
        Assert.False(crossResult.IsSuccess);
    }

    [Fact]
    public async Task QuizService_AttemptUserIsolation_SoftDeletedAttempt_IsInaccessible()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Deleted Attempt Quiz" };
        var qn = new QuizQuestion { Quiz = quiz, QuestionText = "Q?", CorrectAnswer = "A", OptionsJson = "[\"A\"]" };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.Add(qn);
        await _context.SaveChangesAsync();

        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(startRes.IsSuccess);

        // Soft-delete the attempt
        var attempt = await _context.QuizAttempts.FindAsync(startRes.Value.AttemptId);
        Assert.NotNull(attempt);
        attempt.IsDeleted = true;
        await _context.SaveChangesAsync();

        // Reading attempt result should fail
        var readRes = await service.GetAttemptResultAsync(_workspaceAlice.Id, startRes.Value.AttemptId);
        Assert.False(readRes.IsSuccess);
        Assert.Equal("QuizAttempt.NotFound", readRes.Error.Code);

        // Submitting attempt should fail
        var submitRes = await service.SubmitQuizAttemptAsync(_workspaceAlice.Id, startRes.Value.AttemptId, new SubmitQuizAttemptRequest(new List<SubmitQuizAnswerDto>
        {
            new(qn.Id, "A")
        }));
        Assert.False(submitRes.IsSuccess);
        Assert.Equal("QuizAttempt.NotFound", submitRes.Error.Code);
    }

    // ==================== 9. Quiz Question Validation & Grading Tests ====================

    [Fact]
    public async Task QuizService_SubmitAttempt_RejectsUnknownQuestionId()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Validation Quiz" };
        var qn = new QuizQuestion { Quiz = quiz, QuestionText = "Valid Q", CorrectAnswer = "Valid A", OptionsJson = "[\"Valid A\"]" };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.Add(qn);
        await _context.SaveChangesAsync();

        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(startRes.IsSuccess);

        // Submit with non-existent question ID
        var fakeId = Guid.NewGuid();
        var submitRes = await service.SubmitQuizAttemptAsync(_workspaceAlice.Id, startRes.Value.AttemptId, new SubmitQuizAttemptRequest(new List<SubmitQuizAnswerDto>
        {
            new(fakeId, "Valid A")
        }));

        Assert.False(submitRes.IsSuccess);
        Assert.Equal("QuizAttempt.InvalidQuestionId", submitRes.Error.Code);
        Assert.Contains(fakeId.ToString(), submitRes.Error.Description);
    }

    [Fact]
    public async Task QuizService_SubmitAttempt_RejectsQuestionBelongingToAnotherQuiz()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz1 = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Quiz 1" };
        var qn1 = new QuizQuestion { Quiz = quiz1, QuestionText = "Q1", CorrectAnswer = "A1", OptionsJson = "[\"A1\"]" };

        var quiz2 = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Quiz 2" };
        var qn2 = new QuizQuestion { Quiz = quiz2, QuestionText = "Q2", CorrectAnswer = "A2", OptionsJson = "[\"A2\"]" };

        _context.Quizzes.AddRange(quiz1, quiz2);
        _context.QuizQuestions.AddRange(qn1, qn2);
        await _context.SaveChangesAsync();

        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz1.Id);
        Assert.True(startRes.IsSuccess);

        // Submit quiz1 attempt but inject Q2 from quiz2
        var submitRes = await service.SubmitQuizAttemptAsync(_workspaceAlice.Id, startRes.Value.AttemptId, new SubmitQuizAttemptRequest(new List<SubmitQuizAnswerDto>
        {
            new(qn1.Id, "A1"),
            new(qn2.Id, "A2")
        }));

        Assert.False(submitRes.IsSuccess);
        Assert.Equal("QuizAttempt.InvalidQuestionId", submitRes.Error.Code);
    }

    [Fact]
    public async Task QuizService_SubmitAttempt_RejectsDuplicateQuestionIds()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Duplicate Q Quiz" };
        var qn = new QuizQuestion { Quiz = quiz, QuestionText = "Duplicate Q", CorrectAnswer = "Alpha", OptionsJson = "[\"Alpha\", \"Beta\"]" };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.Add(qn);
        await _context.SaveChangesAsync();

        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(startRes.IsSuccess);

        // Submit multiple answers for the same question ID
        var submitRes = await service.SubmitQuizAttemptAsync(_workspaceAlice.Id, startRes.Value.AttemptId, new SubmitQuizAttemptRequest(new List<SubmitQuizAnswerDto>
        {
            new(qn.Id, "Alpha"),
            new(qn.Id, "Beta")
        }));

        Assert.False(submitRes.IsSuccess);
        Assert.Equal("QuizAttempt.DuplicateQuestionId", submitRes.Error.Code);
    }

    [Fact]
    public async Task QuizService_SubmitAttempt_GradesUsingDatabaseAuthoritativeAnswer()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Grading Quiz" };
        var qn1 = new QuizQuestion { Quiz = quiz, QuestionText = "Capital of France?", CorrectAnswer = "Paris", OptionsJson = "[\"Paris\", \"London\", \"Rome\"]" };
        var qn2 = new QuizQuestion { Quiz = quiz, QuestionText = "Capital of Italy?", CorrectAnswer = "Rome", OptionsJson = "[\"Paris\", \"London\", \"Rome\"]" };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.AddRange(qn1, qn2);
        await _context.SaveChangesAsync();

        var startRes = await service.StartQuizAttemptAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(startRes.IsSuccess);

        // Client submits 1 correct and 1 wrong answer
        var submitRes = await service.SubmitQuizAttemptAsync(_workspaceAlice.Id, startRes.Value.AttemptId, new SubmitQuizAttemptRequest(new List<SubmitQuizAnswerDto>
        {
            new(qn1.Id, "Paris"),  // Correct
            new(qn2.Id, "London")  // Wrong
        }));

        Assert.True(submitRes.IsSuccess);
        Assert.True(submitRes.Value.IsCompleted);
        Assert.Equal(2, submitRes.Value.TotalQuestions);
        Assert.Equal(1, submitRes.Value.CorrectAnswers);
        Assert.Equal(50.0, submitRes.Value.ScorePercentage);

        // Verify database state is authoritative
        var savedAttempt = await _context.QuizAttempts.Include(a => a.Answers).FirstOrDefaultAsync(a => a.Id == startRes.Value.AttemptId);
        Assert.NotNull(savedAttempt);
        Assert.True(savedAttempt.IsCompleted);
        Assert.Equal(50.0, savedAttempt.ScorePercentage);
        Assert.Equal(2, savedAttempt.Answers.Count);
        Assert.Contains(savedAttempt.Answers, a => a.QuizQuestionId == qn1.Id && a.IsCorrect);
        Assert.Contains(savedAttempt.Answers, a => a.QuizQuestionId == qn2.Id && !a.IsCorrect);
    }

    // ==================== 10. Quiz Answer-Key Authorization Tests ====================

    [Fact]
    public async Task QuizService_AnswerKeyAuthorization_OnlyCreatorOrOwnerCanAccessFullAnswerKey()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Secret Exam" };
        var qn = new QuizQuestion
        {
            Quiz = quiz,
            QuestionText = "Top Secret Question",
            CorrectAnswer = "SuperSecretAnswer42",
            Explanation = "Classified Explanation",
            OptionsJson = "[\"SuperSecretAnswer42\", \"Wrong1\", \"Wrong2\"]"
        };
        _context.Quizzes.Add(quiz);
        _context.QuizQuestions.Add(qn);
        await _context.SaveChangesAsync();

        // Add Bob as Editor (study taker, not creator, not owner)
        _context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = _workspaceAlice.Id, UserId = _bob.Id, Role = WorkspaceRole.Editor });
        await _context.SaveChangesAsync();

        // Switch to Bob
        _currentUserService.UserId = _bob.Id;
        _currentUserService.Email = _bob.Email;

        // 1. Bob requests full quiz (with answer key) -> Denied!
        var bobFullRes = await service.GetQuizByIdAsync(_workspaceAlice.Id, quiz.Id);
        Assert.False(bobFullRes.IsSuccess);
        Assert.Equal("Quiz.AccessDenied", bobFullRes.Error.Code);

        // 2. Bob requests safe quiz -> Allowed! Answers and explanations are omitted.
        var bobSafeRes = await service.GetSafeQuizByIdAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(bobSafeRes.IsSuccess);
        Assert.Single(bobSafeRes.Value.Questions);
        Assert.Equal("Top Secret Question", bobSafeRes.Value.Questions[0].QuestionText);

        // 3. Switch back to Alice (creator and workspace owner)
        _currentUserService.UserId = _alice.Id;
        _currentUserService.Email = _alice.Email;

        // Alice requests full quiz -> Allowed with full answer keys!
        var aliceFullRes = await service.GetQuizByIdAsync(_workspaceAlice.Id, quiz.Id);
        Assert.True(aliceFullRes.IsSuccess);
        Assert.Equal("SuperSecretAnswer42", aliceFullRes.Value.Questions[0].CorrectAnswer);
        Assert.Equal("Classified Explanation", aliceFullRes.Value.Questions[0].Explanation);
    }

    [Fact]
    public async Task QuizService_AnswerKeyAuthorization_WorkspaceOwnerCanAccessAnyQuizFullDetail()
    {
        var service = new QuizService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<QuizService>.Instance);

        // Add Bob as Editor
        _context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = _workspaceAlice.Id, UserId = _bob.Id, Role = WorkspaceRole.Editor });
        await _context.SaveChangesAsync();

        // Bob creates a quiz in Alice's workspace
        var bobQuiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _bob.Id, Title = "Bob Created Quiz" };
        var qn = new QuizQuestion { Quiz = bobQuiz, QuestionText = "Q?", CorrectAnswer = "SecretKey", OptionsJson = "[\"SecretKey\"]" };
        _context.Quizzes.Add(bobQuiz);
        _context.QuizQuestions.Add(qn);
        await _context.SaveChangesAsync();

        // Alice (owner of _workspaceAlice) requests full quiz
        _currentUserService.UserId = _alice.Id;
        _currentUserService.Email = _alice.Email;

        var ownerRes = await service.GetQuizByIdAsync(_workspaceAlice.Id, bobQuiz.Id);
        Assert.True(ownerRes.IsSuccess);
        Assert.Equal("SecretKey", ownerRes.Value.Questions[0].CorrectAnswer);
    }

    // ==================== 11. Deterministic Spaced Repetition (SM-2) Coverage ====================

    [Fact]
    public void SpacedRepetition_DeterministicOutput_ForIdenticalInput()
    {
        var service = new SpacedRepetitionService();
        var card = new Flashcard
        {
            EaseFactor = 2.4,
            Repetitions = 2,
            IntervalDays = 6,
            State = FlashcardState.Review
        };

        var fixedTimestamp = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

        var run1 = service.CalculateNextReview(card, ReviewRating.Good, fixedTimestamp);
        var run2 = service.CalculateNextReview(card, ReviewRating.Good, fixedTimestamp);

        Assert.Equal(run1.Repetitions, run2.Repetitions);
        Assert.Equal(run1.IntervalDays, run2.IntervalDays);
        Assert.Equal(run1.EaseFactor, run2.EaseFactor);
        Assert.Equal(run1.NextReviewDateUtc, run2.NextReviewDateUtc);
        Assert.Equal(run1.State, run2.State);
    }

    [Fact]
    public void SpacedRepetition_ProgressionAndTransitions_CoversAllRatingsAndMastery()
    {
        var service = new SpacedRepetitionService();
        var now = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);

        // 1. Initial new card
        var card = new Flashcard
        {
            EaseFactor = 2.5,
            Repetitions = 0,
            IntervalDays = 1,
            State = FlashcardState.New
        };

        // First review: Good -> 1 rep, 1 interval, State: Review
        var step1 = service.CalculateNextReview(card, ReviewRating.Good, now);
        Assert.Equal(1, step1.Repetitions);
        Assert.Equal(1, step1.IntervalDays);
        Assert.Equal(2.5, step1.EaseFactor);
        Assert.Equal(FlashcardState.Review, step1.State);
        Assert.Equal(now.AddDays(1), step1.NextReviewDateUtc);

        // Second review: Good -> 2 reps, 6 interval, State: Review
        card.Repetitions = step1.Repetitions;
        card.IntervalDays = step1.IntervalDays;
        card.EaseFactor = step1.EaseFactor;
        var step2 = service.CalculateNextReview(card, ReviewRating.Good, now);
        Assert.Equal(2, step2.Repetitions);
        Assert.Equal(6, step2.IntervalDays);
        Assert.Equal(2.5, step2.EaseFactor);
        Assert.Equal(FlashcardState.Review, step2.State);
        Assert.Equal(now.AddDays(6), step2.NextReviewDateUtc);

        // Third review: Easy -> 3 reps, accelerated interval, State: Mastered!
        card.Repetitions = step2.Repetitions;
        card.IntervalDays = step2.IntervalDays;
        card.EaseFactor = step2.EaseFactor;
        var step3 = service.CalculateNextReview(card, ReviewRating.Easy, now);
        Assert.Equal(3, step3.Repetitions);
        Assert.True(step3.IntervalDays >= 8);
        Assert.Equal(2.65, step3.EaseFactor);
        Assert.Equal(FlashcardState.Mastered, step3.State);

        // Fourth review: Again -> resets to 0 reps, 1 interval, State: Learning!
        card.Repetitions = step3.Repetitions;
        card.IntervalDays = step3.IntervalDays;
        card.EaseFactor = step3.EaseFactor;
        var step4 = service.CalculateNextReview(card, ReviewRating.Again, now);
        Assert.Equal(0, step4.Repetitions);
        Assert.Equal(1, step4.IntervalDays);
        Assert.Equal(2.45, step4.EaseFactor);
        Assert.Equal(FlashcardState.Learning, step4.State);
    }

    // ==================== 12. Large Document Context & AI Provenance ====================

    [Fact]
    public async Task FlashcardService_DocumentContext_SmallDocumentUsesCompleteContent()
    {
        var spacedRep = new SpacedRepetitionService();
        var mockLlm = new MockLlmService
        {
            ResponseContent = "[{\"front\": \"Small front\", \"back\": \"Small back\", \"difficulty\": \"Easy\"}]"
        };

        var doc = new Document { WorkspaceId = _workspaceAlice.Id, Title = "Small Spec", ExtractedText = "Small doc text" };
        _context.Documents.Add(doc);

        // 3 small chunks that fit well within MaxContextCharacters
        var c1 = new DocumentChunk { DocumentId = doc.Id, WorkspaceId = _workspaceAlice.Id, ChunkIndex = 0, StartPosition = 0, EndPosition = 50, Text = "Part 1: Introduction to system." };
        var c2 = new DocumentChunk { DocumentId = doc.Id, WorkspaceId = _workspaceAlice.Id, ChunkIndex = 1, StartPosition = 51, EndPosition = 100, Text = "Part 2: Core mechanisms." };
        var c3 = new DocumentChunk { DocumentId = doc.Id, WorkspaceId = _workspaceAlice.Id, ChunkIndex = 2, StartPosition = 101, EndPosition = 150, Text = "Part 3: Final conclusions." };
        _context.DocumentChunks.AddRange(c1, c2, c3);
        await _context.SaveChangesAsync();

        var service = new FlashcardService(_context, _currentUserService, spacedRep, Options.Create(new StudyOptions()), NullLogger<FlashcardService>.Instance, mockLlm);

        var result = await service.GenerateFlashcardsAsync(_workspaceAlice.Id, null, new GenerateFlashcardsRequest("Document", doc.Id, 1, "Easy"));
        Assert.True(result.IsSuccess);

        // Check that the prompt sent to LLM contains ALL three parts
        Assert.NotNull(mockLlm.LastRequest);
        var prompt = mockLlm.LastRequest.Messages[0].Content;
        Assert.Contains("Part 1: Introduction to system.", prompt);
        Assert.Contains("Part 2: Core mechanisms.", prompt);
        Assert.Contains("Part 3: Final conclusions.", prompt);
    }

    [Fact]
    public async Task FlashcardService_DocumentContext_LargeDocumentSelectsDistributedRepresentativeChunks()
    {
        var spacedRep = new SpacedRepetitionService();
        var mockLlm = new MockLlmService
        {
            ResponseContent = "[{\"front\": \"Large front\", \"back\": \"Large back\", \"difficulty\": \"Medium\"}]"
        };

        var doc = new Document { WorkspaceId = _workspaceAlice.Id, Title = "Large Architecture Book", ExtractedText = "Large doc" };
        _context.Documents.Add(doc);

        // Create 20 chunks with non-contiguous chunk indexes and large text that exceeds MaxContextCharacters (6000)
        for (int i = 0; i < 20; i++)
        {
            _context.DocumentChunks.Add(new DocumentChunk
            {
                DocumentId = doc.Id,
                WorkspaceId = _workspaceAlice.Id,
                ChunkIndex = i * 10, // Non-contiguous chunk index
                StartPosition = i * 500,
                EndPosition = (i + 1) * 500,
                Text = $"[SECTION_{i}] " + new string((char)('A' + (i % 26)), 450)
            });
        }
        await _context.SaveChangesAsync();

        var service = new FlashcardService(_context, _currentUserService, spacedRep, Options.Create(new StudyOptions()), NullLogger<FlashcardService>.Instance, mockLlm);

        var result = await service.GenerateFlashcardsAsync(_workspaceAlice.Id, null, new GenerateFlashcardsRequest("Document", doc.Id, 1, "Medium"));
        Assert.True(result.IsSuccess);

        Assert.NotNull(mockLlm.LastRequest);
        var prompt = mockLlm.LastRequest.Messages[0].Content;

        // Verify distributed chunks were selected (beginning, middle, and end)
        Assert.Contains("[SECTION_0]", prompt);   // Beginning
        Assert.Contains("[SECTION_19]", prompt);  // End

        // Context must remain bounded
        Assert.True(prompt.Length <= 6500, $"Prompt length {prompt.Length} exceeded expected bounded context size.");
    }

    [Fact]
    public async Task StudyServices_RecordAiGenerationProvenance()
    {
        var spacedRep = new SpacedRepetitionService();
        var mockLlm = new MockLlmService
        {
            ResponseContent = "[{\"front\": \"Provenance Front\", \"back\": \"Provenance Back\", \"difficulty\": \"Hard\"}]"
        };

        var doc = new Document { WorkspaceId = _workspaceAlice.Id, Title = "Provenance Doc", ExtractedText = "Sample text for provenance." };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var flashcardService = new FlashcardService(_context, _currentUserService, spacedRep, Options.Create(new StudyOptions()), NullLogger<FlashcardService>.Instance, mockLlm);

        var genRes = await flashcardService.GenerateFlashcardsAsync(_workspaceAlice.Id, null, new GenerateFlashcardsRequest("Document", doc.Id, 1, "Hard"));
        Assert.True(genRes.IsSuccess);
        Assert.Single(genRes.Value);

        var card = genRes.Value[0];
        Assert.NotNull(card.AiGenerationId);

        // Verify AiGeneration entity was created in database
        var aiGen = await _context.AiGenerations.FindAsync(card.AiGenerationId.Value);
        Assert.NotNull(aiGen);
        Assert.Equal("GenerateFlashcards", aiGen.Operation);
        Assert.Equal(doc.Id, aiGen.SourceDocumentId);
        Assert.Equal(_alice.Id, aiGen.UserId);
    }

    // ==================== 13. Assessment & Dashboard Aggregations ====================

    [Fact]
    public async Task KnowledgeAssessmentService_HandlesEmptyWorkspaceWithoutErrors()
    {
        var assessmentService = new KnowledgeAssessmentService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<KnowledgeAssessmentService>.Instance);

        var emptyWs = new Workspace { Name = "Empty WS", OwnerId = _alice.Id };
        _context.Workspaces.Add(emptyWs);
        await _context.SaveChangesAsync();

        var assessRes = await assessmentService.GetAssessmentAsync(emptyWs.Id);
        Assert.True(assessRes.IsSuccess);
        Assert.Equal(0.0, assessRes.Value.OverallMasteryPercentage);
        Assert.Equal(0, assessRes.Value.TotalFlashcards);
        Assert.Empty(assessRes.Value.StrongAreas);
        Assert.Empty(assessRes.Value.WeakAreas);

        var dashRes = await assessmentService.GetDashboardAsync(emptyWs.Id);
        Assert.True(dashRes.IsSuccess);
        Assert.Equal(0, dashRes.Value.TotalTopics);
        Assert.Equal(0, dashRes.Value.TotalFlashcards);
        Assert.Equal(0.0, dashRes.Value.OverallMasteryPercentage);
        Assert.Empty(dashRes.Value.DueFlashcardPreviews);
        Assert.Empty(dashRes.Value.RecentTopics);
    }

    [Fact]
    public async Task KnowledgeAssessmentService_AggregatesCountsAndAveragesAccurately()
    {
        var assessmentService = new KnowledgeAssessmentService(_context, _currentUserService, Options.Create(new StudyOptions()), NullLogger<KnowledgeAssessmentService>.Instance);

        var topic = new StudyTopic { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, Title = "Algorithms" };
        _context.StudyTopics.Add(topic);

        // Add 2 flashcards: 1 due, 1 not due
        var now = DateTime.UtcNow;
        var dueCard = new Flashcard
        {
            WorkspaceId = _workspaceAlice.Id,
            UserId = _alice.Id,
            StudyTopicId = topic.Id,
            FrontText = "F1",
            BackText = "B1",
            NextReviewDateUtc = now.AddHours(-1),
            ReviewCount = 2,
            CorrectCount = 2,
            WrongCount = 0
        };
        var futureCard = new Flashcard
        {
            WorkspaceId = _workspaceAlice.Id,
            UserId = _alice.Id,
            StudyTopicId = topic.Id,
            FrontText = "F2",
            BackText = "B2",
            NextReviewDateUtc = now.AddDays(5),
            ReviewCount = 2,
            CorrectCount = 1,
            WrongCount = 1
        };
        _context.Flashcards.AddRange(dueCard, futureCard);

        // Add 1 quiz and 2 completed attempts with scores 80% and 100% -> Average 90%
        var quiz = new Quiz { WorkspaceId = _workspaceAlice.Id, UserId = _alice.Id, StudyTopicId = topic.Id, Title = "Algo Quiz" };
        _context.Quizzes.Add(quiz);

        var att1 = new QuizAttempt
        {
            WorkspaceId = _workspaceAlice.Id,
            QuizId = quiz.Id,
            UserId = _alice.Id,
            IsCompleted = true,
            ScorePercentage = 80.0,
            TotalQuestions = 5,
            CorrectAnswers = 4
        };
        var att2 = new QuizAttempt
        {
            WorkspaceId = _workspaceAlice.Id,
            QuizId = quiz.Id,
            UserId = _alice.Id,
            IsCompleted = true,
            ScorePercentage = 100.0,
            TotalQuestions = 5,
            CorrectAnswers = 5
        };
        _context.QuizAttempts.AddRange(att1, att2);
        await _context.SaveChangesAsync();

        var dashRes = await assessmentService.GetDashboardAsync(_workspaceAlice.Id);
        Assert.True(dashRes.IsSuccess);
        Assert.Equal(1, dashRes.Value.TotalTopics);
        Assert.Equal(2, dashRes.Value.TotalFlashcards);
        Assert.Equal(1, dashRes.Value.DueFlashcards);
        Assert.Equal(1, dashRes.Value.TotalQuizzes);
        Assert.Equal(2, dashRes.Value.CompletedAttempts);
        Assert.Equal(90.0, dashRes.Value.AverageQuizScore);
        Assert.True(dashRes.Value.OverallMasteryPercentage > 0);
        Assert.Single(dashRes.Value.DueFlashcardPreviews);
    }
}
