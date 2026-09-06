using Nexus.Application.DTOs.AI;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Xunit;

namespace Nexus.Desktop.Tests;

public class AiOperationViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly FakeNavigationService _fakeNavigationService;
    private readonly FakeDialogService _fakeDialogService;
    private readonly UserSession _userSession;
    private readonly Guid _workspaceId;

    public AiOperationViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
        _fakeNavigationService = new FakeNavigationService();
        _fakeDialogService = new FakeDialogService();

        _workspaceId = Guid.NewGuid();
        _userSession = new UserSession
        {
            SelectedWorkspace = new WorkspaceDto(_workspaceId, "AI Studio Workspace", "Test", "📁", "#3B82F6", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)
        };
    }

    private AiOperationViewModel CreateViewModel()
    {
        return new AiOperationViewModel(_fakeApiClient, _fakeDialogService, _fakeNavigationService, _userSession);
    }

    [Fact]
    public async Task Initialize_PopulatesSourcesAndPages()
    {
        var doc = new DocumentSummaryDto(Guid.NewGuid(), _workspaceId, null, null, "Doc1.pdf", "Doc1.pdf", "application/pdf", ".pdf", 100, Domain.Enums.DocumentStatus.Processed, DateTime.UtcNow);
        _fakeApiClient.DocumentSummaries.Add(doc);

        var pageTree = new PageTreeNodeDto(Guid.NewGuid(), _workspaceId, null, "Root Page", "📄", 0, new List<PageTreeNodeDto>());
        _fakeApiClient.PageTrees.Add(pageTree);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.NotEmpty(vm.AvailableSources);
        Assert.Equal(doc.Id, vm.AvailableSources[0].Id);
        Assert.NotNull(vm.SelectedSource);
        Assert.NotEmpty(vm.AvailablePagesForNote);
    }

    [Fact]
    public async Task ExecuteOperation_Summarize_Success()
    {
        var docId = Guid.NewGuid();
        var vm = CreateViewModel();
        vm.AvailableSources.Add(new AiSourceItem(docId, "Doc1.pdf", "Document", ""));
        vm.SelectedSource = vm.AvailableSources[0];
        vm.SelectedOperation = "Summarize";

        await vm.ExecuteOperationAsync();

        Assert.NotNull(vm.CurrentResult);
        Assert.Equal("Summarize", vm.CurrentResult.Operation);
        Assert.True(vm.HasResult);
        Assert.NotEmpty(vm.ResultSources);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteOperation_Explain_Success()
    {
        var vm = CreateViewModel();
        vm.AvailableSources.Add(new AiSourceItem(Guid.NewGuid(), "Concept Note", "Note", ""));
        vm.SelectedSource = vm.AvailableSources[0];
        vm.SelectedOperation = "Explain";
        vm.AdditionalInstructions = "Simplify for beginners";

        await vm.ExecuteOperationAsync();

        Assert.NotNull(vm.CurrentResult);
        Assert.Equal("Explain", vm.CurrentResult.Operation);
        Assert.Contains("Simplify for beginners", vm.CurrentResult.Content);
    }

    [Fact]
    public async Task ExecuteOperation_KeyPoints_PopulatesResultKeyPoints()
    {
        var vm = CreateViewModel();
        vm.AvailableSources.Add(new AiSourceItem(Guid.NewGuid(), "Meeting Note", "Note", ""));
        vm.SelectedSource = vm.AvailableSources[0];
        vm.SelectedOperation = "Extract Key Points";

        await vm.ExecuteOperationAsync();

        Assert.NotNull(vm.CurrentResult);
        Assert.Equal("KeyPoints", vm.CurrentResult.Operation);
        Assert.NotEmpty(vm.ResultKeyPoints);
        Assert.Equal(3, vm.ResultKeyPoints.Count);
    }

    [Fact]
    public async Task ExecuteOperation_Questions_PopulatesResultQuestions()
    {
        var vm = CreateViewModel();
        vm.AvailableSources.Add(new AiSourceItem(Guid.NewGuid(), "Chapter 1", "Document", ""));
        vm.SelectedSource = vm.AvailableSources[0];
        vm.SelectedOperation = "Generate Questions";

        await vm.ExecuteOperationAsync();

        Assert.NotNull(vm.CurrentResult);
        Assert.Equal("Questions", vm.CurrentResult.Operation);
        Assert.NotEmpty(vm.ResultQuestions);
        Assert.False(vm.ResultQuestions[0].IsAnswerRevealed);

        // Test toggle answer
        vm.ResultQuestions[0].ToggleAnswerCommand.Execute(null);
        Assert.True(vm.ResultQuestions[0].IsAnswerRevealed);
    }

    [Fact]
    public async Task ExecuteOperation_StudyMaterial_Success()
    {
        var vm = CreateViewModel();
        vm.AvailableSources.Add(new AiSourceItem(Guid.NewGuid(), "Exam Page", "Page", ""));
        vm.SelectedSource = vm.AvailableSources[0];
        vm.SelectedOperation = "Study Material";

        await vm.ExecuteOperationAsync();

        Assert.NotNull(vm.CurrentResult);
        Assert.Equal("StudyMaterial", vm.CurrentResult.Operation);
        Assert.Contains("Study Guide", vm.CurrentResult.Content);
    }

    [Fact]
    public async Task PendingAiTarget_AutomaticallyConfiguresAndExecutes()
    {
        var targetDocId = Guid.NewGuid();
        _userSession.PendingAiTarget = new AiWorkflowTarget("Document", targetDocId, "AutoDoc.pdf", "Explain", "Quick explanation");

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.Equal("Document", vm.SelectedSourceType);
        Assert.Equal("Explain", vm.SelectedOperation);
        Assert.Equal("Quick explanation", vm.AdditionalInstructions);
        Assert.NotNull(vm.CurrentResult);
        Assert.Null(_userSession.PendingAiTarget);
    }

    [Fact]
    public async Task SaveAsNote_ValidInput_CreatesNoteSuccessfully()
    {
        var vm = CreateViewModel();
        vm.AvailableSources.Add(new AiSourceItem(Guid.NewGuid(), "Doc", "Document", ""));
        vm.SelectedSource = vm.AvailableSources[0];
        vm.SelectedOperation = "Summarize";
        await vm.ExecuteOperationAsync();

        var pageId = Guid.NewGuid();
        vm.AvailablePagesForNote.Add(new PageSummaryDto(pageId, _workspaceId, null, "Study Page", "📄", 0));
        vm.SelectedPageForNote = vm.AvailablePagesForNote[0];
        vm.SaveNoteTitle = "My Summary Note";

        await vm.SaveAsNoteAsync();

        Assert.NotNull(vm.SaveNoteSuccessMessage);
        Assert.Contains("My Summary Note", vm.SaveNoteSuccessMessage);
        Assert.Contains(_fakeApiClient.Notes, n => n.Title == "My Summary Note" && n.PageId == pageId);
    }

    [Fact]
    public async Task SaveAsNote_MissingPage_ShowsError()
    {
        var vm = CreateViewModel();
        vm.AvailableSources.Add(new AiSourceItem(Guid.NewGuid(), "Doc", "Document", ""));
        vm.SelectedSource = vm.AvailableSources[0];
        vm.SelectedOperation = "Summarize";
        await vm.ExecuteOperationAsync();

        vm.SelectedPageForNote = null;
        vm.SaveNoteTitle = "Title";

        await vm.SaveAsNoteAsync();

        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("destination page", vm.ErrorMessage);
    }

    [Fact]
    public async Task DeleteHistoryItem_Confirmed_RemovesFromList()
    {
        var vm = CreateViewModel();
        var gen = new AiOperationResultDto(Guid.NewGuid(), "Summarize", "Content", null, null, new List<ChatSourceDto>(), "gpt-4o", DateTime.UtcNow);
        _fakeApiClient.AiGenerations.Add(gen);

        await vm.LoadHistoryAsync();
        Assert.Single(vm.GenerationsHistory);

        _fakeDialogService.ConfirmationResult = true;
        await vm.DeleteHistoryItemAsync(vm.GenerationsHistory[0]);

        Assert.Empty(vm.GenerationsHistory);
    }
}
