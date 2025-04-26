Shader "Custom/URPCondensationShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.1
        _IntegrationValue ("Integration Value", Range(0, 1)) = 0
        _ParticleSize ("Particle Size", Range(0.1, 10)) = 1
        _ParticleSpread ("Particle Spread", Range(0.1, 10)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 noiseUV : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _EdgeColor;
                float _EdgeWidth;
                float _IntegrationValue;
                float _ParticleSize;
                float _ParticleSpread;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.noiseUV = input.uv * _ParticleSpread;
                return output;
            }

            float random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.noiseUV).r;
                
                float2 pixelPos = input.uv * _ParticleSize;
                float particleNoise = random(floor(pixelPos));
                float finalNoise = (noise + particleNoise) * 0.5;
                
                // Gradually remove noise effect as integration approaches 1
                float noiseInfluence = 1 - smoothstep(0.8, 1.0, _IntegrationValue);
                finalNoise = lerp(1.0, finalNoise, noiseInfluence);
                
                float threshold = 1 - _IntegrationValue;
                float edge = threshold + _EdgeWidth;
                
                if (finalNoise < threshold)
                    discard;
                    
                if (finalNoise < edge)
                {
                    float edgeLerp = (finalNoise - threshold) / _EdgeWidth;
                    col.rgb = lerp(_EdgeColor.rgb, col.rgb, edgeLerp);
                    col.a *= edgeLerp;
                }
                
                // Adjust particle threshold based on integration
                float particleThreshold = lerp(0.95, 0.5, _IntegrationValue);
                if (finalNoise > particleThreshold)
                {
                    float particleIntensity = (finalNoise - particleThreshold) / (1 - particleThreshold);
                    // Reduce particle effect as integration approaches 1
                    float particleEffect = (1 - particleIntensity) * (1 - _IntegrationValue) * noiseInfluence;
                    col.a *= particleEffect;
                }
                
                // Ensure full opacity at maximum integration
                col.a = lerp(col.a, 1.0, smoothstep(0.6, 1.0, _IntegrationValue));
                
                return col;
            }
            ENDHLSL
        }
    }
}