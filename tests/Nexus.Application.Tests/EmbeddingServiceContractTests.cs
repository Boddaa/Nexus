using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Xunit;

namespace Nexus.Application.Tests;

public class EmbeddingServiceContractTests
{
    private class TestConsumer
    {
        private readonly IEmbeddingService _embeddingService;

        public TestConsumer(IEmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;
        }

        public async Task<float[]> GetVectorAsync(string text)
        {
            return await _embeddingService.GenerateEmbeddingAsync(text);
        }
    }

    private class FakeEmbeddingService : IEmbeddingService
    {
        public int EmbeddingDimension => 1536;

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new float[1536]);
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            var list = new List<float[]>();
            for (int i = 0; i < texts.Count; i++)
            {
                list.Add(new float[1536]);
            }
            return Task.FromResult<IReadOnlyList<float[]>>(list);
        }
    }

    [Fact]
    public async Task Application_Layer_Can_Consume_IEmbeddingService_Without_Infrastructure_Reference()
    {
        IEmbeddingService service = new FakeEmbeddingService();
        var consumer = new TestConsumer(service);

        var vector = await consumer.GetVectorAsync("Hello Clean Architecture");

        Assert.NotNull(vector);
        Assert.Equal(1536, vector.Length);
        Assert.Equal(1536, service.EmbeddingDimension);
    }

    [Fact]
    public void EmbeddingOptions_Defaults_Are_Safe_And_Valid()
    {
        var options = new EmbeddingOptions();

        Assert.Equal(EmbeddingOptions.SectionName, "Embeddings");
        Assert.Equal("None", options.Provider);
        Assert.Equal(1536, options.Dimensions);
        Assert.Empty(options.Model);
        Assert.Null(options.ApiKey);
        Assert.Null(options.Endpoint);
    }
}
