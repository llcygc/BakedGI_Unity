#ifndef LIGHTING_INCLUDED
#define LIGHTING_INCLUDED

#include "CoreRP/ShaderLibrary/Common.hlsl"
#include "CoreRP/ShaderLibrary/ImageBasedLighting.hlsl"
//#include "Core.hlsl"
//#include "Shadows.hlsl"

#ifdef NO_ADDITIONAL_LIGHTS
#undef _ADDITIONAL_LIGHTS
#endif

CBUFFER_START(_PerFrame)
half4 _GlossyEnvironmentColor;
half4 _SubtractiveShadowColor;
CBUFFER_END
///////////////////////////////////////////////////////////////////////////////
//                        Attenuation Functions                               /
///////////////////////////////////////////////////////////////////////////////
half CookieAttenuation(float3 worldPos)
{
#ifdef _MAIN_LIGHT_COOKIE
#ifdef _MAIN_LIGHT_DIRECTIONAL
    float2 cookieUV = mul(_WorldToLight, float4(worldPos, 1.0)).xy;
    return SAMPLE_TEXTURE2D(_MainLightCookie, sampler_MainLightCookie, cookieUV).a;
#elif defined(_MAIN_LIGHT_SPOT)
    float4 projPos = mul(_WorldToLight, float4(worldPos, 1.0));
    float2 cookieUV = projPos.xy / projPos.w + 0.5;
    return SAMPLE_TEXTURE2D(_MainLightCookie, sampler_MainLightCookie, cookieUV).a;
#endif // POINT LIGHT cookie not supported
#endif

    return 1;
}

// Matches Unity Vanila attenuation
// Attenuation smoothly decreases to light range.
half DistanceAttenuation(half distanceSqr, half3 distanceAttenuation)
{
    // We use a shared distance attenuation for additional directional and puctual lights
    // for directional lights attenuation will be 1
    half quadFalloff = distanceAttenuation.x;
    half denom = distanceSqr * quadFalloff + 1.0;
    half lightAtten = 1.0 / denom;

    // We need to smoothly fade attenuation to light range. We start fading linearly at 80% of light range
    // Therefore:
    // fadeDistance = (0.8 * 0.8 * lightRangeSq)
    // smoothFactor = (lightRangeSqr - distanceSqr) / (lightRangeSqr - fadeDistance)
    // We can rewrite that to fit a MAD by doing
    // distanceSqr * (1.0 / (fadeDistanceSqr - lightRangeSqr)) + (-lightRangeSqr / (fadeDistanceSqr - lightRangeSqr)
    // distanceSqr *        distanceAttenuation.y            +             distanceAttenuation.z
    half smoothFactor = saturate(distanceSqr * distanceAttenuation.y + distanceAttenuation.z);
    return lightAtten * smoothFactor;
}

///////////////////////////////////////////////////////////////////////////////
//                         BRDF Functions                                    //
///////////////////////////////////////////////////////////////////////////////

#ifdef UNITY_COLORSPACE_GAMMA
#define unity_ColorSpaceGrey half4(0.5, 0.5, 0.5, 0.5)
#define unity_ColorSpaceDouble half4(2.0, 2.0, 2.0, 2.0)
#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)
#define unity_ColorSpaceLuminance half4(0.22, 0.707, 0.071, 0.0) // Legacy: alpha is set to 0.0 to specify gamma mode
#else // Linear values
#define unity_ColorSpaceGrey half4(0.214041144, 0.214041144, 0.214041144, 0.5)
#define unity_ColorSpaceDouble half4(4.59479380, 4.59479380, 4.59479380, 2.0)
#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)
#define unity_ColorSpaceLuminance half4(0.0396819152, 0.458021790, 0.00609653955, 1.0) // Legacy: alpha is set to 1.0 to specify linear mode
#endif

struct BRDFData
{
    half3 diffuse;
    half3 specular;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half grazingTerm;
};

struct BRDFData_Direct
{
    half3 diffuse;
    half roughness;
    half3 specular;
    half roughness2;
    half alpha;
    half occlusion;
};

struct BRDFData_Indirect
{
#ifdef _EMISSION
    half3 emission;
#endif
    half perceptualRoughness;
    half3 bakedGI;
    half grazingTerm;
};

struct InputDataSimple
{
    float3 positionWS;
    half3 normalWS;
    half3 viewDirectionWS;
#ifdef _BRDF_ANISO
    half3 tangentWS;
#endif
    half3 clusterId;

#ifdef _FORWARD_CLUSTER_FOG
    half3 clusterUV;
#endif
};

