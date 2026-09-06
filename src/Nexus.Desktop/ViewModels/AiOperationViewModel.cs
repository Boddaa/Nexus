using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.AI;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Pages;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public record AiSourceItem(Guid Id, string Title, string Type, string Detail);

public partial class GeneratedQuestionItemViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Question { get; }
    public string Difficulty { get; }
    public string? Answer { get; }
    public IReadOnlyList<ChatSourceDto>? Sources { get; }

    [ObservableProperty]
    private bool _isAnswerRevealed;

    public GeneratedQuestionItemViewModel(GeneratedQuestionDto dto)
    {
        Id = dto.Id;
        Question = dto.Question;
        Difficulty = dto.Difficulty;
        Answer = dto.Answer;
        Sources = dto.Sources;
    }

    [RelayCommand]
    private void ToggleAnswer() => IsAnswerRevealed = !IsAnswerRevealed;
}

public partial class AiOperationViewModel : ViewModelBase
{
    protected readonly IApiClient _apiClient;
    protected readonly IDialogService _dialogService;
    protected readonly INavigationService _navigationService;
    protected readonly UserSession _userSession;

    // Source Selection
    [ObservableProperty]
    private ObservableCollection<string> _availableSourceTypes = new() { "Document", "Page", "Note" };

    [ObservableProperty]
    private string _selectedSourceType = "Document";

    [ObservableProperty]
    private ObservableCollection<AiSourceItem> _availableSources = new();

    [ObservableProperty]
    private AiSourceItem? _selectedSource;

    [ObservableProperty]
    private string _additionalInstructions = string.Empty;

    // Operation Configuration
    [ObservableProperty]
    private ObservableCollection<string> _availableOperations = new()
    {
        "Summarize",
        "Explain",
        "Extract Key Points",
        "Generate Questions",
        "Study Material"
    };

    [ObservableProperty]
    private string _selectedOperation = "Summarize";

    // Question options
    [ObservableProperty]
    private int _questionCount = 5;

    [ObservableProperty]
    private ObservableCollection<int> _questionCountOptions = new() { 3, 5, 10 };

    [ObservableProperty]
    private string _difficulty = "Intermediate";

    [ObservableProperty]
    private ObservableCollection<string> _difficultyOptions = new() { "Beginner", "Intermediate", "Advanced" };

    [ObservableProperty]
    private bool _includeAnswers = true;

    // Execution & Busy State
    [ObservableProperty]
    private bool _isExecuting;

    // Active Results
    [ObservableProperty]
    private AiOperationResultDto? _currentResult;

    [ObservableProperty]
    private ObservableCollection<string> _resultKeyPoints = new();

    [ObservableProperty]
    private ObservableCollection<GeneratedQuestionItemViewModel> _resultQuestions = new();

    [ObservableProperty]
    private ObservableCollection<ChatSourceDto> _resultSources = new();

    // Save as Note
    [ObservableProperty]
    private bool _isSaveNoteOpen;

    [ObservableProperty]
    private string _saveNoteTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PageSummaryDto> _availablePagesForNote = new();

    [ObservableProperty]
    private PageSummaryDto? _selectedPageForNote;

    [ObservableProperty]
    private bool _isSavingNote;

    [ObservableProperty]
    private string? _saveNoteSuccessMessage;

    // History
    [ObservableProperty]
    private ObservableCollection<AiGenerationSummaryDto> _generationsHistory = new();

    [ObservableProperty]
    private bool _isLoadingHistory;

    [ObservableProperty]
    private int _activeTab; // 0: Studio, 1: History

    public bool HasResult => CurrentResult != null;
    public bool IsQuestionsOperation => SelectedOperation == "Generate Questions";

    public AiOperationViewModel(
        IApiClient apiClient,
        IDialogService dialogService,
        INavigationService navigationService,
        UserSession userSession)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _userSession = userSession;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        if (_userSession.CurrentWorkspaceId == Guid.Empty) return;

