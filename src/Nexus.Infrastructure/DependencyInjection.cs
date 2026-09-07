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
        var envName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var isProduction = string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=NexusDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        if (connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
        {
            if (isProduction)
            {
                throw new InvalidOperationException("InMemory database provider is strictly prohibited in Production environment. A valid SQL Server connection string is required.");
            }

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

        // Document Text Extractors & Chunker
        services.AddSingleton<IDocumentTextExtractor, PlainTextDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, MarkdownDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, DocxDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PdfDocumentExtractor>();
        services.Configure<Nexus.Application.Common.Options.ChunkingOptions>(configuration.GetSection(Nexus.Application.Common.Options.ChunkingOptions.SectionName));
        services.AddSingleton<IDocumentChunker, Nexus.Infrastructure.AI.Chunking.DocumentChunker>();

        // AI & Vector Layer (Replaceable & decoupled)
        services.Configure<Nexus.Application.Common.Options.EmbeddingOptions>(configuration.GetSection(Nexus.Application.Common.Options.EmbeddingOptions.SectionName));
        services.Configure<Nexus.Application.Common.Options.HybridSearchOptions>(configuration.GetSection(Nexus.Application.Common.Options.HybridSearchOptions.SectionName));
        services.Configure<Nexus.Application.Common.Options.LlmOptions>(configuration.GetSection(Nexus.Application.Common.Options.LlmOptions.SectionName));
        services.Configure<Nexus.Application.Common.Options.RagOptions>(configuration.GetSection(Nexus.Application.Common.Options.RagOptions.SectionName));
        services.Configure<Nexus.Application.Common.Options.VisualThinkingOptions>(configuration.GetSection(Nexus.Application.Common.Options.VisualThinkingOptions.SectionName));

        services.AddHttpClient<Nexus.Infrastructure.AI.Embeddings.OpenAiEmbeddingProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<Nexus.Infrastructure.AI.Embeddings.OllamaEmbeddingProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<Nexus.Infrastructure.AI.Embeddings.IEmbeddingProvider, Nexus.Infrastructure.AI.Embeddings.OpenAiEmbeddingProvider>();
        services.AddScoped<Nexus.Infrastructure.AI.Embeddings.IEmbeddingProvider, Nexus.Infrastructure.AI.Embeddings.OllamaEmbeddingProvider>();
        services.AddScoped<IEmbeddingService, Nexus.Infrastructure.AI.Embeddings.EmbeddingService>();

        services.AddHttpClient<Nexus.Infrastructure.AI.LLM.OpenAiLlmProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<Nexus.Infrastructure.AI.LLM.OllamaLlmProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<Nexus.Infrastructure.AI.LLM.ILlmProvider, Nexus.Infrastructure.AI.LLM.OpenAiLlmProvider>();
        services.AddScoped<Nexus.Infrastructure.AI.LLM.ILlmProvider, Nexus.Infrastructure.AI.LLM.OllamaLlmProvider>();
        services.AddScoped<Nexus.Application.Common.Interfaces.ILLMService, Nexus.Infrastructure.AI.LLM.LLMService>();



        return services;
    }
}
