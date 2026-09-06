using Nexus.Application.DTOs.Study;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Nexus.Domain.Enums;
using Xunit;

namespace Nexus.Desktop.Tests;

public class StudyViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly FakeNavigationService _fakeNavigationService;
    private readonly FakeDialogService _fakeDialogService;
    private readonly UserSession _userSession;
    private readonly StudyViewModel _viewModel;
    private readonly Guid _workspaceId;

    public StudyViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
        _fakeNavigationService = new FakeNavigationService();
        _fakeDialogService = new FakeDialogService();

        _workspaceId = Guid.NewGuid();
        _userSession = new UserSession
        {
            SelectedWorkspace = new WorkspaceDto(_workspaceId, "Study Workspace", "Workspace for learning", "📚", "#10B981", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)
        };

        _viewModel = new StudyViewModel(_fakeApiClient, _fakeDialogService, _fakeNavigationService, _userSession);
    }

    [Fact]
    public async Task InitializeStudyAsync_LoadsAllDataAndSetsWelcomeTutorMessage()
    {
        // Arrange
        var topic = new StudyTopicDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), "Operating Systems", "OS concepts", null, null, null, 0, 0, DateTime.UtcNow, null);
        _fakeApiClient.StudyTopics.Add(topic);

        var card = new FlashcardDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), topic.Id, null, "What is virtual memory?", "Paging & swap mechanism", 2.5, 0, 1, DateTime.UtcNow.AddMinutes(-5), 0, 0, 0, null, "Medium", FlashcardState.New, null, null, null, null, null, DateTime.UtcNow);
        _fakeApiClient.Flashcards.Add(card);

        var quiz = new QuizDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), topic.Id, "OS Midterm Quiz", "Test on memory and processes", "Medium", 5, DateTime.UtcNow);
        _fakeApiClient.Quizzes.Add(quiz);

        // Act
        await _viewModel.InitializeStudyAsync();

        // Assert
        Assert.Single(_viewModel.Topics);
        Assert.Equal("Operating Systems", _viewModel.Topics[0].Title);

        Assert.Single(_viewModel.DueFlashcards);
        Assert.NotNull(_viewModel.CurrentFlashcard);
        Assert.Equal(card.Id, _viewModel.CurrentFlashcard.Id);

        Assert.Single(_viewModel.Quizzes);
        Assert.NotNull(_viewModel.SelectedQuiz);

        Assert.NotNull(_viewModel.Dashboard);
        Assert.Single(_viewModel.TutorMessages);
        Assert.Equal("Assistant", _viewModel.TutorMessages[0].Role);
        Assert.Contains("Hello! I am your NEXUS AI Study Tutor", _viewModel.TutorMessages[0].Content);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("3", 3)]
    [InlineData("4", 4)]
    public void SwitchTab_UpdatesSelectedTabIndex(string input, int expectedIndex)
    {
        _viewModel.SwitchTabCommand.Execute(input);
        Assert.Equal(expectedIndex, _viewModel.SelectedTabIndex);
    }

    [Fact]
    public async Task LoadDashboardAsync_PopulatesDashboardAndAssessment()
    {
        await _viewModel.LoadDashboardAsync();

        Assert.NotNull(_viewModel.Dashboard);
        Assert.NotNull(_viewModel.Assessment);
        Assert.False(_viewModel.IsLoadingStudy);
    }

    [Fact]
    public async Task CreateTopicAsync_Success_AddsToTopicsAndSelects()
    {
        _viewModel.NewTopicTitle = "Computer Networks";
        _viewModel.NewTopicDescription = "TCP/IP and OSI model";

        await _viewModel.CreateTopicAsync();

        Assert.Single(_viewModel.Topics);
        Assert.Equal("Computer Networks", _viewModel.Topics[0].Title);
        Assert.NotNull(_viewModel.SelectedTopic);
        Assert.Equal("Computer Networks", _viewModel.SelectedTopic.Title);
        Assert.Empty(_viewModel.NewTopicTitle);
        Assert.Empty(_viewModel.NewTopicDescription);
    }

    [Fact]
    public async Task CreateTopicAsync_EmptyTitle_DoesNothing()
    {
        _viewModel.NewTopicTitle = "   ";

        await _viewModel.CreateTopicAsync();

        Assert.Empty(_viewModel.Topics);
        Assert.Null(_viewModel.SelectedTopic);
    }

    [Fact]
    public async Task DeleteTopicAsync_Confirmed_RemovesTopic()
    {
        var topic = new StudyTopicDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), "To Delete", null, null, null, null, 0, 0, DateTime.UtcNow, null);
        _fakeApiClient.StudyTopics.Add(topic);
        _viewModel.Topics.Add(topic);
        _viewModel.SelectedTopic = topic;
        _fakeDialogService.ConfirmationResult = true;

        await _viewModel.DeleteTopicAsync(topic);

        Assert.Empty(_viewModel.Topics);
        Assert.Null(_viewModel.SelectedTopic);
        Assert.Equal(1, _fakeDialogService.ConfirmCallCount);
    }

    [Fact]
    public async Task DeleteTopicAsync_Cancelled_DoesNotRemoveTopic()
    {
        var topic = new StudyTopicDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), "Keep Me", null, null, null, null, 0, 0, DateTime.UtcNow, null);
        _fakeApiClient.StudyTopics.Add(topic);
        _viewModel.Topics.Add(topic);
        _viewModel.SelectedTopic = topic;
        _fakeDialogService.ConfirmationResult = false;

        await _viewModel.DeleteTopicAsync(topic);

        Assert.Single(_viewModel.Topics);
        Assert.NotNull(_viewModel.SelectedTopic);
    }

    [Fact]
    public async Task StartSessionAsync_CreatesActiveSession()
    {
        var topic = new StudyTopicDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), "Topic 1", null, null, null, null, 0, 0, DateTime.UtcNow, null);
        _viewModel.SelectedTopic = topic;
        _viewModel.SessionNotes = "Focusing on chapter 3";

        await _viewModel.StartSessionAsync();

        Assert.True(_viewModel.HasActiveSession);
        Assert.NotNull(_viewModel.ActiveSession);
        Assert.Equal("Study: Topic 1", _viewModel.ActiveSession.Title);
        Assert.Equal(StudySessionStatus.InProgress, _viewModel.ActiveSession.Status);
    }

    [Fact]
    public async Task CompleteSessionAsync_CompletesActiveSessionAndResets()
    {
        await _viewModel.StartSessionAsync();
        Assert.True(_viewModel.HasActiveSession);

        _viewModel.SessionNotes = "Finished 10 flashcards";
        await _viewModel.CompleteSessionAsync();

        Assert.False(_viewModel.HasActiveSession);
        Assert.Null(_viewModel.ActiveSession);
        Assert.Empty(_viewModel.SessionNotes);
    }

    [Fact]
    public async Task FlashcardReview_RevealAnswer_SetsIsCardRevealed()
    {
        var card = new FlashcardDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), null, null, "Question?", "Answer!", 2.5, 0, 1, DateTime.UtcNow.AddHours(-1), 0, 0, 0, null, "Easy", FlashcardState.New, null, null, null, null, null, DateTime.UtcNow);
        _fakeApiClient.Flashcards.Add(card);

        await _viewModel.LoadDueFlashcardsAsync();

        Assert.NotNull(_viewModel.CurrentFlashcard);
        Assert.False(_viewModel.IsCardRevealed);

        _viewModel.RevealCardAnswerCommand.Execute(null);

        Assert.True(_viewModel.IsCardRevealed);
    }

    [Fact]
    public async Task RateFlashcardAsync_AdvancesToNextCard()
    {
        var card1 = new FlashcardDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), null, null, "Card 1", "Answer 1", 2.5, 0, 1, DateTime.UtcNow.AddHours(-1), 0, 0, 0, null, "Easy", FlashcardState.New, null, null, null, null, null, DateTime.UtcNow);
        var card2 = new FlashcardDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), null, null, "Card 2", "Answer 2", 2.5, 0, 1, DateTime.UtcNow.AddHours(-1), 0, 0, 0, null, "Easy", FlashcardState.New, null, null, null, null, null, DateTime.UtcNow);
        _fakeApiClient.Flashcards.AddRange(new[] { card1, card2 });

        await _viewModel.LoadDueFlashcardsAsync();
        Assert.Equal(2, _viewModel.DueFlashcards.Count);
        Assert.Equal(card1.Id, _viewModel.CurrentFlashcard?.Id);

        _viewModel.RevealCardAnswerCommand.Execute(null);
        Assert.True(_viewModel.IsCardRevealed);

        await _viewModel.RateFlashcardAsync("Good");

        Assert.Single(_viewModel.DueFlashcards);
        Assert.Equal(card2.Id, _viewModel.CurrentFlashcard?.Id);
        Assert.False(_viewModel.IsCardRevealed);
    }

    [Fact]
    public async Task RateFlashcardAsync_LastCard_SetsCurrentFlashcardToNull()
    {
        var card1 = new FlashcardDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), null, null, "Card 1", "Answer 1", 2.5, 0, 1, DateTime.UtcNow.AddHours(-1), 0, 0, 0, null, "Easy", FlashcardState.New, null, null, null, null, null, DateTime.UtcNow);
        _fakeApiClient.Flashcards.Add(card1);

        await _viewModel.LoadDueFlashcardsAsync();
        Assert.Single(_viewModel.DueFlashcards);

        await _viewModel.RateFlashcardAsync("Easy");

        Assert.Empty(_viewModel.DueFlashcards);
        Assert.Null(_viewModel.CurrentFlashcard);
    }

    [Fact]
    public async Task CreateFlashcardAsync_AddsNewFlashcard()
    {
        _viewModel.NewCardFront = "What does SM-2 stand for?";
        _viewModel.NewCardBack = "SuperMemo 2 spaced repetition algorithm";
        _viewModel.NewCardDifficulty = "Easy";

        await _viewModel.CreateFlashcardAsync();

        Assert.Single(_viewModel.DueFlashcards);
        Assert.NotNull(_viewModel.CurrentFlashcard);
        Assert.Equal("What does SM-2 stand for?", _viewModel.CurrentFlashcard.FrontText);
        Assert.Empty(_viewModel.NewCardFront);
        Assert.Empty(_viewModel.NewCardBack);
    }

    [Fact]
    public async Task GenerateFlashcardsAsync_WithTopic_AddsGeneratedCards()
    {
        var topic = new StudyTopicDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), "Databases", null, null, null, null, 0, 0, DateTime.UtcNow, null);
        _viewModel.SelectedTopic = topic;
        _viewModel.GenerateCardCount = 3;

        await _viewModel.GenerateFlashcardsAsync();

        Assert.Equal(3, _viewModel.DueFlashcards.Count);
        Assert.NotNull(_viewModel.CurrentFlashcard);
        Assert.Contains("Generated 3 flashcards successfully", _viewModel.StudyStatusMessage);
    }

    [Fact]
    public async Task StartQuizAsync_LoadsSafeQuizAndStartsAttempt()
    {
        var quizId = Guid.NewGuid();
        var questions = new List<QuizQuestionDto>
        {
            new(Guid.NewGuid(), quizId, "What is 2+2?", "MultipleChoice", new[] { "3", "4", "5" }, "4", "Math rule", "Easy", 0, null, null, null),
            new(Guid.NewGuid(), quizId, "What is the capital of France?", "MultipleChoice", new[] { "Berlin", "Paris", "Madrid" }, "Paris", "Geography", "Easy", 1, null, null, null)
        };
        var quizDetail = new QuizDetailDto(quizId, _workspaceId, Guid.NewGuid(), null, "Sample Quiz", "Desc", "Easy", questions, DateTime.UtcNow);
        var quizDto = new QuizDto(quizId, _workspaceId, Guid.NewGuid(), null, "Sample Quiz", "Desc", "Easy", 2, DateTime.UtcNow);

        _fakeApiClient.QuizDetails.Add(quizDetail);
        _fakeApiClient.Quizzes.Add(quizDto);

        await _viewModel.StartQuizAsync(quizDto);

        Assert.True(_viewModel.IsTakingQuiz);
        Assert.False(_viewModel.IsQuizFinished);
        Assert.NotNull(_viewModel.ActiveQuiz);
        Assert.Equal(2, _viewModel.ActiveQuiz.Questions.Count);
        Assert.Equal(0, _viewModel.CurrentQuestionIndex);
        Assert.Equal("What is 2+2?", _viewModel.CurrentQuestion?.QuestionText);
        Assert.NotNull(_viewModel.LastQuizResult);
    }

    [Fact]
    public async Task QuizNavigation_NextAndPreviousQuestion_PreservesSelection()
    {
        var quizId = Guid.NewGuid();
        var q1 = new QuizQuestionDto(Guid.NewGuid(), quizId, "Q1", "MultipleChoice", new[] { "A", "B" }, "A", "Expl 1", "Easy", 0, null, null, null);
        var q2 = new QuizQuestionDto(Guid.NewGuid(), quizId, "Q2", "MultipleChoice", new[] { "X", "Y" }, "X", "Expl 2", "Easy", 1, null, null, null);
        _fakeApiClient.QuizDetails.Add(new QuizDetailDto(quizId, _workspaceId, Guid.NewGuid(), null, "Nav Quiz", null, "Easy", new[] { q1, q2 }, DateTime.UtcNow));
        var quizDto = new QuizDto(quizId, _workspaceId, Guid.NewGuid(), null, "Nav Quiz", null, "Easy", 2, DateTime.UtcNow);

        await _viewModel.StartQuizAsync(quizDto);

        // Select Option A on Q1
        _viewModel.SelectAnswerOptionCommand.Execute("A");
        Assert.Equal("A", _viewModel.SelectedAnswerOption);

        // Move to Q2
        _viewModel.NextQuizQuestionCommand.Execute(null);
        Assert.Equal(1, _viewModel.CurrentQuestionIndex);
        Assert.Null(_viewModel.SelectedAnswerOption);

        // Select Option Y on Q2
        _viewModel.SelectAnswerOptionCommand.Execute("Y");
        Assert.Equal("Y", _viewModel.SelectedAnswerOption);

        // Move back to Q1
        _viewModel.PreviousQuizQuestionCommand.Execute(null);
        Assert.Equal(0, _viewModel.CurrentQuestionIndex);
        Assert.Equal("A", _viewModel.SelectedAnswerOption);
    }

    [Fact]
    public async Task SubmitQuizAsync_GradesAttemptAndShowsScore()
    {
        var quizId = Guid.NewGuid();
        var q1 = new QuizQuestionDto(Guid.NewGuid(), quizId, "Q1", "MultipleChoice", new[] { "Option A", "Option B" }, "Option A", "Expl", "Easy", 0, null, null, null);
        _fakeApiClient.QuizDetails.Add(new QuizDetailDto(quizId, _workspaceId, Guid.NewGuid(), null, "Scored Quiz", null, "Easy", new[] { q1 }, DateTime.UtcNow));
        var quizDto = new QuizDto(quizId, _workspaceId, Guid.NewGuid(), null, "Scored Quiz", null, "Easy", 1, DateTime.UtcNow);

        await _viewModel.StartQuizAsync(quizDto);
        _viewModel.SelectAnswerOptionCommand.Execute("Option A");

        await _viewModel.SubmitQuizAsync();

        Assert.False(_viewModel.IsTakingQuiz);
        Assert.True(_viewModel.IsQuizFinished);
        Assert.NotNull(_viewModel.LastQuizResult);
        Assert.Equal(100.0, _viewModel.LastQuizResult.ScorePercentage);
        Assert.Equal(1, _viewModel.LastQuizResult.CorrectAnswers);
        Assert.Contains("Quiz Completed! Score: 100.0%", _viewModel.StudyStatusMessage);
    }

    [Fact]
    public async Task GenerateQuizAsync_AddsNewQuizAndSelectsIt()
    {
        var topic = new StudyTopicDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), "Compilers", null, null, null, null, 0, 0, DateTime.UtcNow, null);
        _viewModel.SelectedTopic = topic;
        _viewModel.GenerateQuizCount = 3;

        await _viewModel.GenerateQuizAsync();

        Assert.Single(_viewModel.Quizzes);
        Assert.NotNull(_viewModel.SelectedQuiz);
        Assert.Equal("Generated Quiz", _viewModel.SelectedQuiz.Title);
        Assert.Contains("Created quiz 'Generated Quiz'", _viewModel.StudyStatusMessage);
    }

    [Fact]
    public async Task SendTutorMessageAsync_AppendsUserAndAssistantMessages()
    {
        _viewModel.TutorInput = "Can you clarify dynamic programming?";

        await _viewModel.SendTutorMessageAsync();

        Assert.Equal(2, _viewModel.TutorMessages.Count);
        Assert.Equal("User", _viewModel.TutorMessages[0].Role);
        Assert.Equal("Can you clarify dynamic programming?", _viewModel.TutorMessages[0].Content);

        Assert.Equal("Assistant", _viewModel.TutorMessages[1].Role);
        Assert.Contains("I am your AI Study Tutor", _viewModel.TutorMessages[1].Content);
        Assert.NotEmpty(_viewModel.TutorMessages[1].KeyTakeaways!);
        Assert.Empty(_viewModel.TutorInput);
        Assert.False(_viewModel.IsTutorBusy);
    }

    [Fact]
    public async Task GetTutorHintAsync_SetsTutorHintAndShowsDialog()
    {
        var quizId = Guid.NewGuid();
        var q1 = new QuizQuestionDto(Guid.NewGuid(), quizId, "Question needing hint", "MultipleChoice", new[] { "A", "B" }, "A", "Expl", "Hard", 0, null, null, null);
        _fakeApiClient.QuizDetails.Add(new QuizDetailDto(quizId, _workspaceId, Guid.NewGuid(), null, "Hint Quiz", null, "Hard", new[] { q1 }, DateTime.UtcNow));
        var quizDto = new QuizDto(quizId, _workspaceId, Guid.NewGuid(), null, "Hint Quiz", null, "Hard", 1, DateTime.UtcNow);

        await _viewModel.StartQuizAsync(quizDto);

        await _viewModel.GetTutorHintAsync();

        Assert.NotNull(_viewModel.TutorHint);
        Assert.Contains("Think about how the core principle operates", _viewModel.TutorHint);
        Assert.Equal(1, _fakeDialogService.ShowMessageCallCount);
        Assert.Contains("💡 Tutor Hint", _fakeDialogService.LastMessageTitle);
    }

    [Fact]
    public async Task ExplainWrongAnswerAsync_SetsExplanationAndShowsDialog()
    {
        var wrongResult = new QuizAnswerResultDto(Guid.NewGuid(), "What is O(1)?", "Linear", "Constant", false, "Linear is O(N)");

        await _viewModel.ExplainWrongAnswerAsync(wrongResult);

        Assert.NotNull(_viewModel.TutorExplanation);
        Assert.Contains("misconception", _viewModel.TutorExplanation.Explanation);
        Assert.Equal(1, _fakeDialogService.ShowMessageCallCount);
        Assert.Contains("AI Tutor Explanation", _fakeDialogService.LastMessageTitle);
    }

    [Fact]
    public async Task GenerateMiniExerciseAsync_AddsExerciseToChat()
    {
        await _viewModel.GenerateMiniExerciseAsync();

        Assert.NotNull(_viewModel.TutorMiniExercise);
        Assert.Single(_viewModel.TutorMessages);
        Assert.Contains("Mini Practice Exercise", _viewModel.TutorMessages[0].Content);
    }
}
