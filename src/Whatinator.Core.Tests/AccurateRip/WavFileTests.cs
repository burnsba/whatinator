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
    public void ReadDataChunk_HandlesOddSizedChunkBeforeData()
    {
        var samples = new byte[] { 7, 7, 7, 7 };
        var path = WriteWav(samples, channels: 2, sampleRate: 44100, bitsPerSample: 16, oddSizedChunkBeforeData: true);

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
    public void ReadDataChunk_ThrowsInvalidDataException_OnTruncatedDataChunk()
    {
        var path = WriteWav([1, 2, 3, 4], channels: 2, sampleRate: 44100, bitsPerSample: 16);

        // Corrupt the data chunk's declared size to claim more bytes than
        // the file actually contains -- the realistic corruption mode for a
        // rip interrupted mid-write.
        var bytes = File.ReadAllBytes(path);
        var dataSizeOffset = bytes.Length - 4 - 4; // 4 data bytes, preceded by their 4-byte size field.
        BitConverter.GetBytes(1000u).CopyTo(bytes, dataSizeOffset);
        File.WriteAllBytes(path, bytes);

        try
        {
            Assert.Throws<InvalidDataException>(() => WavFile.ReadDataChunk(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadDataChunk_ThrowsInvalidDataException_OnMissingDataChunk()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(0u);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16u);
            writer.Write((ushort)1); // PCM
            writer.Write((ushort)2); // channels
            writer.Write(44100u); // sample rate
            writer.Write(176400u); // byte rate
            writer.Write((ushort)4); // block align
            writer.Write((ushort)16); // bits per sample
        }

        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, stream.ToArray());

        try
        {
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
        bool extraChunkBeforeData = false,
        bool oddSizedChunkBeforeData = false)
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

            if (oddSizedChunkBeforeData)
            {
                // An odd-length chunk carries one pad byte after its data,
                // not counted in the chunk's own declared size, so that
                // every chunk after it stays word-aligned.
                writer.Write("LIST"u8);
                writer.Write(3u);
                writer.Write((byte)'I');
                writer.Write((byte)'N');
                writer.Write((byte)'F');
                writer.Write((byte)0); // Pad byte.
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
