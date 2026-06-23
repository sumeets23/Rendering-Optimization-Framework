Shader "AAAOptimizer/Impostor/HDRPUnlitAtlasCutout"
{
    Properties
    {
        [MainTexture] _MainTex ("Albedo Atlas (RGBA)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _NormalDepthMap ("Normal Depth Map", 2D) = "white" {}
        _Parallax ("Parallax Scale", Range(0, 0.2)) = 0.0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _AtlasScale ("Atlas Scale", Vector) = (1,1,0,0)
        _AtlasOffset ("Atlas Offset", Vector) = (0,0,0,0)
        _NextAtlasOffset ("Next Atlas Offset", Vector) = (0,0,0,0)
        _FrameBlend ("Frame Blend", Range(0,1)) = 0
        _TransitionAlpha ("Transition Alpha", Range(0,1)) = 1
        _DepthParams ("Depth Params (Near, Far, Orbit, Ortho)", Vector) = (0.1, 100, 10, 5)
        _ImpostorSunDir ("Impostor Sun Direction", Vector) = (0,1,0,0)
        _ImpostorLightColor ("Impostor Light Color", Color) = (1,1,1,1)
        _ImpostorAmbientColor ("Impostor Ambient Color", Color) = (0.45,0.45,0.45,1)
        _ImpostorLightStrength ("Impostor Light Strength", Range(0,1)) = 0.75
        _ImpostorAmbientStrength ("Impostor Ambient Strength", Range(0,1)) = 0.55
    }

    // HDRP Pipeline SubShader
    SubShader
    {
        Tags
        {
            "RenderPipeline"="HDRenderPipeline"
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
        }

        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
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
                float _TransitionAlpha;
                float4 _DepthParams;
                float4 _ImpostorSunDir;
                float4 _ImpostorLightColor;
                float4 _ImpostorAmbientColor;
                float _ImpostorLightStrength;
                float _ImpostorAmbientStrength;
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
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                float3 viewDirWS = GetCameraPositionWS() - TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirOS = TransformWorldToObjectDir(viewDirWS);
                return output;
            }

            float Bayer4(float2 p)
            {
                int2 i = int2(fmod(abs(p), 4.0));
                const float bayer[16] = {
                    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
                    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[i.y * 4 + i.x];
            }


            float3 ApplySceneSunLift(float3 rgb, float2 atlasUV)
            {
                float4 normalDepth = SAMPLE_TEXTURE2D(_NormalDepthMap, sampler_MainTex, atlasUV);
                float3 normalWS = normalize(normalDepth.xyz * 2.0 - 1.0);
                float3 lightDirWS = normalize(_ImpostorSunDir.xyz);
                float ndotl = saturate(dot(normalWS, lightDirWS));

                float3 ambient = _ImpostorAmbientColor.rgb * _ImpostorAmbientStrength;
                float3 direct = _ImpostorLightColor.rgb * (0.35 + 0.65 * ndotl);
                float3 lightResponse = ambient + direct;

                float3 multiplier = max(float3(1.0, 1.0, 1.0), lightResponse);
                return rgb * lerp(float3(1.0, 1.0, 1.0), multiplier, _ImpostorLightStrength);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv1 = input.uv * _AtlasScale.xy + _AtlasOffset.xy;
                float2 uv2 = input.uv * _AtlasScale.xy + _NextAtlasOffset.xy;

                float dither = Bayer4(input.positionCS.xy);
                float2 activeUV = (_FrameBlend > dither) ? uv2 : uv1;

                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, activeUV) * _BaseColor;

                clip(col.a - _Cutoff);
                clip(_TransitionAlpha - dither - 0.001);

                col.rgb = ApplySceneSunLift(col.rgb, activeUV);
                #if defined(SHADERPASS) || defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARD)
                col.rgb *= GetCurrentExposureMultiplier();
                #endif
                return float4(col.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _Parallax;
                float4 _AtlasScale;
                float4 _AtlasOffset;
                float4 _NextAtlasOffset;
                float _FrameBlend;
                float _TransitionAlpha;
                float4 _DepthParams;
                float4 _ImpostorSunDir;
                float4 _ImpostorLightColor;
                float4 _ImpostorAmbientColor;
                float _ImpostorLightStrength;
                float _ImpostorAmbientStrength;
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Bayer4(float2 p)
            {
                int2 i = int2(fmod(abs(p), 4.0));
                float4x4 bayer = float4x4(
                    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
                    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
                );
                return bayer[i.y][i.x];
            }

            float4 frag(Varyings input) : SV_Target
            {
                float dither = Bayer4(input.positionCS.xy);
                float2 uv1 = input.uv * _AtlasScale.xy + _AtlasOffset.xy;
                float2 uv2 = input.uv * _AtlasScale.xy + _NextAtlasOffset.xy;
                float2 activeUV = (_FrameBlend > dither) ? uv2 : uv1;
                
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, activeUV) * _BaseColor;
                clip(col.a - _Cutoff);
                clip(_TransitionAlpha - dither - 0.001);
                return 0;
            }
            ENDHLSL
        }
    }

    // URP / Legacy / Built-in Pipeline SubShader
    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
        }

        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
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
            float _TransitionAlpha;
            float4 _DepthParams;
            float4 _ImpostorSunDir;
            fixed4 _ImpostorLightColor;
            fixed4 _ImpostorAmbientColor;
            float _ImpostorLightStrength;
            float _ImpostorAmbientStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirOS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float3 viewDirWS = _WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDirOS = mul((float3x3)unity_WorldToObject, viewDirWS);
                return o;
            }

            float Bayer4(float2 p)
            {
                int2 i = int2(fmod(abs(p), 4.0));
                const float bayer[16] = {
                    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
                    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[i.y * 4 + i.x];
            }


            float3 ApplySceneSunLift(float3 rgb, float2 atlasUV)
            {
                float4 normalDepth = tex2D(_NormalDepthMap, atlasUV);
                float3 normalWS = normalize(normalDepth.xyz * 2.0 - 1.0);
                float3 lightDirWS = normalize(_ImpostorSunDir.xyz);
                float ndotl = saturate(dot(normalWS, lightDirWS));

                float3 ambient = _ImpostorAmbientColor.rgb * _ImpostorAmbientStrength;
                float3 direct = _ImpostorLightColor.rgb * (0.35 + 0.65 * ndotl);
                float3 lightResponse = ambient + direct;

                float3 multiplier = max(float3(1.0, 1.0, 1.0), lightResponse);
                return rgb * lerp(float3(1.0, 1.0, 1.0), multiplier, _ImpostorLightStrength);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv1 = i.uv * _AtlasScale.xy + _AtlasOffset.xy;
                float2 uv2 = i.uv * _AtlasScale.xy + _NextAtlasOffset.xy;

                float dither = Bayer4(i.pos.xy);
                float2 activeUV = (_FrameBlend > dither) ? uv2 : uv1;

                fixed4 col = tex2D(_MainTex, activeUV) * _BaseColor;

                clip(col.a - _Cutoff);
                clip(_TransitionAlpha - dither - 0.001);
                col.rgb = ApplySceneSunLift(col.rgb, activeUV);
                return col;
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _BaseColor;
            float _Cutoff;
            float4 _AtlasScale;
            float4 _AtlasOffset;
            float4 _NextAtlasOffset;
            float _FrameBlend;
            float _TransitionAlpha;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Bayer4(float2 p)
            {
                int2 i = int2(fmod(abs(p), 4.0));
                const float bayer[16] = {
                    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
                    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[i.y * 4 + i.x];
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float dither = Bayer4(i.pos.xy);
                float2 uv1 = i.uv * _AtlasScale.xy + _AtlasOffset.xy;
                float2 uv2 = i.uv * _AtlasScale.xy + _NextAtlasOffset.xy;
                float2 activeUV = (_FrameBlend > dither) ? uv2 : uv1;
                
                fixed4 col = tex2D(_MainTex, activeUV) * _BaseColor;
                clip(col.a - _Cutoff);
                clip(_TransitionAlpha - dither - 0.001);
                return 0;
            }
            ENDCG
        }
    }

    FallBack Off
}