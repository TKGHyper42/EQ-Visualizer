using System.Buffers.Binary;
using NAudio.Dsp;
namespace EQVisualizer;

// Bounded in-memory analysis only; no playback, files or network output.
internal sealed class AudioAnalyzer
{
    private const int Size = 2048;
    private readonly Complex[] fft = new Complex[Size];
    private readonly float[] samples = new float[Size];
    private int filled;
    public float[] Spectrum { get; private set; } = new float[64];
    public void Reset() { filled = 0; Spectrum = new float[64]; }
    public void Push(ReadOnlySpan<byte> bytes, int channels, int bits, bool floating, int sampleRate)
    {
        int stride = bits / 8;
        if (channels < 1 || sampleRate < 1 || stride is < 2 or > 4 || floating && bits != 32) return;
        int frameBytes = stride * channels;
        for (int offset = 0; offset + frameBytes <= bytes.Length; offset += frameBytes)
        {
            float sum = 0;
            float energy = 0;
            for (int c = 0; c < channels; c++)
            {
                var b = bytes.Slice(offset + c * stride, stride);
                float value = floating ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(b)) : bits switch
                {
                    16 => BinaryPrimitives.ReadInt16LittleEndian(b) / 32768f,
                    24 => ((b[0] | b[1] << 8 | b[2] << 16) << 8) / 2147483648f,
                    32 => BinaryPrimitives.ReadInt32LittleEndian(b) / 2147483648f,
                    _ => 0
                };
                if (!float.IsFinite(value)) value = 0;
                sum += value;
                energy += value * value;
            }
            // RMS mixing keeps genuine stereo energy visible even when channels are antiphase.
            float polarity = sum < 0 ? -1 : 1;
            samples[filled++] = Math.Clamp(polarity * MathF.Sqrt(energy / channels), -1, 1);
            if (filled == Size) { Analyze(sampleRate); filled = 0; }
        }
    }
    private void Analyze(int sampleRate)
    {
        for (int i = 0; i < Size; i++) { fft[i].X = samples[i] * (.5f - .5f * MathF.Cos(MathF.Tau * i / (Size - 1))); fft[i].Y = 0; }
        FastFourierTransform.FFT(true, 11, fft);
        var next = new float[64];
        float top = Math.Min(18000, sampleRate / 2f);
        for (int band = 0; band < 64; band++)
        {
            int low = Math.Clamp((int)(30 * MathF.Pow(top / 30, band / 64f) * Size / sampleRate), 1, Size / 2 - 1);
            int high = Math.Clamp((int)(30 * MathF.Pow(top / 30, (band + 1) / 64f) * Size / sampleRate), low + 1, Size / 2);
            float peak = 0;
            for (int bin = low; bin < high; bin++) peak = Math.Max(peak, MathF.Sqrt(fft[bin].X * fft[bin].X + fft[bin].Y * fft[bin].Y));
            next[band] = Math.Clamp(MathF.Sqrt(peak * 8), 0, 1);
        }
        Spectrum = next;
    }
}
