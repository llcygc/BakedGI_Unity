#ifndef VERTEX_DATA
#define VERTEX_DATA

// If lightmap is not defined than we evaluate GI (ambient + probes) from SH
// We might do it fully or partially in vertex to save shader ALU
#if !defined(LIGHTMAP_ON)
#if defined(SHADER_API_GLES) || (SHADER_TARGET < 30) || !defined(_NORMALMAP)
// Evaluates SH fully in vertex
#define EVALUATE_SH_VERTEX
#elif !SHADER_HINT_NICE_QUALITY
// Evaluates L2 SH in vertex and L0L1 in pixel
#define EVALUATE_SH_MIXED
#endif
// Otherwise evaluate SH fully per-pixel
#endif


#ifdef LIGHTMAP_ON
#define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT) OUT.xy = lightmapUV.xy * lightmapScaleOffset.xy + lightmapScaleOffset.zw;
#define OUTPUT_SH(normalWS, OUT)
#else
#define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT)
#define OUTPUT_SH(normalWS, OUT) OUT.xyz = SampleSHVertex(normalWS)
#endif

///////////////////////////////////////////////////////////////////////////////
#ifdef _NORMALMAP
#define OUTPUT_NORMAL(IN, OUT) OutputTangentToWorld(IN.tangent, IN.normal, OUT.tangent.xyz, OUT.binormal.xyz, OUT.normal.xyz)
#else
#define OUTPUT_NORMAL(IN, OUT) OUT.normal = TransformObjectToWorldNormal(IN.normal)
#endif

struct VertexInput
{
    float4 vertex : POSITION;
#ifdef	_ENABLE_WIND
	half4 color : COLOR;
#endif
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 texcoord : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID

};

struct VertexInputDepth
{
    float4 vertex : POSITION;
#ifdef	_ENABLE_WIND
	half4 color : COLOR;
#endif
    float3 normal : NORMAL;
#if defined(_ALPHATEST_ON)
    float2 texcoord : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexInputMeta
{
    float4 vertex : POSITION;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
#ifdef _TANGENT_TO_WORLD
    half4 tangent   : TANGENT;
#endif
};

struct VertexOutput
{
    float4 uv : TEXCOORD0;
    float4 lightmapUVOrVertexSH : TEXCOORD1; // holds either lightmapUV or vertex SH. depending on LIGHTMAP_ON
    float3 posWS : TEXCOORD2;
    half3 normal : TEXCOORD3;

#if defined(_NORMALMAP) || defined(_BRDF_ANISO) || defined(_DOUBLESIDED_ON)
    half3 tangent                   : TEXCOORD4;
    half3 binormal                  : TEXCOORD5;
#endif

    half3 viewDir : TEXCOORD6;
    float4 clusterPos : TEXCOORD7;
    float4 clipPos : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO

#if defined(_DOUBLESIDED_ON) && SHADER_STAGE_FRAGMENT
    FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

struct VertexOutputDepth
{
#if defined(_ALPHATEST_ON)
    float2 uv : TEXCOORD0;
#endif
    float4 clipPos : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct VertexOutputVelocity
{
    float2 uv : TEXCOORD0;
    float4 clipPos : SV_POSITION;
    float3 positionCS : TEXCOORD1;
    float3 positionCSLastFrame : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct VertexOutputMeta
{
    float4 uv : TEXCOORD0;
    float4 pos : SV_POSITION;
};

//VertexOutput ZeroInitVertexOutput()
//{
//    VertexOutput o;
//    o.
//}

#endif
