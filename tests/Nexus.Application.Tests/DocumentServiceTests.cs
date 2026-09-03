using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.Features.Documents.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Parsing;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class DocumentServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly FakeTestFileStorage _fakeFileStorage;
    private readonly List<IDocumentTextExtractor> _extractors;
    private readonly DocumentService _documentService;
    private readonly User _testUser;
    private readonly User _otherUser;
    private readonly Workspace _testWorkspace;
    private readonly Workspace _otherWorkspace;
    private readonly Page _testPage;

    public DocumentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "DocumentTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(options, _currentUserService);
        _fakeFileStorage = new FakeTestFileStorage();
        _extractors = new List<IDocumentTextExtractor>
        {
            new PlainTextDocumentExtractor(),
            new MarkdownDocumentExtractor(),
            new DocxDocumentExtractor(),
            new PdfDocumentExtractor()
        };

        _documentService = new DocumentService(_context, _currentUserService, _fakeFileStorage, _extractors);

        // Seed Users
        _testUser = new User { Email = "user@nexus.ai", FullName = "Test User" };
        _otherUser = new User { Email = "other@nexus.ai", FullName = "Other User" };
        _context.Users.AddRange(_testUser, _otherUser);
        _context.SaveChanges();

        // Seed Workspaces
        _testWorkspace = new Workspace { Name = "Main Workspace", OwnerId = _testUser.Id, Icon = "🚀" };
        _otherWorkspace = new Workspace { Name = "Other Workspace", OwnerId = _otherUser.Id, Icon = "🔒" };
        _context.Workspaces.AddRange(_testWorkspace, _otherWorkspace);
        _context.SaveChanges();

        // Seed Page
        _testPage = new Page { WorkspaceId = _testWorkspace.Id, Title = "Architecture Spec" };
        _context.Pages.Add(_testPage);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    #region Extractor Tests

    [Fact]
    public async Task PlainTextDocumentExtractor_Should_Extract_Content()
    {
        var extractor = new PlainTextDocumentExtractor();
        Assert.True(extractor.CanHandle(".txt", "text/plain"));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello plain text document"));
        var result = await extractor.ExtractTextAsync(stream);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello plain text document", result.Value);
    }

    [Fact]
    public async Task MarkdownDocumentExtractor_Should_Extract_Content()
    {
        var extractor = new MarkdownDocumentExtractor();
        Assert.True(extractor.CanHandle(".md", "text/markdown"));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("# Header 1\n\n- Item 1\n- Item 2"));
        var result = await extractor.ExtractTextAsync(stream);

        Assert.True(result.IsSuccess);
        Assert.Equal("# Header 1\n\n- Item 1\n- Item 2", result.Value);
    }

    [Fact]
    public async Task DocxDocumentExtractor_Should_Extract_Paragraphs()
    {
        var extractor = new DocxDocumentExtractor();
        Assert.True(extractor.CanHandle(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));

        using var docxStream = CreateMinimalDocxStream("First Paragraph Text", "Second Paragraph Text");
        var result = await extractor.ExtractTextAsync(docxStream);

        Assert.True(result.IsSuccess);
        Assert.Contains("First Paragraph Text", result.Value);
        Assert.Contains("Second Paragraph Text", result.Value);
    }

    #endregion

    #region Upload Tests

    [Fact]
    public async Task UploadAsync_Valid_Txt_Should_Store_File_And_Persist_Metadata()
    {
        var content = "This is a test note content.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var request = new UploadDocumentStreamRequest(stream, "notes.txt", "text/plain", stream.Length, "My Notes", _testPage.Id);

        var result = await _documentService.UploadAsync(_testWorkspace.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("notes.txt", result.Value.FileName);
        Assert.Equal("My Notes", result.Value.Title);
        Assert.Equal(_testPage.Id, result.Value.PageId);
        Assert.Equal("Architecture Spec", result.Value.PageTitle);
        Assert.Equal(DocumentStatus.Processed, result.Value.Status);
        Assert.Equal(content.Length, result.Value.ExtractedTextLength);

        // Verify storage has the file
        Assert.Single(_fakeFileStorage.Files);

        // Verify database has entity
        var dbDoc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == result.Value.Id);
        Assert.NotNull(dbDoc);
        Assert.Equal(content, dbDoc!.ExtractedText);
    }

    [Fact]
    public async Task UploadAsync_Valid_Markdown_Should_Succeed()
    {
        var content = "# Guide\n\nStep 1: Install NEXUS";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var request = new UploadDocumentStreamRequest(stream, "guide.md", "text/markdown", stream.Length);

        var result = await _documentService.UploadAsync(_testWorkspace.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("guide.md", result.Value.FileName);
        Assert.Equal(DocumentStatus.Processed, result.Value.Status);
    }

    [Fact]
    public async Task UploadAsync_Empty_File_Should_Fail()
    {
        using var stream = new MemoryStream();
        var request = new UploadDocumentStreamRequest(stream, "empty.txt", "text/plain", 0);

        var result = await _documentService.UploadAsync(_testWorkspace.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Documents.EmptyFile", result.Error.Code);
        Assert.Empty(_fakeFileStorage.Files);
    }

    [Fact]
    public async Task UploadAsync_File_Over_50MB_Should_Fail()
    {
        using var stream = new MemoryStream(new byte[10]);
        long over50Mb = 53 * 1024 * 1024;
        var request = new UploadDocumentStreamRequest(stream, "huge.txt", "text/plain", over50Mb);

        var result = await _documentService.UploadAsync(_testWorkspace.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Documents.FileTooLarge", result.Error.Code);
        Assert.Empty(_fakeFileStorage.Files);
    }

    [Fact]
    public async Task UploadAsync_Unsupported_Extension_Should_Fail()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("alert('hack')"));
        var request = new UploadDocumentStreamRequest(stream, "virus.exe", "application/x-msdownload", stream.Length);

        var result = await _documentService.UploadAsync(_testWorkspace.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Documents.UnsupportedFormat", result.Error.Code);
        Assert.Empty(_fakeFileStorage.Files);
    }

    [Fact]
    public async Task UploadAsync_Page_From_Different_Workspace_Should_Fail()
    {
        var otherPage = new Page { WorkspaceId = _otherWorkspace.Id, Title = "Other Page" };
        _context.Pages.Add(otherPage);
        await _context.SaveChangesAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        var request = new UploadDocumentStreamRequest(stream, "test.txt", "text/plain", stream.Length, PageId: otherPage.Id);

        var result = await _documentService.UploadAsync(_testWorkspace.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Documents.PageNotFound", result.Error.Code);
        Assert.Empty(_fakeFileStorage.Files);
    }

    [Fact]
    public async Task UploadAsync_When_Extraction_Fails_Should_Clean_Up_Physical_File()
    {
        // Faulty extractor
        var faultyExtractors = new List<IDocumentTextExtractor>
        {
            new FailingExtractor()
        };
        var serviceWithFailingExtractor = new DocumentService(_context, _currentUserService, _fakeFileStorage, faultyExtractors);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("corrupted content"));
        var request = new UploadDocumentStreamRequest(stream, "bad.txt", "text/plain", stream.Length);

        var result = await serviceWithFailingExtractor.UploadAsync(_testWorkspace.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Extraction.Failed", result.Error.Code);
        // Verify physical file was deleted and not left orphaned
        Assert.Empty(_fakeFileStorage.Files);
        Assert.Empty(_context.Documents);
    }

    #endregion

    #region Retrieval & Deletion Tests

    [Fact]
    public async Task GetDocumentsAsync_Should_Return_Workspace_Documents_Only()
    {
        // Arrange
        var doc1 = new Document { WorkspaceId = _testWorkspace.Id, Title = "Doc 1", FileName = "doc1.txt", StoragePath = "p1", Status = DocumentStatus.Processed };
        var doc2 = new Document { WorkspaceId = _testWorkspace.Id, Title = "Doc 2", FileName = "doc2.txt", StoragePath = "p2", Status = DocumentStatus.Processed };
        var docOther = new Document { WorkspaceId = _otherWorkspace.Id, Title = "Other Doc", FileName = "other.txt", StoragePath = "p3", Status = DocumentStatus.Processed };

        _context.Documents.AddRange(doc1, doc2, docOther);
        await _context.SaveChangesAsync();

        // Act
        var result = await _documentService.GetDocumentsAsync(_testWorkspace.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, d => d.Title == "Doc 1");
        Assert.Contains(result.Value, d => d.Title == "Doc 2");
    }

    [Fact]
    public async Task GetDocumentByIdAsync_Should_Return_Full_Details_Including_ExtractedText()
    {
        var doc = new Document
        {
            WorkspaceId = _testWorkspace.Id,
            Title = "Detailed Spec",
            FileName = "spec.txt",
            ContentType = "text/plain",
            FileSizeBytes = 100,
            StoragePath = "spec.txt",
            Status = DocumentStatus.Processed,
            ExtractedText = "Full body of spec..."
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var result = await _documentService.GetDocumentByIdAsync(_testWorkspace.Id, doc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Detailed Spec", result.Value.Title);
        Assert.Equal("Full body of spec...", result.Value.ExtractedText);
    }

    [Fact]
    public async Task DeleteAsync_Should_Delete_Physical_File_And_Soft_Delete_Metadata()
    {
        var doc = new Document
        {
            WorkspaceId = _testWorkspace.Id,
            Title = "Delete Me",
            FileName = "delete.txt",
            StoragePath = "stored/delete.txt",
            Status = DocumentStatus.Processed
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();
        _fakeFileStorage.Files["stored/delete.txt"] = new byte[] { 1, 2, 3 };

        var result = await _documentService.DeleteAsync(_testWorkspace.Id, doc.Id);

        Assert.True(result.IsSuccess);
        Assert.False(_fakeFileStorage.Files.ContainsKey("stored/delete.txt")); // Physical file deleted

        // Soft deleted in DB
        var dbDoc = await _context.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == doc.Id);
        Assert.NotNull(dbDoc);
        Assert.True(dbDoc!.IsDeleted);
    }

    [Fact]
    public async Task Workspace_Isolation_Unauthorized_User_Cannot_Access_Documents()
    {
        // Current user is _testUser, trying to query _otherWorkspace
        var result = await _documentService.GetDocumentsAsync(_otherWorkspace.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(Error.Unauthorized, result.Error);
    }

    #endregion

    private static MemoryStream CreateMinimalDocxStream(params string[] paragraphs)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var entryStream = entry.Open();
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            var doc = new XDocument(
                new XElement(w + "document",
                    new XElement(w + "body",
                        paragraphs.Select(p =>
                            new XElement(w + "p",
                                new XElement(w + "r",
                                    new XElement(w + "t", p)
                                )
                            )
                        )
                    )
                )
            );

            doc.Save(entryStream);
        }

        stream.Position = 0;
        return stream;
    }

    private class FailingExtractor : IDocumentTextExtractor
    {
        public bool CanHandle(string extension, string contentType) => true;

        public Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Failure<string>(new Error("Extraction.Failed", "Simulated extraction crash.")));
        }
    }

    private class FakeTestFileStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new();

        public Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            return SaveFileAsync(fileStream, "sub", fileName, contentType, cancellationToken);
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string subDirectory, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, cancellationToken);
            var key = $"{subDirectory}/{fileName}".Replace('\\', '/');
            Files[key] = ms.ToArray();
            return key;
        }

        public Task<Stream> GetFileStreamAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            var key = storagePath.Replace('\\', '/');
            if (Files.TryGetValue(key, out var bytes))
            {
                return Task.FromResult<Stream>(new MemoryStream(bytes));
            }
            throw new FileNotFoundException();
        }

        public Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            var key = storagePath.Replace('\\', '/');
            return Task.FromResult(Files.Remove(key));
        }

        public Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            var key = storagePath.Replace('\\', '/');
            return Task.FromResult(Files.ContainsKey(key));
        }
    }
}
