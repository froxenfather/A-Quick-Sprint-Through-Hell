// yellow and black caution stripes that run around corners without breaking.
// a texture can't do that on ProBuilder shapes: every face gets its own flat texture map, so the stripes on two neighbouring faces point different ways and meet in a V.
// this shader doesn't use the texture map at all.
// it works out the stripe from the point's position on the object (x + y + z), which is the same number on both sides of a corner, so the stripes carry straight on.
// the stripes are 45 degrees on every face and the same width everywhere, measured in meters.
// it uses the object's own space, so a rotated or mirrored room keeps the same stripes.
// it is lit by the sun (with shadows) and the ambient light.
Shader "Custom/Caution Stripes"
{
    Properties
    {
        _ColorA ("Stripe Color (yellow)", Color) = (0.98, 0.84, 0.01, 1)
        _ColorB ("Stripe Color (black)", Color) = (0.02, 0.02, 0.02, 1)

        // how far apart the stripes repeat, in meters, measured along an axis of the object.
        // a pair of yellow and black stripes is this wide on a flat wall.
        // smaller = thinner stripes
        _Spacing ("Stripe Spacing (m)", Float) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                float _Spacing;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // the same number on both faces of a corner, so the stripes continue.
                // frac() makes it repeat, and the triangle wave (0 to 1 and back) has no jump at the repeat, so the edge can be smoothed
                float stripe = (input.positionOS.x + input.positionOS.y + input.positionOS.z) / max(_Spacing, 0.01);
                float wave = abs(frac(stripe) - 0.5) * 2.0;

                // half yellow, half black, with the edge one pixel wide so it isn't jagged
                float edge = max(fwidth(wave), 0.0001);
                float yellow = smoothstep(0.5 - edge, 0.5 + edge, wave);
                half3 albedo = lerp(_ColorB.rgb, _ColorA.rgb, yellow);

                half3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half3 sun = LightingLambert(light.color, light.direction, normal) * light.shadowAttenuation;
                half3 ambient = SampleSH(normal);

                return half4(albedo * (sun + ambient), 1);
            }
            ENDHLSL
        }

        // so the doors cast shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // so the doors are in the depth texture
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
