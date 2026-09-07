using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.API.Middlewares;
using Xunit;

namespace Nexus.API.Tests;

public class SecurityAndHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SecurityAndHardeningTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_Endpoint_Should_Return_200_OK_And_Healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public void Production_Environment_Without_SecretKey_Should_Fail_Fast()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=NexusDb;Trusted_Connection=True;");
                builder.UseSetting("JwtSettings:SecretKey", "");
            });

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("JwtSettings:SecretKey", ex.ToString());
    }

    [Fact]
    public void Production_Environment_With_Short_SecretKey_Should_Fail_Fast()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=NexusDb;Trusted_Connection=True;");
                builder.UseSetting("JwtSettings:SecretKey", "ShortKeyUnder32Chars");
            });

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("JwtSettings:SecretKey", ex.ToString());
    }

    [Fact]
    public void Production_Environment_With_InMemory_Database_Should_Fail_Fast()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "InMemory:ProdShouldFail");
                builder.UseSetting("JwtSettings:SecretKey", "A_Very_Strong_Production_Secret_Key_32_Bytes_Long!");
            });
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Production");
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Jwt:SecretKey"] = "ProductionVeryStrongSecureKey1234567890123456!",
                            ["ConnectionStrings:DefaultConnection"] = "InMemory:NexusProductionDb",
                            ["Database:ApplyMigrationsOnStartup"] = "false"
                        });
                    });
                });

            using var client = factory.CreateClient();
        });
    }

    [Fact]
    public void Development_Environment_With_InMemory_Database_Should_Start()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "InMemory:NexusDevDb",
                        ["Database:ApplyMigrationsOnStartup"] = "false"
                    });
                });
            });

        using var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    [Fact]
    public async Task Unauthenticated_Access_To_Protected_Endpoint_Should_Return_401()
    {
        using var unauthenticatedClient = _factory.CreateClient();
        var randomWorkspaceId = Guid.NewGuid();
        var response = await unauthenticatedClient.GetAsync($"/api/workspaces/{randomWorkspaceId}/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_Access_To_Forbidden_Workspace_Should_Return_403()
    {
        using var clientA = _factory.CreateClient();
        var userAEmail = $"userA_{Guid.NewGuid():N}@nexus.ai";
        var regResA = await clientA.PostAsJsonAsync("/api/auth/register", new Nexus.Application.DTOs.Auth.RegisterRequest(userAEmail, "Password123!", "User A"));
        var authA = await regResA.Content.ReadFromJsonAsync<Nexus.Application.DTOs.Auth.AuthResponse>();

        clientA.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authA!.Token);
        var wsRes = await clientA.PostAsJsonAsync("/api/workspaces", new Nexus.Application.DTOs.Workspaces.CreateWorkspaceRequest("Workspace A", "Description A", "📁"));
        var ws = await wsRes.Content.ReadFromJsonAsync<Nexus.Application.DTOs.Workspaces.WorkspaceDto>();

        // User B
        using var clientB = _factory.CreateClient();
        var userBEmail = $"userB_{Guid.NewGuid():N}@nexus.ai";
        var regResB = await clientB.PostAsJsonAsync("/api/auth/register", new Nexus.Application.DTOs.Auth.RegisterRequest(userBEmail, "Password123!", "User B"));
        var authB = await regResB.Content.ReadFromJsonAsync<Nexus.Application.DTOs.Auth.AuthResponse>();

        clientB.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authB!.Token);

        // Client B tries to access Workspace A documents
        var forbiddenResponse = await clientB.GetAsync($"/api/workspaces/{ws!.Id}/documents");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_Should_Sanitize_Errors_In_Production()
    {
        var prodEnv = new TestHostEnvironment { EnvironmentName = "Production" };
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Sensitive SQL syntax error: table Users connection string leaked!"),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            prodEnv);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        Assert.DoesNotContain("Sensitive SQL", responseBody);
        Assert.DoesNotContain("table Users", responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        Assert.True(doc.RootElement.TryGetProperty("message", out var message) ||
                    doc.RootElement.TryGetProperty("Message", out message));
        Assert.Equal("An unexpected error occurred. Please try again later.", message.GetString());

        var hasDetailed = doc.RootElement.TryGetProperty("detailed", out _) ||
                          doc.RootElement.TryGetProperty("Detailed", out _);
        Assert.False(hasDetailed, "Production error response must not leak Detailed property.");
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_Should_Include_Details_In_Development()
    {
        var devEnv = new TestHostEnvironment { EnvironmentName = "Development" };
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Dev error message with diagnostic info"),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            devEnv);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        Assert.Contains("Dev error message with diagnostic info", responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        var hasDetailed = doc.RootElement.TryGetProperty("detailed", out var detailed) ||
                          doc.RootElement.TryGetProperty("Detailed", out detailed);
        Assert.True(hasDetailed);
        Assert.Equal("Dev error message with diagnostic info", detailed.GetString());
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Nexus.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