half ReflectivitySpecular(half3 specular)
{
#if (SHADER_TARGET < 30)
    // SM2.0: instruction count limitation
    return specular.r; // Red channel - because most metals are either monocrhome or with redish/yellowish tint
#else
    return max(max(specular.r, specular.g), specular.b);
#endif
}

half OneMinusReflectivityMetallic(half metallic)
{
    // We'll need oneMinusReflectivity, so
    //   1-reflectivity = 1-lerp(dielectricSpec, 1, metallic) = lerp(1-dielectricSpec, 0, metallic)
    // store (1-dielectricSpec) in kDieletricSpec.a, then
    //   1-reflectivity = lerp(alpha, 0, metallic) = alpha + metallic*(0 - alpha) =
    //                  = alpha - metallic * alpha
    half oneMinusDielectricSpec = unity_ColorSpaceDielectricSpec.a;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

inline void InitializeBRDFData(half3 albedo, half metallic, half3 specular, half smoothness, half alpha, out BRDFData outBRDFData)
{
#ifdef _SPECULAR_SETUP
    half reflectivity = ReflectivitySpecular(specular);
    half oneMinusReflectivity = 1.0 - reflectivity;

    outBRDFData.diffuse = albedo * (half3(1.0h, 1.0h, 1.0h) - specular);
    outBRDFData.specular = specular;
#else

    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;

    outBRDFData.diffuse = albedo * reflectivity;
    outBRDFData.specular = lerp(unity_ColorSpaceDielectricSpec.rgb, albedo, metallic);
#endif

    outBRDFData.grazingTerm = saturate(smoothness + reflectivity);
    outBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    outBRDFData.roughness = PerceptualRoughnessToRoughness(outBRDFData.perceptualRoughness);
    outBRDFData.roughness2 = outBRDFData.roughness * outBRDFData.roughness;

#ifdef _ALPHAPREMULTIPLY_ON
    outBRDFData.diffuse *= alpha;
    alpha = alpha * oneMinusReflectivity + reflectivity;
#endif
}

half3 EnvironmentBRDF(BRDFData brdfData, half3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm)
{
    half3 c = indirectDiffuse * brdfData.diffuse;
    float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0);
    c += surfaceReduction * indirectSpecular * lerp(brdfData.specular, brdfData.grazingTerm, fresnelTerm);
    return c;
}


// Based on Minimalist CookTorrance BRDF
// Implementation is slightly different from original derivation: http://www.thetenthplanet.de/archives/255
//
// * NDF [Modified] GGX
// * Modified Kelemen and Szirmay-​Kalos for Visibility term
// * Fresnel approximated with 1/LdotH
half3 DirectBDRF(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
#ifndef _SPECULARHIGHLIGHTS_OFF
    half3 halfDir = SafeNormalize(lightDirectionWS + viewDirectionWS);

    half NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155
    half d = NoH * NoH * (brdfData.roughness2 - 1.h) + 1.00001h;

    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / ((d * d) * max(0.1h, LoH2) * (brdfData.roughness + 0.5h) * 4);

    // on mobiles (where half actually means something) denominator have risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

    half3 color = specularTerm * brdfData.specular + brdfData.diffuse;
    return color;
#else
    return brdfData.diffuse;
#endif
}

///////////////////////////////////////////////////////////////////////////////
//                      Global Illumination                                  //
///////////////////////////////////////////////////////////////////////////////

// Samples SH L0, L1 and L2 terms
half3 SampleSH(half3 normalWS)
{
    // LPPV is not supported in Ligthweight Pipeline
    real4 SHCoefficients[7];
    SHCoefficients[0] = unity_SHAr;
    SHCoefficients[1] = unity_SHAg;
    SHCoefficients[2] = unity_SHAb;
    SHCoefficients[3] = unity_SHBr;
    SHCoefficients[4] = unity_SHBg;
    SHCoefficients[5] = unity_SHBb;
    SHCoefficients[6] = unity_SHC;

    return max(half3(0, 0, 0), SampleSH9(SHCoefficients, normalWS));
}

