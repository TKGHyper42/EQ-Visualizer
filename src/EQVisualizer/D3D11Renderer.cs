using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
namespace EQVisualizer;

internal sealed class D3D11Renderer : IDisposable
{
    private readonly D3D11DeviceProbe graphics;
    private readonly IDXGISwapChain1 swapChain;
    private ID3D11Texture2D? backBuffer;
    private ID3D11RenderTargetView? target;
    private ID3D11ShaderResourceView? artwork;
    private ID3D11ShaderResourceView? caption;
    private ID3D11ShaderResourceView? background;
    private readonly ID3D11VertexShader vertexShader;
    private readonly ID3D11PixelShader pixelShader;
    private readonly ID3D11InputLayout inputLayout;
    private readonly ID3D11Buffer vertexBuffer;
    private readonly ID3D11SamplerState sampler;
    private readonly ID3D11BlendState blend;
    private readonly ID3D11RasterizerState rasterizer;
    private int renderWidth, renderHeight;
    public bool UsesSoftware => graphics.UsesWarp;
    public D3D11Renderer(IntPtr hwnd, int width, int height)
    {
        graphics = D3D11DeviceProbe.Create();
        try
        {
            swapChain = graphics.Factory.CreateSwapChainForHwnd(graphics.Device, hwnd, new SwapChainDescription1
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
            });
            graphics.Factory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAltEnter);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EQVisualizer.Assets.Visualizer.hlsl")
                ?? throw new InvalidOperationException("Shader integrado no disponible.");
            using var reader = new StreamReader(stream);
            string shader = reader.ReadToEnd();
            var vs = Compiler.Compile(shader, "VSMain", "embedded", "vs_4_0");
            var ps = Compiler.Compile(shader, "PSMain", "embedded", "ps_4_0");
            vertexShader = graphics.Device.CreateVertexShader(vs.Span); pixelShader = graphics.Device.CreatePixelShader(ps.Span);
            inputLayout = graphics.Device.CreateInputLayout([
                new("POSITION",0,Format.R32G32_Float,0,0),new("TEXCOORD",0,Format.R32G32_Float,8,0),
                new("COLOR",0,Format.R32G32B32A32_Float,16,0),new("TEXINDEX",0,Format.R32_Float,32,0)
            ], vs.Span);
            vertexBuffer = graphics.Device.CreateBuffer((uint)(16384 * Marshal.SizeOf<GpuVertex>()), BindFlags.VertexBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
            sampler = graphics.Device.CreateSamplerState(SamplerDescription.LinearClamp);
            blend = graphics.Device.CreateBlendState(BlendDescription.NonPremultiplied);
            rasterizer = graphics.Device.CreateRasterizerState(RasterizerDescription.CullNone);
            renderWidth = Math.Max(1, width); renderHeight = Math.Max(1, height); CreateTarget();
        }
        catch { Dispose(); throw; }
    }
    public void Resize(int width, int height)
    {
        width = Math.Max(1, width); height = Math.Max(1, height);
        if (width == renderWidth && height == renderHeight) return;
        graphics.Context.ClearState(); ReleaseTarget();
        swapChain.ResizeBuffers(0, (uint)width, (uint)height, Format.Unknown, SwapChainFlags.None).CheckError();
        renderWidth = width; renderHeight = height; CreateTarget();
    }
    public void SetArtwork(Image? image)
    {
        graphics.Context.ClearState(); artwork?.Dispose(); artwork = null;
        if (image is null) return;
        int size = Math.Min(1024, Math.Max(image.Width, image.Height));
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap)) g.DrawImage(image, 0, 0, size, size);
        var data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var description = new Texture2DDescription
            {
                Width = (uint)size,
                Height = (uint)size,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = SampleDescription.Default,
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource
            };
            using var texture = graphics.Device.CreateTexture2D(description, new SubresourceData(data.Scan0, (uint)data.Stride));
            artwork = graphics.Device.CreateShaderResourceView(texture);
        }
        finally { bitmap.UnlockBits(data); }
    }
    public void Render(ReadOnlySpan<GpuVertex> vertices, string? capturePath = null)
    {
        if (target is null) return;
        var c = graphics.Context;
        c.OMSetRenderTargets(target); c.ClearRenderTargetView(target, new Color4(.025f, .025f, .035f, 1));
        c.RSSetViewport(0, 0, renderWidth, renderHeight); c.RSSetState(rasterizer); c.OMSetBlendState(blend);
        c.VSSetShader(vertexShader); c.PSSetShader(pixelShader); c.PSSetSampler(0, sampler);
        if (artwork is not null) c.PSSetShaderResource(0, artwork);
        if (caption is not null) c.PSSetShaderResource(1, caption);
        if (background is not null) c.PSSetShaderResource(2, background);
        else if (artwork is not null) c.PSSetShaderResource(2, artwork);
        c.IASetInputLayout(inputLayout); c.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        c.IASetVertexBuffer(0, vertexBuffer, (uint)Marshal.SizeOf<GpuVertex>());
        if (!vertices.IsEmpty)
        {
            var mapped = c.Map(vertexBuffer, 0, MapMode.WriteDiscard);
            try { vertices.CopyTo(mapped.AsSpan<GpuVertex>(vertices.Length)); } finally { c.Unmap(vertexBuffer, 0); }
            c.Draw((uint)vertices.Length, 0);
        }
        if (capturePath is not null) SaveFrame(capturePath);
        swapChain.Present(0, PresentFlags.None).CheckError();
    }
    private void SaveFrame(string path)
    {
        var description = backBuffer!.Description; description.Usage = ResourceUsage.Staging;
        description.BindFlags = BindFlags.None; description.CPUAccessFlags = CpuAccessFlags.Read;
        description.MiscFlags = ResourceOptionFlags.None;
        using var staging = graphics.Device.CreateTexture2D(description);
        graphics.Context.CopyResource(staging, backBuffer);
        var mapped = graphics.Context.Map(staging, 0, MapMode.Read);
        try
        {
            using var image = new Bitmap(renderWidth, renderHeight, PixelFormat.Format32bppArgb);
            var data = image.LockBits(new Rectangle(0, 0, renderWidth, renderHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[renderWidth * 4];
                for (int y = 0; y < renderHeight; y++)
                {
                    Marshal.Copy(mapped.DataPointer + y * (int)mapped.RowPitch, row, 0, row.Length);
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
                }
            }
            finally { image.UnlockBits(data); }
            int peak = 0;
            for (int y = 0; y < renderHeight; y += 8)
                for (int x = 0; x < renderWidth; x += 8)
                {
                    var pixel = image.GetPixel(x, y);
                    peak = Math.Max(peak, pixel.R + pixel.G + pixel.B);
                }
            if (peak < 40) throw new InvalidOperationException("GPU readback is unexpectedly black.");
            image.Save(path, ImageFormat.Png);
        }
        finally { graphics.Context.Unmap(staging, 0); }
    }
    public void SetCaption(Bitmap? image)
        => ReplaceTexture(ref caption, image);
    public void SetBackground(Bitmap? image)
        => ReplaceTexture(ref background, image);
    private void ReplaceTexture(ref ID3D11ShaderResourceView? view, Bitmap? image)
    {
        graphics.Context.ClearState(); view?.Dispose(); view = null;
        if (image is null) return;
        var data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var description = new Texture2DDescription
            {
                Width = (uint)image.Width,
                Height = (uint)image.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = SampleDescription.Default,
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource
            };
            using var texture = graphics.Device.CreateTexture2D(description, new SubresourceData(data.Scan0, (uint)data.Stride));
            view = graphics.Device.CreateShaderResourceView(texture);
        }
        finally { image.UnlockBits(data); }
    }
    private void CreateTarget()
    {
        backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0); target = graphics.Device.CreateRenderTargetView(backBuffer);
    }
    private void ReleaseTarget() { target?.Dispose(); target = null; backBuffer?.Dispose(); backBuffer = null; }
    public void Dispose()
    {
        graphics.Context.ClearState(); ReleaseTarget(); artwork?.Dispose(); caption?.Dispose(); background?.Dispose();
        rasterizer?.Dispose(); blend?.Dispose(); sampler?.Dispose(); vertexBuffer?.Dispose();
        inputLayout?.Dispose(); pixelShader?.Dispose(); vertexShader?.Dispose(); swapChain?.Dispose(); graphics.Dispose();
    }
}

