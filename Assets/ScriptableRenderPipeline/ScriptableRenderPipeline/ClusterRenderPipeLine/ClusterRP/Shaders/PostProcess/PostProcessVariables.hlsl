#include "CoreRP/ShaderLibrary/Common.hlsl"
#include "../../ShaderVariables.hlsl"

#if defined(SHADER_API_D3D11)
#define RW_TEXTURE2D_ARRAY(type, textureName) RWTexture2DArray<type> textureName
#elif defined(SHADER_API_PSSL)
#define RW_TEXTURE2D_ARRAY(type, textureName) RW_Texture2D_Array<type> textureName
#endif

#ifdef UNITY_STEREO_INSTANCING_ENABLED
    #define LOAD_BUFFER_COLOR(textureName, coord) LOAD_TEXTURE2D_ARRAY(textureName. coord.xy, coord,z)
    #define GET_RW_TEXTURE_COLOR(textureName, coord) textureName[coord]
    #define SET_RW_TEXTURE_COLOR(textureName, coord, value) textureName[coord] = value
#else
    #define LOAD_BUFFER_COLOR(textureName, coord) LOAD_TEXTURE2D(textureName, coord.xy)
    #define GET_RW_TEXTURE_COLOR(textureName, coord) textureName[coord.xy]
    #define SET_RW_TEXTURE_COLOR(textureName, coord, value) textureName[coord.xy] = value
#endif

float4 _GroupSize;