// SH Vertex Evaluation. Depending on target SH sampling might be
// done completely per vertex or mixed with L2 term per vertex and L0, L1
// per pixel. See SampleSHPixel
half3 SampleSHVertex(half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return max(half3(0, 0, 0), SampleSH(normalWS));
#elif defined(EVALUATE_SH_MIXED)
    // no max since this is only L2 contribution
    return SHEvalLinearL2(normalWS, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
#endif

    // Fully per-pixel. Nothing to compute.
    return half3(0.0, 0.0, 0.0);
}

// SH Pixel Evaluation. Depending on target SH sampling might be done
// mixed or fully in pixel. See SampleSHVertex
half3 SampleSHPixel(half3 L2Term, half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return L2Term;
#elif defined(EVALUATE_SH_MIXED)
    half3 L0L1Term = SHEvalLinearL0L1(normalWS, unity_SHAr, unity_SHAg, unity_SHAb);
    return max(half3(0, 0, 0), L2Term + L0L1Term);
#endif

    // Default: Evaluate SH fully per-pixel
    return SampleSH(normalWS);
}

// Sample baked lightmap. Non-Direction and Directional if available.
// Realtime GI is not supported.
half3 SampleLightmap(float2 lightmapUV, half3 normalWS)
{
#ifdef UNITY_LIGHTMAP_FULL_HDR
    bool encodedLightmap = false;
#else
    bool encodedLightmap = true;
#endif

    // The shader library sample lightmap functions transform the lightmap uv coords to apply bias and scale.
    // However, lightweight pipeline already transformed those coords in vertex. We pass half4(1, 1, 0, 0) and
    // the compiler will optimize the transform away.
    half4 transformCoords = half4(1, 1, 0, 0);

#ifdef DIRLIGHTMAP_COMBINED
    return SampleDirectionalLightmap(TEXTURE2D_PARAM(unity_Lightmap, samplerunity_Lightmap),
        TEXTURE2D_PARAM(unity_LightmapInd, samplerunity_Lightmap),
        lightmapUV, transformCoords, normalWS, encodedLightmap, unity_Lightmap_HDR);
#else
    return SampleSingleLightmap(TEXTURE2D_PARAM(unity_Lightmap, samplerunity_Lightmap), lightmapUV, transformCoords, encodedLightmap, unity_Lightmap_HDR);
#endif
}

// We either sample GI from baked lightmap or from probes.
// If lightmap: sampleData.xy = lightmapUV
// If probe: sampleData.xyz = L2 SH terms
half3 SampleGI(float4 sampleData, half3 normalWS)
{
#ifdef LIGHTMAP_ON
    return SampleLightmap(sampleData.xy, normalWS);
#endif

    // If lightmap is not enabled we sample GI from SH
    return SampleSHPixel(sampleData.xyz, normalWS);
}

half3 GlossyEnvironmentReflection(half3 reflectVector, half perceptualRoughness, half occlusion)
{
#if !defined(_GLOSSYREFLECTIONS_OFF)
    half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip);

#if !defined(UNITY_USE_NATIVE_HDR)
    half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
#else
    half3 irradiance = encodedIrradiance.rbg;
#endif

    return irradiance * occlusion;
#endif // GLOSSY_REFLECTIONS

    return _GlossyEnvironmentColor.rgb * occlusion;
}

//half3 SubtractDirectMainLightFromLightmap(Light mainLight, half3 normalWS, half3 bakedGI)
//{
//    // Let's try to make realtime shadows work on a surface, which already contains
//    // baked lighting and shadowing from the main sun light.
//    // Summary:
//    // 1) Calculate possible value in the shadow by subtracting estimated light contribution from the places occluded by realtime shadow:
//    //      a) preserves other baked lights and light bounces
//    //      b) eliminates shadows on the geometry facing away from the light
//    // 2) Clamp against user defined ShadowColor.
//    // 3) Pick original lightmap value, if it is the darkest one.
//
//
//    // 1) Gives good estimate of illumination as if light would've been shadowed during the bake.
//    // We only subtract the main direction light. This is accounted in the contribution term below.
//    half shadowStrength = 1.0f;//GetShadowStrength();
//    half contributionTerm = saturate(dot(mainLight.direction, normalWS)) * (1.0 - _MainLightPosition.w);
//    half3 lambert = mainLight.color * contributionTerm;
//    half3 estimatedLightContributionMaskedByInverseOfShadow = lambert * (1.0 - mainLight.attenuation);
//    half3 subtractedLightmap = bakedGI - estimatedLightContributionMaskedByInverseOfShadow;
//
//    // 2) Allows user to define overall ambient of the scene and control situation when realtime shadow becomes too dark.
//    half3 realtimeShadow = max(subtractedLightmap, _SubtractiveShadowColor.xyz);
//    realtimeShadow = lerp(bakedGI, realtimeShadow, shadowStrength);
//
//    // 3) Pick darkest color
//    return min(bakedGI, realtimeShadow);
//}

