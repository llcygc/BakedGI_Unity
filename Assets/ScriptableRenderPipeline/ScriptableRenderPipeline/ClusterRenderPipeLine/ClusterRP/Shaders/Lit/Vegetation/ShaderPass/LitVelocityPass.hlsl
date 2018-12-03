#ifndef SHADERPASS
#error Undefine_SHADERPASS
#endif

// TODO: Caution - For now the tesselation doesn't displace along the normal with Velocity shader as the previous previous position
// conflict with the normal in the semantic. This need to be fix! Also no per pixel displacement is possible either.

// Attributes
#define REQUIRE_TANGENT_TO_WORLD defined(_PIXEL_DISPLACEMENT)
#define REQUIRE_NORMAL defined(TESSELLATION_ON) || REQUIRE_TANGENT_TO_WORLD || defined(_VERTEX_WIND) || defined(_VERTEX_DISPLACEMENT)
#define REQUIRE_VERTEX_COLOR ((defined(_VERTEX_DISPLACEMENT) || defined(_TESSELLATION_DISPLACEMENT)) && defined(LAYERED_LIT_SHADER) && (defined(_LAYER_MASK_VERTEX_COLOR_MUL) || defined(_LAYER_MASK_VERTEX_COLOR_ADD))) || defined(_VERTEX_WIND)

// This first set of define allow to say which attributes will be use by the mesh in the vertex and domain shader (for tesselation)

// Tesselation require normal
#if REQUIRE_NORMAL
#define ATTRIBUTES_NEED_NORMAL
#endif
#if REQUIRE_TANGENT_TO_WORLD
#define ATTRIBUTES_NEED_TANGENT
#endif
#if REQUIRE_VERTEX_COLOR
#define ATTRIBUTES_NEED_COLOR
#endif

// About UV
// When UVX is present, we assume that UVX - 1 ... UV0 is present

#if defined(_VERTEX_DISPLACEMENT) || REQUIRE_TANGENT_TO_WORLD || defined(_ALPHATEST_ON) || defined(_TESSELLATION_DISPLACEMENT)
    #define ATTRIBUTES_NEED_TEXCOORD0
    #define ATTRIBUTES_NEED_TEXCOORD1
    #if defined(_REQUIRE_UV2) || defined(_REQUIRE_UV3)
        #define ATTRIBUTES_NEED_TEXCOORD2
    #endif
    #if defined(_REQUIRE_UV3)
        #define ATTRIBUTES_NEED_TEXCOORD3
    #endif
#endif


// Varying - Use for pixel shader
// This second set of define allow to say which varyings will be output in the vertex (no more tesselation)
#define VARYINGS_NEED_POSITION_WS

#if REQUIRE_TANGENT_TO_WORLD
#define VARYINGS_NEED_TANGENT_TO_WORLD
#endif

#if REQUIRE_TANGENT_TO_WORLD || defined(_ALPHATEST_ON)
#define VARYINGS_NEED_TEXCOORD0
    #ifdef LAYERED_LIT_SHADER
    #define VARYINGS_NEED_TEXCOORD1
        #if defined(_REQUIRE_UV2) || defined(_REQUIRE_UV3)
        #define VARYINGS_NEED_TEXCOORD2
        #endif
        #if defined(_REQUIRE_UV3)
        #define VARYINGS_NEED_TEXCOORD3
        #endif
    #endif
#endif

// This include will define the various Attributes/Varyings structure
#include "../../../ShaderPasses/FragData.hlsl"
#include "../../../MaterialUtilities.hlsl"
#include "VegetationPBR.cginc"

#if SHADERPASS != SHADERPASS_VELOCITY
#error SHADERPASS_is_not_correctly_define
#endif

// Available semantic start from TEXCOORD4
struct AttributesPass
{
    float3 previousPositionOS : TEXCOORD4; // Contain previous transform position (in case of skinning for example)
};


void VelocityPositionZBias(VertexOutputVelocity input)
{
#if defined(UNITY_REVERSED_Z)
    input.clipPos.z -= unity_MotionVectorsParams.z * input.clipPos.w;
#else
    input.clipPos.z += unity_MotionVectorsParams.z * input.clipPos.w;
#endif
}

VertexOutputVelocity Vert(SpeedTreeVB v,
    AttributesPass inputPass)
{
    VertexOutputVelocity o = (VertexOutputVelocity)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);


    OffsetSpeedTreeVertex(v, unity_LODFade.x);
    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);

    // normal bias is negative since we want to apply an inset normal offset
    float3 posWorld = TransformObjectToWorld(v.vertex.xyz);;
    float4 clipPos = TransformWorldToHClip(posWorld);

    o.clipPos = clipPos;

#if !defined(TESSELLATION_ON)
    VelocityPositionZBias(o);
#endif

#if defined(USING_STEREO_MATRICES)
    float4x4 nonJitteredViewProjMatrix = _NonJitteredViewProjMatrixStereo[unity_StereoEyeIndex];
    float4x4 prevViewProjMatrix = _PrevViewProjMatrixStereo[unity_StereoEyeIndex];
#else
    float4x4 nonJitteredViewProjMatrix = _NonJitteredViewProjMatrix;
    float4x4 prevViewProjMatrix = _PrevViewProjMatrix;
#endif
    // It is not possible to correctly generate the motion vector for tesselated geometry as tessellation parameters can change
    // from one frame to another (adaptative, lod) + in Unity we only receive information for one non tesselated vertex.
    // So motion vetor will be based on interpolate previous position at vertex level instead.
    float4 positionCS = mul(nonJitteredViewProjMatrix, float4(posWorld, 1.0));
    o.positionCS = positionCS.xyw;

    float4 previousPositionCS = float4(0.0, 0.0, 0.0, 1.0);
    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (!forceNoMotion)
    {
        bool hasDeformation = unity_MotionVectorsParams.x > 0.0; // Skin or morph target
                                                                 //Need to apply any vertex animation to the previous worldspace position, if we want it to show up in the velocity buffer
        float3 previousPositionWS = mul(unity_MatrixPreviousM, hasDeformation ? float4(inputPass.previousPositionOS, 1.0) : float4(v.vertex.xyz, 1.0)).xyz;

        //#if defined(HAVE_VERTEX_MODIFICATION)
        //        ApplyVertexModification(inputMesh, normalWS, previousPositionWS, _LastTime);
        //#endif

        //Need this since we are using the current position from VertMesh()
        //previousPositionWS = GetCameraRelativePositionWS(previousPositionWS);

        float4 previousPositionCS = mul(prevViewProjMatrix, float4(previousPositionWS, 1.0));
        o.positionCSLastFrame = previousPositionCS.xyw;
    }

    return o;
}

void Frag(VertexOutputVelocity IN,
    out float4 outColor : SV_Target
#ifdef _DEPTHOFFSET_ON
    , out float outputDepth : SV_Depth
#endif
)
{

#if defined(SPEEDTREE_ALPHATEST)
    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

#if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
    alpha = _Color.a;
#endif

    clip(alpha - _Cutoff);
#endif
    // TODO: How to allow overriden velocity vector from GetSurfaceAndBuiltinData ?
    float2 velocity = CalculateVelocity(float4(IN.positionCS.xy, 0.0, IN.positionCS.z), float4(IN.positionCSLastFrame.xy, 0.0, IN.positionCSLastFrame.z));

    EncodeVelocity(velocity, outColor);

    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (forceNoMotion)
        outColor = float4(0.0, 0.0, 0.0, 0.0);

#ifdef _DEPTHOFFSET_ON
    outputDepth = posInput.deviceDepth;
#endif
}


