#ifndef CLUSTER_GLOBAL_ILLUMINATION_INCLUDED
#define CLUSTER_GLOBAL_ILLUMINATION_INCLUDED

// Functions sampling light environment data (lightmaps, light probes, reflection probes), which is then returned as the UnityGI struct.

#define IMPROVED_BAKED_AND_REALTIME_SHADOW_MIXING 1

#include "ClusterImageBasedLighting.cginc"
#include "UnityStandardUtils.cginc"

inline half3 DecodeDirectionalSpecularLightmap(half3 color, fixed4 dirTex, half3 normalWorld, bool isRealtimeLightmap, fixed4 realtimeNormalTex, out UnityLight o_light)
{
	o_light.color = color;
	o_light.dir = dirTex.xyz * 2 - 1;
	o_light.ndotl = 0; // Not use;

					   // The length of the direction vector is the light's "directionality", i.e. 1 for all light coming from this direction,
					   // lower values for more spread out, ambient light.
	half directionality = max(0.001, length(o_light.dir));
	o_light.dir /= directionality;

#ifdef DYNAMICLIGHTMAP_ON
	if (isRealtimeLightmap)
	{
		// Realtime directional lightmaps' intensity needs to be divided by N.L
		// to get the incoming light intensity. Baked directional lightmaps are already
		// output like that (including the max() to prevent div by zero).
		half3 realtimeNormal = realtimeNormalTex.xyz * 2 - 1;
		o_light.color /= max(0.125, dot(realtimeNormal, o_light.dir));
	}
#endif

	// Split light into the directional and ambient parts, according to the directionality factor.
	half3 ambient = o_light.color * (1 - directionality);
	o_light.color = o_light.color * directionality;

	// Technically this is incorrect, but helps hide jagged light edge at the object silhouettes and
	// makes normalmaps show up.
	ambient *= saturate(dot(normalWorld, o_light.dir));
	return ambient;
}

inline half3 MixLightmapWithRealtimeAttenuation(half3 lightmap, half attenuation, fixed4 bakedColorTex, half3 normalWorld)
{
	// Let's try to make realtime shadows work on a surface, which already contains
	// baked lighting and shadowing from the current light.

#if IMPROVED_BAKED_AND_REALTIME_SHADOW_MIXING
	half shadowStrength = _LightShadowData.x;

	// Calculate possible value in the shadow by two very distinct ways:
	// 1) by subtracting estimated light contribution from the places occluded by realtime shadow:
	//		a) preserves other baked lights and light bounces
	//		b) eliminates shadows on the geometry facing away from the light
	// 2) by attenuating lightmap - usually produces results that are:
	// 		a) too dark in region where baked and realtime shadow overlap
	//		b) destroys other baked lights
	//		c) shadows are visible on the geometry facing away from the light
	// Then use min/max arbiter to get a solution.

	// 1) Gives good estimate of illumination as if light would've been shadowed during the bake.
	//    Preserves bounce and other baked lights
	//    No shadows on the geometry facing away from the light
	half ndotl = saturate(dot(normalWorld, _WorldSpaceLightPos0.xyz));
	half3 estimatedLightContributionMaskedByInverseOfShadow = ndotl * (1 - attenuation) * _LightColor0.rgb;
	half3 subtractedLightmap = lightmap - estimatedLightContributionMaskedByInverseOfShadow;

	// 2) Keeps lightmap tint in shadow when ShadowStrength < 1.
	//    Preserves unshadowed areas when ShadowStrength < 1.
	half3 lightmapTint = bakedColorTex.rgb;
	half3 attenuatedLightmap = lightmapTint * attenuation;

	// Arbiter. Pick lightest shadows or the lightmap value, if it is the darkest one.
	half3 realtimeShadow = max(attenuatedLightmap, subtractedLightmap);
	realtimeShadow = lerp(realtimeShadow, lightmap, shadowStrength);
	return min(lightmap, realtimeShadow);
#else
	// Generally do min(lightmap,shadow), with "shadow" taking overall lightmap tint into account.
	half3 shadowLightmapColor = bakedColorTex.rgb * attenuation;
	half3 darkerColor = min(lightmap, shadowLightmapColor);

	// However this can darken overbright lightmaps, since "shadow color" will
	// never be overbright. So take a max of that color with attenuated lightmap color.
	return max(darkerColor, lightmap * attenuation);
#endif
}

