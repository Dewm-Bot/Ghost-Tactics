Shader "Toon/Basic" {
	Properties {
		_Color ("Main Color", Color) = (.5,.5,.5,1)
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_ToonShade ("ToonShader Cubemap(RGB)", CUBE) = "" { }
	}

	SubShader {
		Tags { 
			"RenderType"="Opaque"
			"RenderPipeline"="UniversalRenderPipeline"
		}
		LOD 200
		
		Pass {
			Name "ForwardLit"
			Tags { "LightMode"="UniversalForward" }
			Cull Off
			
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			TEXTURECUBE(_ToonShade);
			SAMPLER(sampler_ToonShade);
			
			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				float3 normalOS : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct Varyings {
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 cubeNormalVS : TEXCOORD1;
				float fogFactor : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings vert (Attributes input)
			{
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = vertexInput.positionCS;
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				
				// Transform normal to view space for cubemap lookup
				float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.cubeNormalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
				
				output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
				return output;
			}

			half4 frag (Varyings input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				
				half4 col = _Color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
				half4 cube = SAMPLE_TEXTURECUBE(_ToonShade, sampler_ToonShade, input.cubeNormalVS);
				half3 finalColor = 2.0 * cube.rgb * col.rgb;
				finalColor = MixFog(finalColor, input.fogFactor);
				return half4(finalColor, col.a);
			}
			ENDHLSL
		}
	}

	Fallback "Universal Render Pipeline/Lit"
}

