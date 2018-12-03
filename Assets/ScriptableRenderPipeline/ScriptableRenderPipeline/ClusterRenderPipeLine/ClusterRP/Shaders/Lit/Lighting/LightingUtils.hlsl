#ifndef LIGHTING_UTILS
#define LIGHTING_UTILS

#include "ClusterUtils.cginc"
#include "Lighting.hlsl"
#include "ClusterLighting.hlsl"
#include "CoreRP/ShaderLibrary/EntityLighting.hlsl"
#include "CoreRP/ShaderLibrary/ImageBasedLighting.hlsl"
#include "CoreRP/ShaderLibrary/Packing.hlsl"


//-----------------------------------------------------------------------------
// Reflection proble / Sky sampling function
// ----------------------------------------------------------------------------

#define SINGLE_PASS_CONTEXT_SAMPLE_REFLECTION_PROBES 0
#define SINGLE_PASS_CONTEXT_SAMPLE_SKY 1

#ifdef _SPECULAR_SETUP
#define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
#define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)
#endif

#if defined(UNITY_NO_DXT5nm)
#define UNPACK_NORMAL_FUNC UnpackNormalRGB
#define UNPACK_DERIVATIVE_FUNC UnpackDerivativeNormalRGB
#else
#define UNPACK_NORMAL_FUNC UnpackNormalmapRGorAG
#define UNPACK_DERIVATIVE_FUNC UnpackDerivativeNormalRGorAG
#endif

#if UNITY_REVERSED_Z
    #if SHADER_API_OPENGL || SHADER_API_GLES || SHADER_API_GLES3
        //GL with reversed z => z clip range is [near, -far] -> should remap in theory but dont do it in practice to save some perf (range is close enough)
        #define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max(-(coord), 0)
    #else
        //D3d with reversed Z => z clip range is [near, 0] -> remapping to [0, far]
        //max is required to protect ourselves from near plane not being correct/meaningfull in case of oblique matrices.
        #define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max(((1.0-(coord)/_ProjectionParams.y)*_ProjectionParams.z),0)
    #endif
#elif UNITY_UV_STARTS_AT_TOP
    //D3d without reversed z => z clip range is [0, far] -> nothing to do
    #define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#else
    //Opengl => z clip range is [-near, far] -> should remap in theory but dont do it in practice to save some perf (range is close enough)
    #define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#endif

half ComputeFogFactor(float z)
{
    float clipZ_01 = UNITY_Z_0_FAR_FROM_CLIPSPACE(z);

#if defined(FOG_LINEAR)
    // factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
    float fogFactor = saturate(clipZ_01 * unity_FogParams.z + unity_FogParams.w);
    return half(fogFactor);
#elif defined(FOG_EXP2)
    // factor = exp(-(density*z)^2)
    // -density * z computed at vertex
    return half(unity_FogParams.x * clipZ_01);
#else
    return 0.0h;
#endif
}

// normal should be normalized, w=1.0
half3 SHEvalLinearL0L1(half4 normal)
{
    half3 x;

    // Linear (L1) + constant (L0) polynomial terms
    x.r = dot(unity_SHAr, normal);
    x.g = dot(unity_SHAg, normal);
    x.b = dot(unity_SHAb, normal);

    return x;
}

// normal should be normalized, w=1.0
half3 SHEvalLinearL2(half4 normal)
{
    half3 x1, x2;
    // 4 of the quadratic (L2) polynomials
    half4 vB = normal.xyzz * normal.yzzx;
    x1.r = dot(unity_SHBr, vB);
    x1.g = dot(unity_SHBg, vB);
    x1.b = dot(unity_SHBb, vB);

    // Final (5th) quadratic (L2) polynomial
    half vC = normal.x * normal.x - normal.y * normal.y;
    x2 = unity_SHC.rgb * vC;

    return x1 + x2;
}

