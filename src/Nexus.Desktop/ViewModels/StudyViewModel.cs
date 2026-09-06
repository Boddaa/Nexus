using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Study;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;
using Nexus.Domain.Enums;

namespace Nexus.Desktop.ViewModels;

public record TutorMessageDisplayItem(
    string Role, // "User" or "Assistant"
    string Content,
    DateTime Timestamp,
    IReadOnlyList<string>? KeyTakeaways = null,
    IReadOnlyList<string>? FollowUpSuggestions = null
);

public partial class StudyViewModel : AiOperationViewModel
{
    // ==================== Navigation & General State ====================

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0 = Dashboard, 1 = Flashcards, 2 = Quizzes, 3 = AI Tutor, 4 = Studio

    [ObservableProperty]
    private bool _isLoadingStudy;

    [ObservableProperty]
    private string? _studyStatusMessage;

    // ==================== Dashboard & Assessment ====================

    [ObservableProperty]
    private StudyDashboardDto? _dashboard;

    [ObservableProperty]
    private KnowledgeAssessmentDto? _assessment;

    [ObservableProperty]
    private ObservableCollection<StudyTopicDto> _topics = new();

    [ObservableProperty]
    private StudyTopicDto? _selectedTopic;

    [ObservableProperty]
    private string _newTopicTitle = string.Empty;

    [ObservableProperty]
    private string _newTopicDescription = string.Empty;

    // ==================== Study Sessions ====================

    [ObservableProperty]
    private StudySessionDto? _activeSession;

    [ObservableProperty]
    private bool _hasActiveSession;

    [ObservableProperty]
    private string _sessionNotes = string.Empty;

    // ==================== Flashcards (Spaced Repetition SM-2) ====================

    [ObservableProperty]
    private ObservableCollection<FlashcardDto> _dueFlashcards = new();

    [ObservableProperty]
    private FlashcardDto? _currentFlashcard;

    [ObservableProperty]
    private int _currentCardIndex = 0;

    [ObservableProperty]
    private bool _isCardRevealed = false;

    [ObservableProperty]
    private string _newCardFront = string.Empty;

    [ObservableProperty]
    private string _newCardBack = string.Empty;

    [ObservableProperty]
    private string _newCardDifficulty = "Medium";

    [ObservableProperty]
    private int _generateCardCount = 5;

    // ==================== Quizzes ====================

    [ObservableProperty]
    private ObservableCollection<QuizDto> _quizzes = new();

    [ObservableProperty]
    private QuizDto? _selectedQuiz;

    [ObservableProperty]
    private SafeQuizDetailDto? _activeQuiz;

    [ObservableProperty]
    private SafeQuizQuestionDto? _currentQuestion;

    [ObservableProperty]
    private int _currentQuestionIndex = 0;

    [ObservableProperty]
    private string? _selectedAnswerOption;

    [ObservableProperty]
    private bool _isTakingQuiz = false;

    [ObservableProperty]
    private bool _isQuizFinished = false;

    [ObservableProperty]
    private QuizAttemptResultDto? _lastQuizResult;

    [ObservableProperty]
    private int _generateQuizCount = 5;

    [ObservableProperty]
    private string _generateQuizDifficulty = "Medium";

    private readonly Dictionary<Guid, string> _quizAnswers = new();

    // ==================== AI Tutor ====================

    [ObservableProperty]
    private ObservableCollection<TutorMessageDisplayItem> _tutorMessages = new();

    [ObservableProperty]
    private string _tutorInput = string.Empty;

    [ObservableProperty]
    private bool _isTutorBusy = false;

    [ObservableProperty]
    private string? _tutorHint;

    [ObservableProperty]
    private TutorExplanationDto? _tutorExplanation;

    [ObservableProperty]
    private TutorMiniExerciseDto? _tutorMiniExercise;

    public StudyViewModel(
        IApiClient apiClient,
        IDialogService dialogService,
        INavigationService navigationService,
        UserSession userSession)
        : base(apiClient, dialogService, navigationService, userSession)
    {
    }