        await LoadSourcesForCurrentTypeAsync();
        await LoadPagesForNoteAsync();
        await LoadHistoryAsync();

        CheckPendingTarget();
    }

    private void CheckPendingTarget()
    {
        var target = _userSession.PendingAiTarget;
        if (target != null)
        {
            _userSession.PendingAiTarget = null;

            SelectedSourceType = target.SourceType;
            SelectedOperation = target.RequestedOperation ?? "Summarize";
            if (!string.IsNullOrWhiteSpace(target.InitialInstructions))
            {
                AdditionalInstructions = target.InitialInstructions;
            }

            var matching = AvailableSources.FirstOrDefault(s => s.Id == target.SourceId);
            if (matching != null)
            {
                SelectedSource = matching;
            }
            else
            {
                var item = new AiSourceItem(target.SourceId, target.SourceTitle, target.SourceType, string.Empty);
                AvailableSources.Insert(0, item);
                SelectedSource = item;
            }

            _ = ExecuteOperationAsync();
        }
    }

    async partial void OnSelectedSourceTypeChanged(string value)
    {
        await LoadSourcesForCurrentTypeAsync();
    }

    partial void OnSelectedOperationChanged(string value)
    {
        OnPropertyChanged(nameof(IsQuestionsOperation));
    }

    partial void OnCurrentResultChanged(AiOperationResultDto? value)
    {
        OnPropertyChanged(nameof(HasResult));
    }

    [RelayCommand]
    public async Task LoadSourcesForCurrentTypeAsync()
    {
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty) return;

        AvailableSources.Clear();
        ErrorMessage = null;

        try
        {
            if (SelectedSourceType == "Document")
            {
                var res = await _apiClient.GetDocumentsAsync(workspaceId);
                if (res.IsSuccess && res.Value != null)
                {
                    foreach (var doc in res.Value)
                    {
                        AvailableSources.Add(new AiSourceItem(doc.Id, doc.FileName, "Document", $"{doc.FileExtension} • {doc.Status}"));
                    }
                }
            }
            else if (SelectedSourceType == "Page")
            {
                var res = await _apiClient.GetPageTreeAsync(workspaceId);
                if (res.IsSuccess && res.Value != null)
                {
                    void AddTree(IReadOnlyList<PageTreeNodeDto> nodes)
                    {
                        foreach (var node in nodes)
                        {
                            AvailableSources.Add(new AiSourceItem(node.Id, node.Title, "Page", $"{node.Icon} Page"));
                            if (node.Children != null && node.Children.Count > 0)
                            {
                                AddTree(node.Children);
                            }
                        }
                    }
                    AddTree(res.Value);
                }
            }
            else if (SelectedSourceType == "Note")
            {
                var res = await _apiClient.GetNotesAsync(workspaceId);
                if (res.IsSuccess && res.Value != null)
                {
                    foreach (var note in res.Value)
                    {
                        AvailableSources.Add(new AiSourceItem(note.Id, note.Title, "Note", note.ContentType));
                    }
                }
            }

            if (SelectedSource == null && AvailableSources.Count > 0)
            {
                SelectedSource = AvailableSources[0];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load sources: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LoadPagesForNoteAsync()
    {
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty) return;

        try
        {
            var res = await _apiClient.GetPageTreeAsync(workspaceId);
            if (res.IsSuccess && res.Value != null)
            {
                AvailablePagesForNote.Clear();
                void AddTree(IReadOnlyList<PageTreeNodeDto> nodes)
                {
                    foreach (var node in nodes)
                    {
                        AvailablePagesForNote.Add(new PageSummaryDto(node.Id, node.WorkspaceId, node.ParentPageId, node.Title, node.Icon, node.OrderIndex));
                        if (node.Children != null && node.Children.Count > 0)
                        {
                            AddTree(node.Children);
                        }
                    }
                }
                AddTree(res.Value);
                if (SelectedPageForNote == null && AvailablePagesForNote.Count > 0)
                {
                    SelectedPageForNote = AvailablePagesForNote[0];
                }
            }
        }
        catch
        {
            // Ignore page list load failure
        }
    }

    [RelayCommand]
    public async Task ExecuteOperationAsync()
    {
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty)
        {
            ErrorMessage = "No active workspace selected.";
            return;
        }

        if (SelectedSource == null)
        {
            ErrorMessage = "Please select a knowledge source.";
            return;
        }

        IsExecuting = true;
        StatusMessage = $"Running {SelectedOperation} on {SelectedSource.Title}...";
        ErrorMessage = null;
        SaveNoteSuccessMessage = null;

        try
        {
            var requestInstructions = string.IsNullOrWhiteSpace(AdditionalInstructions) ? null : AdditionalInstructions;

            Nexus.Domain.Common.Result<AiOperationResultDto> result;

            switch (SelectedOperation)
            {
                case "Summarize":
                    result = await _apiClient.SummarizeAsync(workspaceId, new AiKnowledgeRequest(SelectedSourceType, SelectedSource.Id, requestInstructions));
                    break;
                case "Explain":
                    result = await _apiClient.ExplainAsync(workspaceId, new AiKnowledgeRequest(SelectedSourceType, SelectedSource.Id, requestInstructions));
                    break;
                case "Extract Key Points":
                    result = await _apiClient.ExtractKeyPointsAsync(workspaceId, new AiKnowledgeRequest(SelectedSourceType, SelectedSource.Id, requestInstructions));
                    break;
                case "Generate Questions":
                    result = await _apiClient.GenerateQuestionsAsync(workspaceId, new GenerateQuestionsRequest(SelectedSourceType, SelectedSource.Id, QuestionCount, Difficulty, IncludeAnswers, requestInstructions));
                    break;
                case "Study Material":
                    result = await _apiClient.GenerateStudyMaterialAsync(workspaceId, new GenerateStudyMaterialRequest(SelectedSourceType, SelectedSource.Id, requestInstructions));
                    break;
                default:
                    result = await _apiClient.SummarizeAsync(workspaceId, new AiKnowledgeRequest(SelectedSourceType, SelectedSource.Id, requestInstructions));
                    break;
            }

            if (result.IsSuccess && result.Value != null)
            {
                CurrentResult = result.Value;

                ResultKeyPoints.Clear();
                if (result.Value.KeyPoints != null)
                {
                    foreach (var kp in result.Value.KeyPoints)
                    {
                        ResultKeyPoints.Add(kp);
                    }
                }

                ResultQuestions.Clear();
                if (result.Value.Questions != null)
                {
                    foreach (var q in result.Value.Questions)
                    {
                        ResultQuestions.Add(new GeneratedQuestionItemViewModel(q));
                    }
                }

                ResultSources.Clear();
                if (result.Value.Sources != null)
                {
                    foreach (var s in result.Value.Sources)
                    {
                        ResultSources.Add(s);
                    }
                }

                SaveNoteTitle = $"{SelectedOperation}: {SelectedSource.Title}";
                StatusMessage = $"{SelectedOperation} completed successfully.";

                // Refresh history in background
                _ = LoadHistoryAsync();
            }
            else
            {
                ErrorMessage = result.Error.Description ?? "Operation failed.";
                StatusMessage = null;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error during AI operation: {ex.Message}";
            StatusMessage = null;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    public void CopyResult()
    {
        if (CurrentResult == null) return;

        var textToCopy = CurrentResult.Content;
        if (ResultQuestions.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(CurrentResult.Content);
            sb.AppendLine();
            for (int i = 0; i < ResultQuestions.Count; i++)
            {
                var q = ResultQuestions[i];
                sb.AppendLine($"Q{i + 1} ({q.Difficulty}): {q.Question}");
                if (!string.IsNullOrWhiteSpace(q.Answer))
                {
                    sb.AppendLine($"Answer: {q.Answer}");
                }
                sb.AppendLine();
            }
            textToCopy = sb.ToString();
        }

        try
        {
            Clipboard.SetText(textToCopy);
            StatusMessage = "Copied output to clipboard.";
        }
        catch
        {
            // Clipboard access might fail on some thread environments
        }
    }

    [RelayCommand]
    public void ToggleSaveNotePanel()
    {
        IsSaveNoteOpen = !IsSaveNoteOpen;
        SaveNoteSuccessMessage = null;
    }

    [RelayCommand]
    public async Task SaveAsNoteAsync()
    {
        if (CurrentResult == null) return;
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty) return;

        if (SelectedPageForNote == null)
        {
            ErrorMessage = "Please select a destination page.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SaveNoteTitle))
        {
            ErrorMessage = "Please enter a note title.";
            return;
        }

        IsSavingNote = true;
        ErrorMessage = null;
        SaveNoteSuccessMessage = null;

        try
        {
            var res = await _apiClient.SaveAiOutputAsNoteAsync(
                workspaceId,
                new SaveAiOutputAsNoteRequest(CurrentResult.Id, SelectedPageForNote.Id, SaveNoteTitle.Trim()));

            if (res.IsSuccess)
            {
                SaveNoteSuccessMessage = $"Note '{res.Value.Title}' created successfully!";
                IsSaveNoteOpen = false;
            }
            else
            {
                ErrorMessage = res.Error.Description ?? "Failed to save note.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving note: {ex.Message}";
        }
        finally
        {
            IsSavingNote = false;
        }
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty) return;

        IsLoadingHistory = true;
        try
        {
            var res = await _apiClient.GetAiGenerationsAsync(workspaceId);
            if (res.IsSuccess && res.Value != null)
            {
                GenerationsHistory.Clear();
                foreach (var item in res.Value)
                {
                    GenerationsHistory.Add(item);
                }
            }
        }
        catch
        {
            // Ignore history load error
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    [RelayCommand]
    public async Task ViewHistoryDetailAsync(AiGenerationSummaryDto? item)
    {
        if (item == null) return;
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty) return;

        StatusMessage = "Loading generation details...";
        try
        {
            var res = await _apiClient.GetAiGenerationAsync(workspaceId, item.Id);
            if (res.IsSuccess && res.Value != null)
            {
                CurrentResult = res.Value;
                SelectedOperation = res.Value.Operation;

                ResultKeyPoints.Clear();
                if (res.Value.KeyPoints != null)
                {
                    foreach (var kp in res.Value.KeyPoints) ResultKeyPoints.Add(kp);
                }

                ResultQuestions.Clear();
                if (res.Value.Questions != null)
                {
                    foreach (var q in res.Value.Questions) ResultQuestions.Add(new GeneratedQuestionItemViewModel(q));
                }

                ResultSources.Clear();
                if (res.Value.Sources != null)
                {
                    foreach (var s in res.Value.Sources) ResultSources.Add(s);
                }

                SaveNoteTitle = $"{res.Value.Operation} from History";
                ActiveTab = 0; // Switch to Studio tab
                StatusMessage = "Loaded generation from history.";
            }
            else
            {
                ErrorMessage = res.Error.Description ?? "Could not load generation.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to view generation: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteHistoryItemAsync(AiGenerationSummaryDto? item)
    {
        if (item == null) return;
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty) return;

        var confirmed = await _dialogService.ConfirmAsync(
            "Delete AI Generation",
            "Are you sure you want to delete this AI generation record?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            var res = await _apiClient.DeleteAiGenerationAsync(workspaceId, item.Id);
            if (res.IsSuccess)
            {
                GenerationsHistory.Remove(item);
                if (CurrentResult?.Id == item.Id)
                {
                    CurrentResult = null;
                    ResultKeyPoints.Clear();
                    ResultQuestions.Clear();
                    ResultSources.Clear();
                }
                StatusMessage = "Generation deleted.";
            }
            else
            {
                ErrorMessage = res.Error.Description ?? "Failed to delete generation.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete: {ex.Message}";
        }
    }
}
