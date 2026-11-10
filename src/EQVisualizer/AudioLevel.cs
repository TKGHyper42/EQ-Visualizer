using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace EQVisualizer;

internal sealed class AudioLevel : IDisposable
{
    private WasapiLoopbackCapture? capture;
    private MMDevice? device;
    private readonly AudioAnalyzer analyzer = new();
    private float[] bands = new float[64];
    private long updated;
    private string? activeId;
    public string? Error { get; private set; }
    public float[] Bands => Stopwatch.GetElapsedTime(Volatile.Read(ref updated)).TotalSeconds < .3 ? Volatile.Read(ref bands) : new float[64];

    public void Update(bool needed, string? deviceId)
    {
        if (!needed) { Stop(); return; }
        try
        {
            using var endpoints = new MMDeviceEnumerator();
            using var selected = deviceId is null ? endpoints.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : endpoints.GetDevice(deviceId);
            if (capture is null || activeId != selected.ID) Start(selected.ID);
        }
        catch (Exception e) { Error = e.Message; Stop(); }
    }
    public void Start(string? deviceId)
    {
        Stop();
        try
        {
            using var endpoints = new MMDeviceEnumerator();
            device = deviceId is null ? endpoints.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : endpoints.GetDevice(deviceId);
            var source = new WasapiLoopbackCapture(device); capture = source; activeId = device.ID;
            analyzer.Reset(); Error = null;
            source.DataAvailable += (_, e) =>
            {
                if (!ReferenceEquals(capture, source)) return;
                var f = source.WaveFormat;
                bool floating = f.Encoding == WaveFormatEncoding.IeeeFloat ||
                    f is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
                analyzer.Push(e.Buffer.AsSpan(0, e.BytesRecorded), f.Channels, f.BitsPerSample, floating, f.SampleRate);
                Volatile.Write(ref bands, analyzer.Spectrum);
                Volatile.Write(ref updated, Stopwatch.GetTimestamp());
            };
            source.RecordingStopped += (_, e) => { if (e.Exception is not null) Error = e.Exception.Message; };
            source.StartRecording();
        }
        catch { Stop(); throw; }
    }
    private void Stop()
    {
        var old = capture; capture = null; activeId = null;
        try { old?.StopRecording(); } finally { old?.Dispose(); device?.Dispose(); device = null; }
        Volatile.Write(ref bands, new float[64]);
    }
    public void Dispose() => Stop();
}