inline void ResetUnityLight(out UnityLight outLight)
{
	outLight.color = half3(0, 0, 0);
	outLight.dir = half3(0, 1, 0); // Irrelevant direction, just not null
	outLight.ndotl = 0; // Not used
}

inline void ResetTiledGI(out TiledGI outGI)
{
#ifdef DIRLIGHTMAP_SEPARATE
#ifdef LIGHTMAP_ON
	ResetUnityLight(outGI.light2);
#endif
#ifdef DYNAMICLIGHTMAP_ON
	ResetUnityLight(outGI.light3);
#endif
#endif
	outGI.indirect.diffuse = 0;
	outGI.indirect.specular = 0;
}

half3 ShadeSHPerPixelCustom(half3 normal, half3 ambient, float3 worldPos)
{
	half3 ambient_contrib = 0.0;

#if UNITY_SAMPLE_FULL_SH_PER_PIXEL
	// Completely per-pixel
	ambient_contrib =  ShadeSH9(half4(normal, 1.0));
	ambient = max(half3(0, 0, 0), ambient_contrib);
#elif (SHADER_TARGET < 30) || UNITY_STANDARD_SIMPLE
	// Completely per-vertex
	// nothing to do here
#else
	// L2 per-vertex, L0..L1 & gamma-correction per-pixel				
	// Ambient in this case is expected to be always Linear, see ShadeSHPerVertex()
#if UNITY_LIGHT_PROBE_PROXY_VOLUME
	if (unity_ProbeVolumeParams.x == 1.0)
		ambient_contrib = SHEvalLinearL0L1_SampleProbeVolume(half4(normal, 1.0), worldPos);
	else
		ambient_contrib = SHEvalLinearL0L1(half4(normal, 1.0));
#else
	ambient_contrib = SHEvalLinearL0L1(half4(normal, 1.0));
#endif

	ambient = max(half3(0, 0, 0), ambient + ambient_contrib);		// include L2 contribution in vertex shader before clamp.
#ifdef UNITY_COLORSPACE_GAMMA
	ambient = LinearToGammaSpace(ambient);
#endif
#endif

	return ambient;
}

inline TiledGI TiledGI_Base(TiledGIInput data, half occlusion, half3 normalWorld)
{
	TiledGI o_gi;
	ResetTiledGI(o_gi);

//#if UNITY_SHOULD_SAMPLE_SH
	o_gi.indirect.diffuse = ShadeSHPerPixelCustom(normalWorld, data.ambient, data.worldPos);
	//o_gi.indirect.diffuse = half3(1.0f, 0.0f, 0.0f);
//#endif
#if defined(LIGHTMAP_ON)
	// Baked lightmaps
	fixed4 bakedColorTex = UNITY_SAMPLE_TEX2D(unity_Lightmap, data.lightmapUV.xy);
	half3 bakedColor = DecodeLightmap(bakedColorTex);

#ifdef DIRLIGHTMAP_COMBINED
	fixed4 bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd, unity_Lightmap, data.lightmapUV.xy);
	o_gi.indirect.diffuse = DecodeDirectionalLightmap(bakedColor, bakedDirTex, normalWorld);

#ifdef SHADOWS_SCREEN
	o_gi.indirect.diffuse = MixLightmapWithRealtimeAttenuation(o_gi.indirect.diffuse, data.atten, bakedColorTex, normalWorld);
#endif // SHADOWS_SCREEN

#elif DIRLIGHTMAP_SEPARATE
	// Left halves of both intensity and direction lightmaps store direct light; right halves - indirect.

	// Direct
	fixed4 bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd, unity_Lightmap, data.lightmapUV.xy);
	//o_gi.indirect.diffuse = DecodeDirectionalSpecularLightmap(bakedColor, bakedDirTex, normalWorld, false, 0, o_gi.light);

	// Indirect
	float2 uvIndirect = data.lightmapUV.xy + float2(0.5, 0);
	bakedColor = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, uvIndirect));
	bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd, unity_Lightmap, uvIndirect);
	o_gi.indirect.diffuse += DecodeDirectionalSpecularLightmap(bakedColor, bakedDirTex, normalWorld, false, 0, o_gi.light2);

#ifdef SHADOWS_SCREEN
	//o_gi.light.color = MixLightmapWithRealtimeAttenuation(o_gi.light.color, data.atten, bakedColorTex, normalWorld);
	o_gi.light2.color = MixLightmapWithRealtimeAttenuation(o_gi.light2.color, data.atten, bakedColorTex, normalWorld);
	o_gi.indirect.diffuse = MixLightmapWithRealtimeAttenuation(o_gi.indirect.diffuse, data.atten, bakedColorTex, normalWorld);
