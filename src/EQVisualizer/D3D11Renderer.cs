using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace EQVisualizer;

internal sealed class D3D11Renderer : IDisposable
{
    private readonly D3D11DeviceProbe graphics;
    private readonly IntPtr hwnd;
    private IDXGISwapChain1 swapChain;
    private ID3D11Texture2D? backBuffer;
    private ID3D11RenderTargetView? target;

    public D3D11Renderer(IntPtr hwnd, int width, int height)
    {
        this.hwnd = hwnd;
        graphics = D3D11DeviceProbe.Create();
        var description = new SwapChainDescription1
        {
            Width = (uint)Math.Max(1, width),
            Height = (uint)Math.Max(1, height),
            Format = Format.B8G8R8A8_UNorm,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = SampleDescription.Default,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore
        };
        swapChain = graphics.Factory.CreateSwapChainForHwnd(graphics.Device, hwnd, description);
        graphics.Factory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAltEnter);
        CreateTarget();
    }

    public void Resize(int width, int height)
    {
        if (width < 1 || height < 1) return;
        ReleaseTarget();
        swapChain.ResizeBuffers(0, (uint)width, (uint)height, Format.Unknown, SwapChainFlags.None);
        CreateTarget();
    }

    public void Clear(Color4 color)
    {
        if (target is null) return;
        graphics.Context.OMSetRenderTargets(target);
        graphics.Context.ClearRenderTargetView(target, color);
        swapChain.Present(1, PresentFlags.None);
    }

    private void CreateTarget()
    {
        backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        target = graphics.Device.CreateRenderTargetView(backBuffer);
    }

    private void ReleaseTarget()
    {
        target?.Dispose(); target = null;
        backBuffer?.Dispose(); backBuffer = null;
    }

    public void Dispose()
    {
        ReleaseTarget();
        graphics.Context.ClearState();
        graphics.Context.Flush();
        swapChain.Dispose();
        graphics.Dispose();
    }
}
