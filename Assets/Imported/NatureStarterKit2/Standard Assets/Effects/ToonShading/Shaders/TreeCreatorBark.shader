Shader "Nature/Tree Creator Bark" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_BumpSpecMap ("Normalmap (GA) Spec (R)", 2D) = "bump" {}
		_TranslucencyColor ("Translucency Color", Color) = (0.73,0.85,0.41,1)
		_TranslucencyViewDependency ("View dependency", Range(0,1)) = 0.7
		_ShadowStrength("Shadow Strength", Range(0,1)) = 0.8
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.3
		_SquashAmount ("Squash", Float) = 1
		_Glossiness ("Glossiness", Range(0,1)) = 0.5
	}

	SubShader {
		Tags {
			"Queue" = "Geometry"
			"IgnoreProjector" = "True"
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalRenderPipeline"
		}
		LOD 200
		ZWrite On
		Blend Off

		Pass {
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
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
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_BumpSpecMap);
			SAMPLER(sampler_BumpSpecMap);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
				float4 _TranslucencyColor;
				float _TranslucencyViewDependency;
				float _ShadowStrength;
				float _Cutoff;
				float _SquashAmount;
				float _Glossiness;
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
				float2 uv : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float3 viewDirWS : TEXCOORD2;
				float3 tangentWS : TEXCOORD3;
				float3 bitangentWS : TEXCOORD4;
				float fogFactor : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings vert(Attributes input) {
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				// Squash effect
				float3 positionOS = input.positionOS.xyz;
				positionOS.xyz *= _SquashAmount;

				VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
				VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

				output.positionCS = vertexInput.positionCS;
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				output.normalWS = normalInput.normalWS;
				output.tangentWS = normalInput.tangentWS;
				output.bitangentWS = normalInput.bitangentWS;
				output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
				output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

				return output;
			}

			half4 frag(Varyings input) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				// Sample textures
				half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
				half3 normalMap = SAMPLE_TEXTURE2D(_BumpSpecMap, sampler_BumpSpecMap, input.uv).gaa;
				
				// Reconstruct normal from GA channels
				half3 normalTS = half3(normalMap.r * 2 - 1, normalMap.g * 2 - 1, 0);
				normalTS.z = sqrt(1.0 - saturate(dot(normalTS.xy, normalTS.xy)));

				// Transform normal to world space
				float3x3 TBN = float3x3(normalize(input.tangentWS), normalize(input.bitangentWS), normalize(input.normalWS));
				float3 normalWS = normalize(mul(normalTS, TBN));
				float3 viewDirWS = normalize(input.viewDirWS);

				// Get main light
				Light mainLight = GetMainLight();
				half3 lightDir = mainLight.direction;
				half3 lightColor = mainLight.color;

				// Basic diffuse lighting
				half NdotL = saturate(dot(normalWS, lightDir));
				half3 diffuse = albedo.rgb * lightColor * NdotL;

				// Specular
				half spec = normalMap.b;
				half3 halfDir = normalize(lightDir + viewDirWS);
				half NdotH = saturate(dot(normalWS, halfDir));
				half3 specular = spec * pow(NdotH, _Glossiness * 128) * lightColor;

				// Combine
				half3 finalColor = diffuse + specular;

				// Ambient
				finalColor += albedo.rgb * half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * 0.5;

				// Fog
				finalColor = MixFog(finalColor, input.fogFactor);

				return half4(finalColor, 1.0);
			}
			ENDHLSL
		}

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual
			ColorMask 0

			HLSLPROGRAM
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float _SquashAmount;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings {
				float4 positionCS : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			float3 _LightDirection;

			Varyings ShadowPassVertex(Attributes input) {
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);

				float3 positionOS = input.positionOS.xyz;
				positionOS.xyz *= _SquashAmount;

				float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
				float3 positionWS = TransformObjectToWorld(positionOS);
				output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

				return output;
			}

			half4 ShadowPassFragment(Varyings input) : SV_TARGET {
				UNITY_SETUP_INSTANCE_ID(input);
				return 0;
			}
			ENDHLSL
		}

		Pass {
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }

			ZWrite On
			ColorMask 0

			HLSLPROGRAM
			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float _SquashAmount;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings {
				float4 positionCS : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			Varyings DepthOnlyVertex(Attributes input) {
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);

				float3 positionOS = input.positionOS.xyz;
				positionOS.xyz *= _SquashAmount;

				output.positionCS = TransformObjectToHClip(positionOS);

				return output;
			}

			half4 DepthOnlyFragment(Varyings input) : SV_TARGET {
				UNITY_SETUP_INSTANCE_ID(input);
				return 0;
			}
			ENDHLSL
		}
	}

	Fallback Off
}