    public async Task InitializeStudyAsync()
    {
        await LoadDashboardAsync();
        await LoadTopicsAsync();
        await LoadDueFlashcardsAsync();
        await LoadQuizzesAsync();

        if (TutorMessages.Count == 0)
        {
            TutorMessages.Add(new TutorMessageDisplayItem(
                "Assistant",
                "Hello! I am your NEXUS AI Study Tutor. Ask me any conceptual question, request a hint during quizzes, or ask for a practice exercise!",
                DateTime.UtcNow
            ));
        }
    }

    [RelayCommand]
    private void SwitchTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out int idx))
        {
            SelectedTabIndex = idx;
        }
    }

    // ==================== Dashboard Commands ====================

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsLoadingStudy = true;
        try
        {
            var dashRes = await _apiClient.GetStudyDashboardAsync(ws.Id);
            if (dashRes.IsSuccess)
            {
                Dashboard = dashRes.Value;
            }

            var assessRes = await _apiClient.GetAssessmentAsync(ws.Id, SelectedTopic?.Id);
            if (assessRes.IsSuccess)
            {
                Assessment = assessRes.Value;
            }
        }
        finally
        {
            IsLoadingStudy = false;
        }
    }

    [RelayCommand]
    public async Task LoadTopicsAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        var res = await _apiClient.GetTopicsAsync(ws.Id);
        if (res.IsSuccess)
        {
            Topics.Clear();
            foreach (var t in res.Value)
            {
                Topics.Add(t);
            }
        }
    }

    [RelayCommand]
    public async Task CreateTopicAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || string.IsNullOrWhiteSpace(NewTopicTitle)) return;

        var req = new CreateStudyTopicRequest(NewTopicTitle.Trim(), NewTopicDescription?.Trim());
        var res = await _apiClient.CreateTopicAsync(ws.Id, req);
        if (res.IsSuccess)
        {
            Topics.Insert(0, res.Value);
            SelectedTopic = res.Value;
            NewTopicTitle = string.Empty;
            NewTopicDescription = string.Empty;
            await LoadDashboardAsync();
        }
        else
        {
            _dialogService.ShowMessage("Topic Creation Failed", res.Error.Description);
        }
    }

    [RelayCommand]
    public async Task DeleteTopicAsync(StudyTopicDto? topic)
    {
        var target = topic ?? SelectedTopic;
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || target == null) return;

        var confirmed = await _dialogService.ConfirmAsync("Delete Topic", $"Are you sure you want to delete topic '{target.Title}'?");
        if (!confirmed) return;

        var res = await _apiClient.DeleteTopicAsync(ws.Id, target.Id);
        if (res.IsSuccess)
        {
            Topics.Remove(target);
            if (SelectedTopic?.Id == target.Id) SelectedTopic = null;
            await LoadDashboardAsync();
        }
    }

    // ==================== Study Session Commands ====================

    [RelayCommand]
    public async Task StartSessionAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        var title = SelectedTopic != null ? $"Study: {SelectedTopic.Title}" : "General Study Session";
        var res = await _apiClient.StartStudySessionAsync(ws.Id, SelectedTopic?.Id, new StartStudySessionRequest(title, SessionNotes));
        if (res.IsSuccess)
        {
            ActiveSession = res.Value;
            HasActiveSession = true;
            StudyStatusMessage = $"Session '{ActiveSession.Title}' started.";
        }
    }

    [RelayCommand]
    public async Task CompleteSessionAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || ActiveSession == null) return;

        var res = await _apiClient.CompleteStudySessionAsync(ws.Id, ActiveSession.Id, new CompleteStudySessionRequest(Notes: SessionNotes));
        if (res.IsSuccess)
        {
            StudyStatusMessage = $"Session completed: {res.Value.DurationMinutes} minutes.";
            ActiveSession = null;
            HasActiveSession = false;
            SessionNotes = string.Empty;
            await LoadDashboardAsync();
        }
    }

    // ==================== Flashcard SM-2 Commands ====================

    [RelayCommand]
    public async Task LoadDueFlashcardsAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsLoadingStudy = true;
        try
        {
            var res = await _apiClient.GetDueFlashcardsAsync(ws.Id, SelectedTopic?.Id);
            if (res.IsSuccess)
            {
                DueFlashcards.Clear();
                foreach (var c in res.Value)
                {
                    DueFlashcards.Add(c);
                }
                CurrentCardIndex = 0;
                CurrentFlashcard = DueFlashcards.FirstOrDefault();
                IsCardRevealed = false;
            }
        }
        finally
        {
            IsLoadingStudy = false;
        }
    }

    [RelayCommand]
    private void RevealCardAnswer()
    {
        IsCardRevealed = true;
    }

    [RelayCommand]
    public async Task RateFlashcardAsync(string ratingStr)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || CurrentFlashcard == null) return;

        if (!Enum.TryParse<ReviewRating>(ratingStr, true, out var rating))
        {
            rating = ReviewRating.Good;
        }

        var res = await _apiClient.ReviewFlashcardAsync(ws.Id, CurrentFlashcard.Id, new ReviewFlashcardRequest(rating));
        if (res.IsSuccess)
        {
            // Remove reviewed card and advance
            if (CurrentCardIndex < DueFlashcards.Count)
            {
                DueFlashcards.RemoveAt(CurrentCardIndex);
            }

            if (DueFlashcards.Count > 0)
            {
                if (CurrentCardIndex >= DueFlashcards.Count) CurrentCardIndex = 0;
                CurrentFlashcard = DueFlashcards[CurrentCardIndex];
            }
            else
            {
                CurrentFlashcard = null;
            }

            IsCardRevealed = false;
            await LoadDashboardAsync();
        }
    }

    [RelayCommand]
    public async Task CreateFlashcardAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || string.IsNullOrWhiteSpace(NewCardFront) || string.IsNullOrWhiteSpace(NewCardBack)) return;

        var req = new CreateFlashcardRequest(NewCardFront.Trim(), NewCardBack.Trim(), NewCardDifficulty);
        var res = await _apiClient.CreateFlashcardAsync(ws.Id, SelectedTopic?.Id, req);
        if (res.IsSuccess)
        {
            DueFlashcards.Add(res.Value);
            if (CurrentFlashcard == null)
            {
                CurrentFlashcard = res.Value;
                CurrentCardIndex = DueFlashcards.Count - 1;
            }
            NewCardFront = string.Empty;
            NewCardBack = string.Empty;
            await LoadDashboardAsync();
        }
    }

    [RelayCommand]
    public async Task GenerateFlashcardsAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsLoadingStudy = true;
        StudyStatusMessage = "Generating AI study flashcards...";
        try
        {
            string sourceType = SelectedSourceType;
            Guid sourceId = SelectedSource?.Id ?? SelectedTopic?.Id ?? Guid.Empty;

            if (sourceId == Guid.Empty)
            {
                _dialogService.ShowMessage("Source Required", "Please select a knowledge source or study topic to generate flashcards.");
                return;
            }

            var req = new GenerateFlashcardsRequest(sourceType, sourceId, GenerateCardCount, "Medium");
            var res = await _apiClient.GenerateFlashcardsAsync(ws.Id, SelectedTopic?.Id, req);
            if (res.IsSuccess)
            {
                foreach (var c in res.Value)
                {
                    DueFlashcards.Add(c);
                }
                if (CurrentFlashcard == null)
                {
                    CurrentFlashcard = DueFlashcards.FirstOrDefault();
                }
                StudyStatusMessage = $"Generated {res.Value.Count} flashcards successfully.";
                await LoadDashboardAsync();
            }
            else
            {
                _dialogService.ShowMessage("Generation Failed", res.Error.Description);
            }
        }
        finally
        {
            IsLoadingStudy = false;
        }
    }

    // ==================== Quiz Arena Commands ====================

    [RelayCommand]
    public async Task LoadQuizzesAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        var res = await _apiClient.GetQuizzesAsync(ws.Id, SelectedTopic?.Id);
        if (res.IsSuccess)
        {
            Quizzes.Clear();
            foreach (var q in res.Value)
            {
                Quizzes.Add(q);
            }
            if (SelectedQuiz == null) SelectedQuiz = Quizzes.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task StartQuizAsync(QuizDto? quiz)
    {
        var target = quiz ?? SelectedQuiz;
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || target == null) return;

        IsLoadingStudy = true;
        try
        {
            var safeRes = await _apiClient.GetSafeQuizAsync(ws.Id, target.Id);
            if (!safeRes.IsSuccess)
            {
                _dialogService.ShowMessage("Could not load quiz", safeRes.Error.Description);
                return;
            }

            var attemptRes = await _apiClient.StartQuizAttemptAsync(ws.Id, target.Id);
            if (!attemptRes.IsSuccess)
            {
                _dialogService.ShowMessage("Could not start quiz attempt", attemptRes.Error.Description);
                return;
            }

            ActiveQuiz = safeRes.Value;
            _quizAnswers.Clear();
            CurrentQuestionIndex = 0;
            CurrentQuestion = ActiveQuiz.Questions.FirstOrDefault();
            SelectedAnswerOption = null;
            IsTakingQuiz = true;
            IsQuizFinished = false;
            LastQuizResult = attemptRes.Value;
        }
        finally
        {
            IsLoadingStudy = false;
        }
    }

    [RelayCommand]
    private void SelectAnswerOption(string option)
    {
        if (CurrentQuestion == null) return;
        SelectedAnswerOption = option;
        _quizAnswers[CurrentQuestion.Id] = option;
    }

    [RelayCommand]
    private void NextQuizQuestion()
    {
        if (ActiveQuiz == null || CurrentQuestionIndex >= ActiveQuiz.Questions.Count - 1) return;
        CurrentQuestionIndex++;
        CurrentQuestion = ActiveQuiz.Questions[CurrentQuestionIndex];
        _quizAnswers.TryGetValue(CurrentQuestion.Id, out var existingAnswer);
        SelectedAnswerOption = existingAnswer;
    }

    [RelayCommand]
    private void PreviousQuizQuestion()
    {
        if (ActiveQuiz == null || CurrentQuestionIndex <= 0) return;
        CurrentQuestionIndex--;
        CurrentQuestion = ActiveQuiz.Questions[CurrentQuestionIndex];
        _quizAnswers.TryGetValue(CurrentQuestion.Id, out var existingAnswer);
        SelectedAnswerOption = existingAnswer;
    }

    [RelayCommand]
    public async Task SubmitQuizAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || LastQuizResult == null || ActiveQuiz == null) return;

        IsLoadingStudy = true;
        try
        {
            var answerList = ActiveQuiz.Questions.Select(q => new SubmitQuizAnswerDto(
                QuestionId: q.Id,
                SubmittedAnswer: _quizAnswers.TryGetValue(q.Id, out var a) ? a : string.Empty
            )).ToList();

            var req = new SubmitQuizAttemptRequest(answerList);
            var res = await _apiClient.SubmitQuizAttemptAsync(ws.Id, LastQuizResult.AttemptId, req);
            if (res.IsSuccess)
            {
                LastQuizResult = res.Value;
                IsTakingQuiz = false;
                IsQuizFinished = true;
                StudyStatusMessage = $"Quiz Completed! Score: {res.Value.ScorePercentage:F1}% ({res.Value.CorrectAnswers}/{res.Value.TotalQuestions})";
                await LoadDashboardAsync();
            }
            else
            {
                _dialogService.ShowMessage("Submission Failed", res.Error.Description);
            }
        }
        finally
        {
            IsLoadingStudy = false;
        }
    }

    [RelayCommand]
    public async Task GenerateQuizAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsLoadingStudy = true;
        StudyStatusMessage = "Generating practice quiz with AI...";
        try
        {
            string sourceType = SelectedSourceType;
            Guid sourceId = SelectedSource?.Id ?? SelectedTopic?.Id ?? Guid.Empty;

            if (sourceId == Guid.Empty)
            {
                _dialogService.ShowMessage("Source Required", "Please select a source or topic to generate a quiz.");
                return;
            }

            var req = new GenerateQuizRequest(sourceType, sourceId, GenerateQuizCount, GenerateQuizDifficulty);
            var res = await _apiClient.GenerateQuizAsync(ws.Id, SelectedTopic?.Id, req);
            if (res.IsSuccess)
            {
                var quizDto = new QuizDto(
                    res.Value.Id,
                    res.Value.WorkspaceId,
                    res.Value.UserId,
                    res.Value.StudyTopicId,
                    res.Value.Title,
                    res.Value.Description,
                    res.Value.DifficultyLevel,
                    res.Value.Questions.Count,
                    res.Value.CreatedAtUtc
                );
                Quizzes.Insert(0, quizDto);
                SelectedQuiz = quizDto;
                StudyStatusMessage = $"Created quiz '{quizDto.Title}' with {quizDto.QuestionCount} questions.";
                await LoadDashboardAsync();
            }
            else
            {
                _dialogService.ShowMessage("Quiz Generation Failed", res.Error.Description);
            }
        }
        finally
        {
            IsLoadingStudy = false;
        }
    }

    // ==================== AI Tutor Commands ====================

    [RelayCommand]
    public async Task SendTutorMessageAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || string.IsNullOrWhiteSpace(TutorInput)) return;

        var message = TutorInput.Trim();
        TutorInput = string.Empty;

        TutorMessages.Add(new TutorMessageDisplayItem("User", message, DateTime.UtcNow));
        IsTutorBusy = true;

        try
        {
            var req = new TutorChatRequest(message, SelectedTopic?.Id);
            var res = await _apiClient.TutorChatAsync(ws.Id, req);
            if (res.IsSuccess)
            {
                TutorMessages.Add(new TutorMessageDisplayItem(
                    "Assistant",
                    res.Value.AssistantMessage,
                    DateTime.UtcNow,
                    res.Value.KeyTakeaways,
                    res.Value.FollowUpSuggestions
                ));
            }
            else
            {
                TutorMessages.Add(new TutorMessageDisplayItem("Assistant", $"Sorry, I encountered an issue: {res.Error.Description}", DateTime.UtcNow));
            }
        }
        finally
        {
            IsTutorBusy = false;
        }
    }

    [RelayCommand]
    public async Task GetTutorHintAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || CurrentQuestion == null) return;

        IsTutorBusy = true;
        try
        {
            var req = new TutorHintRequest(CurrentQuestion.Id, SelectedAnswerOption);
            var res = await _apiClient.TutorHintAsync(ws.Id, req);
            if (res.IsSuccess)
            {
                TutorHint = res.Value.Hint;
                _dialogService.ShowMessage("💡 Tutor Hint", res.Value.Hint);
            }
        }
        finally
        {
            IsTutorBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExplainWrongAnswerAsync(QuizAnswerResultDto? answerResult)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || answerResult == null) return;

        IsTutorBusy = true;
        try
        {
            var req = new TutorExplainWrongAnswerRequest(answerResult.QuestionId, answerResult.SubmittedAnswer);
            var res = await _apiClient.TutorExplainWrongAsync(ws.Id, req);
            if (res.IsSuccess)
            {
                TutorExplanation = res.Value;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(res.Value.Explanation);
                sb.AppendLine();
                sb.AppendLine("Remedial Steps:");
                foreach (var step in res.Value.RemedialSteps)
                {
                    sb.AppendLine($"• {step}");
                }
                _dialogService.ShowMessage("🎓 AI Tutor Explanation", sb.ToString());
            }
        }
        finally
        {
            IsTutorBusy = false;
        }
    }

    [RelayCommand]
    public async Task GenerateMiniExerciseAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsTutorBusy = true;
        try
        {
            var req = new TutorMiniExerciseRequest(SelectedTopic?.Id);
            var res = await _apiClient.TutorExerciseAsync(ws.Id, req);
            if (res.IsSuccess)
            {
                TutorMiniExercise = res.Value;
                TutorMessages.Add(new TutorMessageDisplayItem(
                    "Assistant",
                    $"**Mini Practice Exercise**:\n\n{res.Value.Question}\n\n*Type your answer or think about the answer, then click Reveal to check!*",
                    DateTime.UtcNow
                ));
            }
        }
        finally
        {
            IsTutorBusy = false;
        }
    }
}
