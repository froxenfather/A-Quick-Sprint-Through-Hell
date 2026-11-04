// Glass that shows the sky instead of what is behind it.
// The glass is solid, so nothing behind it (like the next rooms) ever shows through. Each pixel draws the red nebula sky in the
// direction you are looking, and the sky is so far away that this looks exactly like clear glass onto an endless void.
// Your glass texture is laid over the top, so it still reads as glass.
//
// The sky settings come from the skybox material, copied in every frame by VoidGlassSkySync.cs, so changing the sky changes the glass too.
Shader "Custom/Void Glass"
{
    Properties
    {
        // The glass look: its color and alpha are laid over the sky (streaks, scratches, soft edge). Transparent pixels show just the sky.
        _GlassTex ("Glass Texture", 2D) = "black" {}

        // How much the clear parts are tinted over the sky. 0 = perfectly clear, 1 = solid color. A little goes a long way.
        _TintColor ("Tint Color", Color) = (0.55, 0.75, 0.85, 1)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0.12

        // Like alpha clipping: parts of the glass texture fainter than this vanish, so only the strongest streaks, scratches and the edge are left.
        // 0 = the whole texture shows (smoky). Around 0.15 = clear glass with just the scratches and edge.
        _ClearBelow ("Clear Below Alpha", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "RedNebulaCommon.cginc"

            sampler2D _GlassTex;
            float4 _GlassTex_ST;
            fixed4 _TintColor;
            float _TintAmount;
            float _ClearBelow;

            // Set for every shader at once by VoidGlassSkySync (these are not material properties).
            float4 _VoidSky_VoidColor;
            float4 _VoidSky_DeepColor;
            float4 _VoidSky_BrightColor;
            float _VoidSky_Scale;
            float _VoidSky_Coverage;
            float _VoidSky_Softness;
            float _VoidSky_Brightness;
            float _VoidSky_Drift;
            float _VoidSky_StarAmount;
            float _VoidSky_StarBrightness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _GlassTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Looking through this pixel, which way would the sky be?
                float3 viewDir = i.worldPos - _WorldSpaceCameraPos;

                float3 sky = RedNebulaColor(
                    viewDir,
                    _VoidSky_VoidColor.rgb, _VoidSky_DeepColor.rgb, _VoidSky_BrightColor.rgb,
                    _VoidSky_Scale, _VoidSky_Coverage, _VoidSky_Softness, _VoidSky_Brightness, _VoidSky_Drift,
                    _VoidSky_StarAmount, _VoidSky_StarBrightness);

                // Slightly less clear: the whole pane picks up a little of the tint color.
                sky = lerp(sky, _TintColor.rgb, _TintAmount);

                fixed4 glass = tex2D(_GlassTex, i.uv);

                // Drop the faint parts of the texture, and stretch what is left back up to full strength.
                float glassAlpha = saturate((glass.a - _ClearBelow) / max(1.0 - _ClearBelow, 0.001));

                return fixed4(lerp(sky, glass.rgb, glassAlpha), 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
