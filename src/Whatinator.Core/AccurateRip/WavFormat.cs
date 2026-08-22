namespace Whatinator.Core.AccurateRip;

/// <summary>A WAV file's PCM format parameters, as read from its <c>fmt </c> chunk.</summary>
/// <param name="Channels">The number of interleaved channels (2 for standard CD audio).</param>
/// <param name="SampleRate">The sample rate in Hz (44100 for standard CD audio).</param>
/// <param name="BitsPerSample">The sample depth in bits (16 for standard CD audio).</param>
public sealed record WavFormat(int Channels, int SampleRate, int BitsPerSample);