// normal should be normalized, w=1.0
// output in active color space
half3 ShadeSH9(half4 normal)
{
    // Linear + constant polynomial terms
    half3 res = SHEvalLinearL0L1(normal);

    // Quadratic polynomials
    res += SHEvalLinearL2(normal);

#ifdef UNITY_COLORSPACE_GAMMA
    res = LinearToGammaSpace(res);
#endif

    return res;
}

// OBSOLETE: for backwards compatibility with 5.0
half3 ShadeSH3Order(half4 normal)
{
    // Quadratic polynomials
    half3 res = SHEvalLinearL2(normal);

#ifdef UNITY_COLORSPACE_GAMMA
    res = LinearToGammaSpace(res);
#endif

    return res;
}

half _LerpOneTo(half b, half t)
{
    half oneMinusT = 1 - t;
    return oneMinusT + b * t;
}

half DetailMask(float2 uv)
{
	return SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
}

half4 AlbedoColor(FragInputs input)
{
    half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texCoord0);

#if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
    albedo.a = _Color.a;
#endif

#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif

#if _DETAIL
    half mask = 1;
//#ifdef _DETAIL_MASK
//    mask = DetailMask(texcoords.xy);
//#endif
    half3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, input.texCoord1).rgb;
#if _DETAIL_MULX2
    albedo.xyz *= LerpWhiteTo(detailAlbedo * unity_ColorSpaceDouble.rgb, mask);
#elif _DETAIL_MUL
    albedo.xyz *= LerpWhiteTo(detailAlbedo, mask);
#elif _DETAIL_ADD
    albedo.xyz += detailAlbedo * mask;
#elif _DETAIL_LERP
    albedo.xyz = lerp(albedo.xyz, detailAlbedo, mask);
#endif
    //albedo.xyz += float3(1.0, 0.0, 0.0);
#endif

    return albedo;
}

half4 AlbedoColor(float4 uv)
{
    half4 albedo = _Color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv.xy);
	
#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif

#if _DETAIL
    half mask = 1;
#if _DETAIL_MASK
    mask = DetailMask(uv.xy);
#endif
    half3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, uv.zw).rgb;
#if _DETAIL_MULX2
    albedo.xyz *= LerpWhiteTo(detailAlbedo * unity_ColorSpaceDouble.rgb, mask);
#elif _DETAIL_MUL
    albedo.xyz *= LerpWhiteTo(detailAlbedo, mask);
#elif _DETAIL_ADD
    albedo.xyz += detailAlbedo * mask;
#elif _DETAIL_LERP
    albedo.xyz = lerp(albedo.xyz, detailAlbedo, mask);
#endif
    //albedo.xyz += float3(1.0, 0.0, 0.0);
#endif

    return albedo;
}

//half3 NormalWorld(FragInputs input)
//{
//#if defined(_NORMALMAP) || defined(EFFECT_BUMP)
//    half3 normalTangent = NormalInTangentSpaceDetail(i_tex);
//    half3 normalWorld = TransformTangentToWorld(normalTangent, input.worldToTangent)
//#else
//    half3 normalWorld = input.worldToTangent[2].xyz;
//#endif
//    return normalWorld;
//}

half4 MetallicSpecGlossAO(float2 texCoord0, half alpha)
{
    // metallic - r, smoothness - a, occlusion - g
    half4 msoColor = half4(0, 0, 0, 0);

#ifdef _METALLICSPECGLOSSMAP

    #ifdef _SPECULAR_SETUP
        msoColor = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, texCoord0);
    #else
        msoColor = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, texCoord0);
		msoColor.r *= _MetallicScale;
        #if _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            msoColor.a = alpha;
        #else
            msoColor.b = OneMinusReflectivityMetallic(msoColor.r);
        #endif
        msoColor.g = _LerpOneTo(msoColor.g, _OcclusionStrength);
		msoColor.a *= _GlossMapScale;
    #endif
#else

#ifdef _SPECULAR_SETUP
    msoColor = _SpecColor;
#else
    msoColor.r = _Metallic;
    msoColor.a = _Glossiness;
