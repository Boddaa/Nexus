using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Auth;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.API.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", "InMemory:NexusApiTests_" + Guid.NewGuid().ToString("N"));
    }
}

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_And_Login_Flow_Should_Succeed()
    {
        // 1. Register
        var registerRequest = new RegisterRequest("api.test@nexus.ai", "Password123!", "API Test User");
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
        var regData = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(regData);
        Assert.Equal("api.test@nexus.ai", regData.Email);
        Assert.False(string.IsNullOrWhiteSpace(regData.Token));

        // 2. Login
        var loginRequest = new LoginRequest("api.test@nexus.ai", "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginData);
        Assert.Equal("api.test@nexus.ai", loginData.Email);
    }

    [Fact]
    public async Task Unauthorized_Access_To_Protected_Endpoint_Should_Return_401()
    {
        var response = await _client.GetAsync("/api/workspaces");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
