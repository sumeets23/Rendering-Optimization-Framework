Shader "Debug/ImpostorDepthCapture"
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
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float depth : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                // Unity view space Z is negative. Map from near clip to far clip.
                o.depth = (-viewPos.z - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Output depth [0, 1] where 0 = near, 1 = far.
                // The camera clears to White (1, 1, 1), so background is effectively 'far'.
                float d = saturate(i.depth);
                return float4(d, d, d, 1.0);
            }
            ENDCG
        }
    }
}
