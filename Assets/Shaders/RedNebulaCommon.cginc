// The red nebula sky, as a function. Shared by RedNebula.shader (the skybox) and VoidGlass.shader (glass that shows the same sky),
// so the two can never drift apart. Include UnityCG.cginc before this.
#ifndef RED_NEBULA_COMMON_INCLUDED
#define RED_NEBULA_COMMON_INCLUDED

float NebulaHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Smooth random value for any point in 3D space.
float NebulaNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);

    return lerp(
        lerp(lerp(NebulaHash(i + float3(0, 0, 0)), NebulaHash(i + float3(1, 0, 0)), f.x),
             lerp(NebulaHash(i + float3(0, 1, 0)), NebulaHash(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(NebulaHash(i + float3(0, 0, 1)), NebulaHash(i + float3(1, 0, 1)), f.x),
             lerp(NebulaHash(i + float3(0, 1, 1)), NebulaHash(i + float3(1, 1, 1)), f.x), f.y),
        f.z);
}

// Layers of noise at smaller and smaller sizes, which is what makes it look like a cloud.
float NebulaFbm(float3 p)
{
    float value = 0.0;
    float amplitude = 0.5;

    for (int i = 0; i < 5; i++)
    {
        value += amplitude * NebulaNoise(p);
        p = p * 2.03 + 11.7;
        amplitude *= 0.5;
    }

    return value;
}

// The sky color for a direction. The parameters are the same ones as the RedNebula material.
float3 RedNebulaColor(
    float3 dir,
    float3 voidColor, float3 deepColor, float3 brightColor,
    float scale, float coverage, float softness, float brightness, float drift,
    float starAmount, float starBrightness)
{
    dir = normalize(dir);
    float3 driftOffset = float3(_Time.y * drift, 0, _Time.y * drift * 0.6);

    // The clouds are warped by a second layer of noise so they swirl instead of blobbing.
    float3 p = dir * scale + driftOffset;
    float3 warp = float3(NebulaFbm(p + 3.1), NebulaFbm(p + 7.7), NebulaFbm(p + 1.3)) - 0.5;
    float cloud = NebulaFbm(p + warp * 1.4);

    float density = smoothstep(1.0 - coverage - softness * 0.5, 1.0 - coverage + softness * 0.5, cloud);
    float core = smoothstep(0.5, 0.9, cloud);

    float3 nebula = lerp(deepColor, brightColor, core) * density * brightness;
    float3 color = voidColor + nebula;

    // Stars. A grid of cells on the sky, a few of them get a dot, and clouds dim the stars behind them.
    float3 cellPos = dir * 220.0;
    float3 cell = floor(cellPos);
    float3 inCell = frac(cellPos) - 0.5;
    float starRandom = NebulaHash(cell);
    float isStar = step(1.0 - starAmount, starRandom);
    float starShape = smoothstep(0.25, 0.0, length(inCell));
    float twinkle = 0.7 + 0.3 * sin(_Time.y * 2.0 + starRandom * 80.0);
    color += isStar * starShape * twinkle * starBrightness * (1.0 - density);

    return color;
}

#endif
