Shader "AAAOptimizer/ImpostorBillboard"
{
    Properties
    {
        [MainTexture] _MainTex ("Albedo Atlas (RGBA)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _NormalDepthMap ("Normal Depth Map", 2D) = "white" {}
        _Parallax ("Parallax Scale", Range(0, 0.2)) = 0.05
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.05
        _AtlasScale ("Atlas Scale", Vector) = (1,1,0,0)
        _AtlasOffset ("Atlas Offset", Vector) = (0,0,0,0)
        _NextAtlasOffset ("Next Atlas Offset", Vector) = (0,0,0,0)
        _FrameBlend ("Frame Blend", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="HDRenderPipeline"
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalDepthMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _Parallax;
                float4 _AtlasScale;
                float4 _AtlasOffset;
                float4 _NextAtlasOffset;
                float _FrameBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirOS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                
                float3 viewDirWS = GetCameraPositionWS() - TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirOS = TransformWorldToObjectDir(viewDirWS);
                
                return output;
            }

            float2 ParallaxOffset(float2 baseUV, float3 viewDirOS, float2 atlasOffset)
            {
                float2 viewDirUV = viewDirOS.xy / max(abs(viewDirOS.z), 0.001);
                float2 uvDelta = 0;
                float2 sampleUV = baseUV * _AtlasScale.xy + atlasOffset;
                float depth = SAMPLE_TEXTURE2D(_NormalDepthMap, sampler_MainTex, sampleUV).a;
                return baseUV + uvDelta * depth;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv1 = ParallaxOffset(input.uv, input.viewDirOS, _AtlasOffset.xy) * _AtlasScale.xy + _AtlasOffset.xy;
                float2 uv2 = ParallaxOffset(input.uv, input.viewDirOS, _NextAtlasOffset.xy) * _AtlasScale.xy + _NextAtlasOffset.xy;

                float4 col1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv1);
                float4 col2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv2);
                float4 col = lerp(col1, col2, _FrameBlend) * _BaseColor;
                clip(col.a - _Cutoff);
                return float4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NormalDepthMap;
            fixed4 _BaseColor;
            float _Cutoff;
            float _Parallax;
            float4 _AtlasScale;
            float4 _AtlasOffset;
            float4 _NextAtlasOffset;
            float _FrameBlend;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirOS : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                float3 viewDirWS = _WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDirOS = mul(unity_WorldToObject, float4(viewDirWS, 0.0)).xyz;
                
                return o;
            }

            float2 ParallaxOffset(float2 baseUV, float3 viewDirOS, float2 atlasOffset)
            {
                float2 viewDirUV = viewDirOS.xy / max(abs(viewDirOS.z), 0.001);
                float2 uvDelta = 0;
                float2 sampleUV = baseUV * _AtlasScale.xy + atlasOffset;
                float depth = tex2D(_NormalDepthMap, sampleUV).a;
                return baseUV + uvDelta * depth;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv1 = ParallaxOffset(i.uv, i.viewDirOS, _AtlasOffset.xy) * _AtlasScale.xy + _AtlasOffset.xy;
                float2 uv2 = ParallaxOffset(i.uv, i.viewDirOS, _NextAtlasOffset.xy) * _AtlasScale.xy + _NextAtlasOffset.xy;

                fixed4 col1 = tex2D(_MainTex, uv1);
                fixed4 col2 = tex2D(_MainTex, uv2);
                fixed4 col = lerp(col1, col2, _FrameBlend) * _BaseColor;
                clip(col.a - _Cutoff);
                return fixed4(col.rgb, 1.0);
            }
            ENDCG
        }
    }
}