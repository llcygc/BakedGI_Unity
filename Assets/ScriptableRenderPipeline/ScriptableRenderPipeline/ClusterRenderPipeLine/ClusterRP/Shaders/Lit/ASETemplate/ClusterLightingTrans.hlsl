#ifndef CLUSTER_LIGHTING_INCLUDED
#define CLUSTER_LIGHTING_INCLUDED

//#include "LWRP/ShaderLibrary/InputSurface.hlsl"
//#include "LightingUtils.hlsl"
//#include "Lighting.hlsl"
#include "ClusterRP/Shaders/Lit/Lighting/ClusterData.cginc"
#include "ClusterRP/Shaders/Lit/Lighting/BSDF.hlsl"
#include "ClusterRP/Shaders/ShadowUtils.cginc"

half3 Surface_ClusterLightingResult_Direct_Trans_OP(half3 diffuse, half3 specular, half roughness, half roughness2, half3 transmission, half3 translucency, half3 ambient, half3 normalWorld, half3 viewDir, float3 posWorld, half3 clusterId)
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
    half3 c = 0;
    half3 d = 0;

    [loop]for (int idx = 0; idx < _DirectionalLightCount; idx++)
    {
        DirectionalLightDataSimple dirLight = _DirectionalLightsDataSimpleBuffer[0];
        float3 vPositionToLightDirWs = -dirLight.ForwardCosAngle.xyz;

        float lightAtten = 1.0;
        lightAtten = Compute_Direction_Light_Shadow(posWorld, dirLight);

        float3 dirLightColor = dirLight.Color.xyz * lightAtten;
        float specTerm, diffTerm;
        BSDF_Direct_Term(normalWorld, viewDir, vPositionToLightDirWs, roughness, roughness2, diffTerm, specTerm);//BRDF_PBS_Direct_Term(data.diffuse, data.specular, nv, normalWorld, viewDir, vPositionToLightDirWs, data.roughness, data.roughness2);

        diffLightingColor += dirLightColor * diffTerm;

        half3 lightDir = vPositionToLightDirWs + normalWorld * _TransNormalDistortion;
        half transVdotL = pow(saturate(dot(viewDir, -lightDir)), _TransScattering);
        half3 translu = dirLightColor * (transVdotL * _TransDirect + ambient * _TransAmbient) * translucency;
        c += diffuse * translu * _Translucency;

        half3 transmi = max(0, -dot(normalWorld, vPositionToLightDirWs)) * dirLightColor * transmission;
        d += diffuse * transmi;

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

        [branch]if (wsPos2LightSq > lightData.PositionSizeSqr.w && lightData.PositionSizeSqr.w > 0)
        {
            continue;
        }

        float3 vPositionToLightDirWs = SafeNormalize(vPosition2LightRayWs.xyz);
        float flTemp = min(dot(-vPositionToLightDirWs.xyz, lightData.ForwardCosAngle.xyz) - lightData.ForwardCosAngle.w, 1.0);
        float fallOffParam = lightData.PositionSizeSqr.w > 0 ? (1.0 - pow(wsPos2LightSq / lightData.PositionSizeSqr.w, 0.175)) : 1.0;

		[branch]if (flTemp <= 0.0 && lightData.PositionSizeSqr.w > 0)
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

        half3 lightDir = vPositionToLightDirWs + normalWorld * _TransNormalDistortion;
        half transVdotL = pow(saturate(dot(viewDir, -lightDir)), _TransScattering);
        half3 translu = lightColor * (transVdotL * _TransDirect + ambient * _TransAmbient) * translucency;
        c += diffuse * translu *_Translucency;

        half3 transmi = max(0, -dot(normalWorld, vPositionToLightDirWs)) * lightColor * transmission;
        d += diffuse * transmi;
    }

    return specLightingColor * specular + diffLightingColor * diffuse + c + d;
}

half3 Surface_ClusterLightingResult_Direct_Trans_OP(half3 diffuse, half3 specular, half roughness, half roughness2, half3 transmission, half3 translucency, half3 ambient, int3 screenCoord, half3 normalWorld, half3 viewDir, float3 posWorld, half3 clusterId)
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
    half3 c = 0;
    half3 d = 0;

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
        half3 halfDir = SafeNormalize(vPositionToLightDirWs + viewDir);
        half LoH = saturate(dot(vPositionToLightDirWs, halfDir));

        half3 lightDir = vPositionToLightDirWs + normalWorld * _TransNormalDistortion;
        half transVdotL = pow(saturate(dot(viewDir, -lightDir)), _TransScattering);
        half3 translu = dirLightColor * (transVdotL * _TransDirect + ambient * _TransAmbient) * translucency;
        c += diffuse * translu * _Translucency;

        half3 transmi = max(0, -dot(normalWorld, vPositionToLightDirWs)) * dirLightColor * transmission;
        d += diffuse * transmi;

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

		[branch]if (wsPos2LightSq > lightData.PositionSizeSqr.w && lightData.PositionSizeSqr.w > 0)
		{
			continue;
		}

		float3 vPositionToLightDirWs = SafeNormalize(vPosition2LightRayWs.xyz);
		float flTemp = min(dot(-vPositionToLightDirWs.xyz, lightData.ForwardCosAngle.xyz) - lightData.ForwardCosAngle.w, 1.0);
		float fallOffParam = lightData.PositionSizeSqr.w > 0 ? (1.0 - pow(wsPos2LightSq / lightData.PositionSizeSqr.w, 0.175)) : 1.0;

		[branch]if (flTemp <= 0.0 && lightData.PositionSizeSqr.w > 0)
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

		half3 lightDir = vPositionToLightDirWs + normalWorld * _TransNormalDistortion;
		half transVdotL = pow(saturate(dot(viewDir, -lightDir)), _TransScattering);
		half3 translu = lightColor * (transVdotL * _TransDirect + ambient * _TransAmbient) * translucency;
		c += diffuse * translu *_Translucency;

		half3 transmi = max(0, -dot(normalWorld, vPositionToLightDirWs)) * lightColor * transmission;
		d += diffuse * transmi;

    }

    return specLightingColor * specular + diffLightingColor * diffuse + c + d;
}

#endif
