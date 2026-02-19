Shader "Nature/Tree Creator Bark (URP)" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.3
        _BumpSpecMap ("Normalmap (GA) Spec (R)", 2D) = "bump" {}
        _ShadowStrength("Shadow Strength", Range(0,1)) = 0.8
        _SquashAmount ("Squash", Float) = 1
        
        //unused, required
        _TranslucencyColor ("Translucency Color", Color) = (0.73,0.85,0.41,1)
        _TranslucencyViewDependency ("View dependency", Range(0,1)) = 0.7
    }

    SubShader {
        Tags {
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        LOD 200
        Cull Off
        ZWrite On
        Blend Off

        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #define _ALPHATEST_ON 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpSpecMap);
            SAMPLER(sampler_BumpSpecMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Cutoff;
                float _ShadowStrength;
                float _SquashAmount;
                float4 _TranslucencyColor;
                float _TranslucencyViewDependency;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN) {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 pos = IN.positionOS.xyz * _SquashAmount;
                VertexPositionInputs vpos = GetVertexPositionInputs(pos);

                Varyings OUT;
                OUT.positionCS = vpos.positionCS;
                OUT.positionWS = vpos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.viewDirWS = GetWorldSpaceViewDir(vpos.positionWS);
                OUT.fogFactor = ComputeFogFactor(vpos.positionCS.z);

                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                clip(albedo.a - _Cutoff);

                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                // Get shadow coordinates
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                
                // Main light with shadows
                Light mainLight = GetMainLight(shadowCoord);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                // Main light contribution
                half3 diffuse = albedo.rgb * mainLight.color * NdotL * shadow;

                // Simple specular
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half3 specular = pow(NdotH, 32.0) * mainLight.color * shadow * 0.3;

                // Additional lights
                half3 additionalLights = half3(0, 0, 0);
                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex) {
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                    half addNdotL = saturate(dot(normalWS, light.direction));
                    half addShadow = light.shadowAttenuation * light.distanceAttenuation;
                    additionalLights += albedo.rgb * light.color * addNdotL * addShadow;
                }
                #endif

                // Ambient/indirect lighting from environment
                half3 ambient = SampleSH(normalWS) * albedo.rgb;

                // Combine all lighting
                half3 color = diffuse + specular + additionalLights + ambient;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #define _ALPHATEST_ON 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Cutoff;
                float _ShadowStrength;
                float _SquashAmount;
                float4 _TranslucencyColor;
                float _TranslucencyViewDependency;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(Attributes input) {
                float3 positionOS = input.positionOS.xyz * _SquashAmount;
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input) {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionCS = GetShadowPositionHClip(input);

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);

                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);

                return 0;
            }
            ENDHLSL
        }

        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Cutoff;
                float _ShadowStrength;
                float _SquashAmount;
                float4 _TranslucencyColor;
                float _TranslucencyViewDependency;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes IN) {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 pos = IN.positionOS.xyz * _SquashAmount;

                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(pos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                return OUT;
            }

            half4 DepthOnlyFragment(Varyings IN) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(IN);

                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(alpha - _Cutoff);

                return 0;
            }
            ENDHLSL
        }

        Pass {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Cutoff;
                float _ShadowStrength;
                float _SquashAmount;
                float4 _TranslucencyColor;
                float _TranslucencyViewDependency;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthNormalsVertex(Attributes IN) {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 pos = IN.positionOS.xyz * _SquashAmount;

                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(pos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                return OUT;
            }

            half4 DepthNormalsFragment(Varyings IN) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(IN);

                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(alpha - _Cutoff);

                float3 normalWS = normalize(IN.normalWS);
                return half4(normalWS, 0);
            }
            ENDHLSL
        }
    }
}
