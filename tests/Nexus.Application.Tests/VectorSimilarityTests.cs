using Nexus.Application.Common.Utilities;
using Xunit;

namespace Nexus.Application.Tests;

public class VectorSimilarityTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var vec = new float[] { 0.2f, 0.5f, -0.8f, 0.1f };
        var similarity = VectorMath.CosineSimilarity(vec, vec);

        Assert.Equal(1.0, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var vecA = new float[] { 1.0f, 0.0f, 0.0f };
        var vecB = new float[] { 0.0f, 1.0f, 0.0f };

        var similarity = VectorMath.CosineSimilarity(vecA, vecB);

        Assert.Equal(0.0, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OpposingVectors_ReturnsNegativeOne()
    {
        var vecA = new float[] { 0.5f, 0.5f };
        var vecB = new float[] { -0.5f, -0.5f };

        var similarity = VectorMath.CosineSimilarity(vecA, vecB);

        Assert.Equal(-1.0, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_ReturnsZeroWithoutException()
    {
        var vecA = new float[] { 0.0f, 0.0f, 0.0f };
        var vecB = new float[] { 0.5f, 0.2f, 0.1f };

        var similarity = VectorMath.CosineSimilarity(vecA, vecB);

        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void CosineSimilarity_DimensionMismatch_ReturnsZero()
    {
        var vecA = new float[] { 0.1f, 0.2f, 0.3f };
        var vecB = new float[] { 0.1f, 0.2f };

        var similarity = VectorMath.CosineSimilarity(vecA, vecB);

        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_ReturnsZero()
    {
        var similarity = VectorMath.CosineSimilarity(ReadOnlySpan<float>.Empty, ReadOnlySpan<float>.Empty);
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void VectorToBytes_And_BytesToVector_RoundtripsAccurately()
    {
        var original = new float[] { 0.123456f, -0.987654f, 42.0f, 0.00001f, -100.5f };
        var bytes = VectorMath.VectorToBytes(original);

        Assert.NotNull(bytes);
        Assert.Equal(original.Length * sizeof(float), bytes.Length);

        var restored = VectorMath.BytesToVector(bytes);

        Assert.Equal(original.Length, restored.Length);
        for (int i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i], restored[i]);
        }
    }

    [Fact]
    public void BytesToVector_NullOrEmpty_ReturnsEmptyArray()
    {
        Assert.Empty(VectorMath.BytesToVector(null));
        Assert.Empty(VectorMath.BytesToVector(Array.Empty<byte>()));
    }

    [Fact]
    public void BytesToVector_InvalidLength_ThrowsArgumentException()
    {
        var invalidBytes = new byte[] { 1, 2, 3 }; // Not a multiple of 4
        Assert.Throws<ArgumentException>(() => VectorMath.BytesToVector(invalidBytes));
    }
}
