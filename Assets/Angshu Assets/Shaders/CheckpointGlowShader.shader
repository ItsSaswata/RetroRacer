Shader "Custom/CheckpointGlowShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0.5,1,1)
        
        [Header(Glow)]
        [HDR]_GlowColor ("Glow Color", Color) = (0,0.5,1,1)
        _GlowIntensity ("Glow Intensity", Range(0,10)) = 2.0
        _GlowSize ("Glow Size", Range(0,0.1)) = 0.01
        _PulseSpeed ("Pulse Speed", Range(0,10)) = 1.0
        _PulseAmount ("Pulse Amount", Range(0,1)) = 0.2
        
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.005
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float4 _GlowColor;
            float _GlowIntensity;
            float _GlowSize;
            float _PulseSpeed;
            float _PulseAmount;
            float4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
            float3 viewDirWS : TEXCOORD3;
            float fogCoord : TEXCOORD4;
        };
        ENDHLSL

        // Main Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Sample base texture
                float4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                // Calculate view direction for fresnel effect
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                // Fresnel effect (rim lighting)
                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, 2.0); // Adjust power for sharper or softer edge
                
                // Pulsing effect
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmount + (1.0 - _PulseAmount);
                
                // Combine glow with base color
                float3 glowColor = _GlowColor.rgb * _GlowIntensity * fresnel * pulse;
                
                // Edge glow based on texture alpha and fresnel
                float edgeGlow = smoothstep(0.5 - _GlowSize, 0.5 + _GlowSize, baseColor.a) * fresnel;
                
                // Final color with glow
                float3 finalColor = baseColor.rgb + glowColor * edgeGlow;
                float finalAlpha = baseColor.a;
                
                // Increase alpha at the edges for stronger glow effect
                finalAlpha = max(finalAlpha, edgeGlow * 0.5);
                
                // Apply fog to the final color
                finalColor = MixFog(finalColor, input.fogCoord);
                
                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
        
        // Outline Pass
        Pass
        {
            Name "Outline"
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float fogCoord : TEXCOORD0;
            };

            OutlineVaryings vert(Attributes input)
            {
                OutlineVaryings output;

                float3 normalOS = normalize(input.normalOS);
                float3 posOS = input.positionOS.xyz + normalOS * _OutlineWidth;
                
                float4 positionCS = TransformObjectToHClip(posOS);
                output.positionCS = positionCS;
                output.fogCoord = ComputeFogFactor(positionCS.z);
                
                return output;
            }

            float4 frag(OutlineVaryings input) : SV_Target
            {
                // Pulse the outline color too
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmount + (1.0 - _PulseAmount);
                float4 outlineColor = _OutlineColor * pulse * _GlowIntensity;
                
                // Apply fog to the outline color
                float3 foggedColor = MixFog(outlineColor.rgb, input.fogCoord);
                
                return float4(foggedColor, outlineColor.a);
            }
            ENDHLSL
        }
    }
}