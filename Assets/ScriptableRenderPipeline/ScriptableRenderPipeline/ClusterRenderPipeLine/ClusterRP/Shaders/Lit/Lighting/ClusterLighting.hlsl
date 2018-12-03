#ifndef CLUSTER_LIGHTING_INCLUDED
#define CLUSTER_LIGHTING_INCLUDED

//#include "LWRP/ShaderLibrary/InputSurface.hlsl"
//#include "LightingUtils.hlsl"
//#include "Lighting.hlsl"
#include "ClusterData.cginc"
#include "BSDF.hlsl"
#include "../../ShadowUtils.cginc"

#define SURFACE_LIGHTING_CLUSTER(brdfData, normalWorld, viewDir, posWorld, clusterId) Surface_ClusterLightingResult(brdfData.diffuse, brdfData.specular, brdfData.roughness, brdfData.roughness2, normalWorld, viewDir, posWorld, clusterId)

#define SURFACE_LIGHTING_CLUSTER_DIRECT(brdfData, normalWorld, viewDir, posWorld, clusterId) Surface_ClusterLightingResult_Direct_OP(brdfData.diffuse, brdfData.specular, brdfData.roughness, brdfData.roughness2, normalWorld, viewDir, posWorld, clusterId)

#define SURFACE_LIGHTING_CLUSTER_DIRECT_SCREENSHADOW(brdfData, screenCoord, normalWorld, viewDir, posWorld, clusterId) Surface_ClusterLightingResult_Direct_OP(brdfData.diffuse, brdfData.specular, brdfData.roughness, brdfData.roughness2, screenCoord, normalWorld, viewDir, posWorld, clusterId)

#define SURFACE_LIGHTING_CLUSTER_DIRECT_ANISO(brdfData, normalWorld, tangentWorld, viewDir, posWorld, clusterId) Surface_ClusterLightingResult_Direct_Aniso_OP(brdfData.diffuse, brdfData.specular, brdfData.roughness, brdfData.roughness2, normalWorld, tangentWorld, viewDir, posWorld, clusterId)

#define SURFACE_LIGHTING_CLUSTER_DIRECT_ANISO_SCREENSHADOW(brdfData, screenCoord, normalWorld, tangentWorld, viewDir, posWorld, clusterId) Surface_ClusterLightingResult_Direct_Aniso_OP(brdfData.diffuse, brdfData.specular, brdfData.roughness, brdfData.roughness2, screenCoord, normalWorld, tangentWorld, viewDir, posWorld, clusterId)

