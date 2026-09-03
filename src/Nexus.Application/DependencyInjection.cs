using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Features.Auth.Services;
using Nexus.Application.Features.Notes.Services;
using Nexus.Application.Features.Pages.Services;
using Nexus.Application.Features.Workspaces.Services;

namespace Nexus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IPageService, PageService>();
        services.AddScoped<INoteService, NoteService>();

        return services;
    }
}
