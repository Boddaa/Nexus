using System.Runtime.InteropServices;

namespace Nexus.Application.Common.Utilities;

public static class VectorMath
{
    /// <summary>
    /// Converts a float array into a compact, lossless byte array for SQL Server varbinary persistence.
    /// </summary>
    public static byte[] VectorToBytes(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray();
    }

    /// <summary>
    /// Deserializes a byte array stored in SQL Server varbinary back into a float array.
    /// </summary>
    public static float[] BytesToVector(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return Array.Empty<float>();
        }

        if (bytes.Length % sizeof(float) != 0)
        {
            throw new ArgumentException($"Invalid vector byte array length {bytes.Length}; must be a multiple of {sizeof(float)}.", nameof(bytes));
        }

        var floatSpan = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
        return floatSpan.ToArray();
    }

    /// <summary>
    /// Computes the cosine similarity between two float vectors.
    /// Returns 0.0 for zero vectors or dimension mismatches.
    /// </summary>
    public static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 0.0;
        }

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            double valA = a[i];
            double valB = b[i];

            dotProduct += valA * valB;
            normA += valA * valA;
            normB += valB * valB;
        }

        if (normA <= 0.0 || normB <= 0.0)
        {
            return 0.0;
        }

        var similarity = dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));

        // Clamp to [-1.0, 1.0] to guard against floating-point inaccuracies
        return Math.Clamp(similarity, -1.0, 1.0);
    }
}
