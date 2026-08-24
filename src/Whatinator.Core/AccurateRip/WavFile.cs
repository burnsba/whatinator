using System.Buffers.Binary;
using System.Text;

namespace Whatinator.Core.AccurateRip;

/// <summary>
/// A minimal RIFF/WAVE chunk reader -- just enough to pull out the raw PCM
/// <c>data</c> chunk bytes and <c>fmt </c> parameters for AccurateRip/CRC32
/// checksumming. Walks chunks rather than assuming a fixed 44-byte header,
/// since a real WAV file can carry extra chunks (e.g. <c>LIST</c>) before
/// <c>data</c>.
/// </summary>
/// <remarks>
/// This project's rip pipeline always controls the WAV files it reads
/// (captured by its own <c>cd-paranoia</c> runner, phase 014), so a
/// malformed or non-PCM file is a real bug, not an input to validate
/// gracefully against -- every method here throws
/// <see cref="InvalidDataException"/> rather than returning a failure value.
/// </remarks>
public static class WavFile
{
    /// <summary>Locates and returns a WAV file's raw <c>data</c> chunk bytes.</summary>
    /// <param name="path">The path to a RIFF/WAVE file.</param>
    /// <returns>The <c>data</c> chunk's bytes, with no RIFF/chunk headers.</returns>
    public static byte[] ReadDataChunk(string path) => ReadChunks(path).Data;

    /// <summary>Reads a WAV file's PCM format parameters from its <c>fmt </c> chunk.</summary>
    /// <param name="path">The path to a RIFF/WAVE file.</param>
    /// <returns>The file's channel count, sample rate, and bit depth.</returns>
    public static WavFormat ReadFormat(string path)
    {
        var fmt = ReadChunks(path).Format;
        if (fmt.Length < 16)
        {
            throw new InvalidDataException($"'{path}' has a truncated 'fmt ' chunk.");
        }

        var audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(0, 2));
        if (audioFormat != 1)
        {
            throw new InvalidDataException($"'{path}' is not PCM-encoded (audio format {audioFormat}).");
        }

        var channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(2, 2));
        var sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(fmt.AsSpan(4, 4));
        var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(14, 2));
        return new WavFormat(channels, (int)sampleRate, bitsPerSample);
    }

    /// <summary>Walks every chunk in a RIFF/WAVE file, returning the <c>fmt </c> and <c>data</c> chunk bytes.</summary>
    /// <param name="path">The path to a RIFF/WAVE file.</param>
    private static (byte[] Format, byte[] Data) ReadChunks(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (!ReadFourCc(reader).Equals("RIFF", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{path}' is not a RIFF file.");
        }

        reader.ReadUInt32(); // Overall RIFF chunk size -- unused, the stream length is authoritative.

        if (!ReadFourCc(reader).Equals("WAVE", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{path}' is not a WAVE file.");
        }

        var chunks = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        while (stream.Position < stream.Length && !(chunks.ContainsKey("fmt ") && chunks.ContainsKey("data")))
        {
            var chunkId = ReadFourCc(reader);
            var chunkSize = reader.ReadUInt32();
            var chunkData = reader.ReadBytes(checked((int)chunkSize));
            if (chunkData.Length != chunkSize)
            {
                // BinaryReader.ReadBytes doesn't throw on a short read -- it
                // silently returns whatever it managed to read. Left
                // unchecked, a truncated chunk (e.g. a rip interrupted
                // mid-write) would feed short PCM straight into
                // AccurateRipChecksum instead of failing loudly, breaking
                // this class's documented "throw, don't degrade" contract.
                throw new InvalidDataException(
                    $"'{path}' has a truncated '{chunkId}' chunk: expected {chunkSize} bytes, found {chunkData.Length}.");
            }

            if (chunkSize % 2 == 1 && stream.Position < stream.Length)
            {
                reader.ReadByte(); // RIFF chunks are word-aligned; an odd size carries one pad byte.
            }

            chunks.TryAdd(chunkId, chunkData);
        }

        if (!chunks.TryGetValue("fmt ", out var format))
        {
            throw new InvalidDataException($"'{path}' has no 'fmt ' chunk.");
        }

        if (!chunks.TryGetValue("data", out var data))
        {
            throw new InvalidDataException($"'{path}' has no 'data' chunk.");
        }

        return (format, data);
    }

    /// <summary>Reads a 4-byte RIFF chunk/format identifier as ASCII.</summary>
    /// <param name="reader">The reader positioned at the start of a four-character code.</param>
    private static string ReadFourCc(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length < 4)
        {
            throw new InvalidDataException("Unexpected end of file while reading a RIFF chunk identifier.");
        }

        return Encoding.ASCII.GetString(bytes);
    }
}
