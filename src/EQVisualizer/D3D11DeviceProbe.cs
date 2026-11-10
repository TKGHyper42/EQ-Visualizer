using SharpGen.Runtime;
using Vortice;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace EQVisualizer;

internal sealed class D3D11DeviceProbe : IDisposable
{
    private static readonly FeatureLevel[] FeatureLevels =
    [
        FeatureLevel.Level_11_1, FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1, FeatureLevel.Level_10_0
    ];

    public IDXGIFactory2 Factory { get; }
    public ID3D11Device Device { get; }
    public ID3D11DeviceContext Context { get; }
    public FeatureLevel FeatureLevel { get; }
    public bool UsesWarp { get; }

    private D3D11DeviceProbe(IDXGIFactory2 factory, ID3D11Device device,
        ID3D11DeviceContext context, FeatureLevel featureLevel, bool usesWarp)
    {
        Factory = factory; Device = device; Context = context;
        FeatureLevel = featureLevel; UsesWarp = usesWarp;
    }

    public static D3D11DeviceProbe Create()
    {
        var factory = CreateDXGIFactory1<IDXGIFactory2>();
        var flags = DeviceCreationFlags.BgraSupport;
        Result result = D3D11CreateDevice(IntPtr.Zero, DriverType.Hardware, flags, FeatureLevels,
            out var device, out var featureLevel, out var context);
        bool usesWarp = false;
        if (result.Failure)
        {
            device?.Dispose(); context?.Dispose();
            result = D3D11CreateDevice(IntPtr.Zero, DriverType.Warp, flags, FeatureLevels,
                out device, out featureLevel, out context);
            usesWarp = true;
        }
        if (result.Failure || device is null || context is null)
        {
            factory.Dispose();
            result.CheckError();
            throw new InvalidOperationException("No se pudo crear un dispositivo Direct3D 11.");
        }
        return new D3D11DeviceProbe(factory, device, context, featureLevel, usesWarp);
    }

    public void Dispose()
    {
        Context.ClearState(); Context.Flush();
        Context.Dispose(); Device.Dispose(); Factory.Dispose();
    }
}
