using System;
using System.Text.Json;
using SmartComponents.LocalEmbeddings;

namespace FypMatchTracker.Services;

/// <summary>
/// Service that generates and compares semantic text embeddings.
/// Directly uses the Microsoft package SmartComponents.LocalEmbeddings with the 16MB local ONNX model.
/// Runs 100% locally on CPU with zero API costs, keeping student data private.
/// </summary>
public class EmbeddingService : IDisposable
{
    private readonly LocalEmbedder _embedder;
    private bool _disposed;

    public EmbeddingService()
    {
        // Initializes the local ONNX model (bge-micro ~16MB) bundled with SmartComponents
        _embedder = new LocalEmbedder();
    }

    /// <summary>
    /// Generates a 384-dimensional semantic embedding for the given text.
    /// Returns the embedding as an EmbeddingF32 object.
    /// </summary>
    public EmbeddingF32 GetEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            text = "empty";

        return _embedder.Embed<EmbeddingF32>(text);
    }

    /// <summary>
    /// Generates an embedding and serializes it to a compact Base64 string for database storage.
    /// </summary>
    public string GetEmbeddingJson(string text)
    {
        var embedding = GetEmbedding(text);
        // EmbeddingF32 wraps ReadOnlyMemory<byte> (1536 bytes for 384 floats)
        return Convert.ToBase64String(embedding.Buffer.Span);
    }

    /// <summary>
    /// Deserializes a stored Base64 string back into an EmbeddingF32 object.
    /// </summary>
    public EmbeddingF32? DeserializeEmbedding(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            return new EmbeddingF32(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Computes calibrated cosine similarity percentage (0 to 100%).
    /// Calibrates the raw cosine similarity by subtracting the baseline (~0.52) of unrelated English technical texts,
    /// ensuring distinct ideas receive low scores (<20%) while truly similar ideas receive high scores.
    /// </summary>
    public double CalibrateCosineSimilarity(float rawSim)
    {
        const double baseline = 0.52; // Threshold below which texts have no meaningful semantic relationship

        if (rawSim <= baseline)
        {
            return 0.0;
        }

        // Linear interpolation from [baseline, 1.0] -> [0.0, 100.0]
        double calibrated = (rawSim - baseline) / (1.0 - baseline) * 100.0;
        return Math.Round(Math.Clamp(calibrated, 0.0, 100.0), 1);
    }

    /// <summary>
    /// Computes cosine similarity between two texts (0 to 100 percentage).
    /// </summary>
    public double CosineSimilarity(string textA, string textB)
    {
        if (string.IsNullOrWhiteSpace(textA) || string.IsNullOrWhiteSpace(textB))
            return 0.0;

        var embedA = GetEmbedding(textA);
        var embedB = GetEmbedding(textB);

        float sim = LocalEmbedder.Similarity(embedA, embedB);
        return CalibrateCosineSimilarity(sim);
    }

    /// <summary>
    /// Computes cosine similarity when one or both embeddings are already stored in the database.
    /// </summary>
    public double CosineSimilarityWithStored(EmbeddingF32 queryEmbedding, string? storedEmbeddingJson, string storedFallbackText)
    {
        EmbeddingF32? targetEmbedding = DeserializeEmbedding(storedEmbeddingJson);

        if (targetEmbedding == null)
        {
            // If stored embedding was null, compute on the fly from the stored text
            targetEmbedding = GetEmbedding(storedFallbackText);
        }

        float sim = LocalEmbedder.Similarity(queryEmbedding, targetEmbedding.Value);
        return CalibrateCosineSimilarity(sim);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _embedder?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