half3 GlobalIllumination(BRDFData brdfData, half3 bakedGI, half occlusion, half3 normalWS, half3 viewDirectionWS)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half fresnelTerm = Pow4(1.0 - saturate(dot(normalWS, viewDirectionWS)));

    half3 indirectDiffuse = bakedGI * occlusion;
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, brdfData.perceptualRoughness, occlusion);

    return EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);
}

half3 EnvironmentBRDF(BRDFData_Direct brdfData, half grazingTerm, half3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm)
{
    half3 c = indirectDiffuse * brdfData.diffuse;
    float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0);
    c += surfaceReduction * indirectSpecular * lerp(brdfData.specular, grazingTerm, fresnelTerm);
    return c;
}

half3 GlobalIllumination(BRDFData_Indirect brdfDataIndirect, BRDFData_Direct brdfData, half3 normalWS, half3 viewDirectionWS)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half fresnelTerm = Pow4(1.0 - saturate(dot(normalWS, viewDirectionWS)));

    half3 indirectDiffuse = brdfDataIndirect.bakedGI * brdfData.occlusion;
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, brdfDataIndirect.perceptualRoughness, brdfData.occlusion);

    return EnvironmentBRDF(brdfData, brdfDataIndirect.grazingTerm, indirectDiffuse, indirectSpecular, fresnelTerm);
}

//void MixRealtimeAndBakedGI(inout Light light, half3 normalWS, inout half3 bakedGI, half4 shadowMask)
//{
//#if defined(_MIXED_LIGHTING_SUBTRACTIVE) && defined(LIGHTMAP_ON)
//    bakedGI = SubtractDirectMainLightFromLightmap(light, normalWS, bakedGI);
//#endif
//
//#if defined(LIGHTMAP_ON)
//    #if defined(_MIXED_LIGHTING_SHADOWMASK)
//        // TODO:
//    #elif defined(_MIXED_LIGHTING_SUBTRACTIVE)
//        // Subtractive Light mode has direct light contribution baked into lightmap for mixed lights.
//        // We need to remove direct realtime contribution from mixed lights
//        // subtractiveModeBakedOcclusion is set 0.0 if this light occlusion was baked in the lightmap, 1.0 otherwise.
//        light.attenuation *= light.subtractiveModeAttenuation;
//    #endif
//#endif
//}

///////////////////////////////////////////////////////////////////////////////
//                      Lighting Functions                                   //
///////////////////////////////////////////////////////////////////////////////
half3 LightingLambert(half3 lightColor, half3 lightDir, half3 normal)
{
    half NdotL = saturate(dot(normal, lightDir));
    return lightColor * NdotL;
}

half3 LightingSpecular(half3 lightColor, half3 lightDir, half3 normal, half3 viewDir, half4 specularGloss, half shininess)
{
    half3 halfVec = SafeNormalize(lightDir + viewDir);
    half NdotH = saturate(dot(normal, halfVec));
    half modifier = pow(NdotH, shininess) * specularGloss.a;
    half3 specularReflection = specularGloss.rgb * modifier;
    return lightColor * specularReflection;
}

half3 LightingPhysicallyBased(BRDFData brdfData, half3 lightColor, half3 lightDirectionWS, half lightAttenuation, half3 normalWS, half3 viewDirectionWS)
{
    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    half3 radiance = lightColor * (lightAttenuation * NdotL);
    return DirectBDRF(brdfData, normalWS, lightDirectionWS, viewDirectionWS) * radiance;
}

half3 VertexLighting(float3 positionWS, half3 normalWS)
{
    half3 vertexLightColor = half3(0.0, 0.0, 0.0);

#if defined(_VERTEX_LIGHTS)
    int vertexLightStart = _AdditionalLightCount.x;
    int vertexLightEnd = min(_AdditionalLightCount.y, unity_LightIndicesOffsetAndCount.y);
    for (int lightIter = vertexLightStart; lightIter < vertexLightEnd; ++lightIter)
    {
        Light light = GetLight(lightIter, positionWS);

        half3 lightColor = light.color * light.attenuation;
        vertexLightColor += LightingLambert(lightColor, light.direction, normalWS);
    }
#endif

    return vertexLightColor;
}

