#ifndef CLUSTER_LIGHTING_ATTEN_INCLUDED
#define CLUSTER_LIGHTING_ATTEN_INCLUDED

//#include "LWRP/ShaderLibrary/InputSurface.hlsl"
//#include "LightingUtils.hlsl"
//#include "Lighting.hlsl"
#include "ClusterData.cginc"
#include "../../ShadowUtils.cginc"

half4 ClusterLightingAtten(float3 posWorld, float4 clusterPos)
{
    half3 clusterId = ComputeClusterID(clusterPos);

    half4 lightColor = half4(0, 0, 0, 1);

    [loop]for (int idx = 0; idx < _DirectionalLightCount; idx++)
    {
        DirectionalLightDataSimple dirLight = _DirectionalLightsDataSimpleBuffer[idx];
        float lightAtten = 1.0;

        lightAtten = Compute_Direction_Light_Shadow(posWorld, dirLight);
        half3 dirLightColor = dirLight.Color.xyz * lightAtten;
        lightColor.rgb += dirLightColor;
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

        float lightAtten = Compute_Light_Attenuation(posWorld, lightRenderingData.ShadowIndex, lightRenderingData.CookieIndex, true);

        /*if (lightRenderingData.ShadowIndex >= 0)
        {
            int shadowIndexOffset = 0;
            ShadowData sd = _ShadowDatasExp[lightRenderingData.ShadowIndex + shadowIndexOffset];
            lightAtten = Compute_Light_Attenuation(posWorld, sd, true, lightRenderingData.CookieIndex);
        }
*/
        float3 vPosition2LightRayWs = lightData.PositionSizeSqr.xyz - posWorld;
        float wsPos2LightSq = dot(vPosition2LightRayWs.xyz, vPosition2LightRayWs.xyz);

        [branch]if (wsPos2LightSq > lightData.PositionSizeSqr.w && lightData.PositionSizeSqr.w > 0)
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

        float spotLightAtten = saturate(flTemp * lightRenderingData.Color.w);
        half3 puncLightColor = lightRenderingData.Color.xyz * spotLightAtten * fallOffParam * lightAtten;

        lightColor.rgb += puncLightColor;
    }
    return lightColor;
}

#endif
