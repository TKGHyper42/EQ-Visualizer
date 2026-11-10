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
    float textured : TEXINDEX;
};

Texture2D coverTexture : register(t0);
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
    float4 source = input.textured > 0.5f
        ? coverTexture.Sample(linearSampler, input.uv)
        : float4(1.0f, 1.0f, 1.0f, 1.0f);
    return source * input.color;
}
