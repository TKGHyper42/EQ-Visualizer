struct VertexInput
{
    float2 position : POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR0;
    float textured : TEXINDEX;
};

struct PixelInput
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR0;
    nointerpolation float textured : TEXINDEX;
};

Texture2D coverTexture : register(t0);
Texture2D captionTexture : register(t1);
Texture2D backgroundTexture : register(t2);
SamplerState linearSampler : register(s0);

PixelInput VSMain(VertexInput input)
{
    PixelInput output;
    output.position = float4(input.position, 0.0f, 1.0f);
    output.uv = input.uv;
    output.color = input.color;
    output.textured = input.textured;
    return output;
}

float4 PSMain(PixelInput input) : SV_TARGET
{
    if (input.textured == 6.0f)
    {
        // Recover each local axis size in pixels, including for rotated bars.
        float2 dx = ddx(input.uv);
        float2 dy = ddy(input.uv);
        float2 size = rsqrt(max(dx * dx + dy * dy, 1e-8f));
        float2 halfSize = size * 0.5f;
        float radius = min(halfSize.x, halfSize.y);
        float2 q = abs((input.uv - 0.5f) * size) - (halfSize - radius);
        float distanceToEdge = length(max(q, 0.0f)) + min(max(q.x, q.y), 0.0f) - radius;
        return float4(input.color.rgb, input.color.a * saturate(0.5f - distanceToEdge));
    }
    if (input.textured > 3.5f && dot(input.uv - 0.5f, input.uv - 0.5f) > 0.25f) discard;
    float4 source = input.textured > 4.5f ? float4(1, 1, 1, 1)
        : input.textured > 3.5f ? coverTexture.Sample(linearSampler, input.uv)
        : input.textured > 2.5f
        ? backgroundTexture.Sample(linearSampler,input.uv)
        : input.textured > 1.5f
        ? captionTexture.Sample(linearSampler, input.uv)
        : input.textured > 0.5f ? coverTexture.Sample(linearSampler, input.uv)
        : float4(1.0f, 1.0f, 1.0f, 1.0f);
    return source * input.color;
}
