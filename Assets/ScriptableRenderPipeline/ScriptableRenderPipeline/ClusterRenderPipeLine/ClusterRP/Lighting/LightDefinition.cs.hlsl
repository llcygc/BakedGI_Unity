//
// This file was automatically generated. Please don't edit by hand.
//

#ifndef LIGHTDEFINITION_CS_HLSL
#define LIGHTDEFINITION_CS_HLSL
//
// Viva.Experimental.Rendering.ClusterPipeline.GPULightType:  static fields
//
#define GPULIGHTTYPE_DIRECTIONAL (0)
#define GPULIGHTTYPE_POINT (1)
#define GPULIGHTTYPE_SPOT (2)
#define GPULIGHTTYPE_PROJECTOR_PYRAMID (3)
#define GPULIGHTTYPE_PROJECTOR_BOX (4)
#define GPULIGHTTYPE_LINE (5)
#define GPULIGHTTYPE_RECTANGLE (6)

//
// Viva.Experimental.Rendering.ClusterPipeline.GPUImageBasedLightingType:  static fields
//
#define GPUIMAGEBASEDLIGHTINGTYPE_REFLECTION (0)
#define GPUIMAGEBASEDLIGHTINGTYPE_REFRACTION (1)

//
// Viva.Experimental.Rendering.ClusterPipeline.EnvShapeType:  static fields
//
#define ENVSHAPETYPE_NONE (0)
#define ENVSHAPETYPE_BOX (1)
#define ENVSHAPETYPE_SPHERE (2)
#define ENVSHAPETYPE_SKY (3)

//
// Viva.Experimental.Rendering.ClusterPipeline.EnvConstants:  static fields
//
#define ENVCONSTANTS_SPEC_CUBE_LOD_STEP (6)

//
// Viva.Experimental.Rendering.ClusterPipeline.StencilLightingUsage:  static fields
//
#define STENCILLIGHTINGUSAGE_NO_LIGHTING (0)
#define STENCILLIGHTINGUSAGE_SPLIT_LIGHTING (1)
#define STENCILLIGHTINGUSAGE_REGULAR_LIGHTING (2)

// Generated from Viva.Experimental.Rendering.ClusterPipeline.DirectionalLightDataSimple
// PackingRules = Exact
struct DirectionalLightDataSimple
{
    float4 ForwardCosAngle;
    float4 Color;
    float4 ShadowOffset;
};

// Generated from Viva.Experimental.Rendering.ClusterPipeline.DirectionalLightData
// PackingRules = Exact
struct DirectionalLightData
{
    float3 positionWS;
    bool tileCookie;
    float3 color;
    int shadowIndex;
    float3 forward;
    int cookieIndex;
    float3 right;
    float specularScale;
    float3 up;
    float diffuseScale;
    bool dynamicShadowCasterOnly;
    float2 fadeDistanceScaleAndBias;
    float unused0;
    float4 shadowMaskSelector;
};

// Generated from Viva.Experimental.Rendering.ClusterPipeline.PunctualLightGeoData
// PackingRules = Exact
struct PunctualLightGeoData
{
    float4 PositionSizeSqr;
    float4 ForwardCosAngle;
};

// Generated from Viva.Experimental.Rendering.ClusterPipeline.PunctualLightRenderingData
// PackingRules = Exact
struct PunctualLightRenderingData
{
    float4 Color;
    float4 ShadowOffset;
};

// Generated from Viva.Experimental.Rendering.ClusterPipeline.AreaLightData
// PackingRules = Exact
struct AreaLightData
{
    float3 positionWS;
    float invSqrAttenuationRaius;
    float3 color;
    int shadowIndex;
    float3 forward;
    int cookieIndex;
    float3 right;
    float specularScale;
    float3 up;
    float diffuseScale;
    float angleScale;
    float angleOffset;
    float shadowDimmer;
    bool dynamicShadowCasterOnly;
    float2 size;
    int lightType;
    float minRoughness;
    float4 shadowMaskSelector;
};

// Generated from Viva.Experimental.Rendering.ClusterPipeline.EnvLightData
// PackingRules = Exact
struct EnvLightData
{
    float3 positionWS;
    int envShapeType;
    float3 forward;
    int envIndex;
    float3 up;
    float blendDistance;
    float3 right;
    float minProjectionDistance;
    float3 innerDistance;
    float unused0;
    float3 offsetLS;
    float unused1;
};

//
// Accessors for Viva.Experimental.Rendering.ClusterPipeline.DirectionalLightDataSimple
//
float4 GetForwardCosAngle(DirectionalLightDataSimple value)
{
	return value.ForwardCosAngle;
}
float4 GetColor(DirectionalLightDataSimple value)
{
	return value.Color;
}
float4 GetShadowOffset(DirectionalLightDataSimple value)
{
	return value.ShadowOffset;
}

