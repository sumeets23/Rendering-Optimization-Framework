Shader "Debug/ImpostorNormalCapture"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalWorld : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalWorld = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Map world normals [-1, 1] to [0, 1] range for texture storage
                float3 normalPacked = normalize(i.normalWorld) * 0.5 + 0.5;
                return float4(normalPacked, 1.0);
            }
            ENDCG
        }
    }
}
