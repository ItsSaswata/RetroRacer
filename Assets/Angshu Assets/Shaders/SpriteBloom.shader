Shader "Custom/SpriteBloomShader"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            // Define the vertex and fragment shader entry points.
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            // Vertex input structure.
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            // Data passed from vertex to fragment.
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _EmissionColor;
            float _EmissionIntensity;
            
            // Vertex shader: transforms vertices and UVs.
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            // Fragment shader: samples the sprite texture and adds an emission term.
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                // Add an emission contribution. Values greater than 1.0 help trigger bloom.
                col.rgb += _EmissionColor.rgb * _EmissionIntensity;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