half3 Surface_ClusterLightingResult(half3 diffuse, half3 specular, half roughness, half roughness2, half3 normalWorld, half3 viewDir, float3 posWorld, half3 clusterId)
{
    // NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
    // In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
    // but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
    // Following define allow to control this. Set it to 0 if ALU is critical on your platform.
    // This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
    // Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.

    half nv = abs(dot(normalWorld, viewDir));	

    half3 lightingColor = 0;

    for (int idx = 0; idx < _DirectionalLightCount; idx++)
    {
        DirectionalLightDataSimple dirLight = _DirectionalLightsDataSimpleBuffer[idx];
        float3 vPositionToLightDirWs = -dirLight.ForwardCosAngle.xyz;

        half3 brdfColor = BRDF_PBS_Direct_Term(diffuse, specular, nv, normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2);

        float lightAtten = 1.0;

        lightAtten = Compute_Direction_Light_Shadow(posWorld, dirLight);

        float3 dirLightColor = dirLight.Color.xyz * lightAtten;


        lightingColor += dirLightColor * brdfColor;
    }

    float startOffset = (clusterId.x * CullingClusterParams.y * CullingClusterParams.z + clusterId.y * CullingClusterParams.z) * 4 + clusterId.z * 4;

    uint nextNode = ClusterStartOffsetIndexIn.Load((int)startOffset);

    int loopCount = 0;

    [loop] while (loopCount < _PunctualLightCount && nextNode != 0xFFFFFFFF)
    {
        loopCount++;

        uint lightLinkNode = ClusterLightsLinkListIn[nextNode];
        uint lightId = lightLinkNode >> 24;

        LightGeoData lightData = AllLightList[lightId];

        uint nextNodeId = lightLinkNode | 0xFF000000;
        if (nextNodeId != 0xFFFFFFFF)
            nextNode = lightLinkNode & 0xFFFFFF;
        else
            nextNode = 0xFFFFFFFF;

        int lightType = 2; //Spot Light;
        if (lightData.PositionSizeSqr.w < 0)
            lightType = 0; //Directional Light
        else if (lightData.ForwardCosAngle.w < 0)
            lightType = 1; //Point Light

        float3 vPosition2LightRayWs = lightData.PositionSizeSqr.xyz - posWorld;
        float wsPos2LightSq = dot(vPosition2LightRayWs.xyz, vPosition2LightRayWs.xyz);

        if ((wsPos2LightSq > lightData.PositionSizeSqr.w || dot(vPosition2LightRayWs, normalWorld) < 0) && lightData.PositionSizeSqr.w > 0)
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

        //half3 brdfColor = BRDF_PBS_Direct_Term(s.diffColor.xyz, s.specColorandSmoothness.xyz, nv, s.normalWorld, -s.eyeVec, vPositionToLightDirWs, grazingTerm, perceptualRoughness, roughness);

        half3 brdfColor = BRDF_PBS_Direct_Term(diffuse, specular, nv, normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2);

        LightRenderingData lightRenderingData = AllLightsRenderingData[lightId];

        float lightAtten = 1.0;

        if (lightRenderingData.ShadowIndex >= 0)
        {
            int shadowIndexOffset = 0;

            ShadowData sd = _ShadowDatasExp[lightRenderingData.ShadowIndex + shadowIndexOffset];

            lightAtten = ComputeShadow_Exponential(posWorld, sd, true);
        }

        float cookieAtten = 1.0;
        float spotLightAtten = saturate(flTemp * lightRenderingData.Color.w);
        float3 lightColor2 = lightRenderingData.Color.xyz * spotLightAtten * fallOffParam * lightAtten * cookieAtten;

        lightingColor += brdfColor * lightColor2; // BRDF_PBS_Direct(s.diffColor.xyz, s.specColorandSmoothness.xyz, nv, s.normalWorld, viewDir, perLight, grazingTerm, perceptualRoughness, roughness);
    }

    return lightingColor;
}

