#if (SHADERPASS != SHADERPASS_DEPTH_ONLY && SHADERPASS != SHADERPASS_SHADOWS)
#error SHADERPASS_is_not_correctly_define
#endif

// x: global clip space bias, y: normal world space bias
float4 _ShadowBias;
float3 _LightDirection;

#include "VertexData.hlsl"

VertexOutputVegetationDepth Vert(SpeedTreeVB v)
{
    VertexOutputVegetationDepth o = (VertexOutputVegetationDepth)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    //OffsetSpeedTreeVertex(v, unity_LODFade.x);
    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
    o.normal = TransformObjectToWorldNormal(v.normal);
    float invNdotL = 1.0 - saturate(dot(_LightDirection, o.normal));
    float scale = invNdotL * _ShadowBias.y;

    // normal bias is negative since we want to apply an inset normal offset
    o.posWS = TransformObjectToWorld(v.vertex.xyz);
    float4 clipPos = TransformWorldToHClip(o.posWS);
    o.posWS = o.normal * scale.xxx + o.posWS;

//    // _ShadowBias.x sign depens on if platform has reversed z buffer
//    clipPos.z += _ShadowBias.x;
//
//#if UNITY_REVERSED_Z
//    clipPos.z = min(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
//#else
//    clipPos.z = max(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
//#endif

    o.clipPos = clipPos;
    return o;
}

#ifdef TESSELLATION_ON

PackedVaryingsToPS VertTesselation(VaryingsToDS input)
{
    VaryingsToPS output;
    output.vmesh = VertMeshTesselation(input.vmesh);
    return PackVaryingsToPS(output);
}

#include "TessellationShare.hlsl"

#endif // TESSELLATION_ON

void Frag(  VertexOutputVegetationDepth IN,
            out half4 outColor : SV_Target
        )
{
    UNITY_SETUP_INSTANCE_ID(IN);

#if defined(SPEEDTREE_ALPHATEST)
    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

#if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
    alpha = _Color.a;
#endif
            
    clip(alpha - _Cutoff);
#endif
    // TODO: handle cubemap shadow
    outColor = half4(0.0, 0.0, 0.0, 0.0);
}
