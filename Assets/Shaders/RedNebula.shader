// A black void with drifting red nebula clouds and a few faint stars. No textures, it is all made from noise.
// Set it in Window > Rendering > Lighting > Environment > Skybox Material.
Shader "Skybox/Red Nebula"
{
    Properties
    {
        [Header(Colors)]
        _VoidColor ("Void", Color) = (0, 0, 0, 1)
        _DeepColor ("Cloud Edge", Color) = (0.25, 0.0, 0.04, 1)
        _BrightColor ("Cloud Core", Color) = (1, 0.12, 0.05, 1)

        [Header(Clouds)]
        _Scale ("Cloud Scale", Range(0.3, 6)) = 1.6
        _Coverage ("Coverage", Range(0, 1)) = 0.45
        _Softness ("Softness", Range(0.05, 1)) = 0.4
        _Brightness ("Brightness", Range(0, 6)) = 1.6
        _Drift ("Drift Speed", Range(0, 0.2)) = 0.01

        [Header(Stars)]
        _StarAmount ("Star Amount", Range(0, 0.2)) = 0.03
        _StarBrightness ("Star Brightness", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _VoidColor;
            fixed4 _DeepColor;
            fixed4 _BrightColor;
            float _Scale;
            float _Coverage;
            float _Softness;
            float _Brightness;
            float _Drift;
            float _StarAmount;
            float _StarBrightness;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // Smooth random value for any point in 3D space.
            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                         lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                         lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            // Layers of noise at smaller and smaller sizes, which is what makes it look like a cloud.
            float Fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * Noise(p);
                    p = p * 2.03 + 11.7;
                    amplitude *= 0.5;
                }

                return value;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 drift = float3(_Time.y * _Drift, 0, _Time.y * _Drift * 0.6);

                // The clouds are warped by a second layer of noise so they swirl instead of blobbing.
                float3 p = dir * _Scale + drift;
                float3 warp = float3(Fbm(p + 3.1), Fbm(p + 7.7), Fbm(p + 1.3)) - 0.5;
                float cloud = Fbm(p + warp * 1.4);

                float density = smoothstep(1.0 - _Coverage - _Softness * 0.5, 1.0 - _Coverage + _Softness * 0.5, cloud);
                float core = smoothstep(0.5, 0.9, cloud);

                float3 nebula = lerp(_DeepColor.rgb, _BrightColor.rgb, core) * density * _Brightness;
                float3 color = _VoidColor.rgb + nebula;

                // Stars. A grid of cells on the sky, a few of them get a dot, and clouds dim the stars behind them.
                float3 cellPos = dir * 220.0;
                float3 cell = floor(cellPos);
                float3 inCell = frac(cellPos) - 0.5;
                float starRandom = Hash(cell);
                float isStar = step(1.0 - _StarAmount, starRandom);
                float starShape = smoothstep(0.25, 0.0, length(inCell));
                float twinkle = 0.7 + 0.3 * sin(_Time.y * 2.0 + starRandom * 80.0);
                color += isStar * starShape * twinkle * _StarBrightness * (1.0 - density);

                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
