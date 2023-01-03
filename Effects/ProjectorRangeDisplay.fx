float uTime;
float uAlpha;
float uResolution;
float uDirectionFactor;
float2 uDirection;
float4 uColor;

float4x4 uMVP;
texture2D uTex;
sampler2D uImage0 = sampler_state
{
    Texture = <uTex>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Wrap;
    AddressV = Clamp;
};

struct VertexIn
{
    float3 pos : POSITION0;
    float2 coords : TEXCOORD0;
};

struct FragmentIn
{
    float4 pos : POSITION0;
    float2 coords : TEXCOORD0;
    float zpos : TEXCOORD1;
};

FragmentIn vertex(VertexIn input)
{
    FragmentIn output;
    output.zpos = input.pos.z;
    output.pos = mul(float4(input.pos, 1.0), uMVP);
    output.pos.x /= output.pos.w;
    output.pos.y /= output.pos.w;
    output.pos.z = 0.0;
    output.pos.w = 1.0;
    output.coords = input.coords; 
    return output;
}

float4 simple_frag(FragmentIn input) : COLOR0
{
    float4 val = tex2D(uImage0, input.coords);
    return float4(val * uColor);
}

technique Technique233
{
    pass Simple 
    { 
        VertexShader = compile vs_2_0 vertex(); 
        PixelShader = compile ps_2_0 simple_frag(); 
    }
}
