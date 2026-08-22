using System.Text.Json;

namespace Whatinator.Core.Metadata;

/// <summary>Reads and writes <c>releaseinfo.json</c> files.</summary>
public static class ReleaseInfoFile
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Serializes <paramref name="releaseInfo"/> to <paramref name="path"/> as indented JSON.</summary>
    /// <param name="releaseInfo">The release metadata to write.</param>
    /// <param name="path">The destination file path.</param>
    public static void Save(ReleaseInfo releaseInfo, string path)
    {
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, releaseInfo, JsonOptions);
    }

    /// <summary>Reads and deserializes a <c>releaseinfo.json</c> file.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The deserialized release metadata.</returns>
    /// <exception cref="JsonException">The file isn't valid JSON, or doesn't match the expected shape.</exception>
    public static ReleaseInfo Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ReleaseInfo>(stream, JsonOptions)
            ?? throw new JsonException($"'{path}' deserialized to null.");
    }
}