#if _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
	msoColor.a = alpha * _GlossMapScale;
#endif
    msoColor.g = 1.0;
    #if defined(_OCCLUSIONMAP) && !defined(_SPECULAR_SETUP)
        half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, texCoord0).g;
        msoColor.g = _LerpOneTo(occ, _OcclusionStrength);
    #endif
#endif
#endif

    return msoColor;
}

#if defined(_NORMALMAP)
half3 NormalInTangentSpaceDetail(float4 texcoords)
{
    half3 normalTangent = UNPACK_NORMAL_FUNC(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, texcoords.xy), _BumpScale);
    // SM20: instruction count limitation
    // SM20: no detail normalmaps
#if _DETAIL && !defined(SHADER_API_MOBILE) && (SHADER_TARGET >= 30) 
	half mask = 1;
#if _DETAIL_MASK
	mask = DetailMask(texcoords.xy);
#endif
    half3 detailNormalTangent = UNPACK_NORMAL_FUNC(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, texcoords.zw), _DetailNormalMapScale);
    //#if _DETAIL_LERP
    //    normalTangent = lerp(
    //        normalTangent,
    //        detailNormalTangent,
    //        mask);
    //#else				
        normalTangent = lerp(
            normalTangent,
            BlendNormalRNM(normalTangent, detailNormalTangent),
            mask);
    //#endif
    //normalTangent = BlendNormalRNM(normalTangent, detailNormalTangent);
#endif
    return normalTangent;
}
#endif

half3 EvaluateSHPerPixel(half3 normalWS, half3 L2Term)
{
#ifdef EVALUATE_SH_MIXED
    return max(half3(0, 0, 0), L2Term + SHEvalLinearL0L1(half4(normalWS, 1.0)));
#endif

    // Default: Evaluate SH fully per-pixel
    return max(half3(0, 0, 0), ShadeSH9(half4(normalWS, 1.0)));
}

half3 EvaluateSHPerVertex(half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return max(half3(0, 0, 0), ShadeSH9(half4(normalWS, 1.0)));
#elif defined(EVALUATE_SH_MIXED)
    // no max since this is only L2 contribution
    return SHEvalLinearL2(half4(normalWS, 1.0));
#endif

    // Fully per-pixel. Nothing to compute.
    return half3(0.0, 0.0, 0.0);
}

half3 GlossyEnvironment(half3 reflectVector, half perceptualRoughness)
{
#if !defined(_GLOSSYREFLECTIONS_OFF)
    half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip);

#if !defined(UNITY_USE_NATIVE_HDR)
    half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
#else
    half3 irradiance = encodedIrradiance.rbg;
#endif    
    return irradiance;
#endif // GLOSSY_REFLECTIONS

    return _GlossyEnvironmentColor;
}

half3 Emission(float2 uv)
{
#ifndef _EMISSION
    return 0;
#else
    return SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
#endif
}

inline void InitializeBRDFData(float4 uv, out half3 normalTS, out BRDFData_Direct brdfDataDirect, out BRDFData_Indirect brdfDataIndirect)
{
    //float4 uv = IN.uv;
    half4 albedoAlpha = AlbedoColor(uv);//SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

    half4 specGlossAO = MetallicSpecGlossAO(uv.xy, albedoAlpha.a);

    brdfDataDirect.diffuse = albedoAlpha.rgb * _Color.rgb;

    half metallic = 1.0h;
    half3 specular = half3(0.0h, 0.0h, 0.0h);

#ifdef _SPECULAR_SETUP
    specular = specGlossAO.rgb;
    metallic = 0;
#else
    metallic = specGlossAO.r;
#endif

    half smoothness = specGlossAO.a;

#if defined(_NORMALMAP)
    normalTS = NormalInTangentSpaceDetail(uv);
#else
    normalTS = half3(0.0h, 0.0h, 1.0h);
#endif    

#ifdef _SPECULAR_SETUP
    half reflectivity = ReflectivitySpecular(specular);
    half oneMinusReflectivity = 1.0 - reflectivity;

    brdfDataDirect.diffuse = albedoAlpha.rgb * (half3(1.0h, 1.0h, 1.0h) - specular);
    brdfDataDirect.specular = specular;
    half occ = 1.0f;
    #if defined(_OCCLUSIONMAP)
        occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv.xy).g;
        occ = _LerpOneTo(occ, _OcclusionStrength);
    #endif
    brdfDataDirect.occlusion = occ;
