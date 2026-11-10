using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace EQVisualizer;

[StructLayout(LayoutKind.Sequential)]
internal struct GpuVertex
{
    public Vector2 Position;
    public Vector2 Uv;
    public Vector4 Color;
    public float Textured;
}

internal sealed class D3D11Renderer : IDisposable
{
    private readonly D3D11DeviceProbe graphics;
    private readonly IntPtr hwnd;
    private IDXGISwapChain1 swapChain;
    private ID3D11Texture2D? backBuffer;
    private ID3D11RenderTargetView? target;
    private readonly ID3D11VertexShader vertexShader;
    private readonly ID3D11PixelShader pixelShader;
    private readonly ID3D11InputLayout inputLayout;
    private readonly ID3D11Buffer vertexBuffer;

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
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Visualizer.hlsl");
        var vertexBytecode = Compiler.CompileFromFile(shaderPath, "VSMain", "vs_4_0");
        var pixelBytecode = Compiler.CompileFromFile(shaderPath, "PSMain", "ps_4_0");
        vertexShader = graphics.Device.CreateVertexShader(vertexBytecode.Span);
        pixelShader = graphics.Device.CreatePixelShader(pixelBytecode.Span);
        inputLayout = graphics.Device.CreateInputLayout(
        [
            new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
            new InputElementDescription("TEXINDEX", 0, Format.R32_Float, 32, 0)
        ], vertexBytecode.Span);
        vertexBuffer = graphics.Device.CreateBuffer(4096 * Marshal.SizeOf<GpuVertex>(), BindFlags.VertexBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
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
        graphics.Context.VSSetShader(vertexShader);
        graphics.Context.PSSetShader(pixelShader);
        graphics.Context.IASetInputLayout(inputLayout);
        graphics.Context.ClearRenderTargetView(target, color);
        swapChain.Present(1, PresentFlags.None);
    }

    public unsafe void Draw(ReadOnlySpan<GpuVertex> vertices)
    {
        if (vertices.Length == 0 || vertices.Length > 4096 || target is null) return;
        var mapped = graphics.Context.Map(vertexBuffer, 0, MapMode.WriteDiscard);
        vertices.CopyTo(mapped.AsSpan<GpuVertex>(vertices.Length));
        graphics.Context.Unmap(vertexBuffer, 0);
        graphics.Context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        graphics.Context.IASetVertexBuffer(0, vertexBuffer, (uint)Marshal.SizeOf<GpuVertex>());
        graphics.Context.Draw((uint)vertices.Length, 0);
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
        vertexBuffer.Dispose();
        inputLayout.Dispose();
        pixelShader.Dispose();
        vertexShader.Dispose();
        swapChain.Dispose();
        graphics.Dispose();
    }
}