half3 Surface_ClusterLightingResult_Direct(half3 diffuse, half3 specular, half roughness, half roughness2, half3 normalWorld, half3 viewDir, float3 posWorld, half3 clusterId)
{

    // NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
    // In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
    // but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
    // Following define allow to control this. Set it to 0 if ALU is critical on your platform.
    // This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
    // Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.

    half nv = abs(dot(normalWorld, viewDir));

    half3 lightingColor = 0;

    [loop]for (int idx = 0; idx < _DirectionalLightCount; idx++)
    {
        DirectionalLightDataSimple dirLight = _DirectionalLightsDataSimpleBuffer[idx];
        float3 vPositionToLightDirWs = -dirLight.ForwardCosAngle.xyz;

        float lightAtten = 1.0;

        lightAtten = Compute_Direction_Light_Shadow(posWorld, dirLight);

        float3 dirLightColor = dirLight.Color.xyz * lightAtten;
        half3 brdfColor = BRDF_PBS_Direct_Term(diffuse, specular, nv, normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2);

        lightingColor += dirLightColor * brdfColor;
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

        if (lightRenderingData.ShadowIndex >= 0)
        {
            int shadowIndexOffset = 0;

            ShadowData sd = _ShadowDatasExp[lightRenderingData.ShadowIndex + shadowIndexOffset];

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
        float3 lightColor2 = lightRenderingData.Color.xyz * spotLightAtten * fallOffParam * lightAtten * cookieAtten;

        half3 brdfColor = BRDF_PBS_Direct_Term(diffuse, specular, nv, normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2);
        lightingColor += brdfColor * lightColor2; // BRDF_PBS_Direct(s.diffColor.xyz, s.specColorandSmoothness.xyz, nv, s.normalWorld, viewDir, perLight, grazingTerm, perceptualRoughness, roughness);
    }

    return lightingColor;
}

half3 Surface_ClusterLightingResult_Direct_OP(half3 diffuse, half3 specular, half roughness, half roughness2, half3 normalWorld, half3 viewDir, float3 posWorld, half3 clusterId)
{

    // NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
    // In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
    // but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
    // Following define allow to control this. Set it to 0 if ALU is critical on your platform.
    // This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
    // Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.

    half nv = abs(dot(normalWorld, viewDir));	// This abs allow to limit artifact

    half3 diffLightingColor = 0;
    half3 specLightingColor = 0;

    [loop]for (int idx = 0; idx < _DirectionalLightCount; idx++)
    {
        DirectionalLightDataSimple dirLight = _DirectionalLightsDataSimpleBuffer[idx];
        float3 vPositionToLightDirWs = SafeNormalize(-dirLight.ForwardCosAngle.xyz);

        float lightAtten = 1.0;

        lightAtten = Compute_Direction_Light_Shadow(posWorld, dirLight);

        float3 dirLightColor = dirLight.Color.xyz * lightAtten;
        float specTerm, diffTerm, lh;
        BSDF_Direct_Term(normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);//BRDF_PBS_Direct_Term(data.diffuse, data.specular, nv, normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2);

        diffLightingColor += dirLightColor * diffTerm;
        specLightingColor += dirLightColor * specTerm/* * FresnelTerm(data.specular, lh)*/;
    }

   

#if LIGHT_CULLING_MASK
	_PunctualLightCount = min(_PunctualLightCount, unity_LightIndicesOffsetAndCount.y);
	uint nextNode = 0;
#else
	float startOffset = (clusterId.x * CullingClusterParams.y * CullingClusterParams.z + clusterId.y * CullingClusterParams.z) * 4 + clusterId.z * 4;

	if (clusterId.x < 0 || clusterId.y < 0 || clusterId.z < 0 || clusterId.x >= CullingClusterParams.x || clusterId.y >= CullingClusterParams.y || clusterId.z >= CullingClusterParams.z)
		return half3(1, 0, 1);
	uint nextNode = ClusterStartOffsetIndexIn.Load((int)startOffset);
#endif

	int loopCount = 0;

    [loop] while (loopCount < _PunctualLightCount && nextNode != 0xFFFFFFFF)
    {

        LightGeoData lightData;
        LightRenderingData lightRenderingData;

#if LIGHT_CULLING_MASK
		int lightId = _LightIndexBuffer[unity_LightIndicesOffsetAndCount.x + loopCount];
		lightData = AllLightList[lightId];
		lightRenderingData = AllLightsRenderingData[lightId];
#else

#ifdef SHADER_API_PSSL
        ulong exec = __s_read_exec();
        do {
            uint first_bit = __s_ff1_i32_b64(exec);
            uint uniform_nextNode = __v_readlane_b32(nextNode, first_bit);
            ulong lane_mask = __v_cmp_eq_u32(nextNode, uniform_nextNode);
            if (__v_cndmask_b32(0, 1, lane_mask)) {
#endif
				uint lightLinkNode = ClusterLightsLinkListIn[nextNode];
				uint lightId = lightLinkNode >> 24;

				uint nextNodeId = lightLinkNode | 0xFF000000;
				if (nextNodeId != 0xFFFFFFFF)
					nextNode = lightLinkNode & 0xFFFFFF;
				else
					nextNode = 0xFFFFFFFF;

				lightData = AllLightList[lightId];
				lightRenderingData = AllLightsRenderingData[lightId];


#ifdef SHADER_API_PSSL
            }
            exec &= ~lane_mask;
        } while (exec != 0);
#endif

#endif

		loopCount++;

        float lightAtten = Compute_Light_Attenuation(posWorld, lightRenderingData.ShadowIndex, lightRenderingData.CookieIndex, true);

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
        BSDF_Direct_Term(normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);
        diffLightingColor += diffTerm * lightColor;
        specLightingColor += specTerm * lightColor;
    }

    return specLightingColor * specular + diffLightingColor * diffuse;
}

half3 Surface_ClusterLightingResult_Direct_OP(half3 diffuse, half3 specular, half roughness, half roughness2, int3 screenCoord, half3 normalWorld, half3 viewDir, float3 posWorld, half3 clusterId)
{

    // NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
    // In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
    // but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
    // Following define allow to control this. Set it to 0 if ALU is critical on your platform.
    // This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
    // Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.

    half nv = abs(dot(normalWorld, viewDir));	// This abs allow to limit artifact

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
        BSDF_Direct_Term(normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);//BRDF_PBS_Direct_Term(data.diffuse, data.specular, nv, normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2);

        diffLightingColor += dirLightColor * diffTerm;
        specLightingColor += dirLightColor * specTerm;
    }

    if (clusterId.x < 0 || clusterId.y < 0 || clusterId.z < 0 || clusterId.x >= CullingClusterParams.x || clusterId.y >= CullingClusterParams.y || clusterId.z >= CullingClusterParams.z)
        return half3(1, 0, 1);

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

        if (lightRenderingData.ShadowIndex >= 0)
        {
            int shadowIndexOffset = 0;

            ShadowData sd = _ShadowDatasExp[lightRenderingData.ShadowIndex + shadowIndexOffset];

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
        BSDF_Direct_Term(normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);
        diffLightingColor += diffTerm * lightColor;
        specLightingColor += specTerm * lightColor;
    }
    
    return specLightingColor * specular + diffLightingColor * diffuse;
}

