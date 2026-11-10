using System.Buffers.Binary;
using NAudio.Dsp;

namespace EQVisualizer;

// Bounded in-memory analysis only; no playback, files or network output.
// Analyze channels separately so antiphase audio does not cancel or rectify the signal.
internal sealed class AudioAnalyzer
{
    private const int Size = 2048;
    private const int BandCount = 64;
    private const int MaxAnalyzedChannels = 8;
    private static readonly float[] Window = Enumerable.Range(0, Size)
        .Select(i => .5f - .5f * MathF.Cos(MathF.Tau * i / (Size - 1))).ToArray();
    private float[][] samples = [];
    private Complex[][] ffts = [];
    private int filled, analyzedChannels, currentSampleRate;

    public float[] Spectrum { get; private set; } = new float[BandCount];

    public void Reset()
    {
        filled = 0;
        analyzedChannels = 0;
        currentSampleRate = 0;
        Spectrum = new float[BandCount];
    }

    public void Push(ReadOnlySpan<byte> bytes, int channels, int bits, bool floating, int sampleRate)
    {
        if (channels is < 1 or > 32 || sampleRate < 1 ||
            bits is not (16 or 24 or 32) || (floating && bits != 32))
            return;

        int stride = bits / 8;
        int frameBytes = stride * channels;
        int trackedChannels = Math.Min(channels, MaxAnalyzedChannels);
        if (analyzedChannels != trackedChannels || currentSampleRate != sampleRate)
        {
            // A change of capture format must not mix unrelated frames into one FFT.
            samples = Enumerable.Range(0, trackedChannels).Select(_ => new float[Size]).ToArray();
            ffts = Enumerable.Range(0, trackedChannels).Select(_ => new Complex[Size]).ToArray();
            analyzedChannels = trackedChannels;
            currentSampleRate = sampleRate;
            filled = 0;
            Spectrum = new float[BandCount];
        }

        for (int offset = 0; offset + frameBytes <= bytes.Length; offset += frameBytes)
        {
            for (int channel = 0; channel < trackedChannels; channel++)
            {
                var b = bytes.Slice(offset + channel * stride, stride);
                float value = floating
                    ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(b))
                    : bits switch
                    {
                        16 => BinaryPrimitives.ReadInt16LittleEndian(b) / 32768f,
                        24 => ((b[0] | b[1] << 8 | b[2] << 16) << 8) / 2147483648f,
                        32 => BinaryPrimitives.ReadInt32LittleEndian(b) / 2147483648f,
                        _ => 0
                    };
                samples[channel][filled] = float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0;
            }

            if (++filled == Size)
            {
                Analyze(sampleRate);
                filled = 0;
            }
        }
    }

    private void Analyze(int sampleRate)
    {
        var next = new float[BandCount];
        float top = Math.Min(18000, sampleRate / 2f);
        for (int channel = 0; channel < analyzedChannels; channel++)
        {
            var fft = ffts[channel];
            var input = samples[channel];
            for (int i = 0; i < Size; i++)
            {
                fft[i].X = input[i] * Window[i];
                fft[i].Y = 0;
            }

            FastFourierTransform.FFT(true, 11, fft);
            for (int band = 0; band < BandCount; band++)
            {
                int low = Math.Clamp((int)(30 * MathF.Pow(top / 30, band / (float)BandCount) * Size / sampleRate), 1, Size / 2 - 1);
                int high = Math.Clamp((int)(30 * MathF.Pow(top / 30, (band + 1) / (float)BandCount) * Size / sampleRate), low + 1, Size / 2);
                float peak = 0;
                for (int bin = low; bin < high; bin++)
                    peak = Math.Max(peak, MathF.Sqrt(fft[bin].X * fft[bin].X + fft[bin].Y * fft[bin].Y));
                next[band] = Math.Max(next[band], Math.Clamp(MathF.Sqrt(peak * 8), 0, 1));
            }
        }
        Spectrum = next;
    }
}
