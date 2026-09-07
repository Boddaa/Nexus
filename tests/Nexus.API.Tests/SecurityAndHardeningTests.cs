using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.API.Middlewares;
using Xunit;

namespace Nexus.API.Tests;

public class SecurityAndHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityAndHardeningTests(CustomWebApplicationFactory factory)
    {
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
                builder.UseSetting("ConnectionStrings:DefaultConnection", "InMemory:SecTests_" + Guid.NewGuid().ToString("N"));
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
                builder.UseSetting("ConnectionStrings:DefaultConnection", "InMemory:SecTests_" + Guid.NewGuid().ToString("N"));
                builder.UseSetting("JwtSettings:SecretKey", "ShortKeyUnder32Chars");
            });

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("JwtSettings:SecretKey", ex.ToString());
    }

    [Fact]
    public void Production_Environment_With_Valid_SecretKey_Should_Succeed()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "InMemory:SecTests_" + Guid.NewGuid().ToString("N"));
                builder.UseSetting("JwtSettings:SecretKey", "A_Very_Strong_Production_Secret_Key_32_Bytes_Long!");
            });

        using var client = factory.CreateClient();
        Assert.NotNull(client);
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