inline void GetBRDFDataFromSurfaceData(SurfaceData surfData, out BRDFData_Direct brdfDataDirect, out BRDFData_Indirect brdfDataIndirect)
{
    brdfDataDirect.diffuse = surfData.albedo;

    half oneMinusReflectivity = OneMinusReflectivityMetallic(surfData.metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;

    brdfDataDirect.diffuse = surfData.albedo * oneMinusReflectivity;
    brdfDataDirect.specular = lerp(unity_ColorSpaceDielectricSpec.rgb, surfData.albedo, surfData.metallic);

    brdfDataDirect.occlusion = surfData.occlusion;
    brdfDataDirect.alpha = surfData.alpha;

#ifdef _EMISSION
    brdfDataIndirect.emission = surfData.emission;
#endif

    brdfDataIndirect.bakedGI = 0;
    brdfDataIndirect.grazingTerm = saturate(surfData.smoothness + reflectivity);
    brdfDataIndirect.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surfData.smoothness);
    brdfDataDirect.roughness = PerceptualRoughnessToRoughness(brdfDataIndirect.perceptualRoughness);
    brdfDataDirect.roughness2 = brdfDataDirect.roughness * brdfDataDirect.roughness;

#ifdef _ALPHAPREMULTIPLY_ON
    brdfDataDirect.diffuse *= brdfDataDirect.alpha;
    brdfDataDirect.alpha = brdfDataDirect.alpha * oneMinusReflectivity + reflectivity;
#endif

}

#define INIT_INPUT_DATA(IN, normalTS, inputData) InitializeInputDataTemplate(IN.posWS, IN.viewDir, IN.tangent, IN.binormal, IN.normal, normalTS, IN.clusterPos, inputData)

void InitializeInputDataTemplate(float3 posWorld, float3 viewDir, float3 tangent, float3 binormal, float3 normal, half3 normalTS, float4 clusterPos, out InputDataSimple inputData)
{
    inputData.positionWS = posWorld;

    inputData.normalWS = TangentToWorldNormal(normalTS, tangent, binormal, normal);

    inputData.viewDirectionWS = SafeNormalize(viewDir);
    inputData.clusterId = ComputeClusterID(clusterPos);
}

void InitializeInputData(VertexOutput IN, half3 normalTS, out InputData inputData)
{
    inputData.positionWS = IN.posWS.xyz;

    half3 viewDir = IN.viewDir;

#ifdef _NORMALMAP
    inputData.normalWS = TangentToWorldNormal(normalTS, IN.tangent.xyz, IN.binormal.xyz, IN.normal.xyz);
#else
    inputData.normalWS = normalize(IN.normal);
#endif

    inputData.viewDirectionWS = SafeNormalize(viewDir);

    //inputData.fogCoord = IN.fogFactorAndVertexLight.x;
    //inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
    inputData.bakedGI = SampleGI(IN.lightmapUVOrVertexSH, inputData.normalWS);
    inputData.clusterId = ComputeClusterID(IN.clusterPos);
}

void InitializeInputData(VertexOutput IN, half3 normalTS,
#ifdef _BRDF_ANISO
    half3 tangentTS,
#endif
    out InputDataSimple inputData)
{
    inputData.positionWS = IN.posWS.xyz;

    half3 viewDir = IN.viewDir;

#if defined(_DOUBLESIDED_ON) && SHADER_STAGE_FRAGMENT
    bool isFrontFace = IS_FRONT_VFACE(IN.cullFace, true, false);
    float2 flipSign = isFrontFace ? float2(1.0, 1.0) : _DoubleSidedConstants.yz; // TOCHECK :  GetOddNegativeScale() is not necessary here as it is apply for tangent space creation.
    IN.binormal.xyz = flipSign.x * IN.binormal.xyz; // bitangent
    IN.normal.xyz = flipSign.y * IN.normal.xyz; // normal
#endif

#ifdef _NORMALMAP
    inputData.normalWS = TangentToWorldNormal(normalTS, IN.tangent.xyz, IN.binormal.xyz, IN.normal.xyz);
#else
    inputData.normalWS = normalize(IN.normal);
#endif

#ifdef _BRDF_ANISO
    inputData.tangentWS = TangentToWorldNormal(half3(tangentTS.xy, 0), IN.tangent.xyz, IN.binormal.xyz, IN.normal.xyz);
#endif

    inputData.viewDirectionWS = SafeNormalize(viewDir);
#ifdef _FORWARD_CLUSTER_FOG
    inputData.clusterId = ComputeClusterIDandUV(IN.clusterPos, inputData.clusterUV);
#else
    inputData.clusterId = ComputeClusterID(IN.clusterPos);
#endif
}

#endif
