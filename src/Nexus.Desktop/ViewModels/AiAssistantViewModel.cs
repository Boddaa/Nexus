using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Conversations;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class AiAssistantViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private ObservableCollection<ConversationDto> _conversations = new();

    [ObservableProperty]
    private ConversationDto? _selectedConversation;

    [ObservableProperty]
    private ObservableCollection<ChatMessageDto> _messages = new();

    [ObservableProperty]
    private string _messageInput = string.Empty;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasConversations = false;

    public AiAssistantViewModel(
        IApiClient apiClient,
        INavigationService navigationService,
        UserSession userSession)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;
        _userSession = userSession;
    }

    public async Task InitializeAsync()
    {
        await LoadConversationsAsync();
    }

    [RelayCommand]
    public async Task LoadConversationsAsync()
    {
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty)
        {
            ErrorMessage = "No active workspace selected.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.GetConversationsAsync(workspaceId);
            if (result.IsSuccess)
            {
                Conversations.Clear();
                foreach (var conv in result.Value)
                {
                    Conversations.Add(conv);
                }
                HasConversations = Conversations.Count > 0;

                if (SelectedConversation == null && Conversations.Count > 0)
                {
                    await SelectConversationAsync(Conversations[0]);
                }
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load conversations: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SelectConversationAsync(ConversationDto? conversation)
    {
        if (conversation == null) return;

        SelectedConversation = conversation;
        var workspaceId = _userSession.CurrentWorkspaceId;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.GetConversationMessagesAsync(workspaceId, conversation.Id);
            if (result.IsSuccess)
            {
                Messages.Clear();
                foreach (var msg in result.Value)
                {
                    Messages.Add(msg);
                }
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load messages: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CreateNewConversationAsync()
    {
        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty) return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var req = new CreateConversationRequest("New Conversation");
            var result = await _apiClient.CreateConversationAsync(workspaceId, req);
            if (result.IsSuccess)
            {
                Conversations.Insert(0, result.Value);
                HasConversations = true;
                SelectedConversation = result.Value;
                Messages.Clear();
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to create conversation: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || IsBusy) return;

        var workspaceId = _userSession.CurrentWorkspaceId;
        if (workspaceId == Guid.Empty) return;

        // If no conversation selected, create one first
        if (SelectedConversation == null)
        {
            var createRes = await _apiClient.CreateConversationAsync(workspaceId, new CreateConversationRequest("New Conversation"));
            if (!createRes.IsSuccess)
            {
                ErrorMessage = createRes.Error.Description;
                return;
            }
            Conversations.Insert(0, createRes.Value);
            HasConversations = true;
            SelectedConversation = createRes.Value;
        }

        var convId = SelectedConversation.Id;
        var textToSend = MessageInput.Trim();
        MessageInput = string.Empty;

        // Optimistically show user message
        var optimisticUserMsg = new ChatMessageDto(
            Guid.NewGuid(),
            convId,
            "User",
            textToSend,
            DateTime.UtcNow,
            null,
            Array.Empty<ChatSourceDto>());
        Messages.Add(optimisticUserMsg);

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var req = new SendChatMessageRequest(textToSend);
            var result = await _apiClient.SendChatMessageAsync(workspaceId, convId, req);

            if (result.IsSuccess)
            {
                // Replace optimistic user message or add assistant message
                Messages.Remove(optimisticUserMsg);
                Messages.Add(result.Value.UserMessage);
                Messages.Add(result.Value.AssistantMessage);

                // Update conversation in sidebar
                var existingIndex = Conversations.IndexOf(Conversations.FirstOrDefault(c => c.Id == convId)!);
                if (existingIndex >= 0)
                {
                    Conversations[existingIndex] = result.Value.Conversation;
                    SelectedConversation = result.Value.Conversation;
                }
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to send message: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ArchiveConversationAsync(ConversationDto? conversation)
    {
        var target = conversation ?? SelectedConversation;
        if (target == null) return;

        var workspaceId = _userSession.CurrentWorkspaceId;
        var result = await _apiClient.ArchiveConversationAsync(workspaceId, target.Id);
        if (result.IsSuccess)
        {
            Conversations.Remove(target);
            HasConversations = Conversations.Count > 0;
            if (SelectedConversation?.Id == target.Id)
            {
                SelectedConversation = Conversations.FirstOrDefault();
                if (SelectedConversation != null)
                {
                    await SelectConversationAsync(SelectedConversation);
                }
                else
                {
                    Messages.Clear();
                }
            }
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public async Task DeleteConversationAsync(ConversationDto? conversation)
    {
        var target = conversation ?? SelectedConversation;
        if (target == null) return;

        var workspaceId = _userSession.CurrentWorkspaceId;
        var result = await _apiClient.DeleteConversationAsync(workspaceId, target.Id);
        if (result.IsSuccess)
        {
            Conversations.Remove(target);
            HasConversations = Conversations.Count > 0;
            if (SelectedConversation?.Id == target.Id)
            {
                SelectedConversation = Conversations.FirstOrDefault();
                if (SelectedConversation != null)
                {
                    await SelectConversationAsync(SelectedConversation);
                }
                else
                {
                    Messages.Clear();
                }
            }
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public async Task OpenSource(ChatSourceDto? source)
    {
        if (source == null) return;

        if (source.PageId.HasValue || source.SourceType.Equals("Page", StringComparison.OrdinalIgnoreCase))
        {
            var id = source.PageId ?? source.Id;
            _navigationService.NavigateTo<PagesViewModel>();
            if (_navigationService.CurrentViewModel is PagesViewModel pagesVm)
            {
                await pagesVm.SelectPageByIdAsync(id);
            }
        }
        else if (source.NoteId.HasValue || source.SourceType.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            var id = source.NoteId ?? source.Id;
            _navigationService.NavigateTo<NotesViewModel>();
            if (_navigationService.CurrentViewModel is NotesViewModel notesVm)
            {
                await notesVm.SelectNoteByIdAsync(id);
            }
        }
        else if (source.DocumentId.HasValue || source.SourceType.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            var id = source.DocumentId ?? source.Id;
            _navigationService.NavigateTo<DocumentsViewModel>();
            if (_navigationService.CurrentViewModel is DocumentsViewModel docsVm)
            {
                await docsVm.SelectDocumentByIdAsync(id);
            }
        }
    }
}