half3 Surface_ClusterLightingResult_Direct_Aniso_OP(half3 diffuse, half3 specular, half roughness, half roughness2, half3 normalWorld, half3 tangentWorld, half3 viewDir, float3 posWorld, half3 clusterId)
{

    // NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
    // In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
    // but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
    // Following define allow to control this. Set it to 0 if ALU is critical on your platform.
    // This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
    // Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.

    half nv = abs(dot(normalWorld, viewDir));	// This abs allow to limit artifact

    half3 diffLightingColor = 0;
    half3 specLightingColor = 0;

    [loop]for (int idx = 0; idx < _DirectionalLightCount; idx++)
    {
        DirectionalLightDataSimple dirLight = _DirectionalLightsDataSimpleBuffer[0];
        float3 vPositionToLightDirWs = -dirLight.ForwardCosAngle.xyz;

        float lightAtten = 1.0;

        lightAtten = Compute_Direction_Light_Shadow(posWorld, dirLight);

        float3 dirLightColor = dirLight.Color.xyz * lightAtten;
        float specTerm, diffTerm;
        BSDF_Aniso_Direct_Term(normalWorld, tangentWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);//BRDF_PBS_Direct_Term(data.diffuse, data.specular, nv, normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2);

        /*diffLightingColor += dirLightColor * diffTerm;
        half3 halfDir = SafeNormalize(vPositionToLightDirWs + viewDir);
        half LoH = saturate(dot(vPositionToLightDirWs, halfDir));*/

        diffLightingColor += dirLightColor * diffTerm;
        specLightingColor += dirLightColor * specTerm/* * FresnelTerm(data.specular, LoH)*/;
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

#if LIGHT_CULLING_MASK
		bool passLightCullingMask = false;
		for (int index = 0; index < _PunctualLightCount; index++)
		{
			int lightIndex = _LightIndexBuffer[unity_LightIndicesOffsetAndCount.x + index];
			if (lightIndex == (int)lightId)
			{
				passLightCullingMask = true;
				continue;
			}
		}

		if (!passLightCullingMask)
			continue;
#endif

        LightGeoData lightData = AllLightList[lightId];
        //half3 brdfColor = BRDF_PBS_Direct_Term(s.diffColor.xyz, s.specColorandSmoothness.xyz, nv, s.normalWorld, -s.eyeVec, vPositionToLightDirWs, grazingTerm, perceptualRoughness, roughness);

        LightRenderingData lightRenderingData = AllLightsRenderingData[lightId];

        float lightAtten = 1.0;

        if (lightRenderingData.ShadowIndex >= 0)
        {
            int shadowIndexOffset = 0;

            ShadowData sd = _ShadowDatasExp[lightRenderingData.ShadowIndex + shadowIndexOffset];

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
        BSDF_Aniso_Direct_Term(normalWorld, tangentWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);
        diffLightingColor += diffTerm * lightColor;
        specLightingColor += specTerm * lightColor;
    }

    return specLightingColor * specular + diffLightingColor * diffuse;
}

half3 Surface_ClusterLightingResult_Direct_Aniso_OP(half3 diffuse, half3 specular, half roughness, half roughness2, int3 screenCoord, half3 normalWorld, half3 tangentWorld, half3 viewDir, float3 posWorld, half3 clusterId)
{

    // NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
    // In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
    // but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
    // Following define allow to control this. Set it to 0 if ALU is critical on your platform.
    // This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
    // Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.

    half nv = abs(dot(normalWorld, viewDir));	// This abs allow to limit artifact

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
        BSDF_Aniso_Direct_Term(normalWorld, tangentWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);//BRDF_PBS_Direct_Term(data.diffuse, data.specular, nv, normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2);

        diffLightingColor += dirLightColor * diffTerm;
        half3 halfDir = SafeNormalize(vPositionToLightDirWs + viewDir);
        half LoH = saturate(dot(vPositionToLightDirWs, halfDir));

        diffLightingColor += dirLightColor * diffTerm;
        specLightingColor += dirLightColor * specTerm/* * FresnelTerm(data.specular, LoH)*/;
    }

    float startOffset = (clusterId.x * CullingClusterParams.y * CullingClusterParams.z + clusterId.y * CullingClusterParams.z) * 4 + clusterId.z * 4;

    uint nextNode = ClusterStartOffsetIndexIn.Load((int)startOffset);

    int loopCount = 0;

    [loop] while (loopCount < _PunctualLightCount && nextNode != 0xFFFFFFFF)
    {
        loopCount++;

        LightGeoData lightData;
        LightRenderingData lightRenderingData;

#ifdef SHADER_API_PSSL
        ulong exec = __s_read_exec();
        do {
            uint first_bit = __s_ff1_i32_b64(exec);
            uint uniform_nextNode = __v_readlane_b32(nextNode, first_bit);
            ulong lane_mask = __v_cmp_eq_u32(nextNode, uniform_nextNode);
            if (__v_cndmask_b32(0, 1, lane_mask)) {
#endif
                uint lightLinkNode = ClusterLightsLinkListIn[nextNode];
                uint lightId = lightLinkNode >> 24;

                uint nextNodeId = lightLinkNode | 0xFF000000;
                if (nextNodeId != 0xFFFFFFFF)
                    nextNode = lightLinkNode & 0xFFFFFF;
                else
                    nextNode = 0xFFFFFFFF;

                lightData = AllLightList[lightId];
                lightRenderingData = AllLightsRenderingData[lightId];

#ifdef SHADER_API_PSSL
            }
            exec &= ~lane_mask;
        } while (exec != 0);
#endif
        float lightAtten = 1.0;

        if (lightRenderingData.ShadowIndex >= 0)
        {
            int shadowIndexOffset = 0;

            ShadowData sd = _ShadowDatasExp[lightRenderingData.ShadowIndex + shadowIndexOffset];

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
        BSDF_Aniso_Direct_Term(normalWorld, tangentWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);
        diffLightingColor += diffTerm * lightColor;
        specLightingColor += specTerm * lightColor;
    }

    return specLightingColor * specular + diffLightingColor * diffuse;
}

#endif
