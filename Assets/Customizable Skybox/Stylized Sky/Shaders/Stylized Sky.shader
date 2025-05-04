Shader "Stylized/Sky"
{
    Properties
    {
        [Header(Sun Disc)]
        _SunDiscColor ("Color", Color) = (1, 1, 1, 1)
        _SunDiscMultiplier ("Multiplier", float) = 25
        _SunDiscExponent ("Exponent", float) = 125000
        
        [Header(Sun Halo)]
        _SunHaloColor ("Color", Color) = (0.8970588, 0.7760561, 0.6661981, 1)
        _SunHaloExponent ("Exponent", float) = 125
        _SunHaloContribution ("Contribution", Range(0, 1)) = 0.75
        
        [Header(Horizon Line)]
        _HorizonLineColor ("Color", Color) = (0.9044118, 0.8872592, 0.7913603, 1)
        _HorizonLineExponent ("Exponent", float) = 4
        _HorizonLineContribution ("Contribution", Range(0, 1)) = 0.25
       
        [Header(Sky Gradient)]
        _SkyGradientTop ("Top", Color) = (0.172549, 0.5686274, 0.6941177, 1)
        _SkyGradientBottom ("Bottom", Color) = (0.764706, 0.8156863, 0.8509805)
        _SkyGradientExponent ("Exponent", float) = 2.5
        
        [Header(Stars)]
        [Toggle] _EnableStars ("Enable Stars", Float) = 1
        _StarColor ("Star Color", Color) = (1, 1, 1, 1)
        _StarDensity ("Star Density", Range(1, 100)) = 50
        _StarIntensity ("Star Intensity", Range(0, 2)) = 1
        _StarTwinkleSpeed ("Twinkle Speed", Range(0, 10)) = 3
        _StarSize ("Star Size", Range(0.01, 0.1)) = 0.05
        
        [Header(Planets)]
        [Toggle] _EnablePlanets ("Enable Planets", Float) = 1
        
        [Header(Earth)]
        _EarthTexture ("Earth Texture", 2D) = "white" {}
        _EarthPosition ("Earth Position", Vector) = (0.5, 0.3, 0.7, 0)
        _EarthSize ("Earth Size", Range(0.01, 0.2)) = 0.05
        _EarthRotationSpeed ("Earth Rotation Speed", Range(0, 1)) = 0.1
        
        [Header(Jupiter)]
        _JupiterTexture ("Jupiter Texture", 2D) = "white" {}
        _JupiterPosition ("Jupiter Position", Vector) = (-0.6, 0.4, 0.3, 0)
        _JupiterSize ("Jupiter Size", Range(0.01, 0.3)) = 0.08
        _JupiterRotationSpeed ("Jupiter Rotation Speed", Range(0, 1)) = 0.05
        
        [Header(Saturn)]
        _SaturnTexture ("Saturn Texture", 2D) = "white" {}
        _SaturnRingTexture ("Saturn Ring Texture", 2D) = "white" {}
        _SaturnPosition ("Saturn Position", Vector) = (0.3, -0.2, -0.5, 0)
        _SaturnSize ("Saturn Size", Range(0.01, 0.3)) = 0.07
        _SaturnRingSize ("Saturn Ring Size", Range(1.0, 3.0)) = 1.8
        _SaturnRotationSpeed ("Saturn Rotation Speed", Range(0, 1)) = 0.07
        
        [Header(Mars)]
        _MarsTexture ("Mars Texture", 2D) = "white" {}
        _MarsPosition ("Mars Position", Vector) = (-0.4, -0.3, 0.2, 0)
        _MarsSize ("Mars Size", Range(0.01, 0.2)) = 0.04
        _MarsRotationSpeed ("Mars Rotation Speed", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
        }
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
           
            float3 _SunDiscColor;
            float _SunDiscExponent;
            float _SunDiscMultiplier;
            float3 _SunHaloColor;
            float _SunHaloExponent;
            float _SunHaloContribution;
            float3 _HorizonLineColor;
            float _HorizonLineExponent;
            float _HorizonLineContribution;
            float3 _SkyGradientTop;
            float3 _SkyGradientBottom;
            float _SkyGradientExponent;
            
            // Star properties
            float _EnableStars;
            float3 _StarColor;
            float _StarDensity;
            float _StarIntensity;
            float _StarTwinkleSpeed;
            float _StarSize;
            
            // Planet properties
            float _EnablePlanets;
            
            // Earth
            sampler2D _EarthTexture;
            float4 _EarthPosition;
            float _EarthSize;
            float _EarthRotationSpeed;
            
            // Jupiter
            sampler2D _JupiterTexture;
            float4 _JupiterPosition;
            float _JupiterSize;
            float _JupiterRotationSpeed;
            
            // Saturn
            sampler2D _SaturnTexture;
            sampler2D _SaturnRingTexture;
            float4 _SaturnPosition;
            float _SaturnSize;
            float _SaturnRingSize;
            float _SaturnRotationSpeed;
            
            // Mars
            sampler2D _MarsTexture;
            float4 _MarsPosition;
            float _MarsSize;
            float _MarsRotationSpeed;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            // Hash function for pseudo-random number generation
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            
            // Function to generate stars
            float3 GenerateStars(float3 dir)
            {
                // Only generate stars in upper hemisphere
                if (dir.y < 0.0) return float3(0, 0, 0);
                
                // Convert direction to spherical coordinates
                float2 uv = float2(atan2(dir.z, dir.x), asin(dir.y));
                uv *= _StarDensity;
                
                // Generate grid cells for stars
                float2 gv = frac(uv) - 0.5;
                float2 id = floor(uv);
                
                float3 starColor = float3(0, 0, 0);
                
                // Sample nearby grid cells
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 cellId = id + offset;
                        
                        // Random position within the cell
                        float cellHash = hash(cellId);
                        float starBrightness = hash(cellId + 0.1);
                        
                        // Random offset within cell
                        float2 cellGv = gv - offset - float2(hash(cellId + 0.3) - 0.5, hash(cellId + 0.4) - 0.5);
                        
                        // Twinkle effect
                        float twinkle = sin(_Time.y * _StarTwinkleSpeed * cellHash) * 0.5 + 0.5;
                        
                        // Star shape (simple distance function)
                        float star = smoothstep(_StarSize, 0.0, length(cellGv));
                        star *= starBrightness * twinkle;
                        
                        starColor += star * _StarColor * _StarIntensity;
                    }
                }
                
                return starColor;
            }
            
            // Helper function to map direction to UV coordinates for planet textures
            float2 DirectionToSphericalUV(float3 dir, float3 planetPos, float rotationSpeed, float planetSize)
            {
                // Calculate direction to planet
                float3 planetDir = normalize(float3(planetPos.x, planetPos.y, planetPos.z));
                
                // Create rotation matrix around Y axis for planet rotation
                float angle = _Time.y * rotationSpeed;
                float cosA = cos(angle);
                float sinA = sin(angle);
                float3x3 rotY = float3x3(
                    cosA, 0, sinA,
                    0, 1, 0,
                    -sinA, 0, cosA
                );
                
                // Transform direction to planet's local space
                // Invert the direction to make it appear as viewed from outside
                float3 localDir = mul(rotY, -dir);
                
                // Calculate the angle between view direction and planet direction
                float cosAngle = dot(normalize(dir), normalize(planetPos));
                float distToSurface = 1.0 - cosAngle;
                
                // Scale factor based on planet size to ensure texture scales with planet size
                float scaleFactor = planetSize / 0.05; // 0.05 is the default size reference
                
                // Convert to spherical coordinates (UV mapping)
                // Adjust mapping to correct the orientation for external view
                float2 uv = float2(
                    0.5 - atan2(localDir.z, localDir.x) / (2.0 * 3.14159),
                    0.5 - asin(localDir.y) / 3.14159
                );
                
                // Apply scaling to maintain texture detail proportional to planet size
                // Center-based scaling to keep texture centered
                uv = (uv - 0.5) / scaleFactor + 0.5;
                
                return uv;
            }
            
            // Function to render Earth
            float4 RenderEarth(float3 dir)
            {
                float3 planetPos = _EarthPosition.xyz;
                float planetSize = _EarthSize;
                
                // Calculate angle between view direction and planet direction
                float cosAngle = dot(normalize(dir), normalize(planetPos));
                
                // Calculate distance to planet surface (simplified as sphere)
                float distToSurface = 1.0 - cosAngle;
                
                // If we're looking at the planet
                if (distToSurface < planetSize)
                {
                    // Calculate UV coordinates for texture mapping
                    float2 uv = DirectionToSphericalUV(dir, planetPos, _EarthRotationSpeed, planetSize);
                    
                    // Sample texture
                    float4 texColor = tex2D(_EarthTexture, uv);
                    
                    // Create proper convex lighting effect
                    // Calculate normalized distance from center (0 at center, 1 at edge)
                    float normalizedDist = distToSurface / planetSize;
                    // Create a lighting gradient that's brightest at center and darker at edges
                    float lightingFactor = 1.0 - pow(normalizedDist, 2.0);
                    // Apply lighting to create convex appearance
                    texColor.rgb *= lightingFactor;
                    
                    // Set alpha based on distance from edge with a sharper falloff
                    float edge = smoothstep(planetSize, planetSize * 0.95, distToSurface);
                    texColor.a = edge;
                    
                    return texColor;
                }
                
                return float4(0, 0, 0, 0);
            }
            
            // Function to render Jupiter
            float4 RenderJupiter(float3 dir)
            {
                float3 planetPos = _JupiterPosition.xyz;
                float planetSize = _JupiterSize;
                
                float cosAngle = dot(normalize(dir), normalize(planetPos));
                float distToSurface = 1.0 - cosAngle;
                
                if (distToSurface < planetSize)
                {
                    float2 uv = DirectionToSphericalUV(dir, planetPos, _JupiterRotationSpeed, planetSize);
                    float4 texColor = tex2D(_JupiterTexture, uv);
                    
                    // Create proper convex lighting effect
                    float normalizedDist = distToSurface / planetSize;
                    float lightingFactor = 1.0 - pow(normalizedDist, 2.0);
                    texColor.rgb *= lightingFactor;
                    
                    // Set alpha based on distance from edge with a sharper falloff
                    float edge = smoothstep(planetSize, planetSize * 0.95, distToSurface);
                    texColor.a = edge;
                    
                    return texColor;
                }
                
                return float4(0, 0, 0, 0);
            }
            
            // Function to render Saturn with rings
            float4 RenderSaturn(float3 dir)
            {
                float3 planetPos = _SaturnPosition.xyz;
                float planetSize = _SaturnSize;
                float ringSize = _SaturnRingSize * planetSize;
                
                float cosAngle = dot(normalize(dir), normalize(planetPos));
                float distToSurface = 1.0 - cosAngle;
                
                // Check if we're looking at the planet or its rings
                if (distToSurface < ringSize)
                {
                    float4 result = float4(0, 0, 0, 0);
                    
                    // Render planet body
                    if (distToSurface < planetSize)
                    {
                        float2 uv = DirectionToSphericalUV(dir, planetPos, _SaturnRotationSpeed, planetSize);
                        float4 texColor = tex2D(_SaturnTexture, uv);
                        
                        // Create proper convex lighting effect
                        float normalizedDist = distToSurface / planetSize;
                        float lightingFactor = 1.0 - pow(normalizedDist, 2.0);
                        texColor.rgb *= lightingFactor;
                        
                        // Set alpha based on distance from edge with a sharper falloff
                        float edge = smoothstep(planetSize, planetSize * 0.95, distToSurface);
                        texColor.a = edge;
                        
                        result = texColor;
                    }
                    // Render rings
                    else if (distToSurface < ringSize && distToSurface > planetSize * 0.9)
                    {
                        // Calculate ring UV (distance from center and angle)
                        float2 uv = DirectionToSphericalUV(dir, planetPos, _SaturnRotationSpeed * 0.4, planetSize);
                        
                        // Adjust UV for ring texture
                        float ringDist = (distToSurface - planetSize) / (ringSize - planetSize);
                        uv.y = ringDist;
                        
                        float4 ringColor = tex2D(_SaturnRingTexture, uv);
                        
                        // Make rings more transparent at edges
                        float ringEdge = 1.0 - abs((ringDist - 0.5) * 2.0);
                        ringColor.a *= ringEdge * 0.7;
                        
                        // Blend with planet if needed
                        result = lerp(result, ringColor, ringColor.a * (1.0 - result.a));
                        result.a = max(result.a, ringColor.a * 0.7);
                    }
                    
                    return result;
                }
                
                return float4(0, 0, 0, 0);
            }
            
            // Function to render Mars
            float4 RenderMars(float3 dir)
            {
                float3 planetPos = _MarsPosition.xyz;
                float planetSize = _MarsSize;
                
                float cosAngle = dot(normalize(dir), normalize(planetPos));
                float distToSurface = 1.0 - cosAngle;
                
                if (distToSurface < planetSize)
                {
                    float2 uv = DirectionToSphericalUV(dir, planetPos, _MarsRotationSpeed, planetSize);
                    float4 texColor = tex2D(_MarsTexture, uv);
                    
                    // Create proper convex lighting effect
                    float normalizedDist = distToSurface / planetSize;
                    float lightingFactor = 1.0 - pow(normalizedDist, 2.0);
                    texColor.rgb *= lightingFactor;
                    
                    // Set alpha based on distance from edge with a sharper falloff
                    float edge = smoothstep(planetSize, planetSize * 0.95, distToSurface);
                    texColor.a = edge;
                    
                    return texColor;
                }
                
                return float4(0, 0, 0, 0);
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Normalize the world position for direction
                float3 viewDir = normalize(i.worldPosition);
                
                // Masks
                float maskHorizon = dot(viewDir, float3(0, 1, 0));
                float maskSunDir = dot(viewDir, _WorldSpaceLightPos0.xyz);
                
                // Sun disc
                float maskSun = pow(saturate(maskSunDir), _SunDiscExponent);
                maskSun = saturate(maskSun * _SunDiscMultiplier);
                
                // Sun halo
                float3 sunHaloColor = _SunHaloColor * _SunHaloContribution;
                float bellCurve = pow(saturate(maskSunDir), _SunHaloExponent * saturate(abs(maskHorizon)));
                float horizonSoften = 1 - pow(1 - saturate(maskHorizon), 50);
                sunHaloColor *= saturate(bellCurve * horizonSoften);
                
                // Horizon line
                float3 horizonLineColor = _HorizonLineColor * saturate(pow(1 - abs(maskHorizon), _HorizonLineExponent));
                horizonLineColor = lerp(0, horizonLineColor, _HorizonLineContribution);
                
                // Sky gradient
                float3 skyGradientColor = lerp(_SkyGradientTop, _SkyGradientBottom, pow(1 - saturate(maskHorizon), _SkyGradientExponent));
                
                // Base sky color
                float3 finalColor = lerp(saturate(sunHaloColor + horizonLineColor + skyGradientColor), _SunDiscColor, maskSun);
                
                // Add stars if enabled
                if (_EnableStars > 0.5)
                {
                    float3 stars = GenerateStars(viewDir);
                    // Only add stars to the darker parts of the sky and avoid adding them to the sun
                    float starMask = (1.0 - maskSun) * (1.0 - saturate(dot(finalColor, float3(0.299, 0.587, 0.114)) * 2.0));
                    finalColor += stars * starMask;
                }
                
                // Add planets if enabled
                if (_EnablePlanets > 0.5)
                {
                    // Render each planet and blend with sky
                    float4 earthColor = RenderEarth(viewDir);
                    float4 jupiterColor = RenderJupiter(viewDir);
                    float4 saturnColor = RenderSaturn(viewDir);
                    float4 marsColor = RenderMars(viewDir);
                    
                    // Blend planets with sky using alpha
                    finalColor = lerp(finalColor, earthColor.rgb, earthColor.a);
                    finalColor = lerp(finalColor, jupiterColor.rgb, jupiterColor.a);
                    finalColor = lerp(finalColor, saturnColor.rgb, saturnColor.a);
                    finalColor = lerp(finalColor, marsColor.rgb, marsColor.a);
                }
                
                return float4(finalColor, 1);
            }
            ENDCG
        }
    }
}