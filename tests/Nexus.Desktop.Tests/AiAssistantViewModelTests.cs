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

public class AiAssistantViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly FakeNavigationService _fakeNavigationService;
    private readonly FakeDialogService _fakeDialogService;
    private readonly FakeFilePickerService _fakeFilePicker;
    private readonly UserSession _userSession;
    private readonly AiAssistantViewModel _viewModel;
    private readonly Guid _workspaceId;

    public AiAssistantViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
        _fakeNavigationService = new FakeNavigationService();
        _fakeDialogService = new FakeDialogService();
        _fakeFilePicker = new FakeFilePickerService();

        _workspaceId = Guid.NewGuid();
        _userSession = new UserSession
        {
            SelectedWorkspace = new WorkspaceDto(_workspaceId, "AI Lab", "Test", "📁", "#3B82F6", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)
        };

        _viewModel = new AiAssistantViewModel(_fakeApiClient, _fakeNavigationService, _userSession);
    }

    [Fact]
    public async Task LoadConversations_PopulatesListAndSelectsFirst()
    {
        var conv1 = new ConversationDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), "Chat 1", false, DateTime.UtcNow, null, 2);
        var conv2 = new ConversationDto(Guid.NewGuid(), _workspaceId, Guid.NewGuid(), "Chat 2", false, DateTime.UtcNow, null, 4);
        _fakeApiClient.Conversations.AddRange(new[] { conv1, conv2 });

        await _viewModel.LoadConversationsAsync();

        Assert.Equal(2, _viewModel.Conversations.Count);
        Assert.True(_viewModel.HasConversations);
        Assert.NotNull(_viewModel.SelectedConversation);
        Assert.Equal(conv1.Id, _viewModel.SelectedConversation.Id);
    }

    [Fact]
    public async Task CreateNewConversation_AddsToTopAndSelects()
    {
        await _viewModel.CreateNewConversationAsync();

        Assert.Single(_viewModel.Conversations);
        Assert.NotNull(_viewModel.SelectedConversation);
        Assert.Equal("New Conversation", _viewModel.SelectedConversation.Title);
        Assert.Empty(_viewModel.Messages);
    }

    [Fact]
    public async Task SendMessage_AppendsUserAndAssistantMessages()
    {
        await _viewModel.CreateNewConversationAsync();

        _viewModel.MessageInput = "What is NEXUS?";
        await _viewModel.SendMessageAsync();

        Assert.Equal(2, _viewModel.Messages.Count);
        Assert.Equal("User", _viewModel.Messages[0].Role);
        Assert.Equal("What is NEXUS?", _viewModel.Messages[0].Content);
        Assert.Equal("Assistant", _viewModel.Messages[1].Role);
        Assert.Contains("This is a mock assistant answer", _viewModel.Messages[1].Content);
        Assert.NotEmpty(_viewModel.Messages[1].Sources);
    }

    [Fact]
    public async Task OpenSource_Document_NavigatesToDocumentsModule()
    {
        var docId = Guid.NewGuid();
        var docDetail = new DocumentDetailDto(docId, _workspaceId, null, null, "Doc.pdf", "doc.pdf", "application/pdf", ".pdf", 1024, Domain.Enums.DocumentStatus.Processed, null, "Text", 1, 10, DateTime.UtcNow, null);
        _fakeApiClient.DocumentDetails.Add(docDetail);

        var docsVm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);
        _fakeNavigationService.ViewModelResolver = type => type == typeof(DocumentsViewModel) ? docsVm : null;

        var source = new ChatSourceDto(Guid.NewGuid(), docId, null, null, null, "Doc.pdf", "Document", 0.9, 1, "Snippet");

        await _viewModel.OpenSource(source);

        Assert.Equal(typeof(DocumentsViewModel), _fakeNavigationService.LastNavigatedType);
        Assert.NotNull(docsVm.SelectedDocument);
        Assert.Equal(docId, docsVm.SelectedDocument.Id);
    }

    [Fact]
    public async Task OpenSource_Page_NavigatesToPagesModule()
    {
        var pageId = Guid.NewGuid();
        var pageDto = new PageDto(pageId, _workspaceId, null, "Intro Page", "📄", null, "{\"text\":\"Content\"}", 0, DateTime.UtcNow, null, 0, 0);
        _fakeApiClient.Pages.Add(pageDto);

        var pagesVm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        _fakeNavigationService.ViewModelResolver = type => type == typeof(PagesViewModel) ? pagesVm : null;

        var source = new ChatSourceDto(Guid.NewGuid(), null, null, pageId, null, "Intro Page", "Page", 0.85, null, "Snippet");

        await _viewModel.OpenSource(source);

        Assert.Equal(typeof(PagesViewModel), _fakeNavigationService.LastNavigatedType);
        Assert.NotNull(pagesVm.SelectedPage);
        Assert.Equal(pageId, pagesVm.SelectedPage.Id);
    }

    [Fact]
    public async Task OpenSource_Note_NavigatesToNotesModule()
    {
        var noteId = Guid.NewGuid();
        var noteDto = new NoteDto(noteId, _workspaceId, null, null, "Meeting Note", "Notes", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.Notes.Add(noteDto);

        var notesVm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        _fakeNavigationService.ViewModelResolver = type => type == typeof(NotesViewModel) ? notesVm : null;

        var source = new ChatSourceDto(Guid.NewGuid(), null, null, null, noteId, "Meeting Note", "Note", 0.88, null, "Snippet");

        await _viewModel.OpenSource(source);

        Assert.Equal(typeof(NotesViewModel), _fakeNavigationService.LastNavigatedType);
        Assert.NotNull(notesVm.SelectedNote);
        Assert.Equal(noteId, notesVm.SelectedNote.Id);
    }
}
