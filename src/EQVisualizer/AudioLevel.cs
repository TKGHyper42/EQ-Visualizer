using System.Diagnostics;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace EQVisualizer;

// Only spectral values leave this class. Captured audio is never replayed or stored.
internal sealed class AudioLevel : IDisposable
{
    private WasapiRecorder? capture;
    private MMDevice? device;
    private readonly AudioAnalyzer analyzer = new();
    private float[] bands = new float[64];
    private long updated, lastFailure;
    private int activeProcessId, generation;
    private string? activeDeviceId;
    private bool starting, captureFailed;
    public string? Error { get; private set; }
    public float[] Bands => Stopwatch.GetElapsedTime(Volatile.Read(ref updated)).TotalSeconds < .3 ? Volatile.Read(ref bands) : new float[64];

    public void Update(bool needed, string? deviceId, int processId = 0)
    {
        if (!needed) { Stop(); return; }
        if (processId > 0)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
            { Stop(); Error = "La captura aislada requiere Windows build 20348 o posterior."; return; }
            if ((capture is not null && !captureFailed || starting) && activeProcessId == processId) return;
            if (activeProcessId == processId && Stopwatch.GetElapsedTime(lastFailure).TotalSeconds < 5) return;
            Stop(); activeProcessId = processId; starting = true;
            _ = StartProcessAsync(processId, generation);
            return;
        }
        try
        {
            using var endpoints = new MMDeviceEnumerator();
            using var selected = deviceId is null ? endpoints.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : endpoints.GetDevice(deviceId);
            if (capture is null || captureFailed || activeProcessId != 0 || activeDeviceId != selected.ID) StartSystem(selected.ID);
        }
        catch (Exception e) { Stop(); Error = e.Message; }
    }

    [SupportedOSPlatform("windows10.0.20348")]
    private async Task StartProcessAsync(int processId, int version)
    {
        WasapiRecorder? source = null;
        try
        {
            source = await new WasapiRecorderBuilder()
                .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
                .BuildAsync();
            if (generation != version) { source.Dispose(); return; }
            Attach(source);
            capture = source;
            source.StartRecording();
            starting = false;
        }
        catch (Exception e)
        {
            source?.Dispose();
            if (generation != version) return;
            capture = null; starting = false; captureFailed = true;
            lastFailure = Stopwatch.GetTimestamp(); Error = e.Message;
        }
    }

    private void StartSystem(string deviceId)
    {
        Stop();
        try
        {
            using var endpoints = new MMDeviceEnumerator();
            device = endpoints.GetDevice(deviceId);
            var source = new WasapiRecorderBuilder().WithDevice(device).WithLoopbackCapture().Build();
            Attach(source);
            capture = source;
            source.StartRecording();
            activeDeviceId = deviceId;
        }
        catch { Stop(); throw; }
    }

    private void Attach(WasapiRecorder source)
    {
        analyzer.Reset(); Error = null; captureFailed = false;
        source.DataAvailable += (buffer, _, _, _) =>
        {
            if (!ReferenceEquals(capture, source)) return;
            var f = source.WaveFormat;
            bool floating = f.Encoding == WaveFormatEncoding.IeeeFloat ||
                f is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
            analyzer.Push(buffer, f.Channels, f.BitsPerSample, floating, f.SampleRate);
            Volatile.Write(ref bands, analyzer.Spectrum);
            Volatile.Write(ref updated, Stopwatch.GetTimestamp());
        };
        source.RecordingStopped += (_, e) =>
        {
            if (ReferenceEquals(capture, source))
            { captureFailed = true; if (e.Exception is not null) Error = e.Exception.Message; }
        };
    }

    private void Stop()
    {
        generation++;
        var old = capture; capture = null; starting = false; activeProcessId = 0; activeDeviceId = null;
        try { old?.StopRecording(); }
        catch { }
        finally { old?.Dispose(); device?.Dispose(); device = null; }
        Volatile.Write(ref bands, new float[64]);
    }
    public void Dispose() => Stop();
}
