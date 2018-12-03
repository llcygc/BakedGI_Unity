#if SHADERPASS != SHADERPASS_LIGHT_TRANSPORT
#error SHADERPASS_is_not_correctly_define
#endif

CBUFFER_START(UnityMetaPass)
// x = use uv1 as raster position
// y = use uv2 as raster position
bool4 unity_MetaVertexControl;

// x = return albedo
// y = return normal
bool4 unity_MetaFragmentControl;
CBUFFER_END

float unity_OneOverOutputBoost;
float unity_MaxOutputValue;
float unity_UseLinearSpace;

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Vertex: Used for Standard and StandardSimpleLighting shaders
VertexOutputMeta Vert(VertexInputMeta v)
{
    VertexOutputMeta o;
    float4 vertex = v.vertex;

    if (unity_MetaVertexControl.x)
    {
        vertex.xy = v.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
        // OpenGL right now needs to actually use incoming vertex position,
        // so use it in a very dummy way
        vertex.z = vertex.z > 0 ? 1.0e-4f : 0.0f;
    }
    if (unity_MetaVertexControl.y)
    {
        vertex.xy = v.uv2 * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
        // OpenGL right now needs to actually use incoming vertex position,
        // so use it in a very dummy way
        vertex.z = vertex.z > 0 ? 1.0e-4f : 0.0f;
    }
    o.pos = TransformWorldToHClip(vertex.xyz); // Need to transfer from world to clip compared to legacy
    o.uv.xy = TRANSFORM_TEX(v.uv0, _MainTex);
#if _DETAIL
    o.uv.zw = TRANSFORM_TEX(v.uv0, _DetailAlbedoMap);
#endif
    return o;
}

half4 Frag(VertexOutputMeta IN) : SV_Target
{
    //UNITY_SETUP_INSTANCE_ID(IN);

    BRDFData_Direct brdfDataDirect;
    BRDFData_Indirect brdfDataIndirect;
    half3 normalTS;
    InitializeBRDFData(IN.uv, normalTS, brdfDataDirect, brdfDataIndirect);
            
    half3 Albedo = brdfDataDirect.diffuse + brdfDataDirect.specular * brdfDataDirect.roughness * 0.5;
    half3 SpecularColor = brdfDataDirect.specular;
    half3 Emission = 0;
#ifdef _EMISSION
    Emission = brdfDataIndirect.emission;
#endif
            
    half4 res = 0;
    if (unity_MetaFragmentControl.x)
    {
        res = half4(Albedo, 1);

        // d3d9 shader compiler doesn't like NaNs and infinity.
        unity_OneOverOutputBoost = saturate(unity_OneOverOutputBoost);

        // Apply Albedo Boost from LightmapSettings.
        res.rgb = clamp(PositivePow(res.rgb, unity_OneOverOutputBoost), 0, unity_MaxOutputValue);
    }
    if (unity_MetaFragmentControl.y)
    {
        res = half4(Emission, 1.0);
    }
#ifdef _EMISSION
    return half4(Emission, 1.0);
#else
    return res;
#endif
}
