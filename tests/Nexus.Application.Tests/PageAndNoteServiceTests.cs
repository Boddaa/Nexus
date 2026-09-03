using Microsoft.EntityFrameworkCore;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.Features.Notes.Services;
using Nexus.Application.Features.Pages.Services;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class PageServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly PageService _pageService;
    private readonly User _testUser;
    private readonly Workspace _testWorkspace;
    private readonly Workspace _otherWorkspace;

    public PageServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "PageTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(options, _currentUserService);
        _pageService = new PageService(_context, _currentUserService);

        // Seed Users first so EF assigns their IDs
        _testUser = new User { Email = "user@nexus.ai", FullName = "Test User" };
        var otherUser = new User { Email = "other@nexus.ai", FullName = "Other User" };

        _context.Users.AddRange(_testUser, otherUser);
        _context.SaveChanges();

        // Seed Workspaces referencing the generated User IDs
        _testWorkspace = new Workspace
        {
            Name = "Primary Workspace",
            OwnerId = _testUser.Id,
            Icon = "📁"
        };

        _otherWorkspace = new Workspace
        {
            Name = "Other Workspace",
            OwnerId = otherUser.Id,
            Icon = "🔒"
        };

        _context.Workspaces.AddRange(_testWorkspace, _otherWorkspace);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    [Fact]
    public async Task CreatePage_Should_Create_Root_Page()
    {
        var request = new CreatePageRequest("Getting Started", "🚀", null, "{\"type\":\"doc\"}", null, 0);

        var result = await _pageService.CreatePageAsync(_testWorkspace.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Getting Started", result.Value.Title);
        Assert.Equal("🚀", result.Value.Icon);
        Assert.Null(result.Value.ParentPageId);
        Assert.Equal(_testWorkspace.Id, result.Value.WorkspaceId);
    }

    [Fact]
    public async Task CreatePage_Should_Create_Child_Page()
    {
        var rootResult = await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Root Page"));
        Assert.True(rootResult.IsSuccess);

        var childRequest = new CreatePageRequest("Child Page", "📄", null, "{}", rootResult.Value.Id, 1);
        var childResult = await _pageService.CreatePageAsync(_testWorkspace.Id, childRequest);

        Assert.True(childResult.IsSuccess);
        Assert.Equal("Child Page", childResult.Value.Title);
        Assert.Equal(rootResult.Value.Id, childResult.Value.ParentPageId);
    }

    [Fact]
    public async Task CreatePage_Should_Reject_CrossWorkspace_Parent()
    {
        // Create page in another workspace (as owner of other workspace temporarily)
        _currentUserService.UserId = _otherWorkspace.OwnerId;
        var otherPage = new Page { WorkspaceId = _otherWorkspace.Id, Title = "Private Page" };
        _context.Pages.Add(otherPage);
        await _context.SaveChangesAsync();

        // Switch back to test user
        _currentUserService.UserId = _testUser.Id;

        var request = new CreatePageRequest("Infiltrator Page", "⚠️", null, "{}", otherPage.Id, 0);
        var result = await _pageService.CreatePageAsync(_testWorkspace.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Pages.InvalidParent", result.Error.Code);
    }

    [Fact]
    public async Task GetPageTree_Should_Build_Correct_Hierarchy()
    {
        // Arrange hierarchy: Root1 -> Child1.1 -> SubChild1.1.1, Root2
        var root1 = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Root 1", "📁", null, "{}", null, 0))).Value;
        var root2 = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Root 2", "📁", null, "{}", null, 1))).Value;
        var child11 = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Child 1.1", "📄", null, "{}", root1.Id, 0))).Value;
        var subChild111 = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("SubChild 1.1.1", "📑", null, "{}", child11.Id, 0))).Value;

        // Act
        var result = await _pageService.GetPageTreeAsync(_testWorkspace.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var treeRoot1 = result.Value.First(p => p.Id == root1.Id);
        Assert.Single(treeRoot1.Children);
        Assert.Equal("Child 1.1", treeRoot1.Children[0].Title);
        Assert.Single(treeRoot1.Children[0].Children);
        Assert.Equal("SubChild 1.1.1", treeRoot1.Children[0].Children[0].Title);

        var treeRoot2 = result.Value.First(p => p.Id == root2.Id);
        Assert.Empty(treeRoot2.Children);
    }

    [Fact]
    public async Task GetPageTree_Should_Order_Siblings_By_OrderIndex()
    {
        await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Page 3", "3️⃣", null, "{}", null, 30));
        await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Page 1", "1️⃣", null, "{}", null, 10));
        await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Page 2", "2️⃣", null, "{}", null, 20));

        var result = await _pageService.GetPageTreeAsync(_testWorkspace.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal("Page 1", result.Value[0].Title);
        Assert.Equal("Page 2", result.Value[1].Title);
        Assert.Equal("Page 3", result.Value[2].Title);
    }

    [Fact]
    public async Task GetPageById_Should_Reject_CrossWorkspace_Access()
    {
        var otherPage = new Page { WorkspaceId = _otherWorkspace.Id, Title = "Secret Page" };
        _context.Pages.Add(otherPage);
        await _context.SaveChangesAsync();

        var result = await _pageService.GetPageByIdAsync(_testWorkspace.Id, otherPage.Id);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task MovePage_Should_Prevent_SelfParenting()
    {
        var page = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Lonely Page"))).Value;

        var result = await _pageService.MovePageAsync(_testWorkspace.Id, page.Id, new MovePageRequest(page.Id, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal("Pages.SelfParenting", result.Error.Code);
    }

    [Fact]
    public async Task MovePage_Should_Prevent_Cycles()
    {
        // Hierarchy: A -> B -> C
        var pageA = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Page A"))).Value;
        var pageB = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Page B", "📄", null, "{}", pageA.Id, 0))).Value;
        var pageC = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Page C", "📄", null, "{}", pageB.Id, 0))).Value;

        // Attempt to move A under C (would create cycle: C -> A -> B -> C)
        var result = await _pageService.MovePageAsync(_testWorkspace.Id, pageA.Id, new MovePageRequest(pageC.Id, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal("Pages.CyclicDependency", result.Error.Code);
    }

    [Fact]
    public async Task MovePage_Should_Reject_CrossWorkspace_Target()
    {
        var page = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Normal Page"))).Value;
        var foreignPage = new Page { WorkspaceId = _otherWorkspace.Id, Title = "Foreign Page" };
        _context.Pages.Add(foreignPage);
        await _context.SaveChangesAsync();

        var result = await _pageService.MovePageAsync(_testWorkspace.Id, page.Id, new MovePageRequest(foreignPage.Id, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal("Pages.InvalidParent", result.Error.Code);
    }

    [Fact]
    public async Task DeletePage_Should_SoftDelete_SubTree()
    {
        var root = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Root"))).Value;
        var child = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Child", "📄", null, "{}", root.Id, 0))).Value;
        var grandChild = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("GrandChild", "📄", null, "{}", child.Id, 0))).Value;

        var deleteResult = await _pageService.DeletePageAsync(_testWorkspace.Id, root.Id);
        Assert.True(deleteResult.IsSuccess);

        // Verify all are soft-deleted and filtered out
        var tree = await _pageService.GetPageTreeAsync(_testWorkspace.Id);
        Assert.Empty(tree.Value);

        var rootInDb = await _context.Pages.IgnoreQueryFilters().FirstAsync(p => p.Id == root.Id);
        var childInDb = await _context.Pages.IgnoreQueryFilters().FirstAsync(p => p.Id == child.Id);
        var grandChildInDb = await _context.Pages.IgnoreQueryFilters().FirstAsync(p => p.Id == grandChild.Id);

        Assert.True(rootInDb.IsDeleted);
        Assert.True(childInDb.IsDeleted);
        Assert.True(grandChildInDb.IsDeleted);
    }

    [Fact]
    public async Task DeletePage_Should_SoftDelete_Attached_Notes()
    {
        var page = (await _pageService.CreatePageAsync(_testWorkspace.Id, new CreatePageRequest("Page with Notes"))).Value;
        var note = new Note { WorkspaceId = _testWorkspace.Id, PageId = page.Id, Title = "Attached Note", Content = "Some content" };
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        var deleteResult = await _pageService.DeletePageAsync(_testWorkspace.Id, page.Id);
        Assert.True(deleteResult.IsSuccess);

        var noteInDb = await _context.Notes.IgnoreQueryFilters().FirstAsync(n => n.Id == note.Id);
        Assert.True(noteInDb.IsDeleted);
    }

    [Fact]
    public async Task Unauthorized_User_Should_Be_Rejected()
    {
        _currentUserService.UserId = null; // Unauthenticated

        var result = await _pageService.GetPageTreeAsync(_testWorkspace.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Error.Unauthorized", result.Error.Code);
    }
}

public class NoteServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly NoteService _noteService;
    private readonly User _testUser;
    private readonly Workspace _testWorkspace;
    private readonly Workspace _otherWorkspace;
    private readonly Page _testPage;

    public NoteServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "NoteTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(options, _currentUserService);
        _noteService = new NoteService(_context, _currentUserService);

        // Seed Users
        _testUser = new User { Email = "author@nexus.ai", FullName = "Note Author" };
        var otherUser = new User { Email = "other2@nexus.ai", FullName = "Other User 2" };

        _context.Users.AddRange(_testUser, otherUser);
        _context.SaveChanges();

        // Seed Workspaces
        _testWorkspace = new Workspace { Name = "Notes Workspace", OwnerId = _testUser.Id };
        _otherWorkspace = new Workspace { Name = "Other Notes Workspace", OwnerId = otherUser.Id };

        _context.Workspaces.AddRange(_testWorkspace, _otherWorkspace);
        _context.SaveChanges();

        // Seed Page
        _testPage = new Page { WorkspaceId = _testWorkspace.Id, Title = "Architecture Spec Page" };
        _context.Pages.Add(_testPage);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    [Fact]
    public async Task CreateNote_Should_Create_Workspace_Note()
    {
        var request = new CreateNoteRequest("Clean Architecture Rules", "# Invariants\n- No framework refs in Domain", "markdown", true, null, new List<string> { "Architecture", "Design" });

        var result = await _noteService.CreateNoteAsync(_testWorkspace.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Clean Architecture Rules", result.Value.Title);
        Assert.True(result.Value.IsPinned);
        Assert.Null(result.Value.PageId);
        Assert.Contains("Architecture", result.Value.Tags);
        Assert.Contains("Design", result.Value.Tags);
    }

    [Fact]
    public async Task CreateNote_Should_Associate_With_Page()
    {
        var request = new CreateNoteRequest("Page Related Note", "Note tied to page", "markdown", false, _testPage.Id);

        var result = await _noteService.CreateNoteAsync(_testWorkspace.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(_testPage.Id, result.Value.PageId);
        Assert.Equal("Architecture Spec Page", result.Value.PageTitle);
    }

    [Fact]
    public async Task CreateNote_Should_Reject_CrossWorkspace_Page()
    {
        var foreignPage = new Page { WorkspaceId = _otherWorkspace.Id, Title = "Foreign Page" };
        _context.Pages.Add(foreignPage);
        await _context.SaveChangesAsync();

        var request = new CreateNoteRequest("Cross-Note", "Body", "markdown", false, foreignPage.Id);
        var result = await _noteService.CreateNoteAsync(_testWorkspace.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Notes.InvalidPage", result.Error.Code);
    }

    [Fact]
    public async Task GetNotes_Should_Filter_By_PageId()
    {
        await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("Standalone Note", "Standalone", "markdown", false, null));
        await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("Page Note 1", "Page 1", "markdown", false, _testPage.Id));
        await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("Page Note 2", "Page 2", "markdown", false, _testPage.Id));

        var result = await _noteService.GetNotesAsync(_testWorkspace.Id, pageId: _testPage.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, n => Assert.Equal(_testPage.Id, n.PageId));
    }

    [Fact]
    public async Task GetNotes_Should_Filter_By_Pinned()
    {
        await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("Pinned Note", "Pinned", "markdown", true));
        await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("Normal Note", "Normal", "markdown", false));

        var pinnedResult = await _noteService.GetNotesAsync(_testWorkspace.Id, isPinned: true);

        Assert.True(pinnedResult.IsSuccess);
        Assert.Single(pinnedResult.Value);
        Assert.True(pinnedResult.Value[0].IsPinned);
    }

    [Fact]
    public async Task UpdateNote_Should_Update_Content()
    {
        var created = (await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("Original Title", "Original Content"))).Value;

        var updateRequest = new UpdateNoteRequest("Updated Title", "Updated Content", "markdown", true, null, new List<string> { "UpdatedTag" });
        var updateResult = await _noteService.UpdateNoteAsync(_testWorkspace.Id, created.Id, updateRequest);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Updated Title", updateResult.Value.Title);
        Assert.Equal("Updated Content", updateResult.Value.Content);
        Assert.True(updateResult.Value.IsPinned);
        Assert.Contains("UpdatedTag", updateResult.Value.Tags);
    }

    [Fact]
    public async Task UpdateNote_Should_Allow_Page_Reassociation()
    {
        var created = (await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("Note to Reassociate", "Content", "markdown", false, null))).Value;
        Assert.Null(created.PageId);

        var updateRequest = new UpdateNoteRequest("Note to Reassociate", "Content", "markdown", false, _testPage.Id);
        var updateResult = await _noteService.UpdateNoteAsync(_testWorkspace.Id, created.Id, updateRequest);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(_testPage.Id, updateResult.Value.PageId);
        Assert.Equal("Architecture Spec Page", updateResult.Value.PageTitle);
    }

    [Fact]
    public async Task UpdateNote_Should_Reject_CrossWorkspace_Page()
    {
        var created = (await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("Note 1", "Content"))).Value;
        var foreignPage = new Page { WorkspaceId = _otherWorkspace.Id, Title = "Foreign Page" };
        _context.Pages.Add(foreignPage);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateNoteRequest("Note 1", "Content", "markdown", false, foreignPage.Id);
        var updateResult = await _noteService.UpdateNoteAsync(_testWorkspace.Id, created.Id, updateRequest);

        Assert.False(updateResult.IsSuccess);
        Assert.Equal("Notes.InvalidPage", updateResult.Error.Code);
    }

    [Fact]
    public async Task DeleteNote_Should_SoftDelete()
    {
        var created = (await _noteService.CreateNoteAsync(_testWorkspace.Id, new CreateNoteRequest("To Delete", "Content"))).Value;

        var deleteResult = await _noteService.DeleteNoteAsync(_testWorkspace.Id, created.Id);
        Assert.True(deleteResult.IsSuccess);

        var noteInDb = await _context.Notes.IgnoreQueryFilters().FirstAsync(n => n.Id == created.Id);
        Assert.True(noteInDb.IsDeleted);

        var listResult = await _noteService.GetNotesAsync(_testWorkspace.Id);
        Assert.Empty(listResult.Value);
    }

    [Fact]
    public async Task Unauthorized_User_Should_Be_Rejected()
    {
        _currentUserService.UserId = null; // Unauthenticated

        var result = await _noteService.GetNotesAsync(_testWorkspace.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Error.Unauthorized", result.Error.Code);
    }
}
