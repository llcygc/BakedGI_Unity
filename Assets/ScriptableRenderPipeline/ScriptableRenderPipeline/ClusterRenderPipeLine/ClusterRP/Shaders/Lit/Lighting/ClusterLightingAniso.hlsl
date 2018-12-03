#ifndef CLUSTER_LIGHTING_INCLUDED
#define CLUSTER_LIGHTING_INCLUDED

//#include "LWRP/ShaderLibrary/InputSurface.hlsl"
//#include "LightingUtils.hlsl"
//#include "Lighting.hlsl"
#include "ClusterData.cginc"
#include "BSDF.hlsl"
#include "../../ShadowUtils.cginc"

#ifdef UNITY_STEREO_INSTANCING_ENABLED
TEXTURE2D_ARRAY(_ScreenSpaceShadowBuffer);
SAMPLER(sampler_ScreenSpaceShadowBuffer);
#else
TEXTURE2D(_ScreenSpaceShadowBuffer);
SAMPLER(sampler_ScreenSpaceShadowBuffer);
#endif

half3 Surface_ClusterLightingResult_Direct_Aniso_OP(BRDFData_Direct data, int3 screenCoord, half3 normalWorld, half3 tagentWorld, half3 viewDir, float3 posWorld, half3 clusterId)
{

    // NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
    // In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
    // but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
    // Following define allow to control this. Set it to 0 if ALU is critical on your platform.
    // This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
    // Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.

#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
    // The amount we shift the normal toward the view vector is defined by the dot product.
    half shiftAmount = dot(data.normalWorld, -data.eyeVec);
    normal = shiftAmount < 0.0f ? data.normalWorld + (-data.eyeVec) * (-shiftAmount + 1e-5f) : data.normalWorld;
    // A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
    //normal = normalize(normal);

    half nv = saturate(dot(normalWorld, viewDir)); // TODO: this saturate should no be necessary here
#else
    half nv = abs(dot(normalWorld, viewDir));	// This abs allow to limit artifact
#endif

    half3 diffLightingColor = 0;
    half3 specLightingColor = 0;

    [loop]for (int idx = 0; idx < _DirectionalLightCount; idx++)
    {
        DirectionalLightDataSimple dirLight = _DirectionalLightsDataSimpleBuffer[0];
        float3 vPositionToLightDirWs = -dirLight.ForwardCosAngle.xyz;

        float lightAtten = 1.0;

    #ifdef UNITY_STEREO_INSTANCING_ENABLED
        lightAtten = LOAD_TEXTURE2D_ARRAY(_ScreenSpaceShadowBuffer, screenCoord.xy, screenCoord.z);
    #else
        lightAtten = LOAD_TEXTURE2D(_ScreenSpaceShadowBuffer, screenCoord.xy);
    #endif
        //lightAtten = Compute_Direction_Light_Shadow(posWorld, dirLight);

        float3 dirLightColor = dirLight.Color.xyz * lightAtten;
        float specTerm, diffTerm;
        BSDF_Direct_Term(normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2, diffTerm, specTerm);//BRDF_PBS_Direct_Term(data.diffuse, data.specular, nv, normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2);

        diffLightingColor += dirLightColor * diffTerm;
        specLightingColor += dirLightColor * specTerm;
    }

    float startOffset = (clusterId.x * CullingClusterParams.y * CullingClusterParams.z + clusterId.y * CullingClusterParams.z) * 4 + clusterId.z * 4;

    uint nextNode = ClusterStartOffsetIndexIn.Load((int)startOffset);

    int loopCount = 0;

    [loop] while (loopCount < _PunctualLightCount && nextNode != 0xFFFFFFFF)
    {
        loopCount++;

        uint lightLinkNode = ClusterLightsLinkListIn[nextNode];
        uint lightId = lightLinkNode >> 24;


        uint nextNodeId = lightLinkNode | 0xFF000000;
        if (nextNodeId != 0xFFFFFFFF)
            nextNode = lightLinkNode & 0xFFFFFF;
        else
            nextNode = 0xFFFFFFFF;

        LightGeoData lightData = AllLightList[lightId];
        //half3 brdfColor = BRDF_PBS_Direct_Term(s.diffColor.xyz, s.specColorandSmoothness.xyz, nv, s.normalWorld, -s.eyeVec, vPositionToLightDirWs, grazingTerm, perceptualRoughness, roughness);

        LightRenderingData lightRenderingData = AllLightsRenderingData[lightId];

        float lightAtten = 1.0;

        if (lightRenderingData.ShadowOffset.w >= 0)
        {
            int shadowIndexOffset = 0;

            ShadowData sd = _ShadowDatasExp[lightRenderingData.ShadowOffset.w + shadowIndexOffset];

            lightAtten = ComputeShadow_Exponential(posWorld, sd, true);
        }

        float3 vPosition2LightRayWs = lightData.PositionSizeSqr.xyz - posWorld;
        float wsPos2LightSq = dot(vPosition2LightRayWs.xyz, vPosition2LightRayWs.xyz);

        [branch]if ((wsPos2LightSq > lightData.PositionSizeSqr.w || dot(vPosition2LightRayWs, normalWorld) < 0) && lightData.PositionSizeSqr.w > 0)
        {
            continue;
        }

        float3 vPositionToLightDirWs = SafeNormalize(vPosition2LightRayWs.xyz);
        float flTemp = min(dot(-vPositionToLightDirWs.xyz, lightData.ForwardCosAngle.xyz) - lightData.ForwardCosAngle.w, 1.0);
        float fallOffParam = lightData.PositionSizeSqr.w > 0 ? (1.0 - pow(wsPos2LightSq / lightData.PositionSizeSqr.w, 0.175)) : 1.0;

        if (flTemp <= 0.0 && lightData.PositionSizeSqr.w > 0)
        {
            continue;
        }

        float cookieAtten = 1.0;
        float spotLightAtten = saturate(flTemp * lightRenderingData.Color.w);
        half3 lightColor = lightRenderingData.Color.xyz * spotLightAtten * fallOffParam * lightAtten * cookieAtten;

        //half3 brdfColor = BRDF_PBS_Direct_Term(data.diffuse, data.specular, nv, normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2);
        float specTerm, diffTerm;
        BSDF_Direct_Term(normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2, diffTerm, specTerm);
        diffLightingColor += diffTerm * lightColor;
        specLightingColor += specTerm * lightColor;
    }

    return specLightingColor * data.specular + diffLightingColor * data.diffuse;
}

#endif
