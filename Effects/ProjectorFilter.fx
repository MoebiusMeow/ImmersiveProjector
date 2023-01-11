float3 uRGB;
float3 uHSV;
float uAlpha;
bool uPrem;
sampler uImage0 : register(s0);

float4 hsv_frag(float2 coords : TEXCOORD0) : COLOR0 {
    float4 colorPrem = tex2D(uImage0, coords);
    float4 color = float4(colorPrem.r * uRGB.r, colorPrem.g * uRGB.g, colorPrem.b * uRGB.b, colorPrem.a); //colorPrem.a == 0 ? float4(0, 0, 0, 0) : float4(colorPrem.rgb / colorPrem.a, colorPrem.a);
    float v = max(max(color.r, color.g), color.b);
    float m = min(min(color.r, color.g), color.b);
    float s = v == 0 ? 0 : 1 - m / v;
    float h = 0;
    if (v == m)
        h = 0;
    else if (v == color.r && color.g >= color.b)
        h = 1.0 / 6.0 * ((color.g - color.b) / (v - m));
    else if (v == color.r && color.g < color.b)
        h = 1.0 / 6.0 * ((color.g - color.b) / (v - m)) + 1.0;
    else if (v == color.g)
        h = 1.0 / 6.0 * ((color.b - color.r) / (v - m)) + 1.0 / 3.0;
    else if (v == color.b)
        h = 1.0 / 6.0 * ((color.r - color.g) / (v - m)) + 2.0 / 3.0;

    h = frac(h + uHSV.x);
    s = clamp(uHSV.y <= 0 ? s * (1 + uHSV.y) : s + uHSV.y, 0, 1);
    v = clamp(uHSV.z <= 0 ? v * (1 + uHSV.z) : v + uHSV.z, 0, 1);
    if (s == 0)
        return uPrem ? float4(v, v, v, 1) * color.a * uAlpha : float4(v, v, v, color.a * uAlpha);

    float C = frac(h * 6.0);

    float X = v * (1 - s);
    float Y = v * (1 - s * C);
    float Z = v * (1 - s * (1 - C));
    float R = 0;
    float G = 0;
    float B = 0;
    int t = int(floor(h * 6));
    [flatten]
    if (t == 0) { R = v; G = Z; B = X; }
    [flatten]
	if (t == 1) { R = Y; G = v; B = X; }
    [flatten]
	if (t == 2) { R = X; G = v; B = Z; }
    [flatten]
	if (t == 3) { R = X; G = Y; B = v; }
    [flatten]
	if (t == 4) { R = Z; G = X; B = v; }
    [flatten]
	if (t == 5) { R = v; G = X; B = Y; }

    return uPrem ? float4(R, G, B, 1) * color.a * uAlpha : float4(R, G, B, color.a * uAlpha);
}

technique Technique233
{
    pass HSV 
    { 
        PixelShader = compile ps_3_0 hsv_frag(); 
    }
}
