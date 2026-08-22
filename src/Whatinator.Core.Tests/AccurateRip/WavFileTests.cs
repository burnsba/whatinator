using System.Text;
using Whatinator.Core.AccurateRip;

namespace Whatinator.Core.Tests;

public class WavFileTests
{
    [Fact]
    public void ReadDataChunkAndReadFormat_RoundTripSyntheticWav()
    {
        var samples = new byte[] { 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0 }; // 3 stereo 16-bit sample pairs
        var path = WriteWav(samples, channels: 2, sampleRate: 44100, bitsPerSample: 16);

        try
        {
            var data = WavFile.ReadDataChunk(path);
            var format = WavFile.ReadFormat(path);

            Assert.Equal(samples, data);
            Assert.Equal(2, format.Channels);
            Assert.Equal(44100, format.SampleRate);
            Assert.Equal(16, format.BitsPerSample);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadDataChunk_SkipsChunksBeforeData()
    {
        var samples = new byte[] { 9, 9, 9, 9 };
        var path = WriteWav(samples, channels: 2, sampleRate: 44100, bitsPerSample: 16, extraChunkBeforeData: true);

        try
        {
            var data = WavFile.ReadDataChunk(path);

            Assert.Equal(samples, data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadDataChunk_ThrowsInvalidDataException_OnNonRiffFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, "not a wav file at all"u8.ToArray());

            Assert.Throws<InvalidDataException>(() => WavFile.ReadDataChunk(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFormat_ThrowsInvalidDataException_OnNonPcmFormat()
    {
        var path = WriteWav([1, 2, 3, 4], channels: 2, sampleRate: 44100, bitsPerSample: 16, audioFormat: 3); // IEEE float, not PCM
        try
        {
            Assert.Throws<InvalidDataException>(() => WavFile.ReadFormat(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Writes a minimal synthetic RIFF/WAVE file for testing.</summary>
    private static string WriteWav(
        byte[] data,
        int channels,
        int sampleRate,
        int bitsPerSample,
        ushort audioFormat = 1,
        bool extraChunkBeforeData = false)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            var blockAlign = (ushort)(channels * (bitsPerSample / 8));
            var byteRate = (uint)(sampleRate * blockAlign);

            writer.Write("RIFF"u8);
            writer.Write(0u); // Overall size -- unused by WavFile, left as 0.
            writer.Write("WAVE"u8);

            writer.Write("fmt "u8);
            writer.Write(16u);
            writer.Write(audioFormat);
            writer.Write((ushort)channels);
            writer.Write((uint)sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write((ushort)bitsPerSample);

            if (extraChunkBeforeData)
            {
                writer.Write("LIST"u8);
                writer.Write(4u);
                writer.Write("INFO"u8);
            }

            writer.Write("data"u8);
            writer.Write((uint)data.Length);
            writer.Write(data);
        }

        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }
}