//
// Accessors for Viva.Experimental.Rendering.ClusterPipeline.DirectionalLightData
//
float3 GetPositionWS(DirectionalLightData value)
{
	return value.positionWS;
}
bool GetTileCookie(DirectionalLightData value)
{
	return value.tileCookie;
}
float3 GetColor(DirectionalLightData value)
{
	return value.color;
}
int GetShadowIndex(DirectionalLightData value)
{
	return value.shadowIndex;
}
float3 GetForward(DirectionalLightData value)
{
	return value.forward;
}
int GetCookieIndex(DirectionalLightData value)
{
	return value.cookieIndex;
}
float3 GetRight(DirectionalLightData value)
{
	return value.right;
}
float GetSpecularScale(DirectionalLightData value)
{
	return value.specularScale;
}
float3 GetUp(DirectionalLightData value)
{
	return value.up;
}
float GetDiffuseScale(DirectionalLightData value)
{
	return value.diffuseScale;
}
bool GetDynamicShadowCasterOnly(DirectionalLightData value)
{
	return value.dynamicShadowCasterOnly;
}
float2 GetFadeDistanceScaleAndBias(DirectionalLightData value)
{
	return value.fadeDistanceScaleAndBias;
}
float GetUnused0(DirectionalLightData value)
{
	return value.unused0;
}
float4 GetShadowMaskSelector(DirectionalLightData value)
{
	return value.shadowMaskSelector;
}

//
// Accessors for Viva.Experimental.Rendering.ClusterPipeline.PunctualLightGeoData
//
float4 GetPositionSizeSqr(PunctualLightGeoData value)
{
	return value.PositionSizeSqr;
}
float4 GetForwardCosAngle(PunctualLightGeoData value)
{
	return value.ForwardCosAngle;
}

//
// Accessors for Viva.Experimental.Rendering.ClusterPipeline.PunctualLightRenderingData
//
float4 GetColor(PunctualLightRenderingData value)
{
	return value.Color;
}
float4 GetShadowOffset(PunctualLightRenderingData value)
{
	return value.ShadowOffset;
}

//
// Accessors for Viva.Experimental.Rendering.ClusterPipeline.AreaLightData
//
float3 GetPositionWS(AreaLightData value)
{
	return value.positionWS;
}
float GetInvSqrAttenuationRaius(AreaLightData value)
{
	return value.invSqrAttenuationRaius;
}
float3 GetColor(AreaLightData value)
{
	return value.color;
}
int GetShadowIndex(AreaLightData value)
{
	return value.shadowIndex;
}
float3 GetForward(AreaLightData value)
{
	return value.forward;
}
int GetCookieIndex(AreaLightData value)
{
	return value.cookieIndex;
}
float3 GetRight(AreaLightData value)
{
	return value.right;
}
float GetSpecularScale(AreaLightData value)
{
	return value.specularScale;
}
float3 GetUp(AreaLightData value)
{
	return value.up;
}
float GetDiffuseScale(AreaLightData value)
{
	return value.diffuseScale;
}
float GetAngleScale(AreaLightData value)
{
	return value.angleScale;
}
float GetAngleOffset(AreaLightData value)
{
	return value.angleOffset;
}
float GetShadowDimmer(AreaLightData value)
{
	return value.shadowDimmer;
}
bool GetDynamicShadowCasterOnly(AreaLightData value)
{
	return value.dynamicShadowCasterOnly;
}
float2 GetSize(AreaLightData value)
{
	return value.size;
}
int GetLightType(AreaLightData value)
{
	return value.lightType;
}
float GetMinRoughness(AreaLightData value)
{
	return value.minRoughness;
}
float4 GetShadowMaskSelector(AreaLightData value)
{
	return value.shadowMaskSelector;
}

//
// Accessors for Viva.Experimental.Rendering.ClusterPipeline.EnvLightData
//
float3 GetPositionWS(EnvLightData value)
{
	return value.positionWS;
}
int GetEnvShapeType(EnvLightData value)
{
	return value.envShapeType;
}
float3 GetForward(EnvLightData value)
{
	return value.forward;
}
int GetEnvIndex(EnvLightData value)
{
	return value.envIndex;
}
float3 GetUp(EnvLightData value)
{
	return value.up;
}
float GetBlendDistance(EnvLightData value)
{
	return value.blendDistance;
}
float3 GetRight(EnvLightData value)
{
	return value.right;
}
float GetMinProjectionDistance(EnvLightData value)
{
	return value.minProjectionDistance;
}
float3 GetInnerDistance(EnvLightData value)
{
	return value.innerDistance;
}
float GetUnused0(EnvLightData value)
{
	return value.unused0;
}
float3 GetOffsetLS(EnvLightData value)
{
	return value.offsetLS;
}
float GetUnused1(EnvLightData value)
{
	return value.unused1;
}


#endif
