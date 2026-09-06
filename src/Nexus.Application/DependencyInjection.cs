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
        services.AddScoped<Nexus.Application.Features.Documents.Services.IDocumentService, Nexus.Application.Features.Documents.Services.DocumentService>();
        services.AddScoped<Nexus.Application.Features.Documents.Services.IDocumentChunkService, Nexus.Application.Features.Documents.Services.DocumentChunkService>();
        services.AddScoped<Nexus.Application.Features.Documents.Services.IChunkEmbeddingService, Nexus.Application.Features.Documents.Services.ChunkEmbeddingService>();
        services.AddScoped<Nexus.Application.Features.Search.Services.IVectorSearchService, Nexus.Application.Features.Search.Services.VectorSearchService>();
        services.AddScoped<Nexus.Application.Features.Search.Services.ISearchService, Nexus.Application.Features.Search.Services.SearchService>();
        services.AddScoped<Nexus.Application.Features.AI.Services.IContextBuilder, Nexus.Application.Features.AI.Services.ContextBuilder>();
        services.AddScoped<Nexus.Application.Features.AI.Services.IPromptBuilder, Nexus.Application.Features.AI.Services.PromptBuilder>();
        services.AddScoped<Nexus.Application.Features.AI.Services.ICitationValidator, Nexus.Application.Features.AI.Services.CitationValidator>();
        services.AddScoped<Nexus.Application.Common.Interfaces.IRagService, Nexus.Application.Features.AI.Services.RagService>();
        services.AddScoped<Nexus.Application.Features.Conversations.Services.IConversationService, Nexus.Application.Features.Conversations.Services.ConversationService>();
        services.AddScoped<Nexus.Application.Features.AI.Services.IAiKnowledgeService, Nexus.Application.Features.AI.Services.AiKnowledgeService>();

        // Phase 6 Study Engine & AI Tutor
        services.AddScoped<Nexus.Application.Features.Study.Services.ISpacedRepetitionService, Nexus.Application.Features.Study.Services.SpacedRepetitionService>();
        services.AddScoped<Nexus.Application.Features.Study.Services.IStudyTopicService, Nexus.Application.Features.Study.Services.StudyTopicService>();
        services.AddScoped<Nexus.Application.Features.Study.Services.IStudySessionService, Nexus.Application.Features.Study.Services.StudySessionService>();
        services.AddScoped<Nexus.Application.Features.Study.Services.IFlashcardService, Nexus.Application.Features.Study.Services.FlashcardService>();
        services.AddScoped<Nexus.Application.Features.Study.Services.IQuizService, Nexus.Application.Features.Study.Services.QuizService>();
        services.AddScoped<Nexus.Application.Features.Study.Services.IKnowledgeAssessmentService, Nexus.Application.Features.Study.Services.KnowledgeAssessmentService>();
        services.AddScoped<Nexus.Application.Features.Study.Services.IAiTutorService, Nexus.Application.Features.Study.Services.AiTutorService>();

        // Phase 7 Visual Thinking & Knowledge Canvas
        services.AddScoped<Nexus.Application.Features.VisualThinking.Services.IBoardService, Nexus.Application.Features.VisualThinking.Services.BoardService>();
        services.AddScoped<Nexus.Application.Features.VisualThinking.Services.IMindMapService, Nexus.Application.Features.VisualThinking.Services.MindMapService>();
        services.AddScoped<Nexus.Application.Features.VisualThinking.Services.IAutoLayoutService, Nexus.Application.Features.VisualThinking.Services.AutoLayoutService>();
        services.AddScoped<Nexus.Application.Features.VisualThinking.Services.IAiMindMapService, Nexus.Application.Features.VisualThinking.Services.AiMindMapService>();

        return services;
    }
}
