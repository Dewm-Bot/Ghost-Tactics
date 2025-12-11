Shader "Nature/Tree Creator Leaves (URP)" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.3
		_TranslucencyColor ("Translucency Color", Color) = (0.73,0.85,0.41,1)
		_TranslucencyViewDependency ("View dependency", Range(0,1)) = 0.7
		_ShadowStrength("Shadow Strength", Range(0,1)) = 0.8
		_ShadowOffsetScale ("Shadow Offset Scale", Float) = 1
		_ShadowTex ("Shadow (R)", 2D) = "white" {}
		_TranslucencyMap ("Translucency (A)", 2D) = "white" {}
		_BumpSpecMap ("Normalmap (GA) Spec (R)", 2D) = "bump" {}
		_SquashAmount ("Squash", Float) = 1
		_AmbientColor ("Ambient Color (fallback)", Color) = (0.05,0.05,0.05,1)
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
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON

			#define _ALPHATEST_ON 1

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_BumpSpecMap);
			SAMPLER(sampler_BumpSpecMap);
			TEXTURE2D(_TranslucencyMap);
			SAMPLER(sampler_TranslucencyMap);
			TEXTURE2D(_ShadowTex);
			SAMPLER(sampler_ShadowTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
				float _Cutoff;
				float4 _TranslucencyColor;
				float _TranslucencyViewDependency;
				float _ShadowStrength;
				float _ShadowOffsetScale;
				float _SquashAmount;
				float4 _AmbientColor;
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
				float3 tangentWS : TEXCOORD2;
				float3 bitangentWS : TEXCOORD3;
				float3 viewDirWS : TEXCOORD4;
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

				// Sample textures (explicit channels)
				half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
				half4 bumpSample = SAMPLE_TEXTURE2D(_BumpSpecMap, sampler_BumpSpecMap, input.uv); // RGBA
				half translucency = SAMPLE_TEXTURE2D(_TranslucencyMap, sampler_TranslucencyMap, input.uv).a;
				half shadow = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, input.uv).r;

				// Alpha test (cutout)
				clip(albedo.a - _Cutoff);

				// Reconstruct normal from BumpSpecMap channels:
				// R = spec, G = normal.x, A = normal.y  (as commented in original)
				half spec = bumpSample.r;
				half nx = bumpSample.g * 2.0 - 1.0;
				half ny = bumpSample.a * 2.0 - 1.0;
				half nz = sqrt(saturate(1.0 - nx*nx - ny*ny));
				half3 normalTS = half3(nx, ny, nz);

				// Transform normal to world space using TBN passed from vertex stage
				float3x3 TBN = float3x3(normalize(input.tangentWS), normalize(input.bitangentWS), normalize(input.normalWS));
				float3 normalWS = normalize(mul(normalTS, TBN));
				float3 viewDirWS = normalize(input.viewDirWS);

				// Main light (URP helper)
				Light mainLight = GetMainLight();
				half3 lightDir = mainLight.direction;
				half3 lightColor = mainLight.color;

				// Diffuse
				half NdotL = saturate(dot(normalWS, lightDir));
				half3 diffuse = albedo.rgb * lightColor * NdotL;

				// Translucency: view-dependent backlight-ish term
				float3 transLightDir = normalize(-lightDir + normalWS * _TranslucencyViewDependency);
				half transDot = saturate(dot(viewDirWS, transLightDir));
				half3 translucencyColor = _TranslucencyColor.rgb * transDot * translucency;

				// Specular (simple Blinn-style using stored spec)
				half3 halfDir = normalize(lightDir + viewDirWS);
				half NdotH = saturate(dot(normalWS, halfDir));
				half3 specular = spec * pow(NdotH, saturate(0.5 + _SquashAmount * 0.5) * 128.0) * lightColor;

				// Combine lighting terms
				half3 litColor = diffuse + specular + translucencyColor;

				// Apply shadow map as a simple multiplicative darken: 
				// shadow==1 -> full shadow applied; shadow==0 -> no shadow
				litColor *= (1.0 - _ShadowStrength * shadow);

				// Ambient fallback (use provided ambient color; avoids pipeline built-in SH)
				half3 ambient = _AmbientColor.rgb;
				half3 finalColor = litColor + albedo.rgb * ambient;

				// Fog
				finalColor = MixFog(finalColor, input.fogFactor);

				// Preserve alpha from albedo for correct cutout and blending
				return half4(finalColor, albedo.a);
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
				float _Cutoff;
				float _SquashAmount;
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
				float2 uv : TEXCOORD0;
				float4 positionCS : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			float4 GetShadowPositionHClip(Attributes input) {
				float3 positionOS = input.positionOS.xyz;
				positionOS.xyz *= _SquashAmount;

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

			half4 ShadowPassFragment(Varyings input) : SV_TARGET {
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
			
			#define _ALPHATEST_ON 1

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float _Cutoff;
				float _SquashAmount;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings {
				float2 uv : TEXCOORD0;
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
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);

				return output;
			}

			half4 DepthOnlyFragment(Varyings input) : SV_TARGET {
				UNITY_SETUP_INSTANCE_ID(input);

				half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
				clip(alpha - _Cutoff);

				return 0;
			}
			ENDHLSL
		}
	}

}
