// Upgrade NOTE: removed variant '__' where variant LOD_FADE_PERCENTAGE is used.

Shader "Banzai/VegetationPBR"
{
	Properties
	{
		_Color("Main Color", Color) = (1,1,1,1)
		_HueVariation("Hue Variation", Color) = (1.0,0.5,0.0,0.1)
		_MainTex("Base (RGB) Trans (A)", 2D) = "white" {}
		_DetailTex("Detail", 2D) = "black" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_Cutoff("Alpha Cutoff", Range(0,1)) = 0.333
		_Specular("Specular Power", Range(0,1)) = 0.333
		_Translucency("Trans Settings", Vector) = (0, 1.0, 0, 0)
		[MaterialEnum(Off,0,Front,1,Back,2)] _Cull("Cull", Int) = 2
		[MaterialEnum(None,0,Fastest,1,Fast,2,Better,3,Best,4,Palm,5)] _WindQuality("Wind Quality", Range(0,5)) = 0
	}

		// targeting SM3.0+
		SubShader
	{
		Tags
		{
			"Queue" = "Geometry"
			"IgnoreProjector" = "True"
			"RenderType" = "Opaque"
			"DisableBatching" = "LODFading"
		}
		LOD 400
		Cull[_Cull]

		/*CGPROGRAM
			#pragma surface surf Lambert vertex:SpeedTreeVert nodirlightmap nodynlightmap
			#pragma target 3.0
			#pragma multi_compile __ LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling lodfade maxcount:50
			#pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
			#pragma shader_feature EFFECT_BUMP
			#pragma shader_feature EFFECT_HUE_VARIATION
			#define ENABLE_WIND
			#include "VegetationPBR.cginc"

			void surf(Input IN, inout SurfaceOutput OUT)
			{
				SpeedTreeFragOut o;
				SpeedTreeFrag(IN, o);
				SPEEDTREE_COPY_FRAG(OUT, o)
			}
		ENDCG*/

		Pass
		{
			Tags{ "LightMode" = "ShadowCaster" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile  LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling lodfade maxcount:50
			#pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
			#pragma multi_compile_shadowcaster
			#define ENABLE_WIND
			#include "VegetationPBR.cginc"

			struct v2f
			{
				V2F_SHADOW_CASTER;
				#ifdef SPEEDTREE_ALPHATEST
					float2 uv : TEXCOORD1;
				#endif
				UNITY_DITHER_CROSSFADE_COORDS_IDX(2)
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert(SpeedTreeVB v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				#ifdef SPEEDTREE_ALPHATEST
						o.uv = v.texcoord.xy;
				#endif
				OffsetSpeedTreeVertex(v, unity_LODFade.x);
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				//UNITY_TRANSFER_DITHER_CROSSFADE_HPOS(o, o.pos)

				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				#ifdef SPEEDTREE_ALPHATEST
					clip(tex2D(_MainTex, i.uv).a * _Color.a - _Cutoff);
				#endif
				UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}

		Pass
		{
			Tags{ "LightMode" = "ClusterForward" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_fog
			#pragma multi_compile  LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling lodfade maxcount:50
			#pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
			#pragma shader_feature EFFECT_BUMP
			#pragma shader_feature EFFECT_HUE_VARIATION
			#pragma multi_compile_fwdbase_fullshadows
			#define ENABLE_WIND
			#include "VegetationPBR.cginc"

			struct v2f
			{
				float4 pos	: SV_POSITION;
				UNITY_FOG_COORDS(0)
				Input data : TEXCOORD1;
				half4 tangentToWorldAndEyeVec[3]	: TEXCOORD5;	// [3x3:tangentToWorld | 1x3:eyeVec]
				SHADOW_COORDS(8)
				half4 ambientOrLightmapUV			: TEXCOORD9;
				float3 posWorld						: TEXCOORD10;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert(SpeedTreeVB v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				SpeedTreeVert(v, o.data);
				//o.data.color.rgb *= ShadeVertexLightsFull(v.vertex, v.normal, 4, true);
				o.pos = UnityObjectToClipPos(v.vertex);
				float3 normalWorld = UnityObjectToWorldNormal(v.normal);
				float4 tangentWorld = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
				float3x3 tangentToWorldTemp = CreateTangentToWorldPerVertex(normalWorld, tangentWorld.xyz, tangentWorld.w);
				o.tangentToWorldAndEyeVec[0].xyz = tangentToWorldTemp[0];
				o.tangentToWorldAndEyeVec[1].xyz = tangentToWorldTemp[1];
				o.tangentToWorldAndEyeVec[2].xyz = tangentToWorldTemp[2];
				o.posWorld = mul(unity_ObjectToWorld, v.vertex).xyz;
				float3 eyeVec = normalize(o.posWorld - _WorldSpaceCameraPos);
				o.tangentToWorldAndEyeVec[0].w = eyeVec.x;
				o.tangentToWorldAndEyeVec[1].w = eyeVec.y;
				o.tangentToWorldAndEyeVec[2].w = eyeVec.z;
				TRANSFER_SHADOW(o);
				o.ambientOrLightmapUV = VertexGIForward(v, o.posWorld, normalWorld);
				UNITY_TRANSFER_FOG(o,o.pos);
				return o;
			}

			fixed4 frag(v2f i, float facing : VFACE) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				SpeedTreeFragOut o;
				SpeedTreeFrag(i.data, o);
				half faceSign = facing >= 0 ? 1.0 : -1.0;
				half isBackFace = facing <= 0 ? 1.0 : 0.0;
				//i.tangentToWorldAndEyeVec[2].xyz = i.tangentToWorldAndEyeVec[2].xyz * faceSign;

				UnityLight mainLight = MainLight();
				half atten = saturate(SHADOW_ATTENUATION(i) + 0.1);
				half3 mainLightBack = mainLight.color;
				mainLight.color *= atten ;
				half3 shadowColor = half3(atten, atten, atten);

				half3 viewDir = -half3(i.tangentToWorldAndEyeVec[0].w, i.tangentToWorldAndEyeVec[1].w, i.tangentToWorldAndEyeVec[2].w);
				half3 halfDir = Unity_SafeNormalize(mainLight.dir + viewDir);

				#if defined(EFFECT_BUMP) && !defined(LIGHTMAP_ON)
					half3 tangent = i.tangentToWorldAndEyeVec[0].xyz;
					half3 binormal = i.tangentToWorldAndEyeVec[1].xyz;
					half3 normal = i.tangentToWorldAndEyeVec[2].xyz;
					half3 normalWorld = normalize(tangent * o.Normal.x + binormal * o.Normal.y + normal * o.Normal.z);
				#else
					half3 normalWorld = i.tangentToWorldAndEyeVec[2].xyz;
				#endif

				half nl = saturate(dot(normalWorld, mainLight.dir));
				half nh = saturate(dot(normalWorld, halfDir));
				half nv = saturate(dot(normalWorld, viewDir));
				half lh = saturate(dot(mainLight.dir, halfDir));

				half perceptualRoughness = 1 - _Specular;
				half roughness = perceptualRoughness * perceptualRoughness;			

				half a = roughness;
				half a2 = a*a;

				half d = nh * nh * (a2 - 1.h) + 1.00001h;
				#ifdef UNITY_COLORSPACE_GAMMA
						// Tighter approximation for Gamma only rendering mode!
						// DVF = sqrt(DVF);
						// DVF = (a * sqrt(.25)) / (max(sqrt(0.1), lh)*sqrt(roughness + .5) * d);
						half specularTerm = a / (max(0.32h, lh) * (1.5h + roughness) * d);
				#else
						half specularTerm = a2 / (max(0.1h, lh*lh) * (roughness + 0.5h) * (d * d) * 4);
				#endif

				#ifdef UNITY_COLORSPACE_GAMMA
						half surfaceReduction = 0.28;
				#else
						half surfaceReduction = (0.6 - 0.08*perceptualRoughness);
				#endif

				half specularPower = PerceptualRoughnessToSpecPower(perceptualRoughness);

				surfaceReduction = 1.0 - roughness*perceptualRoughness*surfaceReduction;

				half3 specColor = lerp(unity_ColorSpaceDielectricSpec.rgb, o.Albedo.xyz, 0);

#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
				half3 ambient = 0;
#else
				half3 ambient = i.ambientOrLightmapUV.rgb;
#endif

#if UNITY_SHOULD_SAMPLE_SH
				half3 indirectDiffuse = ShadeSHPerPixel(normalWorld, ambient, i.posWorld);
#endif
				half fLTAmibient = 0.5;

//#if  defined(GEOM_TYPE_LEAF) || defined(GEOM_TYPE_FACING_LEAF) || defined(GEOM_TYPE_FROND)
//				half3 vLTLight = normalize(mainLight.dir + normalWorld * _Translucency.x);
//				half fLTDot = pow(saturate(dot(viewDir, -vLTLight)), _Translucency.y) * _Translucency.z;
//				half3 fLT = mainLightBack * (fLTDot + fLTAmibient) * _Translucency.w;
//
//				half3 color = (o.Albedo + specularTerm * specColor) * mainLight.color * (nl + isBackFace * fLT) + o.Albedo * fLT * mainLightBack;
//#else
				half3 color = (o.Albedo + specularTerm * specColor) * mainLight.color * nl;
//#endif

//#if UNITY_SHOULD_SAMPLE_SH
//				fixed4 c = fixed4(color + indirectDiffuse * o.Albedo, o.Alpha);
//#else
				fixed4 c = fixed4(color, o.Alpha);
//#endif
				UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
			}
			ENDCG
		}
	}

	FallBack "Transparent/Cutout/VertexLit"
	CustomEditor "SpeedTreeMaterialInspector"
}
