using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Common.Interfaces;
using Nexus.Infrastructure.AI;
using Nexus.Infrastructure.Parsing;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Search;
using Nexus.Infrastructure.Security;
using Nexus.Infrastructure.Storage;

namespace Nexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=NexusDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        if (connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
        {
            var dbName = connectionString.Substring("InMemory:".Length);
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(string.IsNullOrWhiteSpace(dbName) ? "NexusInMemoryDb" : dbName);
            });
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                });
            });
        }

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Security
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // Storage
        services.AddScoped<IFileStorage, LocalFileStorage>();

        // Document Text Extractors
        services.AddSingleton<IDocumentTextExtractor, PlainTextDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, MarkdownDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, DocxDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PdfDocumentExtractor>();

        // AI & Vector Layer (Replaceable & decoupled)
        services.AddSingleton<IChatService, MockChatService>();
        services.AddSingleton<IEmbeddingService, MockEmbeddingService>();
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        services.AddScoped<IRagService, MockRagService>();
        services.AddScoped<IAiDocumentAnalyzer, MockAiDocumentAnalyzer>();
        services.AddScoped<IAiStudyService, MockAiStudyService>();

        // Search
        services.AddScoped<ISearchService, HybridSearchService>();

        return services;
    }
}
