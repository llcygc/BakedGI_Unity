#ifndef SHADOW_DATA
#define SHADOW_DATA

// Generated from UnityEngine.Experimental.Rendering.ShadowData
// PackingRules = Exact
struct ShadowData_old
{
	float4x4 worldToShadow;
	float4x4 shadowToWorld;
	float4 scaleOffset;
	float4 textureSize;
	float4 texelSizeRcp;
	uint id;
	uint shadowType;
	uint payloadOffset;
	float bias;
	float normalBias;
};

// Generated from UnityEngine.Experimental.Rendering.ShadowData
// PackingRules = Exact
struct ShadowData
{
    float4 proj;
    float3 pos;
    float3 rot0;
    float3 rot1;
    float3 rot2;
    float4 scaleOffset;
    float4 textureSize;
    float4 texelSizeRcp;
    uint id;
    uint shadowType;
    uint payloadOffset;
    float slice;
    float4 viewBias;
    float4 normalBias;
    float edgeTolerance;
    float3 _pad;
    float4x4 shadowToWorld;
};

StructuredBuffer<ShadowData> _ShadowDatasExp;
StructuredBuffer<int4>		 _ShadowPayloads;

TEXTURE2D_ARRAY(_CookieTextures);
SAMPLER(sampler_CookieTextures);

#ifdef UNITY_STEREO_INSTANCING_ENABLED
TEXTURE2D_ARRAY(_ScreenSpaceShadowBuffer);
SAMPLER(sampler_ScreenSpaceShadowBuffer);
#else
TEXTURE2D(_ScreenSpaceShadowBuffer);
SAMPLER(sampler_ScreenSpaceShadowBuffer);
#endif

#endif // SHADOW_UTILS
