#ifndef FRAG_DATA
#define FRAG_DATA

#include "VertexData.hlsl"

struct InputData
{
    float3 positionWS;
    half3 normalWS;
    half3 viewDirectionWS;
    half fogCoord;
    half3 vertexLighting;
    half3 bakedGI;
    half3 clusterId;
};

struct PrincipleFragmentData
{
    half4 diffColor;
    half4 specColor;
    half4 transmissionColor;
    half4 subsurfaceColor;
};

// Must match Lightweigth ShaderGraph master node
struct SurfaceData
{
    half3 albedo;
    half3 specular;
    half metallic;
    half smoothness;
    half3 normalTS;
    half3 emission;
    half occlusion;
    half alpha;
};

SurfaceData InitializedSurfaceData()
{
    SurfaceData surfData = (SurfaceData)0;
    surfData.alpha = 1.0;
    surfData.occlusion = 1.0;
    surfData.normalTS = half3(0, 0, 1);
    return surfData;
}

struct PBR_FragmentData
{
    half4 diffColor;
    half4 specColorandSmoothness;
	// Note: smoothness & oneMinusReflectivity for optimization purposes, mostly for DX9 SM2.0 level.
	// Most of the math is being done on these (1-x) values, and that saves a few precious ALU slots.
    half oneMinusReflectivity, occlusion, metallic;
    half3 normalWorld, eyeVec, posWorld;
    half3 clusterId;

#if UNITY_OPTIMIZE_TEXCUBELOD || UNITY_STANDARD_SIMPLE
	half3 reflUVW;
#endif
};

//half3 UnpackNormal(half4 packedNormal)
//{
//    // Compiler will optimize the scale away
//#if defined(UNITY_NO_DXT5nm)
//    return UnpackNormalRGB(packedNormal, 1.0);
//#else
//    return UnpackNormalmapRGorAG(packedNormal, 1.0);
//#endif
//}
//
//half3 UnpackNormalScale(half4 packedNormal, half bumpScale)
//{
//#if defined(UNITY_NO_DXT5nm)
//    return UnpackNormalRGB(packedNormal, bumpScale);
//#else
//    return UnpackNormalmapRGorAG(packedNormal, bumpScale);
//#endif
//}

half3 TangentToWorldNormal(half3 normalTangent, half3 tangent, half3 binormal, half3 normal)
{
    half3x3 tangentToWorld = half3x3(tangent, binormal, normal);
    return normalize(mul(normalTangent, tangentToWorld));
}

void OutputTangentToWorld(half4 vertexTangent, half3 vertexNormal, out half3 tangentWS, out half3 binormalWS, out half3 normalWS)
{
    half sign = vertexTangent.w * unity_WorldTransformParams.w;
    normalWS = TransformObjectToWorldNormal(vertexNormal);
    tangentWS = normalize(mul((float3x3)UNITY_MATRIX_M, vertexTangent.xyz));
    binormalWS = cross(normalWS, tangentWS) * sign;
}


#endif
