// turns the whole picture bright red: a red version of what the camera sees, where light stays light and dark stays dark.
// it is a full-screen pass, so it needs a Full Screen Pass Renderer Feature on the renderer, with a material that uses this shader.
// _RageAmount is a global number that Conseraph sets: 0 leaves the picture alone and 1 is fully red.
// Conseraph multiplies it by his Rage Intensity slider, so a lower intensity keeps part of the real picture showing through the red.
// the numbers in Properties below are only the defaults for a NEW material, a material that already exists keeps the values saved on it
// this is also the place to grow the later look where objects get red outlines and everything else goes black
Shader "Custom/Rage Screen"
{
    Properties
    {
        _Tint ("Red", Color) = (1, 0.02, 0.02, 1)

        // how bright the red is.
        // higher pushes the lights to full red
        _Gain ("Brightness", Range(0.5, 4)) = 1.8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RageScreen"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Gain;
            CBUFFER_END

            // set by script for every material, so it is not in the material's own buffer
            float _RageAmount;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // how bright each pixel is, painted in red
                half luma = dot(color.rgb, half3(0.299, 0.587, 0.114));
                half3 red = _Tint.rgb * saturate(luma * _Gain);

                return half4(lerp(color.rgb, red, saturate(_RageAmount)), color.a);
            }
            ENDHLSL
        }
    }
}