#endif // SHADOWS_SCREEN

#else // not directional lightmap
	o_gi.indirect.diffuse = bakedColor;

#ifdef SHADOWS_SCREEN
	o_gi.indirect.diffuse = MixLightmapWithRealtimeAttenuation(o_gi.indirect.diffuse, data.atten, bakedColorTex, normalWorld);
#endif // SHADOWS_SCREEN
#endif
#endif

#ifdef DYNAMICLIGHTMAP_ON
	// Dynamic lightmaps
	fixed4 realtimeColorTex = UNITY_SAMPLE_TEX2D(unity_DynamicLightmap, data.lightmapUV.zw);
	half3 realtimeColor = DecodeRealtimeLightmap(realtimeColorTex);

#ifdef DIRLIGHTMAP_COMBINED
	half4 realtimeDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_DynamicDirectionality, unity_DynamicLightmap, data.lightmapUV.zw);
	o_gi.indirect.diffuse += DecodeDirectionalLightmap(realtimeColor, realtimeDirTex, normalWorld);

#elif DIRLIGHTMAP_SEPARATE
	half4 realtimeDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_DynamicDirectionality, unity_DynamicLightmap, data.lightmapUV.zw);
	half4 realtimeNormalTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_DynamicNormal, unity_DynamicLightmap, data.lightmapUV.zw);
	o_gi.indirect.diffuse += DecodeDirectionalSpecularLightmap(realtimeColor, realtimeDirTex, normalWorld, true, realtimeNormalTex, o_gi.light3);
#else
	o_gi.indirect.diffuse += realtimeColor;
#endif
#endif

	o_gi.indirect.diffuse *= occlusion;
	return o_gi;
}

inline half3 TiledGI_IndirectSpecular(TiledGIInput data, half occlusion, Unity_GlossyEnvironmentData glossIn)
{
	half3 specular;

#if UNITY_SPECCUBE_BOX_PROJECTION
	// we will tweak reflUVW in glossIn directly (as we pass it to Unity_GlossyEnvironment twice for probe0 and probe1), so keep original to pass into BoxProjectedCubemapDirection
	half3 originalReflUVW = glossIn.reflUVW;
	glossIn.reflUVW = BoxProjectedCubemapDirection(originalReflUVW, data.worldPos, data.probePosition[0], data.boxMin[0], data.boxMax[0]);
#endif

#ifdef _GLOSSYREFLECTIONS_OFF
	specular = unity_IndirectSpecColor.rgb;
#else
	half3 env0 = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0), data.probeHDR[0], glossIn);
#if UNITY_SPECCUBE_BLENDING
	const float kBlendFactor = 0.99999;
	float blendLerp = data.boxMin[0].w;
	UNITY_BRANCH
		if (blendLerp < kBlendFactor)
		{
#if UNITY_SPECCUBE_BOX_PROJECTION
			glossIn.reflUVW = BoxProjectedCubemapDirection(originalReflUVW, data.worldPos, data.probePosition[1], data.boxMin[1], data.boxMax[1]);
#endif

			half3 env1 = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1, unity_SpecCube0), data.probeHDR[1], glossIn);
			specular = lerp(env1, env0, blendLerp);
		}
		else
		{
			specular = env0;
		}
#else
	specular = env0;
#endif
#endif

	return specular * occlusion;
}

// Deprecated old prototype but can't be move to Deprecated.cginc file due to order dependency
inline half3 UnityGI_IndirectSpecular(TiledGIInput data, half occlusion, half3 normalWorld, Unity_GlossyEnvironmentData glossIn)
{
	// normalWorld is not used
	return TiledGI_IndirectSpecular(data, occlusion, glossIn);
}

inline TiledGI TiledGlobalIllumination(TiledGIInput data, half occlusion, half3 normalWorld)
{
	return TiledGI_Base(data, occlusion, normalWorld);
}

inline TiledGI TiledGlobalIllumination(TiledGIInput data, half occlusion, half3 normalWorld, Unity_GlossyEnvironmentData glossIn)
{
	TiledGI o_gi = TiledGI_Base(data, occlusion, normalWorld);
	o_gi.indirect.specular = TiledGI_IndirectSpecular(data, occlusion, glossIn);
	return o_gi;
}


#endif
