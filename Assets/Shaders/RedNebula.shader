// A black void with drifting red nebula clouds and a few faint stars. No textures, it is all made from noise.
// Set it in Window > Rendering > Lighting > Environment > Skybox Material.
// The noise itself lives in RedNebulaCommon.cginc, shared with the Void Glass shader.
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
            #include "RedNebulaCommon.cginc"

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

            fixed4 frag(v2f i) : SV_Target
            {
                float3 color = RedNebulaColor(
                    i.dir,
                    _VoidColor.rgb, _DeepColor.rgb, _BrightColor.rgb,
                    _Scale, _Coverage, _Softness, _Brightness, _Drift,
                    _StarAmount, _StarBrightness);

                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
