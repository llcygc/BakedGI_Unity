#ifndef CLUSTER_SHADING_UTILS_INCLUDED
#define CLUSTER_SHADING_UTILS_INCLUDED

#include "ClusterData.cginc"

#define VERTEX_OUTPUT_CLUSTERED float4 clusterPos : TEXCOORD8;
#define INITIALIZED_OUTPUT_CLUSTERED(posWorld) ComputeClusterPos(posWorld, Cluster_Matrix_LinearZ);


inline float4 ComputeClusterPos(float4 posWS, float4x4 clusterMatrix)
{
	float4 clusterPos = mul(clusterMatrix, posWS);

	clusterPos.xy = float2(clusterPos.x, clusterPos.y * -1) * 0.5 + clusterPos.w * 0.5;
	return clusterPos;
}

float3 ClusterID2FarPos(float3 clusterId, float4 ClusterParams)
{
	float2 screenPos = (clusterId.xy / ClusterParams.xy) * 2 - 1;
	float4 clipPos = float4(screenPos.x, -screenPos.y, 0.0, 1.0);
	float4 farPos = mul(matrix_MVPInv, clipPos);
	farPos /= farPos.w;
	return farPos.xyz;
}

float3 ClusterID2WorldPos(float3 clusterId, float4 ClusterParams)
{
	float2 screenPos = (clusterId.xy / ClusterParams.xy) * 2 - 1;
	float4 clipPos = float4(screenPos.x, -screenPos.y, 0.0, 1.0);
	float4 farPos = mul(matrix_MVPInv, clipPos);
	farPos /= farPos.w;
	return farPos.xyz;
}

inline float3 ComputeClusterIDandUV(float4 clusterPos, out float3 clusterUV)
{
	float2 screenPos = saturate(clusterPos.xy / clusterPos.w);
	//_ProjectionParams	float4	x is 1.0 (or –1.0 if currently rendering with a flipped projection matrix), y is the camera’s near plane, z is the camera’s far plane and w is 1/FarPlane.
	float fn = ClusterProjParams.y / ClusterProjParams.x;
	float depthZ = clusterPos.z / ClusterProjParams.x;
	float clusterZ = (log(depthZ) / log(fn));
	clusterUV = half3(screenPos.xy, clusterZ);
    float3 rawID = float3(floor(screenPos.x * ClusterScreenParams.x / CLUSTER_RES), floor(screenPos.y * ClusterScreenParams.y / CLUSTER_RES), floor(clusterZ * CullingClusterParams.z));
	return clamp(rawID, float3(0, 0, 0), CullingClusterParams.xyz - 1);
}

inline float3 ComputeClusterID(float4 clusterPos)
{
	float2 screenPos = saturate(clusterPos.xy / clusterPos.w);
	//_ProjectionParams	float4	x is 1.0 (or –1.0 if currently rendering with a flipped projection matrix), y is the camera’s near plane, z is the camera’s far plane and w is 1/FarPlane.
	float fn = ClusterProjParams.y / ClusterProjParams.x;
	float depthZ = clusterPos.z / ClusterProjParams.x;
	float clusterZ = (log(depthZ) / log(fn));
    float3 rawID = float3(floor(screenPos.x * ClusterScreenParams.x / CLUSTER_RES), floor(screenPos.y * ClusterScreenParams.y / CLUSTER_RES), floor(clusterZ * CullingClusterParams.z));
    return clamp(rawID, float3(0, 0, 0), CullingClusterParams.xyz - 1);
}

inline float3 ComputeClusterUV(float4 clusterPos)
{
	float2 screenPos = clusterPos.xy / clusterPos.w;
	float fn = ClusterProjParams.y / ClusterProjParams.x;
	float depthZ = clusterPos.z / ClusterProjParams.x;
	float clusterZ = (log(depthZ) / log(fn));
	//return half3(floor(screenPos.x * _ScreenParams.x / CLUSTER_RES), floor(screenPos.y * _ScreenParams.y / CLUSTER_RES), dephtZ);
	return float3(screenPos.xy, clusterZ);
}

inline half3 PosWorldToClusterID(float4 posWs, float4x4 clusterMatrix)
{
	return ComputeClusterID(ComputeClusterPos(posWs, clusterMatrix));
}

inline half3 PosWorldToClusterUV(float4 posWs, float4x4 clusterMatrix)
{
	return ComputeClusterUV(ComputeClusterPos(posWs, clusterMatrix));
}



#endif // TILED_SHADING_UTILS_INCLUDED
