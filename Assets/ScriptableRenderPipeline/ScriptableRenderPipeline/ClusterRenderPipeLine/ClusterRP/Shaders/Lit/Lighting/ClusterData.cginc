#ifndef CLUSTER_SHADING_DATA_INCLUDED
#define CLUSTER_SHADING_DATA_INCLUDED

#define CLUSTER_RES 32
#define NUM_THREADS_X CLUSTER_RES/2
#define NUM_THREADS_Y CLUSTER_RES/2
#define NUM_THREADS_PER_TILE (NUM_THREADS_X*NUM_THREADS_Y)

#define MAX_LIGHT_COUNT 256

#define CLUSTER_SLICES 128

#define STEREO_RES_X 2816
#define STEREO_RES_X_HALF 1408

#define MAX_NUM_LIGHTS_PER_TILE 20
#define LIGHT_INDEX_BUFFER_SENTINEL 1025

#define GLOBAL_FOG_SAMPLE_COUNT 256

struct DirectionalLightDataSimple
{
	float4 ForwardCosAngle;
	float4 Color;
	float4 ShadowOffset;
};

struct LightGeoData
{
	float4 PositionSizeSqr;
	float4 ForwardCosAngle;
};

struct LightRenderingData
{
	float4 Color;
	float4 ShadowBias;
    int ShadowIndex;
    int CookieIndex;
    int AffectVolumetricLight;
    int unused2;
};

struct LightShadowData
{
	float4x4 ShadowMatrix;
};

struct LightLinkNode
{
	uint linkNode;
};

struct FogMediaParam
{
	float4 ColorParam;
	float4 DenstiyParam;
};

CBUFFER_START(PCFInfo)
float4 g_vShadow3x3PCFTerms0;
float4 g_vShadow3x3PCFTerms1;
float4 g_vShadow3x3PCFTerms2;
float4 g_vShadow3x3PCFTerms3;
CBUFFER_END

CBUFFER_START(PerFrameInfo)
float4 ClusterScreenParams;
float4 CullingClusterParams;
float4 FogClusterParams;
float4 ClusterProjParams;
float4x4 Cluster_Matrix_LinearZ;
float4x4 Cluster_Matrix_LinearZ_LastFrame;
float blendWeight;
float4x4 matrix_MVPInv;
float4x4 matrix_MVPInv_lastFrame;
float4 CameraPos;
float4 EyeCenterCameraPos;
int _DirectionalLightCount;
int _PunctualLightCount;
int HaltonIndex;
float4 ClusterDepthSlicesArray[129];
CBUFFER_END

CBUFFER_START(FrustumInfo)
float4 CameraBottomLeft[2];
float4 CameraUpDirLength;
float4 CameraRightDirLength;
float4 ClusterBottomLeft;
float4 ClusterUpDirLength;
float4 ClusterRightDirLength;
CBUFFER_END

#ifdef LIGHT_CULLING_MASK
StructuredBuffer<int> _LightIndexBuffer;
#endif

//Directional lights info - do not need to particpate culling process
StructuredBuffer<DirectionalLightDataSimple> _DirectionalLightsDataSimpleBuffer;

//Clustered local lights info
RWByteAddressBuffer ClusterStartOffsetIndex;
ByteAddressBuffer ClusterStartOffsetIndexIn;
RWStructuredBuffer<uint> ClusterLightsLinkList;
StructuredBuffer<uint> ClusterLightsLinkListIn;
StructuredBuffer<LightGeoData> AllLightList;
StructuredBuffer<LightRenderingData> AllLightsRenderingData;
Buffer<float> ClusterDepthSlicesBuffer;
StructuredBuffer<float4x4> LightsShadowMatrices;

//---------------------------------------
#define VALVE_DECLARE_SHADOWMAP( tex ) Texture2D tex; SamplerComparisonState sampler##tex
#define VALVE_SAMPLE_SHADOW( tex, coord ) tex.SampleCmpLevelZero( sampler##tex, (coord).xy, (coord).z )
#define SAMPLE_TEX2D_LOD( tex, coord, level ) tex.SampleLevel( sampler##tex, (coord).xy, 0 )

VALVE_DECLARE_SHADOWMAP(g_tShadowBuffer);

TEXTURE2D(g_tShadowBufferExp);
SAMPLER(samplerg_tShadowBufferExp);

TEXTURE2D(_StaticShadowmap);
SAMPLER(sampler_StaticShadowmap);

TEXTURE2D(GlobalFogMediaTexture);
SAMPLER(samplerGlobalFogMediaTexture);

TEXTURE3D(_VolumetricFogTexture);
SAMPLER(sampler_VolumetricFogTexture);

#endif // TILED_SHADING_DATA_INCLUDED
