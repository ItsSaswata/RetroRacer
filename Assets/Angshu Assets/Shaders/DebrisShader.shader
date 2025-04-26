Shader "Custom/DebrisCelShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.005
        
        [Header(Toon Shading)]
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.5
        _RampSmoothing ("Ramp Smoothing", Range(0,1)) = 0.1
        _ShadowIntensity ("Shadow Intensity", Range(0,1)) = 0.5
        
        [Header(Emission)]
        [HDR]_EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionIntensity ("Emission Intensity", Range(0,10)) = 1
        
        [Header(Specular)]
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularSmoothness ("Specular Smoothness", Range(0,1)) = 0.1
        _SpecularIntensity ("Specular Intensity", Range(0,1)) = 0.5

        [Header(Debris Displacement)]
        _NoiseScale ("Noise Scale", Range(0,10)) = 5
        _Displacement ("Displacement Strength", Range(0,0.5)) = 0.1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        // -----------------------------------------------------
        //  Include URP core libraries
        // -----------------------------------------------------
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        // -----------------------------------------------------
        //  Cel Shader Properties
        // -----------------------------------------------------
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _RampThreshold;
            float _RampSmoothing;
            float _ShadowIntensity;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float4 _SpecularColor;
            float _SpecularSmoothness;
            float _SpecularIntensity;

            // Procedural noise parameters
            float _NoiseScale;         // Scale for the noise function
            float _Displacement;       // How strong the displacement is
        CBUFFER_END

        // -----------------------------------------------------
        //  Procedural Noise Functions (no texture fetch)
        // -----------------------------------------------------
        float hash21(float2 p)
        {
            // A quick hash function
            float n = sin(dot(p, float2(127.1, 311.7))) * 43758.5453;
            return frac(n);
        }

        // Basic 2D value noise
        float noise2D(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);

            float a = hash21(i + float2(0,0));
            float b = hash21(i + float2(1,0));
            float c = hash21(i + float2(0,1));
            float d = hash21(i + float2(1,1));

            float2 u = f*f*(3.0 - 2.0*f);

            return lerp(
                lerp(a, b, u.x),
                lerp(c, d, u.x),
                u.y
            );
        }

        // -----------------------------------------------------
        //  Vertex/Fragment Structures
        // -----------------------------------------------------
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            float3 normalWS   : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
            float4 shadowCoord: TEXCOORD3;
            float  fogCoord   : TEXCOORD4;
            float3 viewDirWS  : TEXCOORD5;
        };
        ENDHLSL

        // -----------------------------------------------------
        //  Main Pass (ForwardLit)
        // -----------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 1) Generate procedural noise
                float noiseVal = noise2D(input.uv * _NoiseScale);
                // 2) Offset around 0
                float nOffset = noiseVal - 0.5;
                // 3) Multiply by displacement factor
                float disp = nOffset * _Displacement;
                // 4) Displace position along normal
                float3 displacedPos = input.positionOS.xyz + input.normalOS * disp;

                // URP built-in functions for transforming to clip space
                VertexPositionInputs vertexInput = GetVertexPositionInputs(displacedPos);
                VertexNormalInputs   normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = normalInput.normalWS;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.shadowCoord= GetShadowCoord(vertexInput);
                output.fogCoord   = ComputeFogFactor(vertexInput.positionCS.z);
                output.viewDirWS  = GetWorldSpaceViewDir(vertexInput.positionWS);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Sample base texture, apply user color
                float4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;

                // Standard toon lighting logic
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                
                float NdotL = dot(normalWS, mainLight.direction);
                float diffuse = NdotL * 0.5 + 0.5;

                // Toon ramp
                float ramp = saturate((diffuse - _RampThreshold) / _RampSmoothing);
                
                // Shadows & final light intensity
                float shadow = mainLight.shadowAttenuation;
                float lightIntensity = lerp(1 - _ShadowIntensity, 1.0, ramp * shadow);
                
                // Specular
                float3 halfVector = normalize(mainLight.direction + viewDirWS);
                float NdotH = dot(normalWS, halfVector);
                float specular = pow(saturate(NdotH), _SpecularSmoothness * 100);
                specular = step(0.5, specular) * _SpecularIntensity;
                
                float3 lighting = mainLight.color * lightIntensity;
                float3 specularColor = _SpecularColor.rgb * specular;
                
                // Combine final color
                float3 finalColor = baseColor.rgb * lighting + specularColor;
                // Add emission
                finalColor += _EmissionColor.rgb * _EmissionIntensity * baseColor.rgb;

                // Fog
                finalColor = MixFog(finalColor, input.fogCoord);
                
                return float4(finalColor, baseColor.a);
            }
            ENDHLSL
        }

        // -----------------------------------------------------
        //  Outline Pass
        // -----------------------------------------------------
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
                float  fogCoord   : TEXCOORD0;
            };

            OutlineVaryings vert(Attributes input)
            {
                OutlineVaryings output;

                // Procedural noise for displacement
                float noiseVal = noise2D(input.uv * _NoiseScale);
                float nOffset = noiseVal - 0.5;
                float disp = nOffset * _Displacement;
                float3 displacedPos = input.positionOS.xyz + input.normalOS * disp;

                // Expand for outline
                float3 normalOS = normalize(input.normalOS);
                float3 posOS = displacedPos + normalOS * _OutlineWidth;
                
                output.positionCS = TransformObjectToHClip(posOS);
                output.fogCoord   = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }

            float4 frag(OutlineVaryings input) : SV_Target
            {
                float4 outlineColor = _OutlineColor;
                outlineColor.rgb = MixFog(outlineColor.rgb, input.fogCoord);
                return outlineColor;
            }
            ENDHLSL
        }

        // -----------------------------------------------------
        //  Shadow Caster Pass
        // -----------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings vert(Attributes input)
            {
                ShadowVaryings output;

                float noiseVal = noise2D(input.uv * _NoiseScale);
                float nOffset = noiseVal - 0.5;
                float disp = nOffset * _Displacement;
                float3 displacedPos = input.positionOS.xyz + input.normalOS * disp;

                float3 positionWS = TransformObjectToWorld(displacedPos);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                // URP's main light position
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz)
                );
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            float4 frag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // -----------------------------------------------------
        //  Depth Pass
        // -----------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings vert(Attributes input)
            {
                DepthVaryings output;

                float noiseVal = noise2D(input.uv * _NoiseScale);
                float nOffset = noiseVal - 0.5;
                float disp = nOffset * _Displacement;
                float3 displacedPos = input.positionOS.xyz + input.normalOS * disp;

                output.positionCS = TransformObjectToHClip(displacedPos);
                return output;
            }

            float4 frag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
