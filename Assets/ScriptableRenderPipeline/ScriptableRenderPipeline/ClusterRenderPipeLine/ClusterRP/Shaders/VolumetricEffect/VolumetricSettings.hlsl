#include "CoreRP/ShaderLibrary/Common.hlsl"

CBUFFER_START(VolumetrcSettings)
float4 velocityPhg;
half4 ambientColor;
half4 backfaceColor;
float4 noiseClampRange;
half4 noiseScale;
float4 HaltonSequence;
//x - Max height; y - Min height; z - Density Power; w - Not used yet
float4 GlobalFogParams;
float4 FogMediaGradient[256];
float _TimeFog;
CBUFFER_END