#else

    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;

    brdfDataDirect.diffuse = albedoAlpha.rgb * oneMinusReflectivity;
    brdfDataDirect.specular = lerp(unity_ColorSpaceDielectricSpec.rgb, albedoAlpha.rgb, metallic);
    brdfDataDirect.occlusion = specGlossAO.g;;
#endif

#if _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
	albedoAlpha.a = _Color.a;
#endif

    brdfDataDirect.alpha = albedoAlpha.a;

#ifdef _EMISSION
    brdfDataIndirect.emission = Emission(uv);
#endif

    brdfDataIndirect.bakedGI = 0;
    brdfDataIndirect.grazingTerm = saturate(smoothness + reflectivity);
    brdfDataIndirect.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    brdfDataDirect.roughness = PerceptualRoughnessToRoughness(brdfDataIndirect.perceptualRoughness);
    brdfDataDirect.roughness2 = brdfDataDirect.roughness * brdfDataDirect.roughness;

#if _ALPHAPREMULTIPLY_ON
    brdfDataDirect.diffuse *= brdfDataDirect.alpha;
    //brdfDataDirect.alpha = brdfDataDirect.alpha * oneMinusReflectivity + reflectivity;
#endif

}

inline void InitializeStandardLitSurfaceData(float4 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = AlbedoColor(uv);//SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

    half4 specGlossAO = MetallicSpecGlossAO(uv.xy, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _Color.rgb;

#if _SPECULAR_SETUP
    outSurfaceData.metallic = 1.0h;
    outSurfaceData.specular = specGlossAO.rgb;
#else
    outSurfaceData.metallic = specGlossAO.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
#endif

    outSurfaceData.smoothness = specGlossAO.a;
#if defined(_NORMALMAP)
    outSurfaceData.normalTS = NormalInTangentSpaceDetail(uv);
#else
    outSurfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
#endif
    outSurfaceData.occlusion = 1.0f;// specGlossAO.g;
    outSurfaceData.emission = Emission(uv);
    outSurfaceData.alpha = albedoAlpha.a;
}

///////////////////////////////////////////////////////////////////////////////
//                      Fragment Functions                                   //
//       Used by ShaderGraph and others builtin renderers                    //
///////////////////////////////////////////////////////////////////////////////
half4 ClusterLightFragmentPBR(InputData inputData, half3 albedo, half metallic, half3 specular,
    half smoothness, half occlusion, half3 emission, half alpha)
{
    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, specular, smoothness, alpha, brdfData);

    //MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, half4(0, 0, 0, 0));
    half3 color = GlobalIllumination(brdfData, inputData.bakedGI, occlusion, inputData.normalWS, inputData.viewDirectionWS);
    color += SURFACE_LIGHTING_CLUSTER(brdfData, inputData.normalWS, inputData.viewDirectionWS, inputData.positionWS,inputData.clusterId);

    //color += inputData.vertexLighting * brdfData.diffuse;
    color += emission;
    return half4(color, alpha);
}

float3 Surface_SimpleLightingResult(PBR_FragmentData data)
{
    return data.diffColor.rgb;
}

float3 Particle_ClusterLightingResult(PBR_FragmentData data)
{
    return data.diffColor.rgb;
}

float3 Particle_SimpleLightingResult(PBR_FragmentData data)
{
    return data.diffColor.rgb;
}

float3 Particle_VolumetricLightingResult(PBR_FragmentData data)
{
    return data.diffColor.rgb;
}

#endif
