using Microsoft.EntityFrameworkCore;
using Nexus.Application.DTOs.Search;
using Nexus.Application.Features.Search.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class SearchServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly SearchService _searchService;
    private readonly User _testUser;
    private readonly User _otherUser;
    private readonly Workspace _workspaceA;
    private readonly Workspace _workspaceB;

    public SearchServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "SearchTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(options, _currentUserService);
        _searchService = new SearchService(_context, _currentUserService);

        // Seed Users
        _testUser = new User { Email = "alice@nexus.ai", FullName = "Alice Architect" };
        _otherUser = new User { Email = "bob@nexus.ai", FullName = "Bob Builder" };
        _context.Users.AddRange(_testUser, _otherUser);
        _context.SaveChanges();

        // Seed Workspaces
        _workspaceA = new Workspace { Name = "Workspace A", OwnerId = _testUser.Id, Icon = "🅰️" };
        _workspaceB = new Workspace { Name = "Workspace B", OwnerId = _otherUser.Id, Icon = "🅱️" };
        _context.Workspaces.AddRange(_workspaceA, _workspaceB);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    #region Basic & Multi-source Search Tests

    [Fact]
    public async Task SearchAsync_Should_Find_Matching_Pages_Notes_And_Documents()
    {
        // Arrange
        var page = new Page
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Kubernetes Overview",
            ContentJson = "{\"body\":\"Kubernetes architecture and pods deployment.\"}"
        };

        var note = new Note
        {
            WorkspaceId = _workspaceA.Id,
            Title = "DevOps Notes",
            Content = "Ensure Kubernetes clusters are monitored."
        };

        var doc = new Document
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Cloud Infrastructure Guide",
            FileName = "guide.pdf",
            ExtractedText = "Deploying production Kubernetes on Azure.",
            Status = DocumentStatus.Processed
        };

        _context.Pages.Add(page);
        _context.Notes.Add(note);
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        // Act
        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Kubernetes"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Contains(result.Value.Items, i => i.Type == "Page" && i.Title == "Kubernetes Overview");
        Assert.Contains(result.Value.Items, i => i.Type == "Note" && i.Title == "DevOps Notes");
        Assert.Contains(result.Value.Items, i => i.Type == "Document" && i.Title == "Cloud Infrastructure Guide");
    }

    [Fact]
    public async Task SearchAsync_Should_Be_Case_Insensitive()
    {
        // Arrange
        var note = new Note
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Entity Framework Core Guide",
            Content = "Deep dive into ORM and LINQ queries."
        };
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        // Act
        var resLower = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("entity framework"));
        var resUpper = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("ENTITY FRAMEWORK"));
        var resMixed = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Entity Framework"));

        // Assert
        Assert.True(resLower.IsSuccess);
        Assert.True(resUpper.IsSuccess);
        Assert.True(resMixed.IsSuccess);
        Assert.Single(resLower.Value.Items);
        Assert.Single(resUpper.Value.Items);
        Assert.Single(resMixed.Value.Items);
    }

    #endregion

    #region Ranking Tests

    [Fact]
    public async Task SearchAsync_Stronger_Title_Match_Should_Rank_Higher_Than_Content_Match()
    {
        // Arrange
        // Doc A has query in Title
        var docA = new Document
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Entity Framework",
            FileName = "ef.txt",
            ExtractedText = "General database content here.",
            Status = DocumentStatus.Processed
        };

        // Doc B has query only in Content
        var docB = new Document
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Database Systems",
            FileName = "db.txt",
            ExtractedText = "This chapter discusses Entity Framework configurations.",
            Status = DocumentStatus.Processed
        };

        _context.Documents.AddRange(docA, docB);
        await _context.SaveChangesAsync();

        // Act
        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Entity Framework"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        // Doc A must rank before Doc B
        Assert.Equal(docA.Id, result.Value.Items[0].Id);
        Assert.Equal(docB.Id, result.Value.Items[1].Id);
        Assert.True(result.Value.Items[0].Score > result.Value.Items[1].Score);
    }

    #endregion

    #region Snippet Tests

    [Fact]
    public async Task SearchAsync_Snippet_Should_Contain_Context_Around_Match_And_Not_Full_Text()
    {
        // Arrange
        var longText = "Start of document. " + new string('x', 200) +
                       " Important TargetKeyword in middle. " +
                       new string('y', 200) + " End of document.";

        var doc = new Document
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Large Document",
            FileName = "large.txt",
            ExtractedText = longText,
            Status = DocumentStatus.Processed
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        // Act
        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("TargetKeyword"));

        // Assert
        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Contains("TargetKeyword", item.Snippet);
        Assert.True(item.Snippet.Length < longText.Length);
        Assert.True(item.Snippet.Length <= 300);
    }

    #endregion

    #region Type Filter Tests

    [Fact]
    public async Task SearchAsync_Type_Filter_Should_Only_Return_Specified_Entity_Type()
    {
        // Arrange
        var page = new Page { WorkspaceId = _workspaceA.Id, Title = "Docker Architecture", ContentJson = "{}" };
        var note = new Note { WorkspaceId = _workspaceA.Id, Title = "Docker Compose", Content = "Containers" };
        var doc = new Document { WorkspaceId = _workspaceA.Id, Title = "Docker Guide", FileName = "d.txt", ExtractedText = "Docker engines", Status = DocumentStatus.Processed };

        _context.Pages.Add(page);
        _context.Notes.Add(note);
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        // Act - filter by Note
        var noteResult = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Docker", Type: "Note"));
        // Act - filter by Document
        var docResult = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Docker", Type: "Document"));
        // Act - filter by Page
        var pageResult = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Docker", Type: "Page"));

        // Assert
        Assert.True(noteResult.IsSuccess);
        Assert.Single(noteResult.Value.Items);
        Assert.Equal("Note", noteResult.Value.Items[0].Type);

        Assert.True(docResult.IsSuccess);
        Assert.Single(docResult.Value.Items);
        Assert.Equal("Document", docResult.Value.Items[0].Type);

        Assert.True(pageResult.IsSuccess);
        Assert.Single(pageResult.Value.Items);
        Assert.Equal("Page", pageResult.Value.Items[0].Type);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SearchAsync_Empty_Query_Should_Fail_Validation(string? query)
    {
        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest(query!));
        Assert.False(result.IsSuccess);
        Assert.Equal("Search.EmptyQuery", result.Error.Code);
    }

    [Fact]
    public async Task SearchAsync_Query_Over_200_Chars_Should_Fail_Validation()
    {
        var longQuery = new string('a', 201);
        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest(longQuery));
        Assert.False(result.IsSuccess);
        Assert.Equal("Search.QueryTooLong", result.Error.Code);
    }

    [Fact]
    public async Task SearchAsync_Invalid_Type_Filter_Should_Fail_Validation()
    {
        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("test", Type: "MaliciousType"));
        Assert.False(result.IsSuccess);
        Assert.Equal("Search.InvalidType", result.Error.Code);
    }

    #endregion

    #region Workspace Isolation Tests

    [Fact]
    public async Task SearchAsync_Workspace_Isolation_Should_Never_Return_Another_Workspace_Results()
    {
        // Content in Workspace A
        var docA = new Document { WorkspaceId = _workspaceA.Id, Title = "Secret Strategy A", FileName = "a.txt", ExtractedText = "A plan", Status = DocumentStatus.Processed };
        // Content in Workspace B
        var docB = new Document { WorkspaceId = _workspaceB.Id, Title = "Secret Strategy B", FileName = "b.txt", ExtractedText = "B plan", Status = DocumentStatus.Processed };

        _context.Documents.AddRange(docA, docB);
        await _context.SaveChangesAsync();

        // User searches Workspace A
        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Secret Strategy"));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(docA.Id, result.Value.Items[0].Id);
        Assert.DoesNotContain(result.Value.Items, i => i.Id == docB.Id);
    }

    [Fact]
    public async Task SearchAsync_Unauthorized_User_Cannot_Search_Foreign_Workspace()
    {
        // Current user is Alice (_testUser), trying to search Bob's workspace (_workspaceB)
        var result = await _searchService.SearchAsync(_workspaceB.Id, new SearchRequest("Anything"));
        Assert.False(result.IsSuccess);
        Assert.Equal(Error.Unauthorized, result.Error);
    }

    #endregion

    #region Soft Delete Tests

    [Fact]
    public async Task SearchAsync_Soft_Deleted_Entities_Should_Not_Be_Returned()
    {
        var page = new Page { WorkspaceId = _workspaceA.Id, Title = "Deleted Page Active Match", ContentJson = "{}" };
        var note = new Note { WorkspaceId = _workspaceA.Id, Title = "Deleted Note Active Match", Content = "text" };
        var doc = new Document { WorkspaceId = _workspaceA.Id, Title = "Deleted Doc Active Match", FileName = "d.txt", ExtractedText = "txt", Status = DocumentStatus.Processed };

        _context.Pages.Add(page);
        _context.Notes.Add(note);
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        // Soft delete all three
        page.IsDeleted = true;
        note.IsDeleted = true;
        doc.IsDeleted = true;
        await _context.SaveChangesAsync();

        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Active Match"));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task SearchAsync_Pagination_Should_Work_Correctly()
    {
        // Add 5 notes
        for (int i = 1; i <= 5; i++)
        {
            _context.Notes.Add(new Note
            {
                WorkspaceId = _workspaceA.Id,
                Title = $"Item {i:D2} Match",
                Content = "Paginated test note"
            });
        }
        await _context.SaveChangesAsync();

        // Page 1 with size 2
        var page1 = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Match", Page: 1, PageSize: 2));
        // Page 2 with size 2
        var page2 = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Match", Page: 2, PageSize: 2));
        // Page 3 with size 2
        var page3 = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("Match", Page: 3, PageSize: 2));

        Assert.True(page1.IsSuccess);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.Equal(5, page1.Value.TotalCount);
        Assert.Equal(3, page1.Value.TotalPages);

        Assert.True(page2.IsSuccess);
        Assert.Equal(2, page2.Value.Items.Count);

        Assert.True(page3.IsSuccess);
        Assert.Single(page3.Value.Items);

        // Ensure disjoint items between pages
        var page1Ids = page1.Value.Items.Select(i => i.Id);
        var page2Ids = page2.Value.Items.Select(i => i.Id);
        Assert.Empty(page1Ids.Intersect(page2Ids));
    }

    [Fact]
    public async Task SearchAsync_When_Zero_Matches_Should_Return_Empty_With_Zero_TotalCount()
    {
        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("NonExistentQueryXYZ123"));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(0, result.Value.TotalPages);
    }

    [Fact]
    public async Task SearchAsync_Large_Document_Two_Stage_Snippet_Should_Materialize_Accurately()
    {
        // 50,000 chars of filler text with keyword deep inside
        var largeText = new string('a', 15000) + " SpecializedQuantumConcept " + new string('b', 15000);
        var doc = new Document
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Quantum Physics Manual",
            FileName = "quantum.pdf",
            ExtractedText = largeText,
            Status = DocumentStatus.Processed
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var result = await _searchService.SearchAsync(_workspaceA.Id, new SearchRequest("SpecializedQuantumConcept"));

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Document", item.Type);
        Assert.Contains("SpecializedQuantumConcept", item.Snippet);
        Assert.True(item.Snippet.Length < 300);
    }

    #endregion
}
