#ifndef SHADERPASS
#error Undefine_SHADERPASS
#endif


#if defined(_ENABLE_WIND_SINGLE) || defined(_ENABLE_WIND_HIERARCHY) || defined(_ENABLE_WIND_PROCEDURAL)
#define _ENABLE_WIND 1
#else 
#undef _ENABLE_WIND
#endif

// This include will define the various Attributes/Varyings structure
#include "../../../ShaderPasses/FragData.hlsl"
